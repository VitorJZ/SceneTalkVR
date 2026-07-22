using System;
using System.Collections.Generic;

namespace SceneTalkVR.Core
{
    public static class ValidatedRehearsalGoalDetector
    {
        public const string DetectorId = "formal_rehearsal_rule_detector_v1";

        public static int Evaluate(ExperimentLifecycleCoordinator lifecycle, string turnId, string transcript)
            => GoalEvaluationOrchestrator.EvaluateUserTranscript(lifecycle, turnId, transcript);

        public static IReadOnlyList<int> Match(string taskId, string normalizedTranscript)
        {
            var text = Normalize(normalizedTranscript); var result = new List<int>();
            switch (taskId)
            {
                case "hotel_check_in":
                    Add(result, 0, HasAny(text, "my name is", "reservation under", "reservation is under", "booking under", "booked under"));
                    Add(result, 1, Has(text, "breakfast") && HasAny(text, "included", "include", "comes with"));
                    Add(result, 2, HasAny(text, "high floor", "higher floor", "upper floor"));
                    Add(result, 3, HasAny(text, "check out time", "checkout time", "what time is checkout", "what time do i check out", "when is check out", "when is checkout"));
                    break;
                case "furniture_shopping":
                    Add(result, 0, HasAny(text, "centimeter", "centimetre", "meter wide", "metre wide", "inches wide", "desk size", "dimensions"));
                    Add(result, 1, HasAny(text, "what material", "which material", "materials available", "made of", "wood or", "metal or"));
                    Add(result, 2, HasAny(text, "my budget", "maximum budget", "max budget", "spend up to", "price limit"));
                    Add(result, 3, HasAny(text, "home delivery", "deliver to my home", "deliver it", "delivery available"));
                    break;
                case "gym_membership":
                    Add(result, 0, HasAny(text, "my fitness goal", "my goal is", "want to lose weight", "want to build muscle", "get fitter", "improve my fitness"));
                    Add(result, 1, Has(text, "month") && HasAny(text, "price", "cost", "membership", "how much"));
                    Add(result, 2, HasAny(text, "workout plan", "training plan", "exercise plan", "routine do you recommend", "plan do you recommend"));
                    Add(result, 3, Has(text, "trial") && HasAny(text, "free", "complimentary", "no charge"));
                    break;
                case "tourist_assistance":
                    Add(result, 0, Has(text, "museum") && HasAny(text, "how do i get", "how can i get", "directions", "way to", "reach"));
                    Add(result, 1, HasAny(text, "need a ticket", "ticket required", "buy a ticket", "admission ticket"));
                    Add(result, 2, HasAny(text, "take photos", "take pictures", "photography") && HasAny(text, "inside", "indoor", "allowed", "can i"));
                    Add(result, 3, HasAny(text, "another attraction", "nearby attraction", "other place to visit", "recommend nearby", "else should i visit"));
                    break;
            }
            return result;
        }

        private static string Normalize(string value)
        {
            var text = (value ?? string.Empty).Trim().ToLowerInvariant().Replace("-", " ");
            while (text.Contains("  ")) text = text.Replace("  ", " ");
            return text;
        }
        private static bool Has(string text, string token) => text.IndexOf(token, StringComparison.Ordinal) >= 0;
        private static bool HasAny(string text, params string[] tokens)
        { foreach (var token in tokens) if (Has(text, token)) return true; return false; }
        private static void Add(List<int> values, int index, bool matched) { if (matched) values.Add(index); }
    }
}
