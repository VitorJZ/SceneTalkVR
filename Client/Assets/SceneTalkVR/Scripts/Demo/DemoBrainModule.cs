using System;
using System.Collections;
using SceneTalkVR.Core;
using UnityEngine;

namespace SceneTalkVR.Demo
{
    public sealed class DemoBrainModule : MonoBehaviour, ISceneTalkBrain
    {
        [SerializeField]
        private float simulatedProcessingSeconds = 1.5f;

        public IEnumerator GenerateSceneAndReply(string userText, Action<SpringScenePayload> onComplete, Action<string> onError)
        {
            if (string.IsNullOrWhiteSpace(userText))
            {
                onError?.Invoke("User transcript is empty.");
                yield break;
            }

            yield return new WaitForSeconds(Mathf.Max(0f, simulatedProcessingSeconds));

            var payload = new SpringScenePayload
            {
                taskType = "ordering_coffee",
                environmentType = "coffee_shop",
                dialogueReply = "Hi there. What would you like to order today?",
                avatarRole = new AvatarRoleData
                {
                    role = "barista",
                    speakingSpeed = "fast",
                    accent = "american",
                    attitude = "friendly"
                },
                scene = new ScenePayload
                {
                    mode = "skybox",
                    skyboxUrl = "demo://coffee-shop-360",
                    layoutObjects = new[]
                    {
                        new LayoutObjectData
                        {
                            prefabKey = "coffee_table",
                            position = new Vector3(0.8f, 0f, 1.2f),
                            rotationY = 20f
                        },
                        new LayoutObjectData
                        {
                            prefabKey = "menu",
                            position = new Vector3(-0.6f, 0f, 1.35f),
                            rotationY = -15f
                        }
                    }
                }
            };

            onComplete?.Invoke(payload);
        }
    }
}
