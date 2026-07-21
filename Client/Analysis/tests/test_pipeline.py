from __future__ import annotations

import csv
import hashlib
import json
from pathlib import Path

import pytest

from scenetalkvr_analysis.assignment_parser import FORMAL, parse_assignments
from scenetalkvr_analysis.bundle_reader import BundleError, SessionBundle
from scenetalkvr_analysis.checksum_validator import sha256_file
from scenetalkvr_analysis.cli import main
from scenetalkvr_analysis.dictionary import TABLES, rows as dictionary_rows
from scenetalkvr_analysis.pipeline import AnalysisError, analyze_bundle, validate_bundle
from scenetalkvr_analysis.questionnaire_parser import parse_questionnaire
from scenetalkvr_analysis.scoring import reverse_score
from scenetalkvr_analysis.study_parser import mark_primary_attempts, parse_attempts, parse_goals
from scenetalkvr_analysis.timing_parser import parse_turns


@pytest.fixture(params=["formal", "pilot"])
def bundle(request, tmp_path: Path) -> Path:
    return make_bundle(tmp_path / request.param, request.param)


def test_01_read_valid_formal(tmp_path): assert SessionBundle.read(make_bundle(tmp_path/"f","formal")).manifest["sessionMode"] == "formal"
def test_02_read_valid_pilot(tmp_path): assert SessionBundle.read(make_bundle(tmp_path/"p","pilot")).manifest["sessionMode"] == "pilot"
def test_03_checksum_error_fails(tmp_path):
    root=make_bundle(tmp_path/"b","formal");(root/"study/study.jsonl").write_text("tampered",encoding="utf-8")
    with pytest.raises(BundleError,match="checksum_failure"): SessionBundle.read(root)
def test_04_synthetic_default_excluded(tmp_path):
    root=make_bundle(tmp_path/"b","formal")
    with pytest.raises(AnalysisError,match="includeSyntheticForTesting"): analyze_bundle(root,tmp_path/"out")
def test_05_synthetic_explicit_mode_reads(bundle,tmp_path):
    manifest=analyze_bundle(bundle,tmp_path/"out",config_file(tmp_path,True));assert manifest["dataOrigin"]=="synthetic_dry_run";assert not manifest["collectionEligible"]
def test_06_manifest_missing(tmp_path):
    root=tmp_path/"b";root.mkdir()
    with pytest.raises(BundleError,match="manifest_missing"): SessionBundle.read(root)
def test_07_assignment_linkage(tmp_path):
    b=SessionBundle.read(make_bundle(tmp_path/"b","formal"));rows=parse_assignments(b.manifest,b.assignment);assert rows[0]["taskAssignmentId"]=="ta-1";assert rows[0]["conditionRunId"]=="run-1"
def test_08_timing_jsonl_parses(tmp_path):
    b=SessionBundle.read(make_bundle(tmp_path/"b","formal"));rows,qc=parse_turns(b.timing);assert len(rows)==2;assert not qc
def test_09_monotonic_violation_detected(tmp_path):
    b=SessionBundle.read(make_bundle(tmp_path/"b","formal"));events=list(b.timing);events[1]["monotonicElapsedMs"]=-1;_,qc=parse_turns(events);assert any(x["ruleId"]=="timing_non_monotonic" for x in qc)
def test_10_feedback_first_detected(tmp_path):
    b=SessionBundle.read(make_bundle(tmp_path/"b","formal"));events=list(b.timing);next(x for x in events if x["eventType"]=="DialoguePlaybackStarted")["monotonicElapsedMs"]=3;_,qc=parse_turns(events);assert any(x["ruleId"]=="feedback_first_violation" for x in qc)
def test_11_no_feedback_turn_has_no_correction(tmp_path):
    b=SessionBundle.read(make_bundle(tmp_path/"b","formal"));rows,qc=parse_turns(b.timing);assert next(x for x in rows if not x["hasFeedback"])["userEndToFeedbackAudioMs"] is None;assert not qc
def test_12_latency_exact_recompute(tmp_path):
    b=SessionBundle.read(make_bundle(tmp_path/"b","formal"));row=parse_turns(b.timing)[0][0];assert row["userEndToFeedbackAudioMs"]==3;assert row["userEndToDialogueAudioMs"]==6;assert row["feedbackToDialogueGapMs"]==2
def test_13_summary_mismatch_detected(tmp_path):
    b=SessionBundle.read(make_bundle(tmp_path/"b","formal"));next(x for x in b.timing if x["eventType"]=="TurnSummary")["userEndToDialogueAudioMs"]=99;_,qc=parse_turns(b.timing);assert any(x["ruleId"]=="summary_recompute_mismatch" for x in qc)
def test_14_goal_candidate_confirm_reject(tmp_path):
    b=SessionBundle.read(make_bundle(tmp_path/"b","formal"));states={x["state"] for x in parse_goals(b.study)};assert states=={"GoalCandidateSubmitted","GoalConfirmed","GoalRejected"}
def test_15_questionnaire_linkage(tmp_path):
    b=SessionBundle.read(make_bundle(tmp_path/"b","formal"));items,_,_=parse_questionnaire(b.questionnaire);assert items[0]["questionnaireLinkageKey"]=="q-1"
def test_16_reverse_score_formula(): assert reverse_score(2,1,7)==6
def test_17_reverse_score_validation(tmp_path):
    b=SessionBundle.read(make_bundle(tmp_path/"b","formal"));_,_,qc=parse_questionnaire(b.questionnaire);assert not qc;b.questionnaire[0]["scoredValue"]=2;assert parse_questionnaire(b.questionnaire)[2]
def test_18_scale_mean_sum(tmp_path):
    b=SessionBundle.read(make_bundle(tmp_path/"b","formal"));_,scores,_=parse_questionnaire(b.questionnaire);score=next(x for x in scores if x["scale"]=="Pressure / Tension");assert score["scaleMean"]==6;assert score["scaleSum"]==6
def test_19_revision_retained(tmp_path):
    b=SessionBundle.read(make_bundle(tmp_path/"b","formal"));b.questionnaire[0]["revision"]=2;assert parse_questionnaire(b.questionnaire)[0][0]["revision"]==2
def test_20_ranking_unique_in_fixture(bundle):
    b=SessionBundle.read(bundle);ranks=[x["rank"] for x in b.ranking if x["eventType"].endswith("RankingEntry")];assert len(ranks)==len(set(ranks))
def test_21_interview_linkage(tmp_path):
    b=SessionBundle.read(make_bundle(tmp_path/"b","formal"));assert b.interview[0]["interviewLinkageKey"]=="interview-1"
def test_22_technical_invalid_retained(tmp_path):
    b=SessionBundle.read(make_bundle(tmp_path/"b","pilot"));attempts=parse_attempts(b.study);assert any(x["isTechnicalInvalid"] for x in attempts)
def test_23_retry_does_not_overwrite(tmp_path):
    b=SessionBundle.read(make_bundle(tmp_path/"b","pilot"));attempts=parse_attempts(b.study);assert {x["conditionRunId"] for x in attempts}=={"pilot-invalid","run-1"}
def test_24_unconfirmed_attempt_policy_blocks_collection_primary(tmp_path):
    root=make_bundle(tmp_path/"b","formal",collection=True);result=analyze_bundle(root,tmp_path/"out",config_file(tmp_path,False,collection=True));assert not result["primaryAnalysisGenerated"];assert result["primaryAttemptPolicy"]=="UNCONFIRMED"
def test_25_exclusion_rules_output(tmp_path):
    root=make_bundle(tmp_path/"b","formal");out=tmp_path/"out";analyze_bundle(root,out,config_file(tmp_path,True));assert (out/"exclusions.csv").is_file()
def test_26_deterministic_content(bundle,tmp_path):
    cfg=config_file(tmp_path,True);one=analyze_bundle(bundle,tmp_path/"one",cfg);two=analyze_bundle(bundle,tmp_path/"two",cfg);assert one["outputContentHashExcludingRuntimeMetadata"]==two["outputContentHashExcludingRuntimeMetadata"]
def test_27_dictionary_complete():
    fields={x["field"] for x in dictionary_rows()};assert set(TABLES)=={"sessions.csv","assignments_long.csv","turns_long.csv","condition_summary.csv","goals_long.csv","questionnaire_items_long.csv","scale_scores.csv","rankings_long.csv","interviews_long.csv","all_attempts.csv","exclusions.csv"};assert {"provider","style","feedbackToDialogueGapMs","revision"}<=fields
def test_28_source_hashes_unchanged(bundle,tmp_path):
    before={p:sha256_file(bundle/p) for p in ["manifest.json","timing/timing.jsonl","study/study.jsonl"]};analyze_bundle(bundle,tmp_path/"out",config_file(tmp_path,True));after={p:sha256_file(bundle/p) for p in before};assert before==after
def test_29_stage8_formal_schema_analysis(tmp_path):
    root=make_bundle(tmp_path/"stage8-formal","formal");result=analyze_bundle(root,tmp_path/"out",config_file(tmp_path,True));assert result["tables"]["turns_long.csv"]==2
def test_30_stage8_pilot_schema_analysis(tmp_path):
    root=make_bundle(tmp_path/"stage8-pilot","pilot");result=analyze_bundle(root,tmp_path/"out",config_file(tmp_path,True));assert result["tables"]["all_attempts.csv"]==2
def test_31_cli_exit_codes(tmp_path):
    root=make_bundle(tmp_path/"b","formal");assert main(["validate-bundle",str(root)])==0;assert main(["validate-bundle",str(tmp_path/"missing")])==2
def test_32_formal_mapping_human_readable(): assert FORMAL["NE"]==("Non-Split / Dialogue Avatar","Explicit") and FORMAL["SR"]==("Split / Assistant Agent","Recast")

def test_33_editor_demo_requires_explicit_opt_in(tmp_path):
    root=make_bundle(tmp_path/"demo","formal")
    assignment=json.loads((root/"assignment/assignment.json").read_text());assignment.update({"dataOrigin":"editor_demo","collectionEligible":False,"developerTestAssignment":True,"demoMode":True})
    write_json(root/"assignment/assignment.json",assignment)
    manifest=json.loads((root/"manifest.json").read_text());manifest.update({"dataOrigin":"editor_demo","collectionEligible":False,"developerTestAssignment":True,"demoMode":True,"sessionMode":"editor_demo_formal"})
    write_json(root/"manifest.json",manifest);write_checksums(root)
    with pytest.raises(AnalysisError,match="includeDemoForTesting"): analyze_bundle(root,tmp_path/"out")

def test_34_editor_demo_explicit_test_mode_is_never_primary(tmp_path):
    root=make_bundle(tmp_path/"demo","formal")
    assignment=json.loads((root/"assignment/assignment.json").read_text());assignment.update({"dataOrigin":"editor_demo","collectionEligible":False,"developerTestAssignment":True,"demoMode":True})
    write_json(root/"assignment/assignment.json",assignment)
    manifest=json.loads((root/"manifest.json").read_text());manifest.update({"dataOrigin":"editor_demo","collectionEligible":False,"developerTestAssignment":True,"demoMode":True,"sessionMode":"editor_demo_formal"})
    write_json(root/"manifest.json",manifest);write_checksums(root)
    cfg=config_file(tmp_path,False);value=json.loads(cfg.read_text());value["includeDemoForTesting"]=True;write_json(cfg,value)
    result=analyze_bundle(root,tmp_path/"out",cfg);assert result["dataOrigin"]=="editor_demo";assert not result["primaryAnalysisGenerated"]

def test_35_official_collection_policy_generates_primary_dataset(tmp_path):
    root=make_bundle(tmp_path/"collection","formal",collection=True);cfg=config_file(tmp_path,False,collection=True);value=json.loads(cfg.read_text());value["primaryAttemptPolicy"]="latest_valid_completed_attempt";write_json(cfg,value)
    result=analyze_bundle(root,tmp_path/"out",cfg);assert result["primaryAnalysisGenerated"];assert result["primaryAttemptPolicy"]=="latest_valid_completed_attempt"
    with (tmp_path/"out"/"all_attempts.csv").open(encoding="utf-8-sig") as stream: rows=list(csv.DictReader(stream))
    assert len(rows)==1 and rows[0]["isPrimaryAttempt"]=="True"

def test_36_latest_valid_completed_attempt_retains_invalid_and_marks_latest():
    rows=[{"participantId":"p","sessionId":"s","conditionCode":"NE","conditionRunId":"bad","runAttempt":1,"isValidCompletedAttempt":False},{"participantId":"p","sessionId":"s","conditionCode":"NE","conditionRunId":"valid-1","runAttempt":2,"isValidCompletedAttempt":True},{"participantId":"p","sessionId":"s","conditionCode":"NE","conditionRunId":"valid-2","runAttempt":3,"isValidCompletedAttempt":True}]
    mark_primary_attempts(rows,"latest_valid_completed_attempt");assert len(rows)==3;assert [x["conditionRunId"] for x in rows if x["isPrimaryAttempt"]]==["valid-2"]


def config_file(root: Path, include: bool, collection: bool=False) -> Path:
    path=root/("collection-config.json" if collection else "synthetic-config.json")
    path.write_text(json.dumps({"analysisVersion":"1.0","includeSyntheticForTesting":include,"requireCollectionEligible":collection,"requireIntegrityPass":True,"primaryAttemptPolicy":"UNCONFIRMED","timingToleranceMs":0,"includeTranscriptText":False,"includeInterviewTextInAggregate":False,"allowedProtocolVersions":[],"missingRequiredQuestionnaireAction":"exclude_condition","technicalInvalidAction":"retain_and_flag"}),encoding="utf-8");return path


def make_bundle(root: Path, mode: str, collection: bool=False) -> Path:
    for folder in ["assignment","timing","study","questionnaire","ranking","interview","integrity"]:(root/folder).mkdir(parents=True,exist_ok=True)
    condition="NE" if mode=="formal" else "voice_only";task="hotel_check_in" if mode=="formal" else "pilot_restaurant_walk_in";run="run-1"
    if mode=="formal": assignment={"participantId":"p","experimentSessionId":"s","sequenceId":"test","protocolVersion":"fixture-protocol","taskCatalogVersion":"fixture-tasks","assignmentVersion":"fixture-assignment","developerTestAssignment":not collection,"dataOrigin":"participant_collection" if collection else "synthetic_dry_run","collectionEligible":collection,"conditions":[{"conditionPosition":0,"formalConditionCode":0,"formalConditionLabel":"NE","task":{"taskId":task,"taskAssignmentId":"ta-1"},"status":6,"latestConditionRunId":run,"runAttempt":1}]}
    else: assignment={"participantId":"p","sessionId":"s","sequenceId":"test","pilotProtocolVersion":"fixture-protocol","taskCatalogVersion":"fixture-tasks","pilotAssignmentVersion":"fixture-assignment","developerTestAssignment":not collection,"dataOrigin":"participant_collection" if collection else "synthetic_dry_run","collectionEligible":collection,"feedbackStyleLabel":"explicit","conditions":[{"conditionPosition":0,"embodimentCondition":0,"embodimentConditionLabel":"voice_only","task":{"taskId":task,"taskAssignmentId":"ta-1"},"status":7,"latestPilotRunId":run,"runAttempt":2}]}
    write_json(root/"assignment/assignment.json",assignment)
    timing=[];clock=0
    def event(kind,turn,feedback=True,**extra):
        nonlocal clock;value={"schemaVersion":"1.0","dataOrigin":"synthetic_dry_run","collectionEligible":False,"participantId":"p","sessionId":"s","protocolVersion":"fixture-protocol","taskCatalogVersion":"fixture-tasks","questionnaireCatalogVersion":"fixture-questionnaires","assignmentVersion":"fixture-assignment","eventType":kind,"conditionRunId":run,"questionnaireLinkageKey":"q-1","taskAssignmentId":"ta-1","conditionLabel":condition,"taskId":task,"turnId":turn,"technicalValidity":"Valid","monotonicElapsedMs":clock,"hasFeedback":feedback,"feedbackTextHash":"hash" if feedback else ""};value.update(extra);clock+=1;timing.append(value)
    for kind in ["UserSpeechEnded","DialogueGateClosed","CorrectionRequestStarted","CorrectionPlaybackStarted","CorrectionPlaybackEnded","DialogueGateOpened","DialoguePlaybackStarted","DialoguePlaybackEnded","TurnCompleted"]:event(kind,"turn-feedback")
    event("TurnSummary","turn-feedback",userEndToFeedbackAudioMs=3,userEndToDialogueAudioMs=6,feedbackToDialogueGapMs=2)
    for kind in ["UserSpeechEnded","DialogueGateClosed","DialogueGateOpened","DialoguePlaybackStarted","DialoguePlaybackEnded","TurnCompleted"]:event(kind,"turn-no-feedback",False)
    event("TurnSummary","turn-no-feedback",False,userEndToFeedbackAudioMs=-1,userEndToDialogueAudioMs=3,feedbackToDialogueGapMs=-1)
    write_jsonl(root/"timing/timing.jsonl",timing)
    study=[]
    def s(kind,run_id=run,valid="Valid",attempt=1,goal=""):study.append({"eventType":kind,"participantId":"p","sessionId":"s","conditionRunId":run_id,"questionnaireLinkageKey":"q-1","taskAssignmentId":"ta-1","conditionLabel":condition,"taskId":task,"technicalValidity":valid,"runAttempt":attempt,"goalId":goal,"monotonicElapsedMs":len(study)})
    if mode=="pilot":s("ConditionStarted","pilot-invalid","TechnicalInvalid",1);s("ConditionTechnicalInvalid","pilot-invalid","TechnicalInvalid",1);s("RetryAuthorized",run,"Valid",2)
    s("ConditionPrepared",attempt=2 if mode=="pilot" else 1);s("ConditionStarted",attempt=2 if mode=="pilot" else 1);s("GoalCandidateSubmitted",goal="g1");s("GoalConfirmed",goal="g1");s("GoalCandidateSubmitted",goal="g2");s("GoalRejected",goal="g2");s("ConditionCompleted");s("ConditionBoundaryReset")
    write_jsonl(root/"study/study.jsonl",study)
    q=[{"eventType":"QuestionnaireItem","participantId":"p","sessionId":"s","conditionRunId":run,"questionnaireLinkageKey":"q-1","taskAssignmentId":"ta-1","itemId":"pilot_item" if mode=="pilot" else "reverse_item","rawValue":"5" if mode=="pilot" else "2","scoredValue":5 if mode=="pilot" else 6,"reverseScored":mode=="formal","scaleMin":1,"scaleMax":7,"revision":1,"questionnaireStatus":"Submitted","conditionStatus":"Completed"},{"eventType":"QuestionnaireSubmitted","participantId":"p","sessionId":"s","conditionRunId":run,"questionnaireLinkageKey":"q-1"}]
    write_jsonl(root/"questionnaire/questionnaire.jsonl",q)
    ranking=[];labels=["NE","NR","SE","SR"] if mode=="formal" else ["voice_only","floating_orb","humanoid_agent"]
    for i,label in enumerate(labels,1):ranking.append({"eventType":"FinalRankingEntry" if mode=="formal" else "PilotFinalRankingEntry","participantId":"p","sessionId":"s","conditionLabel":label,"rank":i})
    write_jsonl(root/"ranking/ranking.jsonl",ranking)
    write_jsonl(root/"interview/interview.jsonl",[{"eventType":"InterviewSaved","participantId":"p","sessionId":"s","interviewLinkageKey":"interview-1","text":"restricted fixture"}] if mode=="formal" else [])
    write_json(root/"integrity/integrity-report.json",{"result":0})
    manifest={"bundleSchemaVersion":"1.0","dataOrigin":"participant_collection" if collection else "synthetic_dry_run","collectionEligible":collection,"sessionMode":mode,"participantId":"p","sessionId":"s","gitCommit":"060889b3a2deede6654f95adbdf3d77a8d06bec3","protocolVersion":"fixture-protocol","taskCatalogVersion":"fixture-tasks","questionnaireCatalogVersion":"fixture-questionnaires","assignmentVersion":"fixture-assignment","createdAtUtc":"2026-07-20T00:00:00Z","files":[],"integrityStatus":"PASS"}
    write_json(root/"manifest.json",manifest);write_checksums(root);return root


def write_json(path: Path,value):path.write_text(json.dumps(value,ensure_ascii=False,indent=2),encoding="utf-8")
def write_jsonl(path: Path,values):path.write_text("\n".join(json.dumps(x,ensure_ascii=False) for x in values)+("\n" if values else ""),encoding="utf-8")
def write_checksums(root: Path):
    files=sorted(x for x in root.rglob("*") if x.is_file() and x.name!="checksums.sha256");(root/"checksums.sha256").write_text("\n".join(f"{sha256_file(x)}  {x.relative_to(root).as_posix()}" for x in files)+"\n",encoding="utf-8")
