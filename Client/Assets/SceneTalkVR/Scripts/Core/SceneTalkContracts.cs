using System;
using System.Collections;
using UnityEngine;

namespace SceneTalkVR.Core
{
    public interface ISceneTalkSpeechInput
    {
        IEnumerator CaptureSpeech(Action<string> onComplete, Action<string> onError);
    }

    public interface ISceneTalkBrain
    {
        IEnumerator GenerateSceneAndReply(string userText, Action<SpringScenePayload> onComplete, Action<string> onError);
    }

    public interface ISceneTalkScenePresenter
    {
        IEnumerator PresentScene(SpringScenePayload payload, Action onComplete, Action<string> onError);
    }

    public interface ISceneTalkAvatarVoice
    {
        IEnumerator PresentReply(SpringScenePayload payload, Action onComplete, Action<string> onError);
    }

    [Serializable]
    public sealed class SpringScenePayload
    {
        public string taskType;
        public string environmentType;
        public string dialogueReply;
        public AvatarRoleData avatarRole = new AvatarRoleData();
        public ScenePayload scene = new ScenePayload();
    }

    [Serializable]
    public sealed class AvatarRoleData
    {
        public string role;
        public string speakingSpeed;
        public string accent;
        public string attitude;
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
}
