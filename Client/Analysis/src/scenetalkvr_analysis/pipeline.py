from __future__ import annotations

import hashlib
import json
from pathlib import Path
from typing import Any

from . import __version__
from .assignment_parser import parse_assignments
from .bundle_reader import BundleError, SessionBundle
from .config import load_config
from .dictionary import TABLES
from .exclusions import session_exclusions
from .exports import write_csv, write_json
from .interview_parser import parse_interviews
from .quality_control import qc_summary
from .questionnaire_parser import parse_questionnaire
from .ranking_parser import parse_rankings
from .report import markdown_qc
from .study_parser import condition_summaries, parse_attempts, parse_goals
from .timing_parser import parse_turns


class AnalysisError(RuntimeError): pass


def validate_bundle(path: str | Path) -> dict[str, Any]:
    bundle=SessionBundle.read(path,True)
    return {"status":"PASS","sourceBundle":str(bundle.root),"sourceManifestHash":bundle.manifest_hash,"fileCount":len(bundle.source_hashes),"participantId":bundle.manifest.get("participantId",""),"sessionId":bundle.manifest.get("sessionId",""),"dataOrigin":bundle.manifest.get("dataOrigin",""),"collectionEligible":bundle.manifest.get("collectionEligible",False),"integrityStatus":bundle.manifest.get("integrityStatus","")}


def analyze_bundle(path: str | Path, output: str | Path, config_path: str | Path | None = None) -> dict[str, Any]:
    bundle=SessionBundle.read(path,True);config,config_hash=load_config(config_path);before=dict(bundle.source_hashes)
    exclusions=session_exclusions(bundle.manifest,bundle.assignment,config)
    synthetic=bundle.manifest.get("dataOrigin") in {"synthetic_dry_run","synthetic_matrix","developer_placeholder_matrix"}
    if synthetic and not config.get("includeSyntheticForTesting",False):
        raise AnalysisError("synthetic_input_requires_includeSyntheticForTesting")
    assignments=parse_assignments(bundle.manifest,bundle.assignment)
    turns,timing_qc=parse_turns(bundle.timing,int(config.get("timingToleranceMs",0)));exclusions.extend(timing_qc)
    goals=parse_goals(bundle.study);attempts=parse_attempts(bundle.study);conditions=condition_summaries(bundle.study,turns,goals,attempts)
    items,scores,questionnaire_qc=parse_questionnaire(bundle.questionnaire);exclusions.extend(questionnaire_qc)
    rankings=parse_rankings(bundle.ranking);interviews=parse_interviews(bundle.interview,bool(config.get("includeInterviewTextInAggregate",False)))
    collection=bool(bundle.manifest.get("collectionEligible",False)) and not synthetic
    primary_blocked=collection and config.get("primaryAttemptPolicy")=="UNCONFIRMED"
    if primary_blocked: exclusions.append(_session_exclusion(bundle,"primary_attempt_policy_unconfirmed","Primary analysis is blocked until an attempt-selection policy is approved."))
    included=not exclusions and not primary_blocked
    session={"participantId":bundle.manifest.get("participantId",""),"sessionId":bundle.manifest.get("sessionId",""),"sessionMode":bundle.manifest.get("sessionMode",""),"dataOrigin":bundle.manifest.get("dataOrigin",""),"collectionEligible":bundle.manifest.get("collectionEligible",False),"gitCommit":bundle.manifest.get("gitCommit",""),"protocolVersion":bundle.manifest.get("protocolVersion",""),"taskCatalogVersion":bundle.manifest.get("taskCatalogVersion",""),"questionnaireCatalogVersion":bundle.manifest.get("questionnaireCatalogVersion",""),"assignmentVersion":bundle.manifest.get("assignmentVersion",""),"integrityStatus":bundle.manifest.get("integrityStatus",""),"inclusionStatus":"included" if included else "excluded_or_qc_only","exclusionReasons":";".join(sorted({x["ruleId"] for x in exclusions}))}
    tables={"sessions.csv":[session],"assignments_long.csv":assignments,"turns_long.csv":turns,"condition_summary.csv":conditions,"goals_long.csv":goals,"questionnaire_items_long.csv":items,"scale_scores.csv":scores,"rankings_long.csv":rankings,"interviews_long.csv":interviews,"all_attempts.csv":attempts,"exclusions.csv":exclusions}
    root=Path(output);root.mkdir(parents=True,exist_ok=True)
    for name,rows in tables.items(): write_csv(root/name,rows,TABLES.get(name))
    qc=qc_summary([session],exclusions,conditions,attempts,turns);(root/"qc_report.md").write_text(markdown_qc(qc),encoding="utf-8")
    after=SessionBundle.read(path,True).source_hashes
    if before!=after: raise AnalysisError("source_bundle_modified")
    content_hash=_content_hash(tables)
    manifest={"analysisSchemaVersion":"1.0","analysisVersion":config.get("analysisVersion",__version__),"analysisCodeVersion":__version__,"sourceBundle":str(bundle.root),"sourceManifestHash":bundle.manifest_hash,"sourceFileHashes":before,"analysisConfigHash":config_hash,"generatedAtUtc":__import__('datetime').datetime.now(__import__('datetime').timezone.utc).isoformat(),"dataOrigin":bundle.manifest.get("dataOrigin",""),"collectionEligible":bundle.manifest.get("collectionEligible",False),"developerTestAssignment":bundle.assignment.get("developerTestAssignment",False),"containsFreeText":bool(config.get("includeTranscriptText",False) or config.get("includeInterviewTextInAggregate",False)),"restrictedAccess":bool(config.get("includeTranscriptText",False) or config.get("includeInterviewTextInAggregate",False)),"primaryAnalysisGenerated":included and not primary_blocked,"primaryAttemptPolicy":config.get("primaryAttemptPolicy"),"outputContentHashExcludingRuntimeMetadata":content_hash,"tables":{key:len(value) for key,value in tables.items()},"qc":qc}
    write_json(root/"analysis_manifest.json",manifest)
    return manifest


def analyze_batch(path: str | Path, output: str | Path, config_path: str | Path | None = None) -> dict[str, Any]:
    roots=sorted({x.parent for x in Path(path).rglob("manifest.json") if (x.parent/"checksums.sha256").is_file()})
    results=[];errors=[]
    for bundle in roots:
        try: results.append(analyze_bundle(bundle,Path(output)/f"{bundle.name}",config_path))
        except (AnalysisError,BundleError) as exc: errors.append({"bundle":str(bundle),"error":str(exc)})
    value={"bundleCount":len(roots),"analyzedCount":len(results),"errorCount":len(errors),"results":results,"errors":errors}
    write_json(Path(output)/"batch_manifest.json",value);return value


def _content_hash(tables: dict[str,list[dict[str,Any]]]) -> str:
    canonical=json.dumps(tables,ensure_ascii=False,sort_keys=True,separators=(",",":"),default=str)
    return hashlib.sha256(canonical.encode("utf-8")).hexdigest()
def _session_exclusion(bundle: SessionBundle,rule: str,reason: str)->dict[str,Any]: return {"scope":"session","participantId":bundle.manifest.get("participantId",""),"sessionId":bundle.manifest.get("sessionId",""),"conditionRunId":"","turnId":"","ruleId":rule,"severity":"FAIL","reason":reason,"sourceEvidence":"analysis_config"}
