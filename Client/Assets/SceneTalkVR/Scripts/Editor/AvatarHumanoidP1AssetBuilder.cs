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
        private const string FrappeModelFolder = AvatarRoot + "/Models/Props/KenneyFrappe";
        private const string FrappeModelPath = FrappeModelFolder + "/frappe.obj";
        private const string AnimationFolder = AvatarRoot + "/Animations";
        private const string MixamoAnimationFolder = AnimationFolder + "/Mixamo";
        private const string ThinkingAnimationPath = MixamoAnimationFolder + "/Thinking.fbx";
        private const string TalkAnimationPath = MixamoAnimationFolder + "/Thoughtful Head Nod 70AS.fbx";
        private const string ThinkingEnterClipName = "ThinkingEnter";
        private const string ThinkingHoldClipName = "ThinkingHold";
        private const string TalkClipName = "TalkLoop";
        private const float ThinkingEnterFirstFrame = 0f;
        private const float ThinkingEnterLastFrame = 46f;
        private const float ThinkingHoldFirstFrame = 46f;
        private const float ThinkingHoldLastFrame = 70f;
        private const string CommonAnimationFolder = AnimationFolder + "/Common";
        private const string NativeIdleFolder = CommonAnimationFolder + "/NativeIdle";
        private const string TeacherIdlePath = NativeIdleFolder + "/teacher_idle_neutral_loop.anim";
        private const string BaristaIdlePath = NativeIdleFolder + "/barista_idle_neutral_loop.anim";
        private const string PoliceIdlePath = NativeIdleFolder + "/police_idle_neutral_loop.anim";
        private const string MaleBaristaIdlePath = NativeIdleFolder + "/barista_male_idle_neutral_loop.anim";
        private const string FemaleTeacherIdlePath = NativeIdleFolder + "/teacher_female_idle_neutral_loop.anim";
        private const string FemalePoliceIdlePath = NativeIdleFolder + "/police_female_idle_neutral_loop.anim";
        private const string CommonHumanoidControllerPath = CommonAnimationFolder + "/AvatarCommonHumanoid.controller";
        private const string OverrideControllerFolder = CommonAnimationFolder + "/Overrides";
        private const string BaristaOverrideControllerPath = OverrideControllerFolder + "/barista_humanoid_v1.overrideController";
        private const string PoliceOverrideControllerPath = OverrideControllerFolder + "/police_humanoid_v1.overrideController";
        private const string MaleBaristaOverrideControllerPath = OverrideControllerFolder + "/barista_male_humanoid_v1.overrideController";
        private const string FemaleTeacherOverrideControllerPath = OverrideControllerFolder + "/teacher_female_humanoid_v1.overrideController";
        private const string FemalePoliceOverrideControllerPath = OverrideControllerFolder + "/police_female_humanoid_v1.overrideController";
        private const string ConversationMaskPath = CommonAnimationFolder + "/AvatarConversationUpperBody.mask";
        private const string ThinkingHeadMaskPath = CommonAnimationFolder + "/AvatarThinkingHead.mask";
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
            ConfigureHumanoidImporter(MaleBaristaModelPath);
            ConfigureHumanoidImporter(FemaleTeacherModelPath);
            ConfigureHumanoidImporter(FemalePoliceModelPath);
            var thinkingReady = ConfigureMixamoAnimationImporter(
                ThinkingAnimationPath,
                new MixamoClipDefinition(
                    ThinkingEnterClipName,
                    ThinkingEnterFirstFrame,
                    ThinkingEnterLastFrame,
                    false),
                new MixamoClipDefinition(
                    ThinkingHoldClipName,
                    ThinkingHoldFirstFrame,
                    ThinkingHoldLastFrame,
                    true));
            var talkReady = ConfigureMixamoAnimationImporter(TalkAnimationPath, TalkClipName);
            if (!thinkingReady || !talkReady)
            {
                Debug.LogError("[SceneTalkVR] Humanoid build stopped because the Mixamo conversation animations are not valid Humanoid clips.");
                return;
            }

            var teacherIdle = CreateOrUpdateNativeIdleClip(TeacherModelPath, TeacherIdlePath);
            var baristaIdle = CreateOrUpdateNativeIdleClip(BaristaModelPath, BaristaIdlePath);
            var policeIdle = CreateOrUpdateNativeIdleClip(PoliceModelPath, PoliceIdlePath);
            var maleBaristaIdle = CreateOrUpdateNativeIdleClip(MaleBaristaModelPath, MaleBaristaIdlePath);
            var femaleTeacherIdle = CreateOrUpdateNativeIdleClip(FemaleTeacherModelPath, FemaleTeacherIdlePath);
            var femalePoliceIdle = CreateOrUpdateNativeIdleClip(FemalePoliceModelPath, FemalePoliceIdlePath);
            if (teacherIdle == null
                || baristaIdle == null
                || policeIdle == null
                || maleBaristaIdle == null
                || femaleTeacherIdle == null
                || femalePoliceIdle == null)
            {
                Debug.LogError("[SceneTalkVR] Humanoid build stopped because a native looping Idle clip could not be created.");
                return;
            }

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

            var controller = BuildCommonHumanoidAnimatorController(teacherIdle);
            if (controller == null)
            {
                Debug.LogError("[SceneTalkVR] Humanoid build stopped because the shared Animator controller could not be built.");
                return;
            }
            var baristaController = CreateOrUpdateCharacterOverrideController(
                BaristaOverrideControllerPath,
                controller,
                BaristaModelPath,
                baristaIdle);
            var policeController = CreateOrUpdateCharacterOverrideController(
                PoliceOverrideControllerPath,
                controller,
                PoliceModelPath,
                policeIdle);
            var maleBaristaController = CreateOrUpdateCharacterOverrideController(
                MaleBaristaOverrideControllerPath,
                controller,
                MaleBaristaModelPath,
                maleBaristaIdle);
            var femaleTeacherController = CreateOrUpdateCharacterOverrideController(
                FemaleTeacherOverrideControllerPath,
                controller,
                FemaleTeacherModelPath,
                femaleTeacherIdle);
            var femalePoliceController = CreateOrUpdateCharacterOverrideController(
                FemalePoliceOverrideControllerPath,
                controller,
                FemalePoliceModelPath,
                femalePoliceIdle);

            if (baristaController == null
                || policeController == null
                || maleBaristaController == null
                || femaleTeacherController == null
                || femalePoliceController == null)
            {
                Debug.LogError("[SceneTalkVR] P1 humanoid build stopped because a character-native animation override could not be created.");
                return;
            }

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
                baristaController);
            var policePrefab = CreateHumanoidPrefab(
                policeSourceModel,
                "police_humanoid_v1",
                "QuaterniusSWAT",
                PolicePrefabPath,
                PoliceTargetHeightMeters,
                180f,
                policeController);
            var maleBaristaPrefab = CreateHumanoidPrefab(
                maleBaristaSourceModel,
                "barista_male_humanoid_v1",
                "QuaterniusCasualCharacter",
                MaleBaristaPrefabPath,
                TargetHeightMeters,
                180f,
                maleBaristaController);
            var femaleTeacherPrefab = CreateHumanoidPrefab(
                femaleTeacherSourceModel,
                "teacher_female_humanoid_v1",
                "QuaterniusSuit",
                FemaleTeacherPrefabPath,
                FemaleTeacherTargetHeightMeters,
                180f,
                femaleTeacherController);
            var femalePolicePrefab = CreateHumanoidPrefab(
                femalePoliceSourceModel,
                "police_female_humanoid_v1",
                "QuaterniusSoldier",
                FemalePolicePrefabPath,
                FemalePoliceTargetHeightMeters,
                180f,
                femalePoliceController);
            var bookProp = CreateBookPropPrefab();
            var frappeProp = CreateFrappePropPrefab();
            UpsertCatalogEntries(teacherPrefab, baristaPrefab, policePrefab, maleBaristaPrefab, femaleTeacherPrefab, femalePolicePrefab);
            UpsertPropCatalogEntries(bookProp, frappeProp);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = policePrefab;
            Debug.Log($"[SceneTalkVR] Built P1 humanoid source prefabs and fixed-scenario catalog entries: {BaristaHumanoidKey}, {TeacherHumanoidKey}, {MaleBaristaHumanoidKey}, {FemaleTeacherHumanoidKey}.");
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/SceneTalkVR", "Avatar");
            EnsureFolder(AvatarRoot, "Models");
            EnsureFolder(AvatarRoot, "Animations");
            EnsureFolder(AnimationFolder, "Mixamo");
            EnsureFolder(AnimationFolder, "Common");
            EnsureFolder(CommonAnimationFolder, "NativeIdle");
            EnsureFolder(CommonAnimationFolder, "Overrides");
            EnsureFolder(AvatarRoot + "/Models", "Humanoid");
            EnsureFolder(AvatarRoot + "/Models/Humanoid", "QuaterniusBusinessMan");
            EnsureFolder(AvatarRoot + "/Models/Humanoid", "QuaterniusAnimatedWoman");
            EnsureFolder(AvatarRoot + "/Models/Humanoid", "QuaterniusSWAT");
            EnsureFolder(AvatarRoot + "/Models/Humanoid", "QuaterniusCasualCharacter");
            EnsureFolder(AvatarRoot + "/Models/Humanoid", "QuaterniusSuit");
            EnsureFolder(AvatarRoot + "/Models/Humanoid", "QuaterniusSoldier");
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

        private static bool ConfigureMixamoAnimationImporter(string modelPath, string clipName)
        {
            return ConfigureMixamoAnimationImporter(
                modelPath,
                new MixamoClipDefinition(clipName, null, null, true));
        }

        private static bool ConfigureMixamoAnimationImporter(
            string modelPath,
            params MixamoClipDefinition[] definitions)
        {
            var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError($"[SceneTalkVR] Mixamo animation was not found at {modelPath}.");
                return false;
            }

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.autoGenerateAvatarMappingIfUnspecified = true;
            importer.importAnimation = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;

            var clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0)
            {
                clips = importer.defaultClipAnimations;
            }

            if (clips == null || clips.Length == 0)
            {
                Debug.LogError($"[SceneTalkVR] Mixamo animation has no clips: {modelPath}.");
                return false;
            }

            var sourceClip = clips[0];
            var configuredClips = new ModelImporterClipAnimation[definitions.Length];
            for (var i = 0; i < definitions.Length; i++)
            {
                configuredClips[i] = CreateMixamoClip(sourceClip, definitions[i]);
            }

            importer.clipAnimations = configuredClips;

            AssetDatabase.ImportAsset(modelPath, ImportAssetOptions.ForceUpdate);

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            var animator = model != null ? model.GetComponentInChildren<Animator>() : null;
            var isValidHumanoid = animator != null
                && animator.avatar != null
                && animator.avatar.isValid
                && animator.avatar.isHuman;
            var allClipsFound = true;
            for (var i = 0; i < definitions.Length; i++)
            {
                allClipsFound &= LoadExactClip(modelPath, definitions[i].Name) != null;
            }

            if (!isValidHumanoid || !allClipsFound)
            {
                Debug.LogError(
                    $"[SceneTalkVR] Mixamo Humanoid import validation failed: path={modelPath}, "
                    + $"avatarValid={isValidHumanoid}, allClipsFound={allClipsFound}.");
                return false;
            }

            return true;
        }

        private static ModelImporterClipAnimation CreateMixamoClip(
            ModelImporterClipAnimation source,
            MixamoClipDefinition definition)
        {
            return new ModelImporterClipAnimation
            {
                name = definition.Name,
                takeName = source.takeName,
                firstFrame = definition.FirstFrame ?? source.firstFrame,
                lastFrame = definition.LastFrame ?? source.lastFrame,
                wrapMode = definition.LoopTime ? WrapMode.Loop : WrapMode.Once,
                loopTime = definition.LoopTime,
                loopPose = definition.LoopTime,
                cycleOffset = source.cycleOffset,
                mirror = source.mirror,
                keepOriginalOrientation = true,
                keepOriginalPositionY = true,
                keepOriginalPositionXZ = true,
                lockRootRotation = true,
                lockRootHeightY = true,
                lockRootPositionXZ = true,
                heightFromFeet = true,
                rotationOffset = source.rotationOffset,
                heightOffset = source.heightOffset,
                hasAdditiveReferencePose = source.hasAdditiveReferencePose,
                additiveReferencePoseFrame = source.additiveReferencePoseFrame,
                maskType = source.maskType,
                maskSource = source.maskSource,
                curves = source.curves,
                events = source.events
            };
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
                changed |= ConfigureClipLooping(importer);
            }

            if (changed)
            {
                AssetDatabase.ImportAsset(modelPath, ImportAssetOptions.ForceUpdate);
            }
        }

        private static bool ConfigureClipLooping(ModelImporter importer)
        {
            var clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0)
            {
                clips = importer.defaultClipAnimations;
            }

            if (clips == null || clips.Length == 0)
            {
                return false;
            }

            var changed = false;
            for (var i = 0; i < clips.Length; i++)
            {
                var clip = clips[i];
                var name = clip.name ?? string.Empty;
                if (name.IndexOf("Idle", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("Walk", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("Run", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    changed |= clip.wrapMode != WrapMode.Loop || !clip.loopTime || !clip.loopPose;
                    clip.wrapMode = WrapMode.Loop;
                    clip.loopTime = true;
                    clip.loopPose = true;
                    clips[i] = clip;
                }
            }

            if (changed)
            {
                importer.clipAnimations = clips;
            }

            return changed;
        }

        private static AnimationClip CreateOrUpdateNativeIdleClip(string modelPath, string assetPath)
        {
            var source = LoadExactClip(modelPath, "__preview__CharacterArmature|Idle_Neutral");
            if (source == null)
            {
                Debug.LogError($"[SceneTalkVR] Native Idle source clip was not found in {modelPath}.");
                return null;
            }

            var target = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
            if (target == null)
            {
                target = new AnimationClip();
                AssetDatabase.CreateAsset(target, assetPath);
            }

            target.ClearCurves();
            target.name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            target.frameRate = source.frameRate;
            target.localBounds = source.localBounds;
            target.legacy = source.legacy;
            var sourceBindings = AnimationUtility.GetCurveBindings(source);
            for (var i = 0; i < sourceBindings.Length; i++)
            {
                var binding = sourceBindings[i];
                AnimationUtility.SetEditorCurve(
                    target,
                    binding,
                    AnimationUtility.GetEditorCurve(source, binding));
            }

            var objectBindings = AnimationUtility.GetObjectReferenceCurveBindings(source);
            for (var i = 0; i < objectBindings.Length; i++)
            {
                var binding = objectBindings[i];
                AnimationUtility.SetObjectReferenceCurve(
                    target,
                    binding,
                    AnimationUtility.GetObjectReferenceCurve(source, binding));
            }

            AnimationUtility.SetAnimationEvents(
                target,
                AnimationUtility.GetAnimationEvents(source));
            target.wrapMode = WrapMode.Loop;
            var settings = AnimationUtility.GetAnimationClipSettings(source);
            settings.loopTime = true;
            settings.loopBlend = true;
            AnimationUtility.SetAnimationClipSettings(target, settings);
            target.EnsureQuaternionContinuity();

            var hasLeftFootCurves = false;
            var hasRightFootCurves = false;
            var bindings = AnimationUtility.GetCurveBindings(target);
            for (var i = 0; i < bindings.Length; i++)
            {
                var binding = bindings[i];
                if (binding.type != typeof(Transform))
                {
                    continue;
                }

                hasLeftFootCurves |= binding.path == "CharacterArmature/Root/Foot.L";
                hasRightFootCurves |= binding.path == "CharacterArmature/Root/Foot.R";
            }

            if (!hasLeftFootCurves || !hasRightFootCurves)
            {
                Debug.LogError(
                    $"[SceneTalkVR] Native Idle is missing foot transform curves: "
                    + $"path={assetPath}, left={hasLeftFootCurves}, right={hasRightFootCurves}.");
                return null;
            }

            EditorUtility.SetDirty(target);
            return target;
        }

        private static HumanDescription CreateQuaterniusHumanDescription(Transform root)
        {
            var humanBones = new[]
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
            };
            var setupMethod = typeof(ModelImporter).Assembly
                .GetType("UnityEditor.AvatarSetupTool")
                ?.GetMethod(
                    "SetupHumanSkeleton",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            if (setupMethod == null)
            {
                throw new System.InvalidOperationException("Unity AvatarSetupTool.SetupHumanSkeleton was not found.");
            }

            object[] setupArguments = { root.gameObject, humanBones, null, false };
            setupMethod.Invoke(null, setupArguments);

            return new HumanDescription
            {
                human = humanBones,
                skeleton = (SkeletonBone[])setupArguments[2],
                upperArmTwist = 0.5f,
                lowerArmTwist = 0.5f,
                upperLegTwist = 0.5f,
                lowerLegTwist = 0.5f,
                armStretch = 0.05f,
                legStretch = 0.05f,
                feetSpacing = 0f,
                hasTranslationDoF = (bool)setupArguments[3]
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

        private static AnimatorController BuildCommonHumanoidAnimatorController(AnimationClip idleClip)
        {
            var speakWaveClip = LoadClip("CharacterArmature|Wave") ?? idleClip;
            var thinkingEnterClip = LoadClip(ThinkingAnimationPath, ThinkingEnterClipName);
            var thinkingHoldClip = LoadClip(ThinkingAnimationPath, ThinkingHoldClipName);
            var talkClip = LoadClip(TalkAnimationPath, TalkClipName);
            if (idleClip == null
                || speakWaveClip == null
                || thinkingEnterClip == null
                || thinkingHoldClip == null
                || talkClip == null)
            {
                Debug.LogError(
                    $"[SceneTalkVR] Shared animation clips are incomplete: idle={idleClip != null}, "
                    + $"speakWave={speakWaveClip != null}, "
                    + $"thinkingEnter={thinkingEnterClip != null}, thinkingHold={thinkingHoldClip != null}, "
                    + $"talk={talkClip != null}.");
                return null;
            }

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(CommonHumanoidControllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(CommonHumanoidControllerPath);
            }

            ResetAnimatorController(controller);
            controller.AddParameter("Speak", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("IsThinking", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsTalking", AnimatorControllerParameterType.Bool);

            var baseStateMachine = controller.layers[0].stateMachine;
            var idle = baseStateMachine.AddState("Idle");
            idle.motion = idleClip;
            idle.writeDefaultValues = false;
            baseStateMachine.defaultState = idle;

            var layers = controller.layers;
            var baseLayer = layers[0];
            baseLayer.iKPass = true;
            layers[0] = baseLayer;
            controller.layers = layers;

            AddConversationLayer(
                controller,
                idleClip,
                speakWaveClip,
                thinkingEnterClip,
                thinkingHoldClip,
                talkClip);
            AddThinkingHeadStabilizationLayer(
                controller,
                idleClip,
                speakWaveClip,
                talkClip);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        private static void ResetAnimatorController(AnimatorController controller)
        {
            for (var i = controller.layers.Length - 1; i >= 0; i--)
            {
                controller.RemoveLayer(i);
            }

            for (var i = controller.parameters.Length - 1; i >= 0; i--)
            {
                controller.RemoveParameter(i);
            }

            controller.AddLayer("Base Layer");
        }

        private static AnimatorOverrideController CreateOrUpdateCharacterOverrideController(
            string assetPath,
            RuntimeAnimatorController baseController,
            string characterModelPath,
            AnimationClip characterIdle)
        {
            var baseIdle = AssetDatabase.LoadAssetAtPath<AnimationClip>(TeacherIdlePath);
            var baseSpeaking = LoadClip(TeacherModelPath, "CharacterArmature|Wave") ?? baseIdle;
            var characterSpeaking = LoadClip(characterModelPath, "CharacterArmature|Wave") ?? baseIdle;

            if (baseController == null
                || baseIdle == null
                || baseSpeaking == null
                || characterIdle == null
                || characterSpeaking == null)
            {
                Debug.LogError($"[SceneTalkVR] Missing native animation clips for '{characterModelPath}'.");
                return null;
            }

            if (!baseIdle.isLooping || !characterIdle.isLooping)
            {
                Debug.LogError(
                    $"[SceneTalkVR] Native Idle clips must loop: "
                    + $"base={baseIdle.name} ({baseIdle.isLooping}), "
                    + $"character={characterIdle.name} ({characterIdle.isLooping}).");
                return null;
            }

            var overrideController = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(assetPath);
            if (overrideController == null)
            {
                overrideController = new AnimatorOverrideController(baseController);
                AssetDatabase.CreateAsset(overrideController, assetPath);
            }
            else
            {
                // Reassign through the public API so Unity rebuilds its internal clip-key table.
                overrideController.runtimeAnimatorController = null;
                overrideController.runtimeAnimatorController = baseController;
            }

            overrideController.name = System.IO.Path.GetFileNameWithoutExtension(assetPath);

            var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            overrideController.GetOverrides(overrides);
            SetClipOverride(overrides, baseIdle, characterIdle);
            SetClipOverride(overrides, baseSpeaking, characterSpeaking);
            overrideController.ApplyOverrides(overrides);
            EditorUtility.SetDirty(overrideController);
            return overrideController;
        }

        private static void SetClipOverride(
            List<KeyValuePair<AnimationClip, AnimationClip>> overrides,
            AnimationClip source,
            AnimationClip replacement)
        {
            for (var i = 0; i < overrides.Count; i++)
            {
                if (overrides[i].Key == source)
                {
                    overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(source, replacement);
                    return;
                }
            }

            Debug.LogWarning($"[SceneTalkVR] Animator override source clip '{source.name}' was not found in the base controller.");
        }

        private static void AddConversationLayer(
            AnimatorController controller,
            AnimationClip idleClip,
            AnimationClip speakWaveClip,
            AnimationClip thinkingEnterClip,
            AnimationClip thinkingHoldClip,
            AnimationClip talkClip)
        {
            var mask = CreateOrUpdateConversationMask();
            controller.AddLayer("Upper Body Conversation");
            var layers = controller.layers;
            var layer = layers[layers.Length - 1];
            layer.defaultWeight = 1f;
            layer.avatarMask = mask;
            layer.blendingMode = AnimatorLayerBlendingMode.Override;

            var stateMachine = layer.stateMachine;
            var idle = stateMachine.AddState("ConversationIdle");
            idle.motion = idleClip;
            idle.writeDefaultValues = false;
            stateMachine.defaultState = idle;

            var thinkingEnter = stateMachine.AddState("ThinkingEnter");
            thinkingEnter.motion = thinkingEnterClip;
            thinkingEnter.writeDefaultValues = false;
            var thinkingHold = stateMachine.AddState("ThinkingHold");
            thinkingHold.motion = thinkingHoldClip;
            thinkingHold.writeDefaultValues = false;
            var speakWave = stateMachine.AddState("SpeakWave");
            speakWave.motion = speakWaveClip;
            speakWave.writeDefaultValues = false;
            var talking = stateMachine.AddState("TalkLoop");
            talking.motion = talkClip;
            talking.writeDefaultValues = false;

            AddBoolTransition(idle, thinkingEnter, "IsThinking", true);
            AddBoolTransition(idle, talking, "IsTalking", true);
            AddBoolTransition(thinkingEnter, talking, "IsTalking", true);
            AddBoolTransition(thinkingEnter, idle, "IsThinking", false);
            AddExitTimeTransition(
                thinkingEnter,
                thinkingHold,
                "IsThinking",
                true,
                0.95f,
                0.1f);
            AddBoolTransition(thinkingHold, talking, "IsTalking", true);
            AddBoolTransition(thinkingHold, idle, "IsThinking", false);
            AddBoolTransition(talking, idle, "IsTalking", false);

            var open = stateMachine.AddAnyStateTransition(speakWave);
            open.AddCondition(AnimatorConditionMode.If, 0f, "Speak");
            open.duration = 0.1f;
            open.canTransitionToSelf = false;

            AddExitTimeTransition(speakWave, talking, "IsTalking", true);
            AddExitTimeTransition(speakWave, idle, "IsTalking", false);

            layers[layers.Length - 1] = layer;
            controller.layers = layers;
        }

        private static AvatarMask CreateOrUpdateConversationMask()
        {
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(ConversationMaskPath);
            if (mask == null)
            {
                mask = new AvatarMask();
                AssetDatabase.CreateAsset(mask, ConversationMaskPath);
            }

            for (var i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
            {
                mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
            }

            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Head, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, true);

            EditorUtility.SetDirty(mask);
            return mask;
        }

        private static void AddThinkingHeadStabilizationLayer(
            AnimatorController controller,
            AnimationClip idleClip,
            AnimationClip speakWaveClip,
            AnimationClip talkClip)
        {
            var mask = CreateOrUpdateThinkingHeadMask();
            controller.AddLayer("Thinking Head Stabilization");
            var layers = controller.layers;
            var layer = layers[layers.Length - 1];
            layer.defaultWeight = 1f;
            layer.avatarMask = mask;
            layer.blendingMode = AnimatorLayerBlendingMode.Override;

            var stateMachine = layer.stateMachine;
            var idle = stateMachine.AddState("HeadIdle");
            idle.motion = idleClip;
            idle.writeDefaultValues = false;
            stateMachine.defaultState = idle;

            var thinkingHeadIdle = stateMachine.AddState("ThinkingHeadIdle");
            thinkingHeadIdle.motion = idleClip;
            thinkingHeadIdle.writeDefaultValues = false;
            var speakWave = stateMachine.AddState("HeadSpeakWave");
            speakWave.motion = speakWaveClip;
            speakWave.writeDefaultValues = false;
            var talking = stateMachine.AddState("HeadTalkLoop");
            talking.motion = talkClip;
            talking.writeDefaultValues = false;

            AddBoolTransition(idle, thinkingHeadIdle, "IsThinking", true);
            AddBoolTransition(idle, talking, "IsTalking", true);
            AddBoolTransition(thinkingHeadIdle, talking, "IsTalking", true);
            AddBoolTransition(thinkingHeadIdle, idle, "IsThinking", false);
            AddBoolTransition(talking, idle, "IsTalking", false);

            var open = stateMachine.AddAnyStateTransition(speakWave);
            open.AddCondition(AnimatorConditionMode.If, 0f, "Speak");
            open.duration = 0.1f;
            open.canTransitionToSelf = false;

            AddExitTimeTransition(speakWave, talking, "IsTalking", true);
            AddExitTimeTransition(speakWave, idle, "IsTalking", false);

            layers[layers.Length - 1] = layer;
            controller.layers = layers;
        }

        private static AvatarMask CreateOrUpdateThinkingHeadMask()
        {
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(ThinkingHeadMaskPath);
            if (mask == null)
            {
                mask = new AvatarMask();
                AssetDatabase.CreateAsset(mask, ThinkingHeadMaskPath);
            }

            for (var i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
            {
                mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
            }

            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Head, true);

            EditorUtility.SetDirty(mask);
            return mask;
        }

        private static void AddBoolTransition(
            AnimatorState source,
            AnimatorState destination,
            string parameterName,
            bool expectedValue)
        {
            var transition = source.AddTransition(destination);
            transition.hasExitTime = false;
            transition.duration = 0.15f;
            transition.AddCondition(
                expectedValue ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot,
                0f,
                parameterName);
        }

        private static void AddExitTimeTransition(
            AnimatorState source,
            AnimatorState destination,
            string parameterName,
            bool expectedValue,
            float exitTime = 0.9f,
            float duration = 0.15f)
        {
            var transition = source.AddTransition(destination);
            transition.hasExitTime = true;
            transition.exitTime = exitTime;
            transition.duration = duration;
            transition.AddCondition(
                expectedValue ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot,
                0f,
                parameterName);
        }

        private readonly struct MixamoClipDefinition
        {
            public MixamoClipDefinition(
                string name,
                float? firstFrame,
                float? lastFrame,
                bool loopTime)
            {
                Name = name;
                FirstFrame = firstFrame;
                LastFrame = lastFrame;
                LoopTime = loopTime;
            }

            public string Name { get; }
            public float? FirstFrame { get; }
            public float? LastFrame { get; }
            public bool LoopTime { get; }
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

        private static AnimationClip LoadExactClip(string assetPath, string clipName)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            for (var i = 0; i < assets.Length; i++)
            {
                var clip = assets[i] as AnimationClip;
                if (clip != null
                    && string.Equals(clip.name, clipName, System.StringComparison.Ordinal))
                {
                    return clip;
                }
            }

            return null;
        }

        private static AnimationClip FindClip(UnityEngine.Object[] assets, string clipName)
        {
            for (var i = 0; i < assets.Length; i++)
            {
                var clip = assets[i] as AnimationClip;
                if (clip != null
                    && !clip.name.StartsWith("__preview__", System.StringComparison.Ordinal)
                    && IsClipNameMatch(clip.name, clipName))
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
                || maleBaristaPrefab == null
                || femaleTeacherPrefab == null)
            {
                Debug.LogError("[SceneTalkVR] P1 humanoid prefab creation failed.");
                return;
            }

            var catalog = AssetDatabase.LoadAssetAtPath<AvatarCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<AvatarCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
                catalog.defaultAvatarKey = BaristaHumanoidKey;
            }

            catalog.defaultAvatarKey = BaristaHumanoidKey;
            catalog.presets = new[]
            {
                CreateBaristaEntry(baristaPrefab),
                CreateTeacherEntry(teacherPrefab),
                CreateMaleBaristaEntry(maleBaristaPrefab),
                CreateFemaleTeacherEntry(femaleTeacherPrefab)
            };
            EditorUtility.SetDirty(catalog);
        }

        private static AvatarPresetEntry CreateTeacherEntry(GameObject prefab)
        {
            return new AvatarPresetEntry
            {
                key = TeacherHumanoidKey,
                displayName = "Teacher Humanoid v1 - Quaternius Business Man",
                priority = 80,
                prefab = prefab,
                scenarioIds = new[] { "furniture_shopping" },
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
                scenarioIds = new[] { "restaurant_reservation" },
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
                scenarioIds = new[] { "gym_membership" },
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
                scenarioIds = new[] { "hotel_check_in" },
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
