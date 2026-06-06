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

            var payload = BuildPayload(userText);

            onComplete?.Invoke(payload);
        }

        private static SpringScenePayload BuildPayload(string userText)
        {
            if (ContainsAny(userText, "police", "officer", "airport", "security", "customs"))
            {
                return BuildPolicePayload();
            }

            if (ContainsAny(userText, "teacher", "classroom", "school", "lesson", "exam"))
            {
                return BuildTeacherPayload();
            }

            return BuildBaristaPayload();
        }

        private static SpringScenePayload BuildBaristaPayload()
        {
            return new SpringScenePayload
            {
                taskType = "ordering_coffee",
                environmentType = "coffee_shop",
                dialogueReply = "Hi there. What would you like to order today?",
                avatarRole = new AvatarRoleData
                {
                    role = "barista",
                    speakingSpeed = "fast",
                    accent = "american",
                    attitude = "friendly",
                    appearance = new AvatarAppearanceData
                    {
                        styleId = "semi_realistic_v1",
                        genderPresentation = "female",
                        ageBucket = "young_adult",
                        bodyBuild = "average",
                        hairStyle = "short_curly",
                        hairColor = "black",
                        outfitRole = "barista",
                        outfitColor = "green",
                        accessories = new[] { "round_black_glasses" },
                        mustHave = new[] { "green_apron" },
                        seed = 12345
                    }
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
        }

        private static SpringScenePayload BuildTeacherPayload()
        {
            return new SpringScenePayload
            {
                taskType = "classroom_practice",
                environmentType = "classroom",
                dialogueReply = "Good afternoon. Let's practice answering this question clearly.",
                avatarRole = new AvatarRoleData
                {
                    role = "teacher",
                    speakingSpeed = "medium",
                    accent = "british",
                    attitude = "patient",
                    appearance = new AvatarAppearanceData
                    {
                        styleId = "semi_realistic_v1",
                        genderPresentation = "unknown",
                        ageBucket = "adult",
                        bodyBuild = "average",
                        outfitRole = "teacher",
                        outfitColor = "blue",
                        seed = 22345
                    }
                },
                scene = new ScenePayload
                {
                    mode = "skybox",
                    skyboxUrl = "demo://classroom-360",
                    layoutObjects = new[]
                    {
                        new LayoutObjectData
                        {
                            prefabKey = "desk",
                            position = new Vector3(0.7f, 0f, 1.25f),
                            rotationY = 10f
                        },
                        new LayoutObjectData
                        {
                            prefabKey = "whiteboard",
                            position = new Vector3(-0.75f, 0f, 1.5f),
                            rotationY = -8f
                        }
                    }
                }
            };
        }

        private static SpringScenePayload BuildPolicePayload()
        {
            return new SpringScenePayload
            {
                taskType = "asking_for_directions",
                environmentType = "airport",
                dialogueReply = "Please stay calm. Where are you trying to go?",
                avatarRole = new AvatarRoleData
                {
                    role = "police",
                    speakingSpeed = "medium",
                    accent = "american",
                    attitude = "serious",
                    appearance = new AvatarAppearanceData
                    {
                        styleId = "semi_realistic_v1",
                        genderPresentation = "unknown",
                        ageBucket = "adult",
                        bodyBuild = "average",
                        outfitRole = "police",
                        outfitColor = "navy",
                        accessories = new[] { "badge", "cap" },
                        mustHave = new[] { "badge" },
                        seed = 32345
                    }
                },
                scene = new ScenePayload
                {
                    mode = "skybox",
                    skyboxUrl = "demo://airport-360",
                    layoutObjects = new[]
                    {
                        new LayoutObjectData
                        {
                            prefabKey = "sign",
                            position = new Vector3(0.85f, 0f, 1.35f),
                            rotationY = 25f
                        },
                        new LayoutObjectData
                        {
                            prefabKey = "barrier",
                            position = new Vector3(-0.65f, 0f, 1.2f),
                            rotationY = -20f
                        }
                    }
                }
            };
        }

        private static bool ContainsAny(string value, params string[] keywords)
        {
            if (string.IsNullOrWhiteSpace(value) || keywords == null)
            {
                return false;
            }

            for (var i = 0; i < keywords.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(keywords[i]) &&
                    value.IndexOf(keywords[i], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
