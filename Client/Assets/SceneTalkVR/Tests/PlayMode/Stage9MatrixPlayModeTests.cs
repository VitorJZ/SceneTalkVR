using System.Collections;
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace SceneTalkVR.Tests.PlayMode
{
    public sealed class Stage9MatrixPlayModeTests
    {
        [UnityTest]
        public IEnumerator MatrixDefinitions_AreRuntimeReadableAndCollectionSafe()
        {
            var type = Type.GetType("SceneTalkVR.Core.ExperimentMatrixDefinition, Assembly-CSharp");
            Assert.That(type, Is.Not.Null);
            var formal = type.GetMethod("Formal", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
            var pilot = type.GetMethod("Pilot", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
            var cases = type.GetField("cases", BindingFlags.Public | BindingFlags.Instance);
            Assert.That(((Array)cases.GetValue(formal)).Length, Is.EqualTo(16));
            Assert.That(((Array)cases.GetValue(pilot)).Length, Is.EqualTo(9));
            yield return null;
        }
    }
}
