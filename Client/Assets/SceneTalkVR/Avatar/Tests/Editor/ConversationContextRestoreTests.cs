using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using SceneTalkVR.Core;
using SceneTalkVR.History;
using SceneTalkVR.Runtime.Services;
using UnityEngine;

namespace SceneTalkVR.AvatarSystem.Tests.Editor
{
    public sealed class ConversationContextRestoreTests
    {
        [Test]
        public void RealLlmRestoresDialogueMessagesConditionAndErrorHistory()
        {
            var host = new GameObject("LLM History Restore Test");
            try
            {
                var service = host.AddComponent<RealLLMService>();
                var detail = CreateDetail();

                service.RestoreConversationContext(detail);

                var messages = GetPrivateList(service, "chatHistory");
                var errors = GetPrivateList(service, "sessionErrorHistory");
                Assert.That(messages.Count, Is.EqualTo(4), "system + opening + user + assistant");
                Assert.That(errors.Count, Is.EqualTo(1));
                Assert.That(service.CurrentCondition.sessionId, Is.EqualTo("resume-session"));
                Assert.That(service.CurrentCondition.provider, Is.EqualTo("dialogue_avatar"));
                Assert.That(service.FeedbackSensitivity, Is.EqualTo("active"));
                Assert.That(service.NonGoalQuestionsAsked, Is.EqualTo(1));
                Assert.That(GetField<HashSet<string>>(service, "askedNonGoalQuestionIds"),
                    Does.Contain("hotel_journey_comfort"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void AvatarDialoguePrompt_HidesGoalsAndUsesAtMostOneConfiguredQuestion()
        {
            var host = new GameObject("Avatar Dialogue Pacing Test");
            try
            {
                var service = host.AddComponent<RealLLMService>();
                service.ConfigureDialoguePacing(1f, 1);
                Assert.That(service.MaxNonGoalQuestionsPerTask, Is.EqualTo(1));
                service.SetExperimentCondition(PacingCondition("assistant_agent", "explicit"));

                var firstDecision = InvokePrivate(service, "CreateAvatarDialoguePacingDecision", "Hello there");
                var selectedQuestion = GetField<NonGoalQuestionDefinition>(firstDecision, "question");
                var prompt = (string)InvokePrivate(service, "BuildAvatarDialogueSystemPrompt", firstDecision, null);
                Assert.That(prompt, Does.Contain("pacingTriggered: true"));
                Assert.That(prompt, Does.Contain(selectedQuestion.text));
                Assert.That(prompt, Does.Contain(
                    "Never invent, assume, or confirm any specific task detail"));
                Assert.That(PacingCondition("assistant_agent", "explicit").task.nonGoalQuestions
                    .Where(question => question.questionId != selectedQuestion.questionId)
                    .All(question => !prompt.Contains(question.text)), Is.True);
                Assert.That(prompt, Does.Not.Contain("secret reservation goal"));
                Assert.That(prompt, Does.Not.Contain("hotel_check_in"));

                var correctionPrompt = (string)InvokePrivate(service, "BuildCorrectionSystemPrompt");
                Assert.That(correctionPrompt, Does.Contain("A hotel arrival conversation."));
                Assert.That(correctionPrompt, Does.Not.Contain("secret reservation goal"));

                var payload = new SpringScenePayload { dialogueReply = "Welcome." };
                InvokePrivate(service, "CommitAvatarDialoguePacing", payload, firstDecision);
                Assert.That(payload.dialoguePacing.triggered, Is.True);
                Assert.That(service.NonGoalQuestionsAsked, Is.EqualTo(1));

                service.SetExperimentCondition(PacingCondition("dialogue_avatar", "recast"));
                Assert.That(service.NonGoalQuestionsAsked, Is.EqualTo(1));
                var secondDecision = InvokePrivate(service, "CreateAvatarDialoguePacingDecision", "Another line");
                var secondData = GetField<AvatarDialoguePacingData>(secondDecision, "data");
                Assert.That(secondData.triggered, Is.False);
                var secondPrompt = (string)InvokePrivate(
                    service,
                    "BuildAvatarDialogueSystemPrompt",
                    secondDecision,
                    null);
                Assert.That(secondPrompt, Does.Contain("Do not ask any question in this turn."));
                Assert.That(secondPrompt, Does.Contain(
                    "Do not claim that any transaction or task step has occurred."));
                Assert.That(secondPrompt, Does.Not.Contain(selectedQuestion.text));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void AvatarDialoguePacing_CapsAtQuestionBankAndNeverRepeatsAQuestion()
        {
            var host = new GameObject("Pacing Unique Question Bank Test");
            try
            {
                var service = host.AddComponent<RealLLMService>();
                service.ConfigureDialoguePacing(1f, 99);
                service.SetExperimentCondition(PacingCondition("dialogue_avatar", "explicit"));
                Assert.That(service.MaxNonGoalQuestionsPerTask, Is.EqualTo(99),
                    "The configured value is not hard-capped; the task bank supplies the effective cap.");

                var selectedIds = new HashSet<string>(StringComparer.Ordinal);
                for (var turn = 0; turn < 2; turn++)
                {
                    var decision = InvokePrivate(
                        service,
                        "CreateAvatarDialoguePacingDecision",
                        $"Unique turn {turn}");
                    var data = GetField<AvatarDialoguePacingData>(decision, "data");
                    Assert.That(data.triggered, Is.True);
                    Assert.That(selectedIds.Add(data.questionId), Is.True, "A question was selected twice.");
                    InvokePrivate(service, "CommitAvatarDialoguePacing", new SpringScenePayload(), decision);
                }

                var exhausted = InvokePrivate(
                    service,
                    "CreateAvatarDialoguePacingDecision",
                    "The bank is exhausted");
                Assert.That(GetField<AvatarDialoguePacingData>(exhausted, "data").triggered, Is.False);
                Assert.That(service.NonGoalQuestionsAsked, Is.EqualTo(2));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void SelectedQuestion_IsUniqueFinalQuestionAndOtherQuestionsAreRemoved()
        {
            var host = new GameObject("Pacing Output Sanitization");
            try
            {
                var service = host.AddComponent<RealLLMService>();
                service.ConfigureDialoguePacing(1f, 1);
                service.SetExperimentCondition(PacingCondition("dialogue_avatar", "explicit"));
                var decision = InvokePrivate(service, "CreateAvatarDialoguePacingDecision", "Hello there");
                var question = GetField<NonGoalQuestionDefinition>(decision, "question").text;
                var payload = new SpringScenePayload
                {
                    dialogueReply = $"Would you like anything else? First response. Second response. Third response. {question} {question}"
                };

                InvokePrivate(service, "EnsureSelectedQuestionAtEnd", payload, decision);

                Assert.That(payload.dialogueReply, Is.EqualTo($"First response. Second response. {question}"));
                Assert.That(payload.dialogueContinuation, Is.EqualTo(payload.dialogueReply));
                Assert.That(payload.dialogueReply.EndsWith(question, StringComparison.Ordinal), Is.True);
                Assert.That(payload.dialogueReply.Split(new[] { question }, StringSplitOptions.None).Length - 1,
                    Is.EqualTo(1));
                Assert.That(payload.dialogueReply.Count(character => character == '?'), Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ChangingTaskScope_ResetsQuestionLimit()
        {
            var host = new GameObject("Pacing Task Reset");
            try
            {
                var service = host.AddComponent<RealLLMService>();
                service.ConfigureDialoguePacing(1f, 1);
                var firstCondition = PacingCondition("dialogue_avatar", "explicit");
                service.SetExperimentCondition(firstCondition);
                var firstDecision = InvokePrivate(service, "CreateAvatarDialoguePacingDecision", "First task input");
                InvokePrivate(service, "CommitAvatarDialoguePacing", new SpringScenePayload(), firstDecision);
                Assert.That(service.NonGoalQuestionsAsked, Is.EqualTo(1));

                var nextCondition = PacingCondition("dialogue_avatar", "explicit");
                nextCondition.conditionId = "condition-b";
                nextCondition.task.taskId = "furniture_shopping";
                service.SetExperimentCondition(nextCondition);

                Assert.That(service.NonGoalQuestionsAsked, Is.Zero);
                var nextDecision = InvokePrivate(service, "CreateAvatarDialoguePacingDecision", "Second task input");
                Assert.That(GetField<AvatarDialoguePacingData>(nextDecision, "data").triggered, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void AvatarDialoguePacing_ZeroNeverTriggersAndIgnoresCorrectionCondition()
        {
            var hosts = new[]
            {
                new GameObject("Pacing NE"),
                new GameObject("Pacing NR"),
                new GameObject("Pacing SE"),
                new GameObject("Pacing SR")
            };
            try
            {
                var services = hosts.Select(host => host.AddComponent<RealLLMService>()).ToArray();
                var first = services[0];
                first.ConfigureDialoguePacing(0f, 1);
                first.SetExperimentCondition(PacingCondition("dialogue_avatar", "recast"));
                var disabled = InvokePrivate(first, "CreateAvatarDialoguePacingDecision", "Same user input");
                Assert.That(GetField<AvatarDialoguePacingData>(disabled, "data").triggered, Is.False);

                var matrix = new[]
                {
                    (provider: "dialogue_avatar", style: "explicit"),
                    (provider: "dialogue_avatar", style: "recast"),
                    (provider: "assistant_agent", style: "explicit"),
                    (provider: "assistant_agent", style: "recast")
                };
                var decisions = services.Select((service, index) =>
                {
                    service.ConfigureDialoguePacing(1f, 1);
                    service.SetExperimentCondition(PacingCondition(matrix[index].provider, matrix[index].style));
                    return InvokePrivate(service, "CreateAvatarDialoguePacingDecision", "Same user input");
                }).ToArray();
                var samples = decisions.Select(decision => GetField<AvatarDialoguePacingData>(decision, "data"))
                    .ToArray();
                Assert.That(samples.All(data => data.triggered), Is.True);
                Assert.That(samples.Select(data => data.randomSample).Distinct().Count(), Is.EqualTo(1));
                Assert.That(samples.Select(data => data.questionId).Distinct().Count(), Is.EqualTo(1));
            }
            finally
            {
                foreach (var host in hosts)
                {
                    UnityEngine.Object.DestroyImmediate(host);
                }
            }
        }

        private static LearningSessionDetail CreateDetail()
        {
            var scene = new SpringScenePayload
            {
                taskType = "hotel_check_in",
                environmentType = "hotel_lobby",
                dialogueReply = "Welcome. May I have your name?",
                avatarRole = new AvatarRoleData { role = "clerk" },
                scene = new ScenePayload { mode = "skybox", skyboxUrl = "demo://hotel-lobby-360" }
            };
            var response = new SpringScenePayload
            {
                dialogueReply = "Thank you. Let me check your booking.",
                dialoguePacing = new AvatarDialoguePacingData
                {
                    triggered = true,
                    questionId = "hotel_journey_comfort",
                    temperature = 0.4f,
                    randomSample = 0.2f
                },
                correctionFeedback = new CorrectionFeedbackData
                {
                    hasFeedback = true,
                    provider = "dialogue_avatar",
                    style = "recast",
                    errorType = "grammar"
                }
            };

            return new LearningSessionDetail
            {
                summary = new LearningSessionSummary { sessionId = "resume-session", turnCount = 1 },
                settings = new ConversationSettingsSnapshot
                {
                    feedbackSensitivity = "active",
                    condition = new CorrectionExperimentCondition
                    {
                        sessionId = "resume-session",
                        scenarioId = "hotel_check_in",
                        provider = "dialogue_avatar",
                        style = "recast",
                        task = new SceneTalkExperimentTask { scenarioId = "hotel_check_in" }
                    }
                },
                sceneSnapshot = scene,
                turns = new[]
                {
                    new DialogueTurnRecord
                    {
                        sequenceIndex = 0,
                        isOpening = true,
                        assistantText = scene.dialogueReply,
                        payload = scene
                    },
                    new DialogueTurnRecord
                    {
                        sequenceIndex = 1,
                        userText = "I have booking.",
                        assistantText = response.dialogueReply,
                        payload = response
                    }
                }
            };
        }

        private static CorrectionExperimentCondition PacingCondition(string provider, string style)
        {
            return new CorrectionExperimentCondition
            {
                sessionId = "pacing-session",
                conditionId = "condition-a",
                scenarioId = "hotel_check_in",
                provider = provider,
                style = style,
                turnIndex = 2,
                task = new SceneTalkExperimentTask
                {
                    taskId = "hotel_check_in",
                    context = "A hotel arrival conversation.",
                    goals = new[] { "secret reservation goal" },
                    fallbackEnvironmentType = "hotel_lobby",
                    fallbackAvatarRole = "hotel receptionist",
                    nonGoalQuestions = new[]
                    {
                        new NonGoalQuestionDefinition
                        {
                            questionId = "hotel_journey_comfort",
                            text = "Did you have a comfortable journey here?"
                        },
                        new NonGoalQuestionDefinition
                        {
                            questionId = "hotel_first_city_visit",
                            text = "Is this your first visit to our city?"
                        }
                    }
                }
            };
        }

        private static object InvokePrivate(object target, string methodName, params object[] arguments)
        {
            var methods = target.GetType().GetMethods(
                    BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic)
                .Where(method => method.Name == methodName && method.GetParameters().Length == arguments.Length)
                .ToArray();
            Assert.That(methods, Has.Length.EqualTo(1), methodName);
            return methods[0].Invoke(target, arguments);
        }

        private static T GetField<T>(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            return (T)field.GetValue(target);
        }

        private static IList GetPrivateList(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return (IList)field.GetValue(target);
        }
    }
}
