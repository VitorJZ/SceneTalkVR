using NUnit.Framework;
using SceneTalkVR.Core;

namespace SceneTalkVR.Tests.Editor
{
    public sealed class FormalConditionResolverTests
    {
        [TestCase(FormalConditionCode.NE, FeedbackProvider.DialogueAvatar, FeedbackStyle.Explicit)]
        [TestCase(FormalConditionCode.NR, FeedbackProvider.DialogueAvatar, FeedbackStyle.Recast)]
        [TestCase(FormalConditionCode.SE, FeedbackProvider.AssistantAgent, FeedbackStyle.Explicit)]
        [TestCase(FormalConditionCode.SR, FeedbackProvider.AssistantAgent, FeedbackStyle.Recast)]
        public void FormalCode_AlwaysResolvesToOneProviderAndStyle(FormalConditionCode code, FeedbackProvider provider, FeedbackStyle style)
        {
            Assert.That(FormalConditionResolver.TryResolve(code, out var actualProvider, out var actualStyle), Is.True);
            Assert.That(actualProvider, Is.EqualTo(provider));
            Assert.That(actualStyle, Is.EqualTo(style));
        }
    }
}
