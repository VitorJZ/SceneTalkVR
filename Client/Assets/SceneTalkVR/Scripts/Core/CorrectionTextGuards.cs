using System;

namespace SceneTalkVR.Core
{
    public static class CorrectionTextGuards
    {
        private static readonly string[] CorrectionLeakagePatterns =
        {
            "you should say",
            "should say",
            "you can say",
            "try saying",
            "a better way",
            "better way",
            "correct sentence",
            "correct expression",
            "grammar",
            "grammatical",
            "mistake",
            "wrong",
            "incorrect",
            "instead of",
            "not ",
            "the right way",
            "proper way",
            "actually, you",
            "more natural"
        };

        private static readonly string[] RecastForbiddenTerms =
        {
            "wrong",
            "mistake",
            "incorrect",
            "correct",
            "grammar",
            "grammatical",
            "should",
            "should say",
            "you should",
            "you can say",
            "try saying",
            "better way",
            "a more natural way",
            "instead",
            "not",
            "rather than",
            "you mean",
            "I mean",
            "the right way",
            "properly",
            "proper way",
            "actually, you"
        };

        public static bool LooksLikeCorrection(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var lower = text.ToLowerInvariant();
            foreach (var pattern in CorrectionLeakagePatterns)
            {
                if (lower.Contains(pattern))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool ViolatesRecastPurity(string feedbackText)
        {
            if (string.IsNullOrWhiteSpace(feedbackText))
            {
                return false;
            }

            var lower = feedbackText.ToLowerInvariant();
            foreach (var term in RecastForbiddenTerms)
            {
                if (lower.Contains(term))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
