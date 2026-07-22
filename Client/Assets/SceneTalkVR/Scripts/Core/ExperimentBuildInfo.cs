using UnityEngine;

namespace SceneTalkVR.Core
{
    [CreateAssetMenu(fileName = "ExperimentBuildInfo", menuName = "SceneTalkVR/Experiment Build Info")]
    public sealed class ExperimentBuildInfo : ScriptableObject
    {
        [SerializeField] private string gitCommit;
        [SerializeField] private string activeBranch;
        [SerializeField] private string buildVersion;
        [SerializeField] private string buildTimestampUtc;
        [SerializeField] private string unityVersion;
        [SerializeField] private string protocolVersion;
        public string GitCommit => gitCommit ?? string.Empty;
        public string ActiveBranch => activeBranch ?? string.Empty;
        public string BuildVersion => buildVersion ?? string.Empty;
        public string BuildTimestampUtc => buildTimestampUtc ?? string.Empty;
        public string UnityVersion => unityVersion ?? string.Empty;
        public string ProtocolVersion => protocolVersion ?? string.Empty;
    }
}
