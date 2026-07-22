using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SceneTalkVR.Core
{
    public enum RehearsalDecisionStatus { ApprovedForRehearsal }
    public enum RehearsalProtocolPurpose { OperationalRehearsal }
    public enum RehearsalPreflightStatus { REHEARSAL_READY, REHEARSAL_WARNING, REHEARSAL_BLOCKED }

    [Serializable]
    public sealed class RehearsalProtocolDecision
    {
        public string decisionId;
        [TextArea] public string confirmedValue;
        public RehearsalDecisionStatus status = RehearsalDecisionStatus.ApprovedForRehearsal;
        public string confirmedBy = "Project Lead";
        public string confirmedAtUtc = "2026-07-20T00:00:00Z";
        public string evidenceReference = "scenetalkvr-rehearsal-baseline-v1";
        [TextArea] public string notes = "Approved operational fallback; may be revised after team review";
    }

    [Serializable]
    public sealed class RehearsalAvatarMapping
    {
        public string taskId;
        public string taskRole;
        public string avatarPresetKey;
        public bool approvedForRehearsal = true;
        public bool approvedForCollection;
    }

    [Serializable]
    public sealed class RehearsalPanoramaApproval
    {
        public string taskId;
        public string panoramaResourceKey;
        public bool approvedForRehearsal = true;
        public bool replaceableAsset = true;
        public string knownRisk;
    }

    [Serializable]
    public sealed class RehearsalPreflightResult
    {
        public RehearsalPreflightStatus status;
        public string flowMode;
        public string[] checks = Array.Empty<string>();
        public string[] warnings = Array.Empty<string>();
        public string[] blockers = Array.Empty<string>();
    }
}
