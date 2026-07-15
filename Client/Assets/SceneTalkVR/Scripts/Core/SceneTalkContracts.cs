using System;
using System.Collections;
using UnityEngine;

namespace SceneTalkVR.Core
{
    public interface ISceneTalkSpeechInput
    {
        IEnumerator CaptureSpeech(Action<string> onComplete, Action<string> onError);
    }

    public interface ISceneTalkManualSpeechInput
    {
        void RequestStopCapture();

        void CancelCapture();
    }

    public interface ISceneTalkBrain
    {
        IEnumerator GenerateSceneAndReply(string userText, Action<SpringScenePayload> onComplete, Action<string> onError);
    }

    public interface ISceneTalkStreamingBrain : ISceneTalkBrain
    {
        IEnumerator GenerateSceneAndReplyStreaming(string userText, Action<string> onSentenceComplete, Action<SpringScenePayload> onComplete, Action<string> onError);
    }

    public interface ISceneTalkScenePresenter
    {
        IEnumerator PresentScene(SpringScenePayload payload, Action onComplete, Action<string> onError);
    }

    public interface ISceneTalkAvatarVoice
    {
        IEnumerator PresentReply(SpringScenePayload payload, Action onComplete, Action<string> onError);
    }

    public interface ISceneTalkStreamingAvatarVoice : ISceneTalkAvatarVoice
    {
        void PrepareStreaming(SpringScenePayload basePayload);
        void EnqueueSentence(string sentence);
        void SignalStreamingComplete();
    }

    public interface ISceneTalkAvatarReplyContext
    {
        void SetReplyContext(bool isOpeningReply);
    }

    public interface ISceneTalkAvatarSessionReset
    {
        void ClearAvatar();
    }

    public interface ISceneTalkExperimentContextReceiver
    {
        void SetExperimentCondition(CorrectionExperimentCondition condition);
    }

    public interface ISceneTalkCorrectionFeedbackProviderReceiver
    {
        void SetCorrectionFeedbackProvider(string provider);
    }

    [Serializable]
    public sealed class SpringScenePayload
    {
        public string taskType;
        public string environmentType;
        public string dialogueReply;
        public AvatarRoleData avatarRole = new AvatarRoleData();
        public ScenePayload scene = new ScenePayload();
        public CorrectionFeedbackData correctionFeedback;
    }

    [Serializable]
    public sealed class CorrectionFeedbackData
    {
        public bool hasFeedback;
        public string provider;
        public string style;
        public string errorType;
        public string originalText;
        public string correctedText;
        public string feedbackText;
        public string targetSpan;
        public float confidence;
        public string rationaleTag;
    }

    [Serializable]
    public sealed class CorrectionExperimentCondition
    {
        public string participantId;
        public string sessionId;
        public bool formalExperiment;
        public string conditionId;
        public string scenarioId;
        public string provider;
        public string style;
        public int turnIndex;
        public string[] conditionOrder = Array.Empty<string>();
        public SceneTalkExperimentTask task = new SceneTalkExperimentTask();
    }

    [Serializable]
    public sealed class SceneTalkExperimentTask
    {
        public string scenarioId;
        public string context;
        public string[] goals = Array.Empty<string>();
        public string initialQuestion;
        public string fallbackEnvironmentType;
        public string fallbackAvatarRole;
        public string fallbackAvatarGenderPresentation;
        public string fallbackAvatarAttitude;
        public string fallbackSkyboxUrl;
        public LayoutObjectData[] fallbackLayoutObjects = Array.Empty<LayoutObjectData>();
    }

    [Serializable]
    public sealed class AvatarRoleData
    {
        public string role;
        public string speakingSpeed;
        public string accent;
        public string attitude;
        public AvatarAppearanceData appearance = new AvatarAppearanceData();
    }

    [Serializable]
    public sealed class AvatarAppearanceData
    {
        public string styleId;
        public string genderPresentation;
        public string ageBucket;
        public string bodyBuild;
        public string hairStyle;
        public string hairColor;
        public string outfitRole;
        public string outfitColor;
        public string[] accessories = Array.Empty<string>();
        public string[] mustHave = Array.Empty<string>();
        public string[] mustNotHave = Array.Empty<string>();
        public string[] unsupported = Array.Empty<string>();
        public int seed;
    }

    [Serializable]
    public sealed class ScenePayload
    {
        public string mode;
        public string skyboxUrl;
        public LayoutObjectData[] layoutObjects = Array.Empty<LayoutObjectData>();
    }

    [Serializable]
    public sealed class LayoutObjectData
    {
        public string prefabKey;
        public Vector3 position;
        public float rotationY;
    }

    [Serializable]
    public sealed class EdwinVoicePayload
    {
        public string sttText;
        public string ttsAudioPath;
        public string avatarPrefabKey;
        public string avatarStatus;
    }
    public interface ISceneTalkSessionReset
    {
        void ResetSession();
    }

    public interface ISceneTalkExperimentLockReceiver
    {
        void SetExperimentLocked(bool isLocked);
    }
}
