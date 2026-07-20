using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SceneTalkVR.Tests.PlayMode
{
    public sealed class EditorDemoPlayModeTests
    {
        private Component demo;
        [UnitySetUp] public IEnumerator SetUp() { if (SceneManager.GetActiveScene().name != "SampleScene") { SceneManager.LoadScene("SampleScene"); yield return null; } yield return null; }
        [UnityTearDown] public IEnumerator TearDown() { if (demo != null) { demo.GetType().GetMethod("ResetDemoSession")?.Invoke(demo, null); UnityEngine.Object.Destroy(demo); } yield return null; yield return null; }

        [UnityTest] public IEnumerator T01_RuntimeModeIncludesFormalAndPilotDemo() { var t = Type.GetType("SceneTalkVR.Core.ExperimentRuntimeMode, Assembly-CSharp"); Assert.That(Enum.GetNames(t), Does.Contain("EditorDemoFormal").And.Contain("EditorDemoPilot")); yield return null; }
        [UnityTest] public IEnumerator T02_TeamDemoCoordinatorCanBeConfigured() { demo = CreateDemo(); Assert.That(demo, Is.Not.Null); Assert.That(Get("DemoProtocol"), Is.Not.Null); yield return null; }
        [UnityTest] public IEnumerator T03_FormalDemoCanStartWithIsolatedAssignment() { demo = CreateDemo(); AssertStart("StartFormalDemo", "001"); Assert.That(Get("RuntimeMode").ToString(), Is.EqualTo("EditorDemoFormal")); AssertIsolation(Get("FormalAssignment")); yield return null; }
        [UnityTest] public IEnumerator T04_FormalDemoProducesFourConditionAssignment() { demo = CreateDemo(); AssertStart("StartFormalDemo", "002"); Assert.That(Count(Get(Get("FormalAssignment"), "conditions")), Is.EqualTo(4)); yield return null; }
        [UnityTest] public IEnumerator T05_FormalDemoCanPrepareGoalTrackedCondition() { demo = CreateDemo(); AssertStart("StartFormalDemo", "003"); AssertCall("PrepareNextCondition"); yield return null; var lifecycle = GetComponent("SceneTalkVR.Core.ExperimentLifecycleCoordinator, Assembly-CSharp"); var tracker = Get(lifecycle, "GoalTracker"); Assert.That(Count(Get(tracker, "Goals")), Is.EqualTo(4)); }
        [UnityTest] public IEnumerator T06_PilotDemoCanStartWithThreeConditions() { demo = CreateDemo(); AssertStart("StartPilotDemo", "001"); Assert.That(Get("RuntimeMode").ToString(), Is.EqualTo("EditorDemoPilot")); AssertIsolation(Get("PilotAssignment")); Assert.That(Count(Get(Get("PilotAssignment"), "conditions")), Is.EqualTo(3)); yield return null; }
        [UnityTest] public IEnumerator T07_VoiceOnlyProfileHasNoVisualAndHeadLockedAudio() { demo = CreateDemo(); var p = ResolveProfile("VoiceOnly"); Assert.That(Get(p, "visualMode").ToString(), Is.EqualTo("None")); Assert.That(Get(p, "audioSourcePolicy").ToString(), Is.EqualTo("NonSpatialHeadLocked")); Assert.That((float)Get(p, "spatialBlend"), Is.Zero); yield return null; }
        [UnityTest] public IEnumerator T08_OrbProfileUsesFloatingOrbVisual() { demo = CreateDemo(); var p = ResolveProfile("FloatingOrb"); Assert.That(Get(p, "visualMode").ToString(), Is.EqualTo("FloatingOrb")); Assert.That(Get(p, "visualPrefabKey"), Is.EqualTo("generated_orb_v1")); yield return null; }
        [UnityTest] public IEnumerator T09_HumanoidProfileUsesExplicitPrefabWithoutOrbFallback() { demo = CreateDemo(); var p = ResolveProfile("HumanoidAgent"); Assert.That(Get(p, "visualMode").ToString(), Is.EqualTo("Humanoid")); Assert.That(Get(p, "visualPrefab"), Is.Not.Null); Assert.That(Get(p, "visualPrefabKey"), Is.Not.EqualTo("generated_orb_v1")); yield return null; }
        [UnityTest] public IEnumerator T10_DemoBannerIsCreatedOnlyWhenDemoActive() { demo = CreateDemo(); AssertStart("StartFormalDemo", "004"); yield return null; var ui = Resources.FindObjectsOfTypeAll(Type.GetType("SceneTalkVR.Runtime.SceneTalkFlowUiController, Assembly-CSharp")).FirstOrDefault(); ui?.GetType().GetMethod("RefreshExternalState")?.Invoke(ui, null); yield return null; Assert.That(GameObject.Find("EditorDemoBanner"), Is.Not.Null); }
        [UnityTest] public IEnumerator T11_ResetReturnsToDeveloperManualAndClearsAssignment() { demo = CreateDemo(); AssertStart("StartPilotDemo", "005"); demo.GetType().GetMethod("ResetDemoSession").Invoke(demo, null); Assert.That(Get("RuntimeMode").ToString(), Is.EqualTo("DeveloperManual")); Assert.That(Get("PilotAssignment"), Is.Null); yield return null; }
        [UnityTest] public IEnumerator T12_AutoFillIsForbiddenOutsideDemo() { demo = CreateDemo(); var args = new object[] { null }; var ok = (bool)demo.GetType().GetMethod("AutoFillQuestionnaire").Invoke(demo, args); Assert.That(ok, Is.False); Assert.That(args[0], Is.EqualTo("demo_autofill_forbidden_outside_demo")); yield return null; }
        [UnityTest] public IEnumerator T13_PilotDemoResolvesAssignedPilotTaskInsteadOfFormalFallback()
        {
            demo = CreateDemo(); AssertStart("StartPilotDemo", "TASK-PHASE"); AssertCall("PrepareNextCondition"); yield return null;
            var manager = GetComponent("SceneTalkVR.Core.ExperimentConditionManager, Assembly-CSharp");
            var expected = Get(demo, "CurrentTaskId") as string;
            Assert.That(expected, Does.StartWith("pilot_restaurant_"));
            Assert.That(Get(manager, "CurrentDebugLabel") as string, Does.Contain(expected));
        }
        [UnityTest] public IEnumerator T14_HumanoidPilotOwnsVisibilityWithoutGenericOrbLeak()
        {
            demo = CreateDemo(); AssertStart("StartPilotDemo", "VISUAL-ISOLATION");
            var assignment = Get(demo, "PilotAssignment"); var conditions = (Array)Get(assignment, "conditions"); var position = -1;
            for (var i = 0; i < conditions.Length; i++) if (Get(conditions.GetValue(i), "embodimentCondition").ToString() == "HumanoidAgent") position = i;
            Assert.That(position, Is.GreaterThanOrEqualTo(0));
            var pilot = GetComponent("SceneTalkVR.Core.PilotWorkflowCoordinator, Assembly-CSharp"); var prepareArgs = new object[] { position, false, null };
            Assert.That((bool)pilot.GetType().GetMethod("Prepare").Invoke(pilot, prepareArgs), Is.True, prepareArgs[2] as string); yield return null;
            var presenter = GetComponent("SceneTalkVR.AvatarSystem.PilotEmbodimentPresenter, Assembly-CSharp"); presenter.GetType().GetMethod("BeginFeedback").Invoke(presenter, null); yield return null;
            var agent = GetComponent("SceneTalkVR.AvatarSystem.CorrectionAgentPresenter, Assembly-CSharp");
            Assert.That((bool)Get(presenter, "HasVisualEntity"), Is.True);
            Assert.That((bool)Get(agent, "TargetVisible"), Is.False, "Generic Orb must not leak into Humanoid Pilot presentation.");
        }

        private Component CreateDemo()
        {
            var manager = GetComponent("SceneTalkVR.Core.ExperimentConditionManager, Assembly-CSharp"); Assert.That(manager, Is.Not.Null);
            var type = Type.GetType("SceneTalkVR.Core.EditorDemoSessionCoordinator, Assembly-CSharp"); var value = manager.GetComponent(type) ?? manager.gameObject.AddComponent(type);
            var configure = type.GetMethod("Configure"); configure.Invoke(value, new[] { Asset("Assets/SceneTalkVR/ExperimentProtocol/ExperimentV11EditorDemoProtocol.asset", configure.GetParameters()[0].ParameterType), Asset("Assets/SceneTalkVR/ExperimentProtocol/EditorDemoAvatarMapping.asset", configure.GetParameters()[1].ParameterType), Asset("Assets/SceneTalkVR/ExperimentProtocol/ExperimentEditorDemoVoiceProfileCatalog.asset", configure.GetParameters()[2].ParameterType), Asset("Assets/SceneTalkVR/ExperimentProtocol/ExperimentEditorDemoDeploymentCatalog.asset", configure.GetParameters()[3].ParameterType) }); return (Component)value;
        }
        private void AssertStart(string method, string participant) { var args = new object[] { participant, null }; var ok = (bool)demo.GetType().GetMethod(method).Invoke(demo, args); Assert.That(ok, Is.True, args[1] as string); }
        private void AssertCall(string method) { var args = new object[] { null }; var ok = (bool)demo.GetType().GetMethod(method).Invoke(demo, args); Assert.That(ok, Is.True, args[0] as string); }
        private object ResolveProfile(string name) { var method = demo.GetType().GetMethod("ResolvePilotProfile"); var type = method.GetParameters()[0].ParameterType; return method.Invoke(demo, new[] { Enum.Parse(type, name) }); }
        private object Get(string name) => Get(demo, name);
        private static object Get(object value, string name) { var t = value.GetType(); return t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance)?.GetValue(value) ?? t.GetField(name, BindingFlags.Public | BindingFlags.Instance)?.GetValue(value); }
        private static int Count(object value)
        {
            if (value is System.Collections.ICollection collection) return collection.Count;
            var property = value?.GetType().GetProperty("Count") ?? value?.GetType().GetProperty("Length");
            return property == null ? 0 : (int)property.GetValue(value);
        }
        private static Component GetComponent(string typeName) { var t = Type.GetType(typeName); return (Component)Resources.FindObjectsOfTypeAll(t).FirstOrDefault(); }
        private static object Asset(string path, Type type) { var adb = Type.GetType("UnityEditor.AssetDatabase, UnityEditor"); return adb.GetMethod("LoadAssetAtPath", new[] { typeof(string), typeof(Type) }).Invoke(null, new object[] { path, type }); }
        private static void AssertIsolation(object assignment) { Assert.That(Get(assignment, "dataOrigin"), Is.EqualTo("editor_demo")); Assert.That((bool)Get(assignment, "collectionEligible"), Is.False); Assert.That((bool)Get(assignment, "developerTestAssignment"), Is.True); Assert.That((bool)Get(assignment, "demoMode"), Is.True); }
    }
}
