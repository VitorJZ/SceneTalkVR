using System;
using System.Collections;
using SceneTalkVR.Core;
using UnityEngine;

namespace SceneTalkVR.Demo
{
    public sealed class DemoBrainModule : MonoBehaviour, ISceneTalkBrain, ISceneTalkExperimentContextReceiver
    {
        [SerializeField]
        private float simulatedProcessingSeconds = 1.5f;

        private CorrectionExperimentCondition currentCondition;

        public void SetExperimentCondition(CorrectionExperimentCondition condition)
        {
            currentCondition = ExperimentConditionManager.CloneCondition(condition);
        }

        public IEnumerator GenerateSceneAndReply(string userText, Action<SpringScenePayload> onComplete, Action<string> onError)
        {
            if (string.IsNullOrWhiteSpace(userText))
            {
                onError?.Invoke("User transcript is empty.");
                yield break;
            }

            yield return new WaitForSeconds(Mathf.Max(0f, simulatedProcessingSeconds));

            var payload = BuildPayload(userText, currentCondition);

            onComplete?.Invoke(payload);
        }

        private static SpringScenePayload BuildPayload(string userText, CorrectionExperimentCondition condition)
        {
            if (condition != null && condition.task != null && !string.IsNullOrWhiteSpace(condition.scenarioId))
            {
                var experimentPayload = BuildExperimentPayload(userText, condition);
                experimentPayload.correctionFeedback = BuildCorrectionFeedback(userText, condition);
                ApplyExperimentConditionToCorrection(experimentPayload.correctionFeedback, condition);
                return experimentPayload;
            }

            var requestedGender = DetectGenderPresentation(userText, "unknown");
            SpringScenePayload payload;

            if (ContainsAny(userText, "police", "officer", "airport", "security", "customs", "警察", "警官", "安检", "海关"))
            {
                payload = BuildPolicePayload(requestedGender);
                payload.correctionFeedback = BuildCorrectionFeedback(userText, condition);
                return payload;
            }

            if (ContainsAny(userText, "teacher", "classroom", "school", "lesson", "exam", "教师", "老师", "课堂", "学校", "考试"))
            {
                payload = BuildTeacherPayload(requestedGender);
                payload.correctionFeedback = BuildCorrectionFeedback(userText, condition);
                return payload;
            }

            payload = BuildBaristaPayload(DetectGenderPresentation(userText, "female"));
            payload.correctionFeedback = BuildCorrectionFeedback(userText, condition);
            return payload;
        }

        private static SpringScenePayload BuildExperimentPayload(string userText, CorrectionExperimentCondition condition)
        {
            var task = condition.task;
            var fallbackRole = string.IsNullOrWhiteSpace(task.fallbackAvatarRole)
                ? "barista"
                : task.fallbackAvatarRole;
            var gender = DetectGenderPresentation(
                userText,
                string.IsNullOrWhiteSpace(task.fallbackAvatarGenderPresentation)
                    ? "unknown"
                    : task.fallbackAvatarGenderPresentation);
            var roleFamily = ResolveRoleFamily(fallbackRole);
            var initial = condition.turnIndex <= 1 || string.IsNullOrWhiteSpace(userText);

            return new SpringScenePayload
            {
                taskType = condition.scenarioId,
                environmentType = string.IsNullOrWhiteSpace(task.fallbackEnvironmentType)
                    ? condition.scenarioId
                    : task.fallbackEnvironmentType,
                dialogueReply = initial
                    ? ResolveInitialQuestion(task)
                    : ResolveFollowUpQuestion(condition.scenarioId),
                avatarRole = new AvatarRoleData
                {
                    role = fallbackRole,
                    speakingSpeed = "medium",
                    accent = "american",
                    attitude = string.IsNullOrWhiteSpace(task.fallbackAvatarAttitude)
                        ? "helpful"
                        : task.fallbackAvatarAttitude,
                    appearance = new AvatarAppearanceData
                    {
                        styleId = "semi_realistic_v1",
                        genderPresentation = gender,
                        ageBucket = "adult",
                        bodyBuild = "average",
                        outfitRole = roleFamily,
                        outfitColor = ResolveOutfitColor(roleFamily, gender),
                        seed = 42345 + Mathf.Abs((condition.scenarioId ?? string.Empty).GetHashCode() % 1000)
                    }
                },
                scene = new ScenePayload
                {
                    mode = "skybox",
                    skyboxUrl = string.IsNullOrWhiteSpace(task.fallbackSkyboxUrl)
                        ? $"demo://{condition.scenarioId}"
                        : task.fallbackSkyboxUrl,
                    layoutObjects = task.fallbackLayoutObjects ?? Array.Empty<LayoutObjectData>()
                }
            };
        }

        private static SpringScenePayload BuildBaristaPayload(string genderPresentation)
        {
            var male = IsGender(genderPresentation, "male");

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
                        genderPresentation = genderPresentation,
                        ageBucket = "young_adult",
                        bodyBuild = "average",
                        hairStyle = "short_curly",
                        hairColor = "black",
                        outfitRole = "barista",
                        outfitColor = male ? "red" : "green",
                        accessories = male ? Array.Empty<string>() : new[] { "round_black_glasses" },
                        mustHave = male ? Array.Empty<string>() : new[] { "green_apron" },
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

        private static SpringScenePayload BuildTeacherPayload(string genderPresentation)
        {
            var female = IsGender(genderPresentation, "female");

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
                        genderPresentation = genderPresentation,
                        ageBucket = "adult",
                        bodyBuild = "average",
                        outfitRole = "teacher",
                        outfitColor = female ? "black" : "blue",
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

        private static SpringScenePayload BuildPolicePayload(string genderPresentation)
        {
            var female = IsGender(genderPresentation, "female");

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
                        genderPresentation = genderPresentation,
                        ageBucket = "adult",
                        bodyBuild = "average",
                        outfitRole = "police",
                        outfitColor = female ? "grey" : "navy",
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

        private static string ResolveInitialQuestion(SceneTalkExperimentTask task)
        {
            return task == null || string.IsNullOrWhiteSpace(task.initialQuestion)
                ? "How can I help you today?"
                : task.initialQuestion;
        }

        private static string ResolveFollowUpQuestion(string scenarioId)
        {
            if (string.Equals(scenarioId, "restaurant_reservation", StringComparison.OrdinalIgnoreCase))
            {
                return "Thanks. Would you like indoor seating or outdoor seating?";
            }

            if (string.Equals(scenarioId, "furniture_shopping", StringComparison.OrdinalIgnoreCase))
            {
                return "That helps. What size or material would work best for your room?";
            }

            if (string.Equals(scenarioId, "gym_membership", StringComparison.OrdinalIgnoreCase))
            {
                return "Got it. Which facilities matter most to you?";
            }

            if (string.Equals(scenarioId, "hotel_check_in", StringComparison.OrdinalIgnoreCase))
            {
                return "Thank you. Could you confirm your ID and how many nights you are staying?";
            }

            return "Thanks. Could you tell me a little more?";
        }

        private static string ResolveRoleFamily(string role)
        {
            if (ContainsAny(role, "teacher", "instructor", "tutor", "trainer"))
            {
                return "teacher";
            }

            if (ContainsAny(role, "police", "officer", "security", "customs"))
            {
                return "police";
            }

            return "barista";
        }

        private static string ResolveOutfitColor(string roleFamily, string gender)
        {
            if (string.Equals(roleFamily, "teacher", StringComparison.OrdinalIgnoreCase))
            {
                return IsGender(gender, "female") ? "black" : "blue";
            }

            if (string.Equals(roleFamily, "police", StringComparison.OrdinalIgnoreCase))
            {
                return IsGender(gender, "female") ? "grey" : "navy";
            }

            return IsGender(gender, "male") ? "red" : "green";
        }

        private static void ApplyExperimentConditionToCorrection(
            CorrectionFeedbackData feedback,
            CorrectionExperimentCondition condition)
        {
            if (feedback == null || condition == null)
            {
                return;
            }

            feedback.provider = condition.provider;
            feedback.style = condition.style;
        }

        private static string DetectGenderPresentation(string value, string fallback)
        {
            if (ContainsAny(value, "female", "woman", "girl", "lady", "女", "女性", "女士", "女人"))
            {
                return "female";
            }

            if (ContainsAny(value, "male", "man", "boy", "gentleman", "男", "男性", "男士", "男人"))
            {
                return "male";
            }

            return fallback;
        }

        private static CorrectionFeedbackData BuildCorrectionFeedback(string userText, CorrectionExperimentCondition condition)
        {
            bool triggerCorrection = ContainsAny(userText, "correction", "corrective", "feedback", "explicit", "recast", "纠错", "反馈", "更正", "very like");
            if (!triggerCorrection)
            {
                return condition == null
                    ? null
                    : new CorrectionFeedbackData
                    {
                        hasFeedback = false,
                        provider = condition.provider,
                        style = condition.style
                    };
            }

            var provider = (condition != null && !string.IsNullOrEmpty(condition.provider)) 
                ? condition.provider 
                : (ContainsAny(userText, "assistant", "agent", "assistant_agent", "helper", "小助手", "辅助") ? "assistant_agent" : "dialogue_avatar");

            var style = (condition != null && !string.IsNullOrEmpty(condition.style)) 
                ? condition.style 
                : (ContainsAny(userText, "recast", "natural", "重述", "自然") ? "recast" : "explicit");

            var recast = string.Equals(style, "recast", StringComparison.OrdinalIgnoreCase);

            if (ContainsAny(userText, "very like"))
            {
                return new CorrectionFeedbackData
                {
                    hasFeedback = true,
                    provider = provider,
                    style = style,
                    errorType = "grammar",
                    originalText = "I very like this topic.",
                    correctedText = "I really like this topic.",
                    feedbackText = recast
                        ? (string.Equals(provider, "assistant_agent") ? "You mean you really like this topic?" : "Oh, you really like this topic?")
                        : (string.Equals(provider, "assistant_agent") ? "Remember to say: I really like this topic, not I very like this topic." : "You can say: I really like this topic."),
                    targetSpan = "very like",
                    confidence = 0.95f
                };
            }

            return new CorrectionFeedbackData
            {
                hasFeedback = true,
                provider = provider,
                style = style,
                errorType = "grammar",
                originalText = "I want latte.",
                correctedText = "I'd like a latte, please.",
                feedbackText = recast
                    ? (string.Equals(provider, "assistant_agent") ? "You mean you'd like a latte?" : "Sure, a latte, please.")
                    : (string.Equals(provider, "assistant_agent") ? "You should say: I'd like a latte, please." : "Try saying: I'd like a latte, please."),
                targetSpan = "want latte",
                confidence = 0.92f
            };
        }

        private static bool IsGender(string value, string expected)
        {
            return string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);
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
