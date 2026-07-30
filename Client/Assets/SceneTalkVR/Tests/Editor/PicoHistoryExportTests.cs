using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SceneTalkVR.Core;
using SceneTalkVR.History;
using UnityEngine;

namespace SceneTalkVR.Tests.Editor
{
    public sealed class PicoHistoryExportTests
    {
        [Test]
        public void Snapshot_IsChronologicalAndKeepsSkippedQuestionnaire()
        {
            var later = Experiment("later", 200);
            var earlier = Experiment("earlier", 100);
            earlier.conversations = new[]
            {
                new LearningSessionSummary { sessionId = "conversation-1", createdAtUnixMs = 50 }
            };
            earlier.questionnaires = new[]
            {
                new ExperimentQuestionnaireRecord
                {
                    questionnaireRecordId = "skipped",
                    session = new QuestionnaireSession
                    {
                        questionnaireId = "condition",
                        startedAtUtc = "2026-07-29T01:00:00Z",
                        skippedAtUtc = "2026-07-29T01:01:00Z",
                        completionStatus = QuestionnaireCompletionStatus.Skipped,
                        responses = Array.Empty<QuestionnaireResponse>()
                    }
                }
            };

            var snapshot = PicoHistoryExportSnapshotBuilder.Build(
                new[] { later, earlier },
                id => id == "conversation-1"
                    ? new LearningSessionDetail
                    {
                        summary = new LearningSessionSummary
                        {
                            sessionId = id,
                            createdAtUnixMs = 50
                        },
                        turns = new[]
                        {
                            new DialogueTurnRecord { sequenceIndex = 2, createdAtUnixMs = 20 },
                            new DialogueTurnRecord { sequenceIndex = 1, createdAtUnixMs = 10 }
                        }
                    }
                    : null,
                "0123456789abcdef0123456789abcdef",
                "2026-07-29T02:00:00Z",
                "1.0",
                "6000.3",
                "Android",
                "PICO");

            Assert.That(snapshot.experiments.Select(value => value.summary.experimentId),
                Is.EqualTo(new[] { "earlier", "later" }));
            Assert.That(snapshot.experiments[0].conversations[0].turns.Select(value => value.sequenceIndex),
                Is.EqualTo(new[] { 1, 2 }));
            Assert.That(snapshot.experiments[0].questionnaires[0].session.completionStatus,
                Is.EqualTo(QuestionnaireCompletionStatus.Skipped));
            Assert.That(snapshot.experiments[0].questionnaires[0].session.responses, Is.Empty);
            Assert.That(snapshot.questionnaireCount, Is.EqualTo(1));
            Assert.That(snapshot.warnings, Is.Empty);
        }

        [Test]
        public void Snapshot_ReportsMissingConversationWithoutDroppingExperiment()
        {
            var experiment = Experiment("experiment", 100);
            experiment.conversations = new[]
            {
                new LearningSessionSummary { sessionId = "missing", createdAtUnixMs = 110 }
            };

            var snapshot = PicoHistoryExportSnapshotBuilder.Build(
                new[] { experiment },
                _ => null,
                "0123456789abcdef0123456789abcdef",
                "2026-07-29T02:00:00Z",
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty);

            Assert.That(snapshot.experiments, Has.Length.EqualTo(1));
            Assert.That(snapshot.experiments[0].conversations, Is.Empty);
            Assert.That(snapshot.experiments[0].missingConversationSessionIds, Is.EqualTo(new[] { "missing" }));
            Assert.That(snapshot.warnings.Select(value => value.code),
                Is.EqualTo(new[] { "conversation_detail_missing" }));
        }

        [Test]
        public void QuestionnaireDefinitions_ContainFormalPromptsAndRankingChoicesInCatalogOrder()
        {
            var catalog = ScriptableObject.CreateInstance<QuestionnaireCatalog>();
            try
            {
                catalog.EditorSet("catalog-1", new[]
                {
                    new QuestionnaireDefinition
                    {
                        questionnaireId = "formal_condition_v1",
                        questionnaireVersion = "1.0",
                        sections = new[]
                        {
                            new QuestionnaireSection
                            {
                                sectionId = "first",
                                displayOrder = 0,
                                items = new[]
                                {
                                    new QuestionnaireItem
                                    {
                                        questionnaireId = "formal_condition_v1",
                                        sectionId = "first",
                                        itemId = "question-1",
                                        displayOrder = 1,
                                        promptEnglish = "First question",
                                        promptChinese = "第一个问题",
                                        itemType = QuestionnaireItemType.Likert,
                                        scaleMin = 1,
                                        scaleMax = 7
                                    }
                                }
                            }
                        }
                    },
                    new QuestionnaireDefinition
                    {
                        questionnaireId = "formal_final_v1",
                        questionnaireVersion = "1.0",
                        sections = new[]
                        {
                            new QuestionnaireSection
                            {
                                sectionId = "ranking",
                                items = new[]
                                {
                                    new QuestionnaireItem
                                    {
                                        questionnaireId = "formal_final_v1",
                                        sectionId = "ranking",
                                        itemId = "formal_rank_01",
                                        promptEnglish = "Rank the conditions.",
                                        promptChinese = "请为条件排序。",
                                        itemType = QuestionnaireItemType.Ranking,
                                        choiceValues = new[] { "NE", "NR", "SE", "SR" }
                                    }
                                }
                            }
                        }
                    }
                });

                var result = PicoHistoryExportSnapshotBuilder.BuildQuestionnaireDefinitions(catalog, null);

                Assert.That(result.Select(value => value.questionnaireId),
                    Is.EqualTo(new[] { "formal_condition_v1", "formal_final_v1" }));
                Assert.That(result[0].questionnaireCatalogVersion, Is.EqualTo("catalog-1"));
                Assert.That(result[0].items.Single().promptChinese, Is.EqualTo("第一个问题"));
                Assert.That(result[1].items.Single().choiceValues,
                    Is.EqualTo(new[] { "NE", "NR", "SE", "SR" }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void ExperimentHistoryService_ReturnsAllRecordsChronologically()
        {
            var gameObject = new GameObject("history-export-test");
            try
            {
                var store = new FakeExperimentHistoryStore(
                    Experiment("later", 200),
                    Experiment("earlier", 100));
                var service = gameObject.AddComponent<ExperimentHistoryService>();
                service.ConfigureStoreForTests(store);

                var result = service.GetAllExperimentsChronological();

                Assert.That(result.Select(value => value.summary.experimentId),
                    Is.EqualTo(new[] { "earlier", "later" }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        private static ExperimentRecordDetail Experiment(string id, long createdAt)
        {
            return new ExperimentRecordDetail
            {
                summary = new ExperimentRecordSummary
                {
                    experimentId = id,
                    participantId = "participant-" + id,
                    sessionId = "session-" + id,
                    createdAtUnixMs = createdAt
                }
            };
        }

        private sealed class FakeExperimentHistoryStore : IExperimentHistoryStore
        {
            private readonly Dictionary<string, ExperimentRecordDetail> records;

            public FakeExperimentHistoryStore(params ExperimentRecordDetail[] values)
            {
                records = values.ToDictionary(value => value.summary.experimentId, StringComparer.Ordinal);
            }

            public void Initialize() { }
            public int CountExperiments() => records.Count;
            public IReadOnlyList<ExperimentRecordSummary> ListExperiments(int offset, int limit) => records.Values
                .Select(value => value.summary)
                .OrderByDescending(value => value.updatedAtUnixMs)
                .Skip(offset)
                .Take(limit)
                .ToArray();
            public ExperimentRecordDetail GetExperiment(string experimentId) =>
                records.TryGetValue(experimentId, out var value) ? value : null;
            public void CreateExperiment(ExperimentRecordDetail detail) => throw new NotSupportedException();
            public void UpdateExperiment(ExperimentRecordSummary summary) => throw new NotSupportedException();
            public void UpsertAttempt(ExperimentAttemptRecord attempt) => throw new NotSupportedException();
            public void UpsertQuestionnaire(ExperimentQuestionnaireRecord questionnaire) => throw new NotSupportedException();
            public void UpsertRanking(ExperimentRankingRecord ranking) => throw new NotSupportedException();
            public bool DeleteExperiment(string experimentId) => throw new NotSupportedException();
            public void Dispose() { }
        }
    }
}
