using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
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
            "Request a room on a higher floor.",
            "Ask about the check-out time."
        };

        [UnityTest]
        public IEnumerator HomeShowsIndependentPilotAndFormalEntryPoints()
        {
            SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
            yield return null;
            yield return null;

            Assert.That(FindActiveButton("New Experiment"), Is.Null);
            Assert.That(FindActiveButton("Gym Membership"), Is.Null);
            var prompt = FindTransform("SessionNotPreparedPanel");
            Assert.That(prompt == null || !prompt.gameObject.activeInHierarchy, Is.True);
            var pilot = FindActiveButton("Pilot Experiment");
            var formal = FindActiveButton("Formal Experiment");
            Assert.That(pilot, Is.Not.Null);
            Assert.That(formal, Is.Not.Null);
            Assert.That(pilot.interactable, Is.True);
            Assert.That(formal.interactable, Is.True);
        }

        [UnityTest]
        public IEnumerator FormalProtocol_HasElevenConfirmedDecisions()
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
            Assert.That(decisions.Count(x => ReadField<object>(x, "status").ToString() == "Confirmed"), Is.EqualTo(11));

            var args = new object[] { null };
            var valid = (bool)protocol.GetType().GetMethod("ValidateForFormalMode")!.Invoke(protocol, args);
            Assert.That(valid, Is.True, args[0] as string);
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
                    && x.GetComponentInChildren<TMP_Text>(true)?.text == label);
            Assert.That(button, Is.Not.Null, $"Active button '{label}' was not found.");
            button.onClick.Invoke();
        }

        private static Button FindActiveButton(string label) => Resources.FindObjectsOfTypeAll<Button>()
            .FirstOrDefault(x => x.gameObject.scene.IsValid() && x.gameObject.activeInHierarchy
                && x.GetComponentInChildren<TMP_Text>(true)?.text == label);

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
