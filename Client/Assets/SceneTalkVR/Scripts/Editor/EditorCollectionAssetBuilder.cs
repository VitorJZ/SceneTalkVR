using System;
using System.Linq;
using SceneTalkVR.AvatarSystem;
using SceneTalkVR.Core;
using UnityEditor;
using UnityEngine;

namespace SceneTalkVR.EditorTools
{
    public static class EditorCollectionAssetBuilder
    {
        public const string Root = "Assets/SceneTalkVR/ExperimentProtocol/";
        public const string ProtocolPath = Root + "ExperimentV11Protocol.asset";
        public const string TaskPath = Root + "ExperimentTaskCatalog.asset";
        public const string QuestionnairePath = Root + "ExperimentQuestionnaireCatalog.asset";
        public const string VoicePath = Root + "ExperimentVoiceProfileCatalog.asset";
        public const string DeploymentPath = Root + "ExperimentDeploymentCatalog.asset";
        public const string ResourcePath = Root + "ExperimentEditorCollectionResources.asset";
        public const string PilotPresentationPath = Root + "PilotPresentationCatalog.asset";
        public const string AvatarPath = "Assets/SceneTalkVR/Avatar/Catalogs/AvatarCatalog.asset";

        [MenuItem("SceneTalkVR/Experiment/Editor Collection/Create or Update Official Assets")]
        public static void CreateOrUpdate()
        {
            var now = DateTime.UtcNow.ToString("o");
            var protocol = AssetDatabase.LoadAssetAtPath<ExperimentV11ProtocolConfig>(ProtocolPath);
            var previousProtocolVersion = protocol == null ? string.Empty : protocol.ProtocolVersion;
            var decisions = new[]
            {
                Decision("condition_letter_mapping", "a=NE,b=NR,c=SE,d=SR", now),
                Decision("formal_task_no_replacement", "strict_without_replacement", now),
                Decision("formal_social_comfort", "excluded", now),
                Decision("pilot_feedback_style", "explicit", now),
                Decision("voice_only_spatial_audio", "non_spatial_head_locked", now),
                Decision("pilot_sequence_mapping", "a=voice_only,b=floating_orb,c=humanoid_agent", now),
                Decision("formal_max_turns", "6", now),
                Decision("formal_max_duration", "10 minutes", now),
                Decision("pilot_max_turns", "5", now),
                Decision("pilot_max_duration", "8 minutes", now),
                Decision("questionnaire_scale_anchors", "1=Strongly disagree / 非常不同意;7=Strongly agree / 非常同意", now)
            };
            protocol.EditorSetOfficialCollection("1.2.0-editor-collection", "editor-collection-20260721",
                "protocol-1.2.0-editor-collection", decisions, new[]
                {
                    new ExperimentProtocolChange
                    {
                        changedAtUtc = now, changedBy = "ProjectLead", previousProtocolVersion = previousProtocolVersion,
                        newProtocolVersion = "1.2.0-editor-collection", evidenceReference = "formal-editor-collection-directive-v1",
                        summary = "Approved the Unity Editor participant-collection protocol and participant-choice formal flow."
                    }
                });

            var tasks = AssetDatabase.LoadAssetAtPath<ExperimentTaskCatalog>(TaskPath);
            ConfigureTasks(tasks);

            var avatarCatalog = AssetDatabase.LoadAssetAtPath<AvatarCatalog>(AvatarPath);
            ConfigureAvatars(avatarCatalog);

            var voices = AssetDatabase.LoadAssetAtPath<ExperimentVoiceProfileCatalog>(VoicePath);
            var dialogue = Voice("editor_collection_dialogue_voice", "101050", VoiceSubtitlePolicy.AllSpeech);
            var feedback = Voice("editor_collection_feedback_voice", "default_female_en", VoiceSubtitlePolicy.FeedbackOnly);
            voices.EditorSet("1.2-editor-collection", feedback.voiceProfileKey, feedback.voiceProfileKey,
                feedback.voiceProfileKey, new[] { dialogue, feedback });

            var deployments = AssetDatabase.LoadAssetAtPath<ExperimentDeploymentCatalog>(DeploymentPath);
            deployments.EditorSet("1.2-editor-collection", new[]
            {
                new ExperimentDeploymentProfile
                {
                    profileId = ExperimentDeploymentProfileId.EditorCollection,
                    target = ExperimentDeploymentTarget.UnityEditor,
                    voiceGatewayBaseUrl = "http://127.0.0.1:8787", requestTimeoutSeconds = 30,
                    sttProvider = "voice_gateway_live_stt", ttsProvider = "tencent",
                    microphonePolicy = "UnityEditor default microphone", networkRequired = true,
                    approvedForCollection = true, approvedForEditorCollection = true,
                    collectionAllowed = true, loopbackAllowed = true, picoRequired = false,
                    evidenceReference = "formal-editor-collection-directive-v1"
                }
            });

            var resource = AssetDatabase.LoadAssetAtPath<EditorCollectionResourceCatalog>(ResourcePath);
            if (resource == null)
            {
                resource = ScriptableObject.CreateInstance<EditorCollectionResourceCatalog>();
                AssetDatabase.CreateAsset(resource, ResourcePath);
            }
            var mappings = new[]
            {
                Mapping("hotel_check_in", "barista_humanoid_v1"),
                Mapping("furniture_shopping", "teacher_humanoid_v1"),
                Mapping("gym_membership", "barista_male_humanoid_v1"),
                Mapping("tourist_assistance", "teacher_female_humanoid_v1")
            };
            var panoramas = tasks.GetTasks(ExperimentTaskPhase.Formal).Select(x => new EditorCollectionPanoramaApproval
            {
                taskId = x.taskId, panoramaResourceKey = x.panoramaResourceKey, equirectangular = true,
                approvedForEditorCollection = true, approvedForCollection = true, replaceableAsset = true
            }).ToArray();
            resource.EditorSet("resources-1.2-editor-collection", avatarCatalog, mappings, panoramas,
                "correction_agent_presenter_v1", "generated_orb_v1", "teacher_female_humanoid_v1");

            var pilotPresentations = AssetDatabase.LoadAssetAtPath<PilotPresentationCatalog>(PilotPresentationPath);
            ConfigurePilotPresentations(pilotPresentations);

            foreach (var item in new UnityEngine.Object[] { protocol, tasks, avatarCatalog, voices, deployments, resource, pilotPresentations }) EditorUtility.SetDirty(item);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[SceneTalkVR] Official Editor Collection assets updated for protocol 1.2.0-editor-collection.");
        }

        private static ExperimentProtocolDecision Decision(string id, string value, string now) => new ExperimentProtocolDecision
        {
            decisionId = id, status = ProtocolDecisionStatus.Confirmed, confirmedValue = value,
            confirmedBy = "ProjectLead", confirmedAtUtc = now,
            evidenceReference = "formal-editor-collection-directive-v1"
        };

        private static EditorCollectionAvatarMapping Mapping(string taskId, string key) => new EditorCollectionAvatarMapping
        { taskId = taskId, requestedPresetKey = key, approvedForEditorCollection = true, approvedForCollection = true, replaceableAsset = true };

        private static ExperimentVoiceProfile Voice(string key, string voiceId, VoiceSubtitlePolicy subtitles) => new ExperimentVoiceProfile
        {
            voiceProfileKey = key, provider = "tencent", voiceId = voiceId, language = "en-US",
            speakingSpeed = 1f, volume = 1f, pitch = 1f, sampleRate = 24000, subtitlePolicy = subtitles,
            approvedForCollection = true, approvedForEditorCollection = true, replaceableAsset = true,
            approvedBy = "ProjectLead", evidenceReference = "formal-editor-collection-directive-v1",
            assetVersion = "editor-collection-1"
        };

        private static void ConfigureTasks(ExperimentTaskCatalog catalog)
        {
            foreach (var task in catalog.Tasks.Where(x => x != null))
            {
                if (task.phase != ExperimentTaskPhase.Formal) continue;
                task.avatarPresetKey = task.taskId == "hotel_check_in" ? "barista_humanoid_v1"
                    : task.taskId == "furniture_shopping" ? "teacher_humanoid_v1"
                    : task.taskId == "gym_membership" ? "barista_male_humanoid_v1" : "teacher_female_humanoid_v1";
                task.voiceProfileKey = "editor_collection_dialogue_voice";
                task.developerPlaceholderAvatar = false;
                var ids = task.taskId == "hotel_check_in" ? new[] { "reservation_name", "breakfast", "higher_floor", "checkout_time" }
                    : task.taskId == "furniture_shopping" ? new[] { "desk_size", "material", "budget", "delivery" }
                    : task.taskId == "gym_membership" ? new[] { "fitness_goal", "monthly_price", "suitable_workout", "trial" }
                    : new[] { "museum_route", "ticket", "photography", "nearby_attraction" };
                var patterns = Patterns(task.taskId);
                for (var i = 0; i < task.goals.Length; i++)
                {
                    task.goals[i].goalId = ids[i];
                    task.goals[i].evaluationIntent = ids[i];
                    task.goals[i].deterministicPatterns = patterns[i];
                    task.goals[i].llmCriteria = "Return achieved=true only when the participant's user transcript provides or asks for this goal intent. Avatar and agent speech are excluded.";
                    task.goals[i].minimumConfidence = .85f;
                }
            }
            catalog.EditorSet("1.2.1-pilot-collection", catalog.Tasks.ToArray());
        }

        private static string[][] Patterns(string taskId)
        {
            if (taskId == "hotel_check_in") return new[]
            {
                new[] { "my name is", "booking is under", "reservation is under", "booked under", "the reservation should be in", "the booking is under", "it should be under the name" },
                new[] { "breakfast included", "serve breakfast", "time is breakfast", "morning meal", "does the room price include breakfast", "does the room come with breakfast" },
                new[] { "higher floor", "upper floor", "room upstairs", "tenth floor", "put me higher up" },
                new[] { "what time is checkout", "checkout time", "when do i need to check out" }
            };
            if (taskId == "furniture_shopping") return new[]
            {
                new[] { "desk size", "dimensions", "centimeter", "centimeters", "120 by 60", "how wide is the desk" }, new[] { "what material", "made of" },
                new[] { "my budget", "spend up to", "price limit" }, new[] { "home delivery", "do you deliver", "bring it to my apartment", "deliver it to my home", "can you send it to my address" }
            };
            if (taskId == "gym_membership") return new[]
            {
                new[] { "my fitness goal", "want to lose weight", "want to build muscle", "improve my endurance", "build strength", "get fitter" }, new[] { "monthly membership", "per month" },
                new[] { "workout plan", "training plan", "exercise plan" }, new[] { "free trial", "trial available", "try the gym before joining", "test the gym first", "trial session" }
            };
            return new[]
            {
                new[] { "how do i get to the museum", "directions to the museum", "best route to the museum", "how can i reach the museum", "way to the museum" }, new[] { "need a ticket", "ticket required" },
                new[] { "take photos inside", "photography allowed", "use my camera", "take pictures inside", "photos allowed indoors" }, new[] { "nearby attraction", "another attraction" }
            };
        }

        private static void ConfigureAvatars(AvatarCatalog catalog)
        {
            foreach (var entry in catalog.presets.Where(x => x != null))
            {
                if (!new[] { "barista_humanoid_v1", "teacher_humanoid_v1", "barista_male_humanoid_v1", "teacher_female_humanoid_v1" }.Contains(entry.key)) continue;
                var animator = entry.prefab == null ? null : entry.prefab.GetComponentInChildren<Animator>(true);
                entry.animatorController = animator?.runtimeAnimatorController;
                entry.voiceProfileKey = "editor_collection_dialogue_voice";
                entry.voiceId = entry.genderPresentations != null
                    && entry.genderPresentations.Any(x => string.Equals(x, "male", StringComparison.OrdinalIgnoreCase))
                    ? "default_male_en"
                    : "default_female_en";
                entry.idleState = "Idle"; entry.thinkingState = "Thinking"; entry.speakingState = "Talking";
                entry.spawnPosition = Vector3.zero; entry.spawnRotation = new Vector3(0, 180, 0); entry.scale = Vector3.one;
                entry.assetVersion = "editor-collection-1"; entry.approvedForCollection = true;
                entry.approvedForEditorCollection = true; entry.replaceableAsset = true;
                entry.evidenceReference = "formal-editor-collection-directive-v1";
                entry.scenarioIds = entry.key == "barista_humanoid_v1" ? new[] { "hotel_check_in" }
                    : entry.key == "teacher_humanoid_v1" ? new[] { "furniture_shopping" }
                    : entry.key == "barista_male_humanoid_v1" ? new[] { "gym_membership" }
                    : new[] { "tourist_assistance" };
            }
        }

        private static void ConfigurePilotPresentations(PilotPresentationCatalog catalog)
        {
            var humanoid = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/SceneTalkVR/Avatar/Prefabs/Humanoid/teacher_female_humanoid_v1.prefab");
            var animator = humanoid == null ? null : humanoid.GetComponentInChildren<Animator>(true)?.runtimeAnimatorController;
            catalog.EditorSet("1.2-editor-collection", new[]
            {
                new PilotPresentationProfile
                {
                    embodimentCondition = PilotEmbodimentCondition.VoiceOnly, visualMode = PilotVisualMode.None,
                    feedbackActor = "voice_only_feedback_agent", voiceProfileKey = "editor_collection_feedback_voice",
                    audioSourcePolicy = PilotAudioSourcePolicy.NonSpatialHeadLocked, sourcePosition = Vector3.zero,
                    spatialBlend = 0f, minDistance = .2f, maxDistance = 4f, volume = 1f, speakingSpeed = 1f,
                    subtitlePolicy = "feedback_only", visualPrefabKey = "none", audioSourceRequired = true,
                    mobileReady = true, assetVersion = "editor-collection-1", approvedForCollection = true,
                    evidenceReference = "formal-editor-collection-directive-v1", developerPlaceholder = false
                },
                new PilotPresentationProfile
                {
                    embodimentCondition = PilotEmbodimentCondition.FloatingOrb, visualMode = PilotVisualMode.FloatingOrb,
                    feedbackActor = "floating_orb_feedback_agent", voiceProfileKey = "editor_collection_feedback_voice",
                    audioSourcePolicy = PilotAudioSourcePolicy.SpatialFixedSource, sourcePosition = new Vector3(.9f, 1.45f, 1.8f),
                    spatialBlend = 1f, minDistance = .2f, maxDistance = 4f, volume = 1f, speakingSpeed = 1f,
                    subtitlePolicy = "feedback_only", visualPrefabKey = "generated_orb_v1", audioSourceRequired = true,
                    mobileReady = true, assetVersion = "editor-collection-1", approvedForCollection = true,
                    evidenceReference = "formal-editor-collection-directive-v1", developerPlaceholder = false
                },
                new PilotPresentationProfile
                {
                    embodimentCondition = PilotEmbodimentCondition.HumanoidAgent, visualMode = PilotVisualMode.Humanoid,
                    feedbackActor = "humanoid_feedback_agent", voiceProfileKey = "editor_collection_feedback_voice",
                    audioSourcePolicy = PilotAudioSourcePolicy.SpatialFixedSource, sourcePosition = new Vector3(.9f, 0f, 1.8f),
                    spatialBlend = 1f, minDistance = .2f, maxDistance = 4f, volume = 1f, speakingSpeed = 1f,
                    subtitlePolicy = "feedback_only", visualPrefabKey = "teacher_female_humanoid_v1", visualPrefab = humanoid,
                    animatorController = animator, idleParameterOrState = "Idle", speakingParameterOrState = "Talking",
                    spawnRotation = new Vector3(0f, 180f, 0f), scale = Vector3.one, audioSourceRequired = true,
                    mobileReady = true, assetVersion = "editor-collection-1", approvedForCollection = true,
                    evidenceReference = "formal-editor-collection-directive-v1", developerPlaceholder = false
                }
            });
        }
    }
}
