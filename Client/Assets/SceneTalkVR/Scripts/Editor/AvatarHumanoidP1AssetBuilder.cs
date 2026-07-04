using System.Collections.Generic;
using SceneTalkVR.AvatarSystem;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace SceneTalkVR.EditorTools
{
    public static class AvatarHumanoidP1AssetBuilder
    {
        private const string AvatarRoot = "Assets/SceneTalkVR/Avatar";
        private const string TeacherModelFolder = AvatarRoot + "/Models/Humanoid/QuaterniusBusinessMan";
        private const string TeacherModelPath = TeacherModelFolder + "/teacher_business_man.fbx";
        private const string BaristaModelFolder = AvatarRoot + "/Models/Humanoid/QuaterniusAnimatedWoman";
        private const string BaristaModelPath = BaristaModelFolder + "/barista_animated_woman.fbx";
        private const string PoliceModelFolder = AvatarRoot + "/Models/Humanoid/QuaterniusSWAT";
        private const string PoliceModelPath = PoliceModelFolder + "/police_swat.fbx";
        private const string MaleBaristaModelFolder = AvatarRoot + "/Models/Humanoid/QuaterniusCasualCharacter";
        private const string MaleBaristaModelPath = MaleBaristaModelFolder + "/barista_casual_man.fbx";
        private const string FemaleTeacherModelFolder = AvatarRoot + "/Models/Humanoid/QuaterniusSuit";
        private const string FemaleTeacherModelPath = FemaleTeacherModelFolder + "/teacher_suit_woman.fbx";
        private const string FemalePoliceModelFolder = AvatarRoot + "/Models/Humanoid/QuaterniusSoldier";
        private const string FemalePoliceModelPath = FemalePoliceModelFolder + "/police_soldier_woman.fbx";
        private const string TalkingAnimationModelFolder = AvatarRoot + "/Models/Humanoid/QuaterniusAnimatedBaseCharacter";
        private const string TalkingAnimationModelPath = TalkingAnimationModelFolder + "/animation_library_unity_standard.fbx";
        private const string FrappeModelFolder = AvatarRoot + "/Models/Props/KenneyFrappe";
        private const string FrappeModelPath = FrappeModelFolder + "/frappe.obj";
        private const string AnimationFolder = AvatarRoot + "/Animations";
        private const string CommonAnimationFolder = AnimationFolder + "/Common";
        private const string CommonHumanoidControllerPath = CommonAnimationFolder + "/AvatarCommonHumanoid.controller";
        private const string TalkGestureMaskPath = CommonAnimationFolder + "/AvatarTalkGesture.mask";
        private const string HumanoidPrefabFolder = AvatarRoot + "/Prefabs/Humanoid";
        private const string PropPrefabFolder = AvatarRoot + "/Prefabs/Props";
        private const string TeacherPrefabPath = HumanoidPrefabFolder + "/teacher_humanoid_v1.prefab";
        private const string BaristaPrefabPath = HumanoidPrefabFolder + "/barista_humanoid_v1.prefab";
        private const string PolicePrefabPath = HumanoidPrefabFolder + "/police_humanoid_v1.prefab";
        private const string MaleBaristaPrefabPath = HumanoidPrefabFolder + "/barista_male_humanoid_v1.prefab";
        private const string FemaleTeacherPrefabPath = HumanoidPrefabFolder + "/teacher_female_humanoid_v1.prefab";
        private const string FemalePolicePrefabPath = HumanoidPrefabFolder + "/police_female_humanoid_v1.prefab";
        private const string BookPropPath = PropPrefabFolder + "/book_prop_v1.prefab";
        private const string FrappePropPath = PropPrefabFolder + "/frappe_prop_v1.prefab";
        private const string CatalogFolder = AvatarRoot + "/Catalogs";
        private const string CatalogPath = CatalogFolder + "/AvatarCatalog.asset";
        private const string PropCatalogPath = CatalogFolder + "/AvatarPropCatalog.asset";
        private const string TeacherHumanoidKey = "teacher_humanoid_v1";
        private const string BaristaHumanoidKey = "barista_humanoid_v1";
        private const string PoliceHumanoidKey = "police_humanoid_v1";
        private const string MaleBaristaHumanoidKey = "barista_male_humanoid_v1";
        private const string FemaleTeacherHumanoidKey = "teacher_female_humanoid_v1";
        private const string FemalePoliceHumanoidKey = "police_female_humanoid_v1";
        private const string TeacherPlaceholderKey = "teacher_default";
        private const string BaristaPlaceholderKey = "barista_default";
        private const string PolicePlaceholderKey = "police_default";
        private const string BookPropKey = "book_prop_v1";
        private const string FrappePropKey = "frappe_prop_v1";
        private const float TargetHeightMeters = 1.72f;
        private const float BaristaTargetHeightMeters = 1.66f;
        private const float PoliceTargetHeightMeters = 1.78f;
        private const float FemaleTeacherTargetHeightMeters = 1.66f;
        private const float FemalePoliceTargetHeightMeters = 1.7f;

        [MenuItem("SceneTalkVR/Avatar/P1 Build Humanoid Avatars", false, 41)]
        public static void BuildHumanoidAvatars()
        {
            EnsureFolders();
            ConfigureHumanoidImporter(TeacherModelPath);
            ConfigureHumanoidImporter(BaristaModelPath);
            ConfigureHumanoidImporter(PoliceModelPath);
            ConfigureHumanoidImporter(MaleBaristaModelPath, importAnimation: false);
            ConfigureHumanoidImporter(FemaleTeacherModelPath, importAnimation: false);
            ConfigureHumanoidImporter(FemalePoliceModelPath, importAnimation: false);
            ConfigureTalkingAnimationImporter(TalkingAnimationModelPath);

            var teacherSourceModel = AssetDatabase.LoadAssetAtPath<GameObject>(TeacherModelPath);
            if (teacherSourceModel == null)
            {
                Debug.LogError($"[SceneTalkVR] P1 teacher humanoid source model not found at {TeacherModelPath}.");
                return;
            }

            var baristaSourceModel = AssetDatabase.LoadAssetAtPath<GameObject>(BaristaModelPath);
            if (baristaSourceModel == null)
            {
                Debug.LogError($"[SceneTalkVR] P1 barista humanoid source model not found at {BaristaModelPath}.");
                return;
            }

            var policeSourceModel = AssetDatabase.LoadAssetAtPath<GameObject>(PoliceModelPath);
            if (policeSourceModel == null)
            {
                Debug.LogError($"[SceneTalkVR] P1 police humanoid source model not found at {PoliceModelPath}.");
                return;
            }

            var maleBaristaSourceModel = AssetDatabase.LoadAssetAtPath<GameObject>(MaleBaristaModelPath);
            if (maleBaristaSourceModel == null)
            {
                Debug.LogError($"[SceneTalkVR] P1 male barista humanoid source model not found at {MaleBaristaModelPath}.");
                return;
            }

            var femaleTeacherSourceModel = AssetDatabase.LoadAssetAtPath<GameObject>(FemaleTeacherModelPath);
            if (femaleTeacherSourceModel == null)
            {
                Debug.LogError($"[SceneTalkVR] P1 female teacher humanoid source model not found at {FemaleTeacherModelPath}.");
                return;
            }

            var femalePoliceSourceModel = AssetDatabase.LoadAssetAtPath<GameObject>(FemalePoliceModelPath);
            if (femalePoliceSourceModel == null)
            {
                Debug.LogError($"[SceneTalkVR] P1 female police humanoid source model not found at {FemalePoliceModelPath}.");
                return;
            }

            var controller = LoadOrCreateCommonHumanoidAnimatorController();
            var teacherPrefab = CreateHumanoidPrefab(
                teacherSourceModel,
                "teacher_humanoid_v1",
                "QuaterniusBusinessMan",
                TeacherPrefabPath,
                TargetHeightMeters,
                180f,
                controller);
            var baristaPrefab = CreateHumanoidPrefab(
                baristaSourceModel,
                "barista_humanoid_v1",
                "QuaterniusAnimatedWoman",
                BaristaPrefabPath,
                BaristaTargetHeightMeters,
                180f,
                controller);
            var policePrefab = CreateHumanoidPrefab(
                policeSourceModel,
                "police_humanoid_v1",
                "QuaterniusSWAT",
                PolicePrefabPath,
                PoliceTargetHeightMeters,
                180f,
                controller);
            var maleBaristaPrefab = CreateHumanoidPrefab(
                maleBaristaSourceModel,
                "barista_male_humanoid_v1",
                "QuaterniusCasualCharacter",
                MaleBaristaPrefabPath,
                TargetHeightMeters,
                180f,
                controller);
            var femaleTeacherPrefab = CreateHumanoidPrefab(
                femaleTeacherSourceModel,
                "teacher_female_humanoid_v1",
                "QuaterniusSuit",
                FemaleTeacherPrefabPath,
                FemaleTeacherTargetHeightMeters,
                180f,
                controller);
            var femalePolicePrefab = CreateHumanoidPrefab(
                femalePoliceSourceModel,
                "police_female_humanoid_v1",
                "QuaterniusSoldier",
                FemalePolicePrefabPath,
                FemalePoliceTargetHeightMeters,
                180f,
                controller);
            var bookProp = CreateBookPropPrefab();
            var frappeProp = CreateFrappePropPrefab();
            UpsertCatalogEntries(teacherPrefab, baristaPrefab, policePrefab, maleBaristaPrefab, femaleTeacherPrefab, femalePolicePrefab);
            UpsertPropCatalogEntries(bookProp, frappeProp);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = policePrefab;
            Debug.Log($"[SceneTalkVR] Built P1 humanoid avatar prefabs and catalog entries: {TeacherHumanoidKey}, {BaristaHumanoidKey}, {PoliceHumanoidKey}, {MaleBaristaHumanoidKey}, {FemaleTeacherHumanoidKey}, {FemalePoliceHumanoidKey}.");
        }

        [MenuItem("SceneTalkVR/Avatar/P1 Build Teacher Humanoid", false, 42)]
        public static void BuildTeacherHumanoid()
        {
            BuildHumanoidAvatars();
        }

        [MenuItem("SceneTalkVR/Avatar/P1 Build Gender Humanoids", false, 43)]
        public static void BuildGenderHumanoidAvatars()
        {
            EnsureFolders();
            ConfigureHumanoidImporter(MaleBaristaModelPath, importAnimation: false);
            ConfigureHumanoidImporter(FemaleTeacherModelPath, importAnimation: false);
            ConfigureHumanoidImporter(FemalePoliceModelPath, importAnimation: false);

            var maleBaristaSourceModel = AssetDatabase.LoadAssetAtPath<GameObject>(MaleBaristaModelPath);
            if (maleBaristaSourceModel == null)
            {
                Debug.LogError($"[SceneTalkVR] P1 male barista humanoid source model not found at {MaleBaristaModelPath}.");
                return;
            }

            var femaleTeacherSourceModel = AssetDatabase.LoadAssetAtPath<GameObject>(FemaleTeacherModelPath);
            if (femaleTeacherSourceModel == null)
            {
                Debug.LogError($"[SceneTalkVR] P1 female teacher humanoid source model not found at {FemaleTeacherModelPath}.");
                return;
            }

            var femalePoliceSourceModel = AssetDatabase.LoadAssetAtPath<GameObject>(FemalePoliceModelPath);
            if (femalePoliceSourceModel == null)
            {
                Debug.LogError($"[SceneTalkVR] P1 female police humanoid source model not found at {FemalePoliceModelPath}.");
                return;
            }

            var controller = LoadOrCreateCommonHumanoidAnimatorController();
            var maleBaristaPrefab = CreateHumanoidPrefab(
                maleBaristaSourceModel,
                "barista_male_humanoid_v1",
                "QuaterniusCasualCharacter",
                MaleBaristaPrefabPath,
                TargetHeightMeters,
                180f,
                controller);
            var femaleTeacherPrefab = CreateHumanoidPrefab(
                femaleTeacherSourceModel,
                "teacher_female_humanoid_v1",
                "QuaterniusSuit",
                FemaleTeacherPrefabPath,
                FemaleTeacherTargetHeightMeters,
                180f,
                controller);
            var femalePolicePrefab = CreateHumanoidPrefab(
                femalePoliceSourceModel,
                "police_female_humanoid_v1",
                "QuaterniusSoldier",
                FemalePolicePrefabPath,
                FemalePoliceTargetHeightMeters,
                180f,
                controller);

            UpsertGenderCatalogEntries(maleBaristaPrefab, femaleTeacherPrefab, femalePolicePrefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = femalePolicePrefab;
            Debug.Log($"[SceneTalkVR] Built gender humanoid avatar prefabs and catalog entries: {MaleBaristaHumanoidKey}, {FemaleTeacherHumanoidKey}, {FemalePoliceHumanoidKey}.");
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/SceneTalkVR", "Avatar");
            EnsureFolder(AvatarRoot, "Models");
            EnsureFolder(AvatarRoot, "Animations");
            EnsureFolder(AnimationFolder, "Common");
            EnsureFolder(AvatarRoot + "/Models", "Humanoid");
            EnsureFolder(AvatarRoot + "/Models/Humanoid", "QuaterniusBusinessMan");
            EnsureFolder(AvatarRoot + "/Models/Humanoid", "QuaterniusAnimatedWoman");
            EnsureFolder(AvatarRoot + "/Models/Humanoid", "QuaterniusSWAT");
            EnsureFolder(AvatarRoot + "/Models/Humanoid", "QuaterniusCasualCharacter");
            EnsureFolder(AvatarRoot + "/Models/Humanoid", "QuaterniusSuit");
            EnsureFolder(AvatarRoot + "/Models/Humanoid", "QuaterniusSoldier");
            EnsureFolder(AvatarRoot + "/Models/Humanoid", "QuaterniusAnimatedBaseCharacter");
            EnsureFolder(AvatarRoot + "/Models", "Props");
            EnsureFolder(AvatarRoot + "/Models/Props", "KenneyFrappe");
            EnsureFolder(AvatarRoot, "Prefabs");
            EnsureFolder(AvatarRoot + "/Prefabs", "Humanoid");
            EnsureFolder(AvatarRoot + "/Prefabs", "Props");
            EnsureFolder(AvatarRoot, "Catalogs");
        }

        private static void EnsureFolder(string parent, string name)
        {
            var fullPath = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(fullPath))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }

        private static void ConfigureHumanoidImporter(string modelPath)
        {
            ConfigureHumanoidImporter(modelPath, importAnimation: true);
        }

        private static void ConfigureHumanoidImporter(string modelPath, bool importAnimation)
        {
            ConfigureHumanoidImporter(modelPath, CreateQuaterniusHumanDescription, importAnimation);
        }

        private static void ConfigureTalkingAnimationImporter(string modelPath)
        {
            ConfigureHumanoidImporter(modelPath, CreateDefRigHumanDescription, importAnimation: true);
        }

        private static void ConfigureHumanoidImporter(
            string modelPath,
            System.Func<Transform, HumanDescription> humanDescriptionFactory,
            bool importAnimation)
        {
            var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[SceneTalkVR] P1 humanoid importer not ready yet at {modelPath}.");
                return;
            }

            var changed = false;
            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                changed = true;
            }

            if (importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
            {
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                changed = true;
            }

            if (importer.importAnimation != importAnimation)
            {
                importer.importAnimation = importAnimation;
                changed = true;
            }

            var sourceModel = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (sourceModel != null && humanDescriptionFactory != null)
            {
                importer.humanDescription = humanDescriptionFactory(sourceModel.transform);
                changed = true;
            }

            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            if (importAnimation)
            {
                ConfigureClipLooping(importer);
            }

            if (changed)
            {
                AssetDatabase.ImportAsset(modelPath, ImportAssetOptions.ForceUpdate);
            }
        }

        private static void ConfigureClipLooping(ModelImporter importer)
        {
            var clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0)
            {
                clips = importer.defaultClipAnimations;
            }

            if (clips == null || clips.Length == 0)
            {
                return;
            }

            for (var i = 0; i < clips.Length; i++)
            {
                var clip = clips[i];
                var name = clip.name ?? string.Empty;
                if (name.Contains("Idle") || name.Contains("Walk") || name.Contains("Run"))
                {
                    clip.loopTime = true;
                    clip.loopPose = true;
                }
            }

            importer.clipAnimations = clips;
        }

        private static HumanDescription CreateQuaterniusHumanDescription(Transform root)
        {
            return new HumanDescription
            {
                human = new[]
                {
                    Human("Hips", "Body"),
                    Human("Spine", "Hips"),
                    Human("Chest", "Torso"),
                    Human("UpperChest", "Chest"),
                    Human("Neck", "Neck"),
                    Human("Head", "Head"),
                    Human("LeftShoulder", "Shoulder.L"),
                    Human("LeftUpperArm", "UpperArm.L"),
                    Human("LeftLowerArm", "LowerArm.L"),
                    Human("LeftHand", "Wrist.L"),
                    Human("RightShoulder", "Shoulder.R"),
                    Human("RightUpperArm", "UpperArm.R"),
                    Human("RightLowerArm", "LowerArm.R"),
                    Human("RightHand", "Wrist.R"),
                    Human("LeftUpperLeg", "UpperLeg.L"),
                    Human("LeftLowerLeg", "LowerLeg.L"),
                    Human("LeftFoot", "LowerLeg.L_end"),
                    Human("RightUpperLeg", "UpperLeg.R"),
                    Human("RightLowerLeg", "LowerLeg.R"),
                    Human("RightFoot", "LowerLeg.R_end"),
                    Human("Left Thumb Proximal", "Thumb1.L"),
                    Human("Left Thumb Intermediate", "Thumb2.L"),
                    Human("Left Thumb Distal", "Thumb3.L"),
                    Human("Left Index Proximal", "Index1.L"),
                    Human("Left Index Intermediate", "Index2.L"),
                    Human("Left Index Distal", "Index3.L"),
                    Human("Left Middle Proximal", "Middle1.L"),
                    Human("Left Middle Intermediate", "Middle2.L"),
                    Human("Left Middle Distal", "Middle3.L"),
                    Human("Left Ring Proximal", "Ring1.L"),
                    Human("Left Ring Intermediate", "Ring2.L"),
                    Human("Left Ring Distal", "Ring3.L"),
                    Human("Left Little Proximal", "Pinky1.L"),
                    Human("Left Little Intermediate", "Pinky2.L"),
                    Human("Left Little Distal", "Pinky3.L"),
                    Human("Right Thumb Proximal", "Thumb1.R"),
                    Human("Right Thumb Intermediate", "Thumb2.R"),
                    Human("Right Thumb Distal", "Thumb3.R"),
                    Human("Right Index Proximal", "Index1.R"),
                    Human("Right Index Intermediate", "Index2.R"),
                    Human("Right Index Distal", "Index3.R"),
                    Human("Right Middle Proximal", "Middle1.R"),
                    Human("Right Middle Intermediate", "Middle2.R"),
                    Human("Right Middle Distal", "Middle3.R"),
                    Human("Right Ring Proximal", "Ring1.R"),
                    Human("Right Ring Intermediate", "Ring2.R"),
                    Human("Right Ring Distal", "Ring3.R"),
                    Human("Right Little Proximal", "Pinky1.R"),
                    Human("Right Little Intermediate", "Pinky2.R"),
                    Human("Right Little Distal", "Pinky3.R")
                },
                skeleton = CreateSkeleton(root),
                upperArmTwist = 0.5f,
                lowerArmTwist = 0.5f,
                upperLegTwist = 0.5f,
                lowerLegTwist = 0.5f,
                armStretch = 0.05f,
                legStretch = 0.05f,
                feetSpacing = 0f,
                hasTranslationDoF = false
            };
        }

        private static HumanBone Human(string humanName, string boneName)
        {
            return new HumanBone
            {
                humanName = humanName,
                boneName = boneName,
                limit = new HumanLimit
                {
                    useDefaultValues = true
                }
            };
        }

        private static HumanDescription CreateDefRigHumanDescription(Transform root)
        {
            return new HumanDescription
            {
                human = new[]
                {
                    Human("Hips", "DEF-hips"),
                    Human("Spine", "DEF-spine.001"),
                    Human("Chest", "DEF-spine.002"),
                    Human("UpperChest", "DEF-spine.003"),
                    Human("Neck", "DEF-neck"),
                    Human("Head", "DEF-head"),
                    Human("LeftShoulder", "DEF-shoulder.L"),
                    Human("LeftUpperArm", "DEF-upper_arm.L"),
                    Human("LeftLowerArm", "DEF-forearm.L"),
                    Human("LeftHand", "DEF-hand.L"),
                    Human("RightShoulder", "DEF-shoulder.R"),
                    Human("RightUpperArm", "DEF-upper_arm.R"),
                    Human("RightLowerArm", "DEF-forearm.R"),
                    Human("RightHand", "DEF-hand.R"),
                    Human("LeftUpperLeg", "DEF-thigh.L"),
                    Human("LeftLowerLeg", "DEF-shin.L"),
                    Human("LeftFoot", "DEF-foot.L"),
                    Human("RightUpperLeg", "DEF-thigh.R"),
                    Human("RightLowerLeg", "DEF-shin.R"),
                    Human("RightFoot", "DEF-foot.R"),
                    Human("Left Thumb Proximal", "DEF-thumb.01.L"),
                    Human("Left Thumb Intermediate", "DEF-thumb.02.L"),
                    Human("Left Thumb Distal", "DEF-thumb.03.L"),
                    Human("Left Index Proximal", "DEF-f_index.01.L"),
                    Human("Left Index Intermediate", "DEF-f_index.02.L"),
                    Human("Left Index Distal", "DEF-f_index.03.L"),
                    Human("Left Middle Proximal", "DEF-f_middle.01.L"),
                    Human("Left Middle Intermediate", "DEF-f_middle.02.L"),
                    Human("Left Middle Distal", "DEF-f_middle.03.L"),
                    Human("Left Ring Proximal", "DEF-f_ring.01.L"),
                    Human("Left Ring Intermediate", "DEF-f_ring.02.L"),
                    Human("Left Ring Distal", "DEF-f_ring.03.L"),
                    Human("Left Little Proximal", "DEF-f_pinky.01.L"),
                    Human("Left Little Intermediate", "DEF-f_pinky.02.L"),
                    Human("Left Little Distal", "DEF-f_pinky.03.L"),
                    Human("Right Thumb Proximal", "DEF-thumb.01.R"),
                    Human("Right Thumb Intermediate", "DEF-thumb.02.R"),
                    Human("Right Thumb Distal", "DEF-thumb.03.R"),
                    Human("Right Index Proximal", "DEF-f_index.01.R"),
                    Human("Right Index Intermediate", "DEF-f_index.02.R"),
                    Human("Right Index Distal", "DEF-f_index.03.R"),
                    Human("Right Middle Proximal", "DEF-f_middle.01.R"),
                    Human("Right Middle Intermediate", "DEF-f_middle.02.R"),
                    Human("Right Middle Distal", "DEF-f_middle.03.R"),
                    Human("Right Ring Proximal", "DEF-f_ring.01.R"),
                    Human("Right Ring Intermediate", "DEF-f_ring.02.R"),
                    Human("Right Ring Distal", "DEF-f_ring.03.R"),
                    Human("Right Little Proximal", "DEF-f_pinky.01.R"),
                    Human("Right Little Intermediate", "DEF-f_pinky.02.R"),
                    Human("Right Little Distal", "DEF-f_pinky.03.R")
                },
                skeleton = CreateSkeleton(root),
                upperArmTwist = 0.5f,
                lowerArmTwist = 0.5f,
                upperLegTwist = 0.5f,
                lowerLegTwist = 0.5f,
                armStretch = 0.05f,
                legStretch = 0.05f,
                feetSpacing = 0f,
                hasTranslationDoF = false
            };
        }

        private static SkeletonBone[] CreateSkeleton(Transform root)
        {
            var transforms = root.GetComponentsInChildren<Transform>(true);
            var skeleton = new SkeletonBone[transforms.Length];
            for (var i = 0; i < transforms.Length; i++)
            {
                var bone = transforms[i];
                skeleton[i] = new SkeletonBone
                {
                    name = bone.name,
                    position = bone.localPosition,
                    rotation = bone.localRotation,
                    scale = bone.localScale
                };
            }

            return skeleton;
        }

        private static AnimatorController CreateCommonHumanoidAnimatorController()
        {
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(CommonHumanoidControllerPath) != null)
            {
                AssetDatabase.DeleteAsset(CommonHumanoidControllerPath);
            }

            var controller = AnimatorController.CreateAnimatorControllerAtPath(CommonHumanoidControllerPath);
            controller.AddParameter("Think", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Speak", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Talk", AnimatorControllerParameterType.Trigger);

            var stateMachine = controller.layers[0].stateMachine;
            var idleClip = LoadClip("CharacterArmature|Idle_Neutral") ?? LoadClip("CharacterArmature|Idle");
            var thinkClip = LoadClip("CharacterArmature|Interact") ?? idleClip;
            var speakClip = LoadClip("CharacterArmature|Wave") ?? thinkClip ?? idleClip;
            var talkClip = LoadClip(TalkingAnimationModelPath, "Rig|Idle_Talking_Loop")
                ?? LoadClip(TalkingAnimationModelPath, "CharacterArmature|Idle_Talking_Loop");

            var idle = stateMachine.AddState("Idle");
            idle.motion = idleClip;
            stateMachine.defaultState = idle;

            var thinking = stateMachine.AddState("Thinking");
            thinking.motion = thinkClip;
            var speaking = stateMachine.AddState("Speaking");
            speaking.motion = speakClip;

            AddTriggeredReturnTransition(stateMachine, thinking, idle, "Think");
            AddTriggeredReturnTransition(stateMachine, speaking, idle, "Speak");

            if (talkClip != null)
            {
                AddTalkGestureLayer(controller, talkClip);
            }
            else
            {
                var talking = stateMachine.AddState("TalkingFallback");
                talking.motion = thinkClip ?? idleClip;
                AddTriggeredReturnTransition(stateMachine, talking, idle, "Talk");
                Debug.LogWarning("[SceneTalkVR] External talking clip was not found; Talk falls back to the same-rig Interact clip.");
            }

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static AnimatorController LoadOrCreateCommonHumanoidAnimatorController()
        {
            return AssetDatabase.LoadAssetAtPath<AnimatorController>(CommonHumanoidControllerPath)
                ?? CreateCommonHumanoidAnimatorController();
        }

        private static void AddTalkGestureLayer(AnimatorController controller, AnimationClip talkClip)
        {
            var mask = CreateTalkGestureMask();
            controller.AddLayer("Talk Gesture");
            var layers = controller.layers;
            var layer = layers[layers.Length - 1];
            layer.defaultWeight = 1f;
            layer.avatarMask = mask;
            layer.blendingMode = AnimatorLayerBlendingMode.Override;

            var stateMachine = layer.stateMachine;
            var idle = stateMachine.AddState("TalkLayerIdle");
            stateMachine.defaultState = idle;

            var talking = stateMachine.AddState("TalkingGesture");
            talking.motion = talkClip;
            AddTriggeredReturnTransition(stateMachine, talking, idle, "Talk");

            layers[layers.Length - 1] = layer;
            controller.layers = layers;
        }

        private static AvatarMask CreateTalkGestureMask()
        {
            if (AssetDatabase.LoadAssetAtPath<AvatarMask>(TalkGestureMaskPath) != null)
            {
                AssetDatabase.DeleteAsset(TalkGestureMaskPath);
            }

            var mask = new AvatarMask();
            for (var i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
            {
                mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
            }

            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Head, true);

            AssetDatabase.CreateAsset(mask, TalkGestureMaskPath);
            EditorUtility.SetDirty(mask);
            return mask;
        }

        private static void AddTriggeredReturnTransition(
            AnimatorStateMachine stateMachine,
            AnimatorState triggeredState,
            AnimatorState idleState,
            string triggerName)
        {
            var enter = stateMachine.AddAnyStateTransition(triggeredState);
            enter.AddCondition(AnimatorConditionMode.If, 0f, triggerName);
            enter.duration = 0.1f;
            enter.canTransitionToSelf = false;

            var exit = triggeredState.AddTransition(idleState);
            exit.hasExitTime = true;
            exit.exitTime = 0.9f;
            exit.duration = 0.1f;
        }

        private static AnimationClip LoadClip(string clipName)
        {
            return LoadClip(TeacherModelPath, clipName);
        }

        private static AnimationClip LoadClip(string assetPath, string clipName)
        {
            return FindClip(AssetDatabase.LoadAllAssetsAtPath(assetPath), clipName)
                ?? FindClip(AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath), clipName);
        }

        private static AnimationClip FindClip(UnityEngine.Object[] assets, string clipName)
        {
            for (var i = 0; i < assets.Length; i++)
            {
                var clip = assets[i] as AnimationClip;
                if (clip != null && IsClipNameMatch(clip.name, clipName))
                {
                    return clip;
                }
            }

            return null;
        }

        private static bool IsClipNameMatch(string actualName, string expectedName)
        {
            if (string.Equals(actualName, expectedName, System.StringComparison.Ordinal))
            {
                return true;
            }

            var expectedSuffix = expectedName;
            var separatorIndex = expectedName.IndexOf('|');
            if (separatorIndex >= 0 && separatorIndex < expectedName.Length - 1)
            {
                expectedSuffix = expectedName.Substring(separatorIndex + 1);
            }

            return string.Equals(actualName, expectedSuffix, System.StringComparison.Ordinal)
                || actualName.EndsWith("|" + expectedSuffix, System.StringComparison.Ordinal);
        }

        private static GameObject CreateHumanoidPrefab(
            GameObject sourceModel,
            string prefabRootName,
            string modelInstanceName,
            string prefabPath,
            float targetHeightMeters,
            float yRotation,
            RuntimeAnimatorController animatorController)
        {
            var root = new GameObject(prefabRootName);
            var modelInstance = PrefabUtility.InstantiatePrefab(sourceModel) as GameObject;
            if (modelInstance == null)
            {
                modelInstance = Object.Instantiate(sourceModel);
            }
            else if (PrefabUtility.IsPartOfPrefabInstance(modelInstance))
            {
                PrefabUtility.UnpackPrefabInstance(
                    modelInstance,
                    PrefabUnpackMode.Completely,
                    InteractionMode.AutomatedAction);
            }

            modelInstance.name = modelInstanceName;
            modelInstance.transform.SetParent(root.transform, false);
            modelInstance.transform.localPosition = Vector3.zero;
            modelInstance.transform.localRotation = Quaternion.Euler(0f, yRotation, 0f);
            modelInstance.transform.localScale = Vector3.one;

            NormalizeModelHeight(root.transform, modelInstance.transform, targetHeightMeters);
            ConfigureAnimator(modelInstance, animatorController);
            root.AddComponent<AvatarAttachmentSockets>();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void NormalizeModelHeight(Transform root, Transform modelRoot, float targetHeightMeters)
        {
            if (!TryGetBounds(root, out var bounds) || bounds.size.y <= 0.001f)
            {
                return;
            }

            var scale = targetHeightMeters / bounds.size.y;
            modelRoot.localScale *= scale;

            if (TryGetBounds(root, out bounds))
            {
                modelRoot.localPosition += new Vector3(0f, -bounds.min.y, 0f);
            }
        }

        private static bool TryGetBounds(Transform root, out Bounds bounds)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            bounds = default;
            var initialized = false;

            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                if (!initialized)
                {
                    bounds = renderer.bounds;
                    initialized = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return initialized;
        }

        private static void ConfigureAnimator(GameObject modelInstance, RuntimeAnimatorController animatorController)
        {
            var animator = modelInstance.GetComponentInChildren<Animator>();
            if (animator == null)
            {
                animator = modelInstance.AddComponent<Animator>();
            }

            animator.runtimeAnimatorController = animatorController;
            animator.applyRootMotion = false;
        }

        private static GameObject CreateBookPropPrefab()
        {
            var root = new GameObject(BookPropKey);
            var book = GameObject.CreatePrimitive(PrimitiveType.Cube);
            book.name = "BookBlock";
            book.transform.SetParent(root.transform, false);
            book.transform.localPosition = Vector3.zero;
            book.transform.localRotation = Quaternion.identity;
            book.transform.localScale = new Vector3(0.18f, 0.26f, 0.035f);

            var renderer = book.GetComponent<Renderer>();
            if (renderer != null)
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(AvatarRoot + "/Materials/Avatar_White.mat");
                if (material != null)
                {
                    renderer.sharedMaterial = material;
                }
            }

            var spine = GameObject.CreatePrimitive(PrimitiveType.Cube);
            spine.name = "BookSpine";
            spine.transform.SetParent(root.transform, false);
            spine.transform.localPosition = new Vector3(-0.085f, 0f, 0f);
            spine.transform.localRotation = Quaternion.identity;
            spine.transform.localScale = new Vector3(0.02f, 0.265f, 0.04f);

            var spineRenderer = spine.GetComponent<Renderer>();
            if (spineRenderer != null)
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(AvatarRoot + "/Materials/Avatar_Black.mat");
                if (material != null)
                {
                    spineRenderer.sharedMaterial = material;
                }
            }

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, BookPropPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateFrappePropPrefab()
        {
            var root = new GameObject(FrappePropKey);
            var sourceModel = AssetDatabase.LoadAssetAtPath<GameObject>(FrappeModelPath);
            GameObject modelInstance = null;

            if (sourceModel != null)
            {
                modelInstance = PrefabUtility.InstantiatePrefab(sourceModel) as GameObject;
            }

            if (modelInstance == null)
            {
                modelInstance = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                modelInstance.name = "FrappeFallback";
            }

            modelInstance.name = "KenneyFrappe";
            modelInstance.transform.SetParent(root.transform, false);
            modelInstance.transform.localPosition = Vector3.zero;
            modelInstance.transform.localRotation = Quaternion.identity;
            modelInstance.transform.localScale = Vector3.one * 0.55f;

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, FrappePropPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void UpsertCatalogEntries(
            GameObject teacherPrefab,
            GameObject baristaPrefab,
            GameObject policePrefab,
            GameObject maleBaristaPrefab,
            GameObject femaleTeacherPrefab,
            GameObject femalePolicePrefab)
        {
            if (teacherPrefab == null
                || baristaPrefab == null
                || policePrefab == null
                || maleBaristaPrefab == null
                || femaleTeacherPrefab == null
                || femalePolicePrefab == null)
            {
                Debug.LogError("[SceneTalkVR] P1 humanoid prefab creation failed.");
                return;
            }

            var catalog = AssetDatabase.LoadAssetAtPath<AvatarCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<AvatarCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
                catalog.defaultAvatarKey = TeacherPlaceholderKey;
            }

            var presets = catalog.presets == null
                ? new List<AvatarPresetEntry>()
                : new List<AvatarPresetEntry>(catalog.presets);

            presets.RemoveAll(entry => entry != null
                && (entry.key == TeacherHumanoidKey
                    || entry.key == BaristaHumanoidKey
                    || entry.key == PoliceHumanoidKey
                    || entry.key == MaleBaristaHumanoidKey
                    || entry.key == FemaleTeacherHumanoidKey
                    || entry.key == FemalePoliceHumanoidKey));

            InsertBeforePlaceholder(presets, CreateTeacherEntry(teacherPrefab), TeacherPlaceholderKey);
            InsertBeforePlaceholder(presets, CreateFemaleTeacherEntry(femaleTeacherPrefab), TeacherPlaceholderKey);
            InsertBeforePlaceholder(presets, CreateBaristaEntry(baristaPrefab), BaristaPlaceholderKey);
            InsertBeforePlaceholder(presets, CreateMaleBaristaEntry(maleBaristaPrefab), BaristaPlaceholderKey);
            InsertBeforePlaceholder(presets, CreatePoliceEntry(policePrefab), PolicePlaceholderKey);
            InsertBeforePlaceholder(presets, CreateFemalePoliceEntry(femalePolicePrefab), PolicePlaceholderKey);
            catalog.presets = presets.ToArray();
            EditorUtility.SetDirty(catalog);
        }

        private static void UpsertGenderCatalogEntries(
            GameObject maleBaristaPrefab,
            GameObject femaleTeacherPrefab,
            GameObject femalePolicePrefab)
        {
            if (maleBaristaPrefab == null || femaleTeacherPrefab == null || femalePolicePrefab == null)
            {
                Debug.LogError("[SceneTalkVR] P1 gender humanoid prefab creation failed.");
                return;
            }

            var catalog = AssetDatabase.LoadAssetAtPath<AvatarCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<AvatarCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
                catalog.defaultAvatarKey = TeacherPlaceholderKey;
            }

            var presets = catalog.presets == null
                ? new List<AvatarPresetEntry>()
                : new List<AvatarPresetEntry>(catalog.presets);

            presets.RemoveAll(entry => entry != null
                && (entry.key == MaleBaristaHumanoidKey
                    || entry.key == FemaleTeacherHumanoidKey
                    || entry.key == FemalePoliceHumanoidKey));

            InsertBeforePlaceholder(presets, CreateFemaleTeacherEntry(femaleTeacherPrefab), TeacherPlaceholderKey);
            InsertBeforePlaceholder(presets, CreateMaleBaristaEntry(maleBaristaPrefab), BaristaPlaceholderKey);
            InsertBeforePlaceholder(presets, CreateFemalePoliceEntry(femalePolicePrefab), PolicePlaceholderKey);
            catalog.presets = presets.ToArray();
            EditorUtility.SetDirty(catalog);
        }

        private static void InsertBeforePlaceholder(
            List<AvatarPresetEntry> presets,
            AvatarPresetEntry entry,
            string placeholderKey)
        {
            var insertIndex = presets.FindIndex(candidate => candidate != null && candidate.key == placeholderKey);
            if (insertIndex < 0)
            {
                insertIndex = presets.Count;
            }

            presets.Insert(insertIndex, entry);
        }

        private static AvatarPresetEntry CreateTeacherEntry(GameObject prefab)
        {
            return new AvatarPresetEntry
            {
                key = TeacherHumanoidKey,
                displayName = "Teacher Humanoid v1 - Quaternius Business Man",
                priority = 80,
                prefab = prefab,
                roles = new[] { "teacher", "instructor", "tutor" },
                environmentTags = new[] { "classroom", "school" },
                styleIds = new[] { "semi_realistic_v1", "humanoid_v1", "low_poly_v1" },
                genderPresentations = new[] { "unknown", "male" },
                ageBuckets = new[] { "adult", "middle_aged" },
                bodyBuilds = new[] { "average" },
                outfitRoles = new[] { "teacher", "formal", "business" },
                outfitColors = new[] { "blue", "black", "white" },
                accessoryTags = new[] { "book", "suit", "formal" },
                mustHaveTags = new string[0],
                qualityTier = "humanoid_v1",
                mobileReady = true
            };
        }

        private static AvatarPresetEntry CreateBaristaEntry(GameObject prefab)
        {
            return new AvatarPresetEntry
            {
                key = BaristaHumanoidKey,
                displayName = "Barista Humanoid v1 - Quaternius Animated Woman",
                priority = 80,
                prefab = prefab,
                roles = new[] { "barista", "cafe_worker", "clerk" },
                environmentTags = new[] { "coffee_shop", "cafe" },
                styleIds = new[] { "semi_realistic_v1", "humanoid_v1", "low_poly_v1" },
                genderPresentations = new[] { "female", "unknown" },
                ageBuckets = new[] { "young_adult", "adult" },
                bodyBuilds = new[] { "average" },
                outfitRoles = new[] { "barista", "casual" },
                outfitColors = new[] { "green", "white", "black" },
                accessoryTags = new[] { "frappe", "coffee", "cup", "drink" },
                mustHaveTags = new string[0],
                qualityTier = "humanoid_v1",
                mobileReady = true
            };
        }

        private static AvatarPresetEntry CreateMaleBaristaEntry(GameObject prefab)
        {
            return new AvatarPresetEntry
            {
                key = MaleBaristaHumanoidKey,
                displayName = "Barista Male Humanoid v1 - Quaternius Casual Character",
                priority = 80,
                prefab = prefab,
                roles = new[] { "barista", "cafe_worker", "clerk" },
                environmentTags = new[] { "coffee_shop", "cafe" },
                styleIds = new[] { "semi_realistic_v1", "humanoid_v1", "low_poly_v1" },
                genderPresentations = new[] { "male" },
                ageBuckets = new[] { "young_adult", "adult" },
                bodyBuilds = new[] { "average" },
                outfitRoles = new[] { "barista", "casual" },
                outfitColors = new[] { "red", "white", "black" },
                accessoryTags = new[] { "coffee", "cup", "drink", "casual" },
                mustHaveTags = new string[0],
                qualityTier = "humanoid_v1",
                mobileReady = true
            };
        }

        private static AvatarPresetEntry CreatePoliceEntry(GameObject prefab)
        {
            return new AvatarPresetEntry
            {
                key = PoliceHumanoidKey,
                displayName = "Police Humanoid v1 - Quaternius SWAT",
                priority = 80,
                prefab = prefab,
                roles = new[] { "police", "officer", "security", "customs" },
                environmentTags = new[] { "airport", "street", "station" },
                styleIds = new[] { "semi_realistic_v1", "humanoid_v1", "low_poly_v1" },
                genderPresentations = new[] { "unknown", "male" },
                ageBuckets = new[] { "adult", "middle_aged" },
                bodyBuilds = new[] { "average", "strong" },
                outfitRoles = new[] { "police", "security", "uniform", "swat" },
                outfitColors = new[] { "black", "navy", "dark" },
                accessoryTags = new[] { "badge", "uniform", "helmet", "vest", "security" },
                mustHaveTags = new string[0],
                qualityTier = "humanoid_v1",
                mobileReady = true
            };
        }

        private static AvatarPresetEntry CreateFemaleTeacherEntry(GameObject prefab)
        {
            return new AvatarPresetEntry
            {
                key = FemaleTeacherHumanoidKey,
                displayName = "Teacher Female Humanoid v1 - Quaternius Suit",
                priority = 80,
                prefab = prefab,
                roles = new[] { "teacher", "instructor", "tutor" },
                environmentTags = new[] { "classroom", "school" },
                styleIds = new[] { "semi_realistic_v1", "humanoid_v1", "low_poly_v1" },
                genderPresentations = new[] { "female" },
                ageBuckets = new[] { "adult", "middle_aged" },
                bodyBuilds = new[] { "average" },
                outfitRoles = new[] { "teacher", "formal", "business" },
                outfitColors = new[] { "black", "white" },
                accessoryTags = new[] { "book", "suit", "formal" },
                mustHaveTags = new string[0],
                qualityTier = "humanoid_v1",
                mobileReady = true
            };
        }

        private static AvatarPresetEntry CreateFemalePoliceEntry(GameObject prefab)
        {
            return new AvatarPresetEntry
            {
                key = FemalePoliceHumanoidKey,
                displayName = "Police Female Humanoid v1 - Quaternius Soldier",
                priority = 80,
                prefab = prefab,
                roles = new[] { "police", "officer", "security", "customs" },
                environmentTags = new[] { "airport", "street", "station" },
                styleIds = new[] { "semi_realistic_v1", "humanoid_v1", "low_poly_v1" },
                genderPresentations = new[] { "female" },
                ageBuckets = new[] { "adult", "middle_aged" },
                bodyBuilds = new[] { "average", "strong" },
                outfitRoles = new[] { "police", "security", "uniform", "soldier" },
                outfitColors = new[] { "black", "navy", "dark", "grey" },
                accessoryTags = new[] { "badge", "uniform", "security" },
                mustHaveTags = new string[0],
                qualityTier = "humanoid_v1",
                mobileReady = true
            };
        }

        private static void UpsertPropCatalogEntries(GameObject bookProp, GameObject frappeProp)
        {
            if (bookProp == null || frappeProp == null)
            {
                Debug.LogError("[SceneTalkVR] P1 prop prefab creation failed.");
                return;
            }

            var catalog = AssetDatabase.LoadAssetAtPath<AvatarPropCatalog>(PropCatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<AvatarPropCatalog>();
                AssetDatabase.CreateAsset(catalog, PropCatalogPath);
            }

            var props = catalog.props == null
                ? new List<AvatarPropEntry>()
                : new List<AvatarPropEntry>(catalog.props);

            props.RemoveAll(entry => entry != null
                && (entry.key == BookPropKey || entry.key == FrappePropKey));
            props.Insert(0, CreateFrappePropEntry(frappeProp));
            props.Insert(0, CreateBookPropEntry(bookProp));

            catalog.props = props.ToArray();
            EditorUtility.SetDirty(catalog);
        }

        private static AvatarPropEntry CreateBookPropEntry(GameObject bookProp)
        {
            return new AvatarPropEntry
            {
                key = BookPropKey,
                displayName = "Book Prop v1",
                priority = 50,
                prefab = bookProp,
                defaultForRoles = new[] { "teacher", "instructor", "tutor" },
                accessoryTags = new[] { "book", "textbook", "notebook" },
                environmentTags = new[] { "classroom", "school" },
                socket = AvatarPropSocket.LeftHand,
                localPosition = new Vector3(0.035f, 0.02f, 0.015f),
                localEulerAngles = new Vector3(10f, 80f, 100f),
                localScale = Vector3.one,
                mobileReady = true
            };
        }

        private static AvatarPropEntry CreateFrappePropEntry(GameObject frappeProp)
        {
            return new AvatarPropEntry
            {
                key = FrappePropKey,
                displayName = "Frappe Prop v1 - Kenney",
                priority = 50,
                prefab = frappeProp,
                defaultForRoles = new[] { "barista", "cafe_worker" },
                accessoryTags = new[] { "frappe", "coffee", "cup", "drink", "takeaway_cup" },
                environmentTags = new[] { "coffee_shop", "cafe" },
                socket = AvatarPropSocket.RightHand,
                localPosition = new Vector3(0.018f, 0.01f, 0.005f),
                localEulerAngles = new Vector3(0f, 95f, 80f),
                localScale = Vector3.one * 0.78f,
                mobileReady = true
            };
        }
    }
}
