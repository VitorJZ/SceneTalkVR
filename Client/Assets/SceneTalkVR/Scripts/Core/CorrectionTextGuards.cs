using System;

namespace SceneTalkVR.Core
{
    public static class CorrectionTextGuards
    {
        // Keep these correction-specific: ordinary roleplay language such as
        // "this mistake is on us" or "the wrong dish" is valid dialogue.
        private static readonly string[] ExplicitCorrectionLeakagePatterns =
        {
            "you should say",
            "should say",
            "try saying",
            "a better way to say",
            "a better way to phrase",
            "better way to say",
            "better way to phrase",
            "correct sentence",
            "correct expression",
            "grammar",
            "grammatical",
            "instead of saying",
            "instead of using",
            "the right way to say",
            "the right way to phrase",
            "proper way to say",
            "proper way to phrase",
            "more natural to say",
            "more natural to phrase"
        };

        private static readonly string[] LinguisticSubjectPatterns =
        {
            "sentence",
            "phrase",
            "expression",
            "word choice",
            "verb",
            "tense",
            "pronunciation"
        };

        private static readonly string[] CorrectionJudgmentPatterns =
        {
            "mistake",
            "wrong",
            "incorrect",
            "correct",
            "better",
            "natural",
            "proper"
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
            if (ContainsAny(lower, ExplicitCorrectionLeakagePatterns))
            {
                return true;
            }

            return ContainsAny(lower, LinguisticSubjectPatterns)
                && ContainsAny(lower, CorrectionJudgmentPatterns);
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

        private static bool ContainsAny(string value, string[] patterns)
        {
            foreach (var pattern in patterns)
            {
                if (value.Contains(pattern))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
