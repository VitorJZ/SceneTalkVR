using System;
using System.Reflection;
using NUnit.Framework;
using SceneTalkVR.Core;
using SceneTalkVR.Runtime.Services;
using UnityEngine;

namespace SceneTalkVR.AvatarSystem.Tests.Editor
{
    public sealed class CorrectionPolicyTests
    {
        [TestCase("Hello.", "hello")]
        [TestCase("I'd like a table.", "id like a table")]
        [TestCase("check-out", "check out")]
        [TestCase("“I’d   like—a well-made desk.”", "id like a well made desk")]
        public void OralPracticeNormalization_IgnoresNonAudibleWritingDifferences(
            string left,
            string right)
        {
            Assert.That(
                CorrectionPolicyEvaluator.AreEquivalentForOralPractice(left, right),
                Is.True);
        }

        [TestCase("I want reserve a table", "I want to reserve a table")]
        [TestCase("How much cost the plan", "How much does the plan cost")]
        public void OralPracticeNormalization_PreservesRealLanguageDifferences(
            string left,
            string right)
        {
            Assert.That(
                CorrectionPolicyEvaluator.AreEquivalentForOralPractice(left, right),
                Is.False);
        }

        [Test]
        public void NonAudibleDifferenceFilter_ClearsFeedbackAndRecordsRationale()
        {
            var feedback = Feedback(
                original: "Hello",
                corrected: "hello.",
                provider: "assistant_agent",
                style: "recast");
            feedback.rationaleTag = "model_choice";

            var suppressed = CorrectionPolicyEvaluator.ApplyNonAudibleDifferenceFilter(
                new CorrectionPolicySettings(),
                "HELLO!",
                feedback);

            Assert.That(suppressed, Is.True);
            Assert.That(feedback.hasFeedback, Is.False);
            Assert.That(feedback.errorType, Is.EqualTo("none"));
            Assert.That(feedback.originalText, Is.Empty);
            Assert.That(feedback.correctedText, Is.Empty);
            Assert.That(feedback.feedbackText, Is.Empty);
            Assert.That(feedback.recastText, Is.Empty);
            Assert.That(feedback.targetSpan, Is.Empty);
            Assert.That(feedback.provider, Is.EqualTo("assistant_agent"));
            Assert.That(feedback.style, Is.EqualTo("recast"));
            Assert.That(
                feedback.rationaleTag,
                Is.EqualTo("model_choice;non_audible_text_difference_suppressed"));
        }

        [Test]
        public void NonAudibleDifferenceFilter_DoesNotSuppressGenuineGrammarCorrection()
        {
            var feedback = Feedback(
                original: "I want reserve a table",
                corrected: "I want to reserve a table",
                provider: "dialogue_avatar",
                style: "explicit");

            var suppressed = CorrectionPolicyEvaluator.ApplyNonAudibleDifferenceFilter(
                new CorrectionPolicySettings(),
                feedback.originalText,
                feedback);

            Assert.That(suppressed, Is.False);
            Assert.That(feedback.hasFeedback, Is.True);
            Assert.That(feedback.correctedText, Is.EqualTo("I want to reserve a table"));
        }

        [Test]
        public void RealLlmFinalizesConditionAndFilterBeforeReturningCorrection()
        {
            var host = new GameObject("Correction Policy Finalization Test");
            try
            {
                var service = host.AddComponent<RealLLMService>();
                service.SetExperimentCondition(new CorrectionExperimentCondition
                {
                    provider = ExperimentConditionManager.AssistantAgentProvider,
                    style = ExperimentConditionManager.RecastStyle
                });
                var feedback = Feedback(
                    original: "When is check-out?",
                    corrected: "when is check out",
                    provider: string.Empty,
                    style: string.Empty);

                var finalized = (CorrectionFeedbackData)InvokePrivate(
                    service,
                    "FinalizeCorrectionFeedback",
                    "WHEN IS CHECK-OUT",
                    feedback);

                Assert.That(finalized.provider, Is.EqualTo("assistant_agent"));
                Assert.That(finalized.style, Is.EqualTo("recast"));
                Assert.That(finalized.hasFeedback, Is.False);
                Assert.That(
                    finalized.rationaleTag,
                    Does.Contain(CorrectionPolicyEvaluator.NonAudibleDifferenceSuppressionTag));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void CorrectionPrompt_UsesSpeechOnlyAsrSafeRules()
        {
            var host = new GameObject("Correction Policy Prompt Test");
            try
            {
                var service = host.AddComponent<RealLLMService>();
                service.SetExperimentCondition(new CorrectionExperimentCondition
                {
                    provider = ExperimentConditionManager.DialogueAvatarProvider,
                    style = ExperimentConditionManager.ExplicitStyle
                });

                var prompt = (string)InvokePrivate(service, "BuildCorrectionSystemPrompt");

                Assert.That(prompt, Does.Contain("Ignore capitalization, punctuation, whitespace, apostrophes, hyphens"));
                Assert.That(prompt, Does.Contain("Do NOT evaluate pronunciation from transcript text"));
                Assert.That(prompt, Does.Contain("homophone, personal name, proper noun"));
                Assert.That(prompt, Does.Contain("Accept natural conversational ellipsis and concise service phrases"));
                Assert.That(prompt, Does.Contain("when is check-out"));
                Assert.That(
                    prompt,
                    Does.Not.Contain("grammar, vocabulary, pronunciation, or expression errors"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void CorrectionPrompt_ModerateIncludesClearUnnaturalRequestDetection()
        {
            var host = new GameObject("Correction Detection Prompt Test");
            try
            {
                var service = host.AddComponent<RealLLMService>();
                service.SetExperimentCondition(new CorrectionExperimentCondition
                {
                    provider = ExperimentConditionManager.AssistantAgentProvider,
                    style = ExperimentConditionManager.ExplicitStyle
                });

                var prompt = (string)InvokePrivate(service, "BuildCorrectionSystemPrompt");

                Assert.That(prompt, Does.Contain("at most ONE clear language error"));
                Assert.That(prompt, Does.Contain("clearly unnatural request or question constructions"));
                Assert.That(prompt, Does.Contain("I'm asking for you to replace my dish."));
                Assert.That(prompt, Does.Contain("Giving me some recommendations."));
                Assert.That(prompt, Does.Contain("How long the replacement will be?"));
                Assert.That(prompt, Does.Contain("Can you replace my dish?' is already natural"));
                Assert.That(prompt, Does.Not.Contain("at most ONE major error"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ExplicitCorrectionPrompt_StartsDirectlyWithRule()
        {
            var host = new GameObject("Correction Prompt Prefix Test");
            try
            {
                var service = host.AddComponent<RealLLMService>();
                service.SetExperimentCondition(new CorrectionExperimentCondition
                {
                    provider = ExperimentConditionManager.AssistantAgentProvider,
                    style = ExperimentConditionManager.ExplicitStyle
                });

                var prompt = (string)InvokePrivate(service, "BuildCorrectionSystemPrompt");

                Assert.That(prompt, Does.Contain("Start directly with the correction rule"));
                Assert.That(prompt, Does.Contain("Do NOT add a heading or label"));
                Assert.That(prompt, Does.Not.Contain("'Grammar tip: [one short rule]"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [TestCase("Grammar tip: Use 'are' with 'you'.", "Use 'are' with 'you'.")]
        [TestCase("grammar tips：Try: \"I am ready.\"", "Try: \"I am ready.\"")]
        [TestCase("Grammar tip — Use the past tense.", "Use the past tense.")]
        [TestCase("Grammar tip, Use the past tense.", "Use the past tense.")]
        [TestCase("Use the past tense.", "Use the past tense.")]
        [TestCase("Grammar tips are useful.", "Grammar tips are useful.")]
        public void GrammarTipPrefixGuard_RemovesOnlyLeadingLabel(string input, string expected)
        {
            Assert.That(
                CorrectionTextGuards.RemoveGrammarTipPrefix(input),
                Is.EqualTo(expected));
        }

        [Test]
        public void RealLlmFinalization_RemovesLegacyGrammarTipPrefix()
        {
            var host = new GameObject("Correction Prefix Finalization Test");
            try
            {
                var service = host.AddComponent<RealLLMService>();
                service.SetExperimentCondition(new CorrectionExperimentCondition
                {
                    provider = ExperimentConditionManager.AssistantAgentProvider,
                    style = ExperimentConditionManager.ExplicitStyle
                });
                var feedback = Feedback(
                    original: "I is ready",
                    corrected: "I am ready",
                    provider: string.Empty,
                    style: string.Empty);
                feedback.feedbackText = "Grammar tip: Use 'am' with 'I'. Try: \"I am ready.\"";

                var finalized = (CorrectionFeedbackData)InvokePrivate(
                    service,
                    "FinalizeCorrectionFeedback",
                    feedback.originalText,
                    feedback);

                Assert.That(
                    finalized.feedbackText,
                    Is.EqualTo("Use 'am' with 'I'. Try: \"I am ready.\""));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void CorrectionPresenter_RemovesLegacyGrammarTipPrefixBeforeTts()
        {
            var feedback = new CorrectionFeedbackData
            {
                hasFeedback = true,
                style = ExperimentConditionManager.ExplicitStyle,
                correctedText = "I am ready.",
                feedbackText = "Grammar tips: Use 'am' with 'I'. Try: \"I am ready.\""
            };

            Assert.That(
                CorrectionFeedbackPresenter.ResolveFeedbackText(feedback),
                Is.EqualTo("Use 'am' with 'I'. Try: \"I am ready.\""));
        }

        [TestCase(ExperimentConditionManager.ExplicitStyle)]
        [TestCase(ExperimentConditionManager.RecastStyle)]
        public void RealLlmFinalization_RepairsMissingSpokenCorrectionText(string style)
        {
            var host = new GameObject("Correction Spoken Text Repair Test");
            try
            {
                var service = host.AddComponent<RealLLMService>();
                service.SetExperimentCondition(new CorrectionExperimentCondition
                {
                    provider = ExperimentConditionManager.AssistantAgentProvider,
                    style = style
                });
                var feedback = Feedback(
                    original: "I want reserve a table",
                    corrected: "I want to reserve a table",
                    provider: string.Empty,
                    style: string.Empty);
                feedback.feedbackText = string.Empty;
                feedback.recastText = string.Empty;

                var finalized = (CorrectionFeedbackData)InvokePrivate(
                    service,
                    "FinalizeCorrectionFeedback",
                    feedback.originalText,
                    feedback);

                Assert.That(finalized.hasFeedback, Is.True);
                Assert.That(finalized.feedbackText, Is.Not.Empty);
                Assert.That(finalized.feedbackText, Does.Not.StartWith("Grammar tip"));
                if (style == ExperimentConditionManager.RecastStyle)
                {
                    Assert.That(finalized.recastText, Is.Not.Empty);
                }
                Assert.That(finalized.rationaleTag, Does.Contain("missing_spoken_feedback_repaired"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void SttSuppression_UsesExistingThresholdsAndRequiresAvailableConfidence()
        {
            var host = new GameObject("Correction Policy STT Threshold Test");
            try
            {
                var service = host.AddComponent<RealLLMService>();

                SetField(service, "lastRecordingDurationMs", 499f);
                SetField(service, "lastSttConfidence", 1f);
                SetField(service, "lastSttConfidenceAvailable", false);
                var shortResult = InvokeSuppression(service, out var shortReason);
                Assert.That(shortResult, Is.True);
                Assert.That(shortReason, Is.EqualTo("short_recording_suppressed"));

                SetField(service, "lastRecordingDurationMs", 1000f);
                SetField(service, "lastSttConfidence", 0.1f);
                SetField(service, "lastSttConfidenceAvailable", false);
                var unavailableResult = InvokeSuppression(service, out var unavailableReason);
                Assert.That(unavailableResult, Is.False);
                Assert.That(unavailableReason, Is.Empty);

                SetField(service, "lastRecordingDurationMs", 500f);
                SetField(service, "lastSttConfidence", 0.5f);
                SetField(service, "lastSttConfidenceAvailable", true);
                var boundaryResult = InvokeSuppression(service, out var boundaryReason);
                Assert.That(boundaryResult, Is.False);
                Assert.That(boundaryReason, Is.Empty);

                SetField(service, "lastSttConfidence", 0.1f);
                SetField(service, "lastSttConfidenceAvailable", true);
                var lowResult = InvokeSuppression(service, out var lowReason);
                Assert.That(lowResult, Is.True);
                Assert.That(lowReason, Is.EqualTo("low_confidence_suppressed"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        private static CorrectionFeedbackData Feedback(
            string original,
            string corrected,
            string provider,
            string style)
        {
            return new CorrectionFeedbackData
            {
                hasFeedback = true,
                provider = provider,
                style = style,
                errorType = "grammar",
                originalText = original,
                correctedText = corrected,
                feedbackText = "Grammar tip.",
                recastText = corrected,
                targetSpan = original,
                confidence = 1f
            };
        }

        private static bool InvokeSuppression(RealLLMService service, out string reason)
        {
            var arguments = new object[] { null };
            var result = (bool)InvokePrivate(service, "ShouldSuppressCorrectionByStt", arguments);
            reason = arguments[0] as string ?? string.Empty;
            return result;
        }

        private static object InvokePrivate(object target, string methodName, params object[] arguments)
        {
            var method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"Missing private method {methodName}.");
            return method.Invoke(target, arguments);
        }

        private static void SetField<T>(object target, string fieldName, T value)
        {
            var field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing private field {fieldName}.");
            field.SetValue(target, value);
        }
    }
}
