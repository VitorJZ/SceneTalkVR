using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace SceneTalkVR.Tests.PlayMode
{
    public sealed class Stage3FeedbackFirstPlayModeTests
    {
        [UnityTest]
        public IEnumerator PreparedDialogue_RemainsBlockedUntilFeedbackCompletes_AndResetClearsGate()
        {
            var type = Type.GetType("SceneTalkVR.Core.FeedbackFirstPlaybackGate, Assembly-CSharp");
            Assert.That(type, Is.Not.Null);
            var gate = Activator.CreateInstance(type);
            type.GetMethod("PlannerResolved", BindingFlags.Instance | BindingFlags.Public)?.Invoke(gate, new object[] { true });
            Assert.That((bool)type.GetProperty("IsDialogueGateOpen")?.GetValue(gate), Is.False);
            yield return null; // Represents dialogue TTS becoming ready while the playback gate is closed.
            type.GetMethod("FeedbackStarted")?.Invoke(gate, null);
            type.GetMethod("FeedbackEnded")?.Invoke(gate, null);
            Assert.That((bool)type.GetProperty("IsDialogueGateOpen")?.GetValue(gate), Is.True);
            Assert.That((int)type.GetProperty("GateOpenCount")?.GetValue(gate), Is.EqualTo(1));
            type.GetMethod("Reset")?.Invoke(gate, null);
            Assert.That((bool)type.GetProperty("IsDialogueGateOpen")?.GetValue(gate), Is.False);
        }
    }
}
