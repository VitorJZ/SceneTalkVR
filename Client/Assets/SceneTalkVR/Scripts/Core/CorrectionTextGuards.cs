using System;

namespace SceneTalkVR.Core
{
    public static class CorrectionTextGuards
    {
        // Keep these correction-specific: ordinary roleplay negation such as "not allowed" is valid dialogue.
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

        public static string RemoveGrammarTipPrefix(string feedbackText)
        {
            if (string.IsNullOrWhiteSpace(feedbackText))
            {
                return string.Empty;
            }

            var trimmed = feedbackText.Trim();
            var prefixLength = ResolveGrammarTipPrefixLength(trimmed, "Grammar tips");
            if (prefixLength == 0)
            {
                prefixLength = ResolveGrammarTipPrefixLength(trimmed, "Grammar tip");
            }

            return prefixLength == 0
                ? trimmed
                : trimmed.Substring(prefixLength).TrimStart();
        }

        private static int ResolveGrammarTipPrefixLength(string text, string prefix)
        {
            if (!text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            if (text.Length == prefix.Length)
            {
                return prefix.Length;
            }

            var delimiter = text[prefix.Length];
            if (delimiter == ':'
                || delimiter == '：'
                || delimiter == '-'
                || delimiter == '–'
                || delimiter == '—'
                || delimiter == ','
                || delimiter == '，'
                || delimiter == '.'
                || delimiter == '。')
            {
                return prefix.Length + 1;
            }

            return 0;
        }
    }
}
