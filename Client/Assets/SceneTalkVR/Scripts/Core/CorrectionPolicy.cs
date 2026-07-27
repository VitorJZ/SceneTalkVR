using System;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace SceneTalkVR.Core
{
    public enum CorrectionPolicyMode
    {
        OralPracticeSafeV1 = 0
    }

    [Serializable]
    public sealed class CorrectionPolicySettings
    {
        [SerializeField] private CorrectionPolicyMode mode = CorrectionPolicyMode.OralPracticeSafeV1;
        [SerializeField, Min(0)] private int shortRecordingThresholdMs = 500;
        [SerializeField, Range(0f, 1f)] private float lowSttConfidenceThreshold = 0.5f;

        public CorrectionPolicyMode Mode => mode == CorrectionPolicyMode.OralPracticeSafeV1
            ? mode
            : CorrectionPolicyMode.OralPracticeSafeV1;
        public int ShortRecordingThresholdMs => Mathf.Max(0, shortRecordingThresholdMs);
        public float LowSttConfidenceThreshold => Mathf.Clamp01(lowSttConfidenceThreshold);

        public static CorrectionPolicySettings CloneNormalized(CorrectionPolicySettings source)
        {
            return new CorrectionPolicySettings
            {
                mode = CorrectionPolicyMode.OralPracticeSafeV1,
                shortRecordingThresholdMs = source == null
                    ? 500
                    : Mathf.Max(0, source.shortRecordingThresholdMs),
                lowSttConfidenceThreshold = source == null
                    ? 0.5f
                    : Mathf.Clamp01(source.lowSttConfidenceThreshold)
            };
        }
    }

    public static class CorrectionPolicyEvaluator
    {
        public const string NonAudibleDifferenceSuppressionTag =
            "non_audible_text_difference_suppressed";

        public static string NormalizeForCorrectionComparison(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var input = value.Normalize(NormalizationForm.FormKC).ToLowerInvariant();
            var builder = new StringBuilder(input.Length);
            var pendingSpace = false;

            for (var index = 0; index < input.Length; index++)
            {
                var codePoint = char.ConvertToUtf32(input, index);
                var category = char.GetUnicodeCategory(input, index);
                var scalar = char.ConvertFromUtf32(codePoint);
                if (codePoint > char.MaxValue)
                {
                    index++;
                }

                if (IsApostrophe(codePoint))
                {
                    continue;
                }

                if (char.IsWhiteSpace(scalar, 0) || IsPunctuation(category))
                {
                    pendingSpace = builder.Length > 0;
                    continue;
                }

                if (pendingSpace)
                {
                    builder.Append(' ');
                    pendingSpace = false;
                }

                builder.Append(scalar);
            }

            return builder.ToString();
        }

        public static bool AreEquivalentForOralPractice(string left, string right)
        {
            return string.Equals(
                NormalizeForCorrectionComparison(left),
                NormalizeForCorrectionComparison(right),
                StringComparison.Ordinal);
        }

        public static bool ApplyNonAudibleDifferenceFilter(
            CorrectionPolicySettings settings,
            string transcript,
            CorrectionFeedbackData feedback)
        {
            if (feedback == null || !feedback.hasFeedback || string.IsNullOrWhiteSpace(feedback.correctedText))
            {
                return false;
            }

            var effectiveSettings = CorrectionPolicySettings.CloneNormalized(settings);
            if (effectiveSettings.Mode != CorrectionPolicyMode.OralPracticeSafeV1)
            {
                return false;
            }

            var transcriptMatches = !string.IsNullOrWhiteSpace(transcript)
                                    && AreEquivalentForOralPractice(transcript, feedback.correctedText);
            var originalMatches = !string.IsNullOrWhiteSpace(feedback.originalText)
                                  && AreEquivalentForOralPractice(feedback.originalText, feedback.correctedText);
            if (!transcriptMatches && !originalMatches)
            {
                return false;
            }

            feedback.hasFeedback = false;
            feedback.errorType = "none";
            feedback.originalText = string.Empty;
            feedback.correctedText = string.Empty;
            feedback.feedbackText = string.Empty;
            feedback.recastText = string.Empty;
            feedback.targetSpan = string.Empty;
            feedback.rationaleTag = AppendRationale(
                feedback.rationaleTag,
                NonAudibleDifferenceSuppressionTag);
            return true;
        }

        private static bool IsApostrophe(int codePoint)
        {
            return codePoint == '\''
                   || codePoint == 0x2018
                   || codePoint == 0x2019
                   || codePoint == 0x02BC
                   || codePoint == 0xFF07;
        }

        private static bool IsPunctuation(UnicodeCategory category)
        {
            return category == UnicodeCategory.ConnectorPunctuation
                   || category == UnicodeCategory.DashPunctuation
                   || category == UnicodeCategory.OpenPunctuation
                   || category == UnicodeCategory.ClosePunctuation
                   || category == UnicodeCategory.InitialQuotePunctuation
                   || category == UnicodeCategory.FinalQuotePunctuation
                   || category == UnicodeCategory.OtherPunctuation;
        }

        private static string AppendRationale(string current, string next)
        {
            if (string.IsNullOrWhiteSpace(current))
            {
                return next;
            }

            return current.IndexOf(next, StringComparison.Ordinal) >= 0
                ? current
                : $"{current};{next}";
        }
    }
}
