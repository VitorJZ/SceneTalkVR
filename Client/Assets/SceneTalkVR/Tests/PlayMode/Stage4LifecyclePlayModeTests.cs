using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace SceneTalkVR.Tests.PlayMode
{
    public sealed class Stage4LifecyclePlayModeTests
    {
        private static readonly string[] GymGoals =
        {
            "Explain a fitness goal.",
            "Ask about the monthly membership price.",
            "Ask about a suitable workout plan.",
            "Ask whether a free trial is available."
        };

        private static readonly string[] HotelGoals =
        {
            "Provide the reservation name.",
            "Ask whether breakfast is included.",
            "Ask whether a high-floor room can be arranged.",
            "Ask about the check-out time."
        };

        [UnityTest]
        public IEnumerator DeveloperTaskPath_ShowsReadOnlyGoals_AndExitClearsSession()
        {
            SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var lifecycleType = Type.GetType("SceneTalkVR.Core.ExperimentLifecycleCoordinator, Assembly-CSharp");
            Assert.That(lifecycleType, Is.Not.Null);
            var lifecycle = FindSceneComponent(lifecycleType);
            Assert.That(lifecycle, Is.Not.Null);

            InvokeButton("Start");
            yield return null;
            InvokeButton("Gym Membership");

            var panel = FindTransform("ReadOnlyTaskGoalPanel");
            Assert.That(panel, Is.Not.Null);
            yield return WaitFor(() => panel.gameObject.activeInHierarchy, 10f, "Gym Goal Panel did not become visible.");
            Assert.That(panel.GetComponentsInChildren<Button>(true), Is.Empty, "Participant Goal Panel must stay read-only.");

            var goalText = panel.GetComponentsInChildren<Text>(true).Single(x => x.name == "GoalStateText");
            AssertGoals(goalText.text, "gym_membership", GymGoals);
            var tracker = lifecycleType.GetProperty("GoalTracker")!.GetValue(lifecycle);
            AssertTrackerGoals(tracker, GymGoals);

            var assignment = lifecycleType.GetProperty("Assignment")!.GetValue(lifecycle);
            Assert.That(assignment, Is.Not.Null);
            Assert.That(ReadField<string>(assignment, "dataOrigin"), Is.EqualTo("developer_manual"));
            Assert.That(ReadField<bool>(assignment, "collectionEligible"), Is.False);
            Assert.That(ReadField<bool>(assignment, "developerTestAssignment"), Is.True);
            Assert.That(lifecycleType.GetProperty("ConditionRunId")!.GetValue(lifecycle) as string, Is.Not.Empty);
            Assert.That(lifecycleType.GetProperty("QuestionnaireLinkageKey")!.GetValue(lifecycle) as string, Is.Not.Empty);

            InvokeButton("Exit");
            yield return null;
            yield return null;
            Assert.That(panel.gameObject.activeInHierarchy, Is.False);
            Assert.That(goalText.text, Is.Empty);
            AssertTrackerGoals(tracker, Array.Empty<string>());
            Assert.That(lifecycleType.GetProperty("Assignment")!.GetValue(lifecycle), Is.Null);

            InvokeButton("Start");
            yield return null;
            InvokeButton("Hotel Check-In");
            yield return WaitFor(() => panel.gameObject.activeInHierarchy, 10f, "Hotel Goal Panel did not become visible.");
            AssertGoals(goalText.text, "hotel_check_in", HotelGoals);
            foreach (var gymGoal in GymGoals) Assert.That(goalText.text, Does.Not.Contain(gymGoal));
            AssertTrackerGoals(tracker, HotelGoals);
        }

        [UnityTest]
        public IEnumerator FormalProtocol_RemainsBlockedByElevenUnconfirmedDecisions()
        {
            SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var managerType = Type.GetType("SceneTalkVR.Core.ExperimentConditionManager, Assembly-CSharp");
            var manager = FindSceneComponent(managerType);
            var protocol = managerType!.GetProperty("ExperimentProtocol")!.GetValue(manager);
            var decisions = ((IEnumerable)protocol.GetType().GetProperty("RequiredDecisions")!.GetValue(protocol))
                .Cast<object>().ToArray();
            Assert.That(decisions, Has.Length.EqualTo(11));
            Assert.That(decisions.Count(x => ReadField<object>(x, "status").ToString() == "Unconfirmed"), Is.EqualTo(11));

            var args = new object[] { null };
            var valid = (bool)protocol.GetType().GetMethod("ValidateForFormalMode")!.Invoke(protocol, args);
            Assert.That(valid, Is.False);
            Assert.That(args[0] as string, Does.Contain("unconfirmed protocol decision"));
        }

        private static IEnumerator WaitFor(Func<bool> predicate, float timeoutSeconds, string failure)
        {
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!predicate() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(predicate(), Is.True, failure);
        }

        private static void InvokeButton(string label)
        {
            var button = Resources.FindObjectsOfTypeAll<Button>()
                .FirstOrDefault(x => x.gameObject.scene.IsValid() && x.gameObject.activeInHierarchy
                    && x.GetComponentInChildren<Text>(true)?.text == label);
            Assert.That(button, Is.Not.Null, $"Active button '{label}' was not found.");
            button.onClick.Invoke();
        }

        private static Transform FindTransform(string name) => Resources.FindObjectsOfTypeAll<Transform>()
            .FirstOrDefault(x => x.gameObject.scene.IsValid() && x.name == name);

        private static Component FindSceneComponent(Type componentType) => Resources.FindObjectsOfTypeAll(componentType)
            .OfType<Component>().FirstOrDefault(component => component.gameObject.scene.IsValid());

        private static void AssertGoals(string text, string taskId, string[] expected)
        {
            Assert.That(text, Does.StartWith(taskId));
            foreach (var goal in expected) Assert.That(text, Does.Contain(goal));
        }

        private static void AssertTrackerGoals(object tracker, string[] expected)
        {
            var goals = ((IEnumerable)tracker.GetType().GetProperty("Goals")!.GetValue(tracker)).Cast<object>().ToArray();
            Assert.That(goals, Has.Length.EqualTo(expected.Length));
            Assert.That(goals.Select(x => ReadField<string>(x, "goalText")), Is.EqualTo(expected));
        }

        private static T ReadField<T>(object target, string name) =>
            (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public)!.GetValue(target);
    }
}
