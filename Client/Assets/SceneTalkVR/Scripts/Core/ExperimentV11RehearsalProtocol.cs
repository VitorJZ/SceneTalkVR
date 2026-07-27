using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SceneTalkVR.Core
{
    [CreateAssetMenu(fileName = "ExperimentV11RehearsalProtocol", menuName = "SceneTalkVR/Experiment/Rehearsal Protocol")]
    public sealed class ExperimentV11RehearsalProtocol : ScriptableObject
    {
        [SerializeField] private string protocolVersion = "1.1-rehearsal-2";
        [SerializeField] private RehearsalProtocolPurpose protocolPurpose = RehearsalProtocolPurpose.OperationalRehearsal;
        [SerializeField] private bool approvedForRehearsal = true;
        [SerializeField] private bool approvedForCollection;
        [SerializeField] private string approvalAuthority = "Project Lead Approval";
        [SerializeField] private string evidenceReference = "scenetalkvr-rehearsal-participant-choice-v2";
        [SerializeField] private string protocolSnapshotId = "v1.1-rehearsal-2-protocol";
        [SerializeField] private RehearsalProtocolDecision[] decisions = CreateDecisions();
        public string ProtocolVersion => protocolVersion?.Trim() ?? string.Empty;
        public RehearsalProtocolPurpose ProtocolPurpose => protocolPurpose;
        public bool ApprovedForRehearsal => approvedForRehearsal;
        public bool ApprovedForCollection => approvedForCollection;
        public string ApprovalAuthority => approvalAuthority?.Trim() ?? string.Empty;
        public string EvidenceReference => evidenceReference?.Trim() ?? string.Empty;
        public string ProtocolSnapshotId => protocolSnapshotId?.Trim() ?? string.Empty;
        public IReadOnlyList<RehearsalProtocolDecision> Decisions => decisions;
        public AssignmentPolicy FormalAssignmentPolicy => AssignmentPolicy.StrictWithoutReplacement;
        public FormalConditionOrderPolicy FormalConditionOrderPolicy => SceneTalkVR.Core.FormalConditionOrderPolicy.ParticipantChoice;
        public GoalConfirmationPolicy GoalConfirmationPolicy => SceneTalkVR.Core.GoalConfirmationPolicy.AutomaticOnValidatedDetection;
        public QuestionnaireReturnPolicy QuestionnaireReturnPolicy => SceneTalkVR.Core.QuestionnaireReturnPolicy.ReturnToModeSelection;
        public string FormalTaskAssignmentPolicy => "random_bijection_without_replacement";
        public PilotFeedbackStyleChoice PilotFeedbackStyle => PilotFeedbackStyleChoice.Explicit;
        public PilotAudioSourcePolicy VoiceOnlyAudioPolicy => PilotAudioSourcePolicy.NonSpatialHeadLocked;
        public int FormalMaxTurns => 9; public float FormalMaxDurationMinutes => 10f; public int PilotMaxTurns => 8; public float PilotMaxDurationMinutes => 8f;
        public AssignmentSequence[] FormalSequences => new[] { Sequence("a-b-c-d",FormalConditionCode.NE,FormalConditionCode.NR,FormalConditionCode.SE,FormalConditionCode.SR),Sequence("b-c-d-a",FormalConditionCode.NR,FormalConditionCode.SE,FormalConditionCode.SR,FormalConditionCode.NE),Sequence("c-d-a-b",FormalConditionCode.SE,FormalConditionCode.SR,FormalConditionCode.NE,FormalConditionCode.NR),Sequence("d-a-b-c",FormalConditionCode.SR,FormalConditionCode.NE,FormalConditionCode.NR,FormalConditionCode.SE) };
        public PilotSequenceDefinition[] PilotSequences => new[] { PilotSequence("a-b-c",PilotEmbodimentCondition.VoiceOnly,PilotEmbodimentCondition.FloatingOrb,PilotEmbodimentCondition.HumanoidAgent),PilotSequence("b-c-a",PilotEmbodimentCondition.FloatingOrb,PilotEmbodimentCondition.HumanoidAgent,PilotEmbodimentCondition.VoiceOnly),PilotSequence("c-a-b",PilotEmbodimentCondition.HumanoidAgent,PilotEmbodimentCondition.VoiceOnly,PilotEmbodimentCondition.FloatingOrb) };
        public bool Validate(out string error)
        {
            var issues=new List<string>();var required=RequiredValues();if(ProtocolVersion!="1.1-rehearsal-2")issues.Add("rehearsal_protocol_version_invalid");if(!approvedForRehearsal||approvedForCollection)issues.Add("rehearsal_qualification_invalid");if(ProtocolPurpose!=RehearsalProtocolPurpose.OperationalRehearsal)issues.Add("rehearsal_purpose_invalid");if(ApprovalAuthority!="Project Lead Approval"||EvidenceReference!="scenetalkvr-rehearsal-participant-choice-v2")issues.Add("rehearsal_approval_provenance_invalid");if(FormalConditionOrderPolicy!=FormalConditionOrderPolicy.ParticipantChoice||GoalConfirmationPolicy!=GoalConfirmationPolicy.AutomaticOnValidatedDetection||QuestionnaireReturnPolicy!=QuestionnaireReturnPolicy.ReturnToModeSelection||FormalTaskAssignmentPolicy!="random_bijection_without_replacement")issues.Add("rehearsal_flow_policy_invalid");if(decisions==null||decisions.Length!=required.Count)issues.Add("rehearsal_requires_eleven_decisions");foreach(var pair in required){var item=decisions?.FirstOrDefault(x=>x!=null&&x.decisionId==pair.Key);if(item==null||item.status!=RehearsalDecisionStatus.ApprovedForRehearsal||item.confirmedValue!=pair.Value)issues.Add("rehearsal_decision_invalid:"+pair.Key);else if(item.confirmedBy!="Project Lead"||string.IsNullOrWhiteSpace(item.confirmedAtUtc)||item.evidenceReference!="scenetalkvr-rehearsal-baseline-v1")issues.Add("rehearsal_decision_provenance_invalid:"+pair.Key);}error=string.Join(";",issues);return issues.Count==0;
        }
        public static IReadOnlyDictionary<string,string> RequiredValues()=>new Dictionary<string,string>{{"condition_letter_mapping","a=NE,b=NR,c=SE,d=SR"},{"formal_task_no_replacement","strict_without_replacement"},{"formal_social_comfort","excluded"},{"pilot_feedback_style","explicit"},{"voice_only_spatial_audio","non_spatial_head_locked"},{"pilot_sequence_mapping","a=voice_only,b=floating_orb,c=humanoid_agent"},{"formal_max_turns","9"},{"formal_max_duration","10 minutes"},{"pilot_max_turns","8"},{"pilot_max_duration","8 minutes"},{"questionnaire_scale_anchors","1 = Strongly disagree / 非常不同意; 7 = Strongly agree / 非常同意"}};
        private static RehearsalProtocolDecision[] CreateDecisions()=>RequiredValues().Select(x=>new RehearsalProtocolDecision{decisionId=x.Key,confirmedValue=x.Value}).ToArray();
        private static AssignmentSequence Sequence(string id,params FormalConditionCode[] values)=>new AssignmentSequence{sequenceId=id,conditions=values};
        private static PilotSequenceDefinition PilotSequence(string id,params PilotEmbodimentCondition[] values)=>new PilotSequenceDefinition{sequenceId=id,conditions=values,confirmed=true};
    }
}
