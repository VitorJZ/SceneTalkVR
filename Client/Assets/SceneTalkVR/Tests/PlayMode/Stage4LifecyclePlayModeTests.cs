using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SceneTalkVR.Tests.PlayMode
{
    public sealed class Stage4LifecyclePlayModeTests
    {
        [UnityTest]
        public IEnumerator SampleScene_HasLifecycleAndReadOnlyGoalPanel()
        {
            if (!string.Equals(SceneManager.GetActiveScene().name, "SampleScene", StringComparison.Ordinal))
                SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
            yield return null; yield return null;
            var lifecycleType = Type.GetType("SceneTalkVR.Core.ExperimentLifecycleCoordinator, Assembly-CSharp");
            Assert.That(lifecycleType, Is.Not.Null);
            Assert.That(Resources.FindObjectsOfTypeAll(lifecycleType).Length, Is.EqualTo(1));
            var panel = Resources.FindObjectsOfTypeAll<Transform>().FirstOrDefault(x => x.name == "ReadOnlyTaskGoalPanel");
            Assert.That(panel, Is.Not.Null, "The participant task panel must exist and contain no goal mutation buttons.");
            Assert.That(panel.GetComponentsInChildren<UnityEngine.UI.Button>(true), Is.Empty);
        }
    }
}
