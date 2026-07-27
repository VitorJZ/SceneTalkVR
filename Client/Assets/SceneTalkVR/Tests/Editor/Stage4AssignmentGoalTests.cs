using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using SceneTalkVR.Core;
using UnityEditor;
using UnityEngine;

namespace SceneTalkVR.Tests.Editor
{
    public sealed class Stage4AssignmentGoalTests
    {
        private static readonly string[] Tasks = { "hotel_check_in", "furniture_shopping", "gym_membership", "tourist_assistance" };
        private static AssignmentSequence[] Sequences => new[]
        {
            Seq("test-s1", FormalConditionCode.NE, FormalConditionCode.NR, FormalConditionCode.SE, FormalConditionCode.SR),
            Seq("test-s2", FormalConditionCode.NR, FormalConditionCode.SE, FormalConditionCode.SR, FormalConditionCode.NE),
            Seq("test-s3", FormalConditionCode.SE, FormalConditionCode.SR, FormalConditionCode.NE, FormalConditionCode.NR),
            Seq("test-s4", FormalConditionCode.SR, FormalConditionCode.NE, FormalConditionCode.NR, FormalConditionCode.SE)
        };

        [Test]
        public void SameParticipant_GeneratesStableAssignment()
        {
            var a = Create("p100"); var b = Create("p100");
            Assert.That(a.sequenceId, Is.EqualTo(b.sequenceId));
            Assert.That(a.assignmentSeed, Is.EqualTo(b.assignmentSeed));
            CollectionAssert.AreEqual(a.conditions.Select(x => x.task.taskId), b.conditions.Select(x => x.task.taskId));
        }

        [Test]
        public void DifferentParticipants_DistributeAcrossSequences()
        {
            var ids = Enumerable.Range(0, 100).Select(i => Create("p" + i).sequenceId).Distinct().ToArray();
            Assert.That(ids.Length, Is.EqualTo(4));
        }

        [Test]
        public void TestProtocol_HasEveryConditionOnce()
        {
            Assert.That(new HashSet<FormalConditionCode>(Create("p1").conditions.Select(x => x.formalConditionCode)).Count, Is.EqualTo(4));
        }

        [Test]
        public void StrictWithoutReplacement_HasEveryTaskOnce()
        {
            CollectionAssert.AreEquivalent(Tasks, Create("p2").conditions.Select(x => x.task.taskId));
        }

        [Test]
        public void ConditionTaskPairs_AreApproximatelyBalanced()
        {
            var counts = new Dictionary<string, int>();
            foreach (var code in Enum.GetValues(typeof(FormalConditionCode)).Cast<FormalConditionCode>())
                foreach (var task in Tasks) counts[code + "/" + task] = 0;
            for (var i = 0; i < 400; i++)
                foreach (var item in Create("balance-" + i).conditions) counts[item.formalConditionCode + "/" + item.task.taskId]++;
            Assert.That(counts.Values.Max() - counts.Values.Min(), Is.LessThanOrEqualTo(60));
        }

        [Test]
        public void FormalAllocator_BlocksUnconfirmedMappingAndTaskDecision()
        {
            var protocol = ScriptableObject.CreateInstance<ExperimentV11ProtocolConfig>();
            var catalog = ScriptableObject.CreateInstance<ExperimentTaskCatalog>();
            var ok = new ExperimentAssignmentAllocator().TryCreateFormal("p", "s", protocol, catalog,
                AssignmentPolicy.StrictWithoutReplacement, out _, out var error);
            Assert.That(ok, Is.False);
            StringAssert.Contains("condition_mapping_unconfirmed", error);
            StringAssert.Contains("formal_task_no_replacement_unconfirmed", error);
            UnityEngine.Object.DestroyImmediate(protocol); UnityEngine.Object.DestroyImmediate(catalog);
        }

        [Test]
        public void UndefinedTaskPolicy_IsRejected()
        {
            var ok = new ExperimentAssignmentAllocator().TryCreateForTesting("p", "s", "v", "c", Sequences, Tasks,
                AssignmentPolicy.Undefined, out _, out var error);
            Assert.That(ok, Is.False); StringAssert.Contains("undefined", error);
        }

        [Test]
        public void Assignment_SaveAndRestore()
        {
            var path = Path.Combine(Path.GetTempPath(), "SceneTalkVR-stage4-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                var expected = Create("persist"); ExperimentAssignmentAllocator.Save(expected, path);
                var actual = ExperimentAssignmentAllocator.Load(path);
                Assert.That(actual.assignmentSeed, Is.EqualTo(expected.assignmentSeed));
                Assert.That(actual.conditions.Length, Is.EqualTo(4));
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }

        [Test]
        public void ChangedProtocolOrCatalog_RejectsStoredAssignment()
        {
            var a = Create("compat");
            Assert.That(ExperimentAssignmentAllocator.IsCompatible(a, "changed", "test-catalog", out var protocolError), Is.False);
            Assert.That(protocolError, Is.EqualTo("protocol_version_changed"));
            Assert.That(ExperimentAssignmentAllocator.IsCompatible(a, "test-protocol", "changed", out var catalogError), Is.False);
            Assert.That(catalogError, Is.EqualTo("task_catalog_version_changed"));
        }

        [Test]
        public void Candidate_IsNotConfirmed_AndNeedsExperimenter()
        {
            var tracker = Tracker(); var id = tracker.Goals[0].goalId;
            Assert.That(tracker.SubmitGoalCandidate(id, "fake_llm", new GoalEvidence(), out _), Is.True);
            Assert.That(tracker.Goals[0].state, Is.EqualTo(GoalProgressState.Candidate));
            Assert.That(tracker.ConfirmGoal(id, "", "", out var error), Is.False);
            Assert.That(error, Is.EqualTo("experimenter_identity_required"));
        }

        [Test]
        public void ExperimenterConfirmAndReject_AreAuditableTransitions()
        {
            var tracker = Tracker(); var actions = new List<string>(); tracker.GoalChanged += (_, action) => actions.Add(action);
            var first = tracker.Goals[0].goalId; var second = tracker.Goals[1].goalId;
            tracker.SubmitGoalCandidate(first, "manual", new GoalEvidence { turnId = "t1", transcript = "evidence" }, out _);
            tracker.ConfirmGoal(first, "experimenter-1", "ok", out _);
            AdvanceAfterConfirmedGoal(tracker, "t1");
            tracker.SubmitGoalCandidate(second, "manual", new GoalEvidence(), out _);
            tracker.RejectGoal(second, "experimenter-1", "insufficient", out _);
            CollectionAssert.AreEqual(new[] { "candidate", "confirmed", "candidate", "rejected" }, actions);
            Assert.That(tracker.Goals[0].confirmedBy, Is.EqualTo("experimenter-1"));
            Assert.That(tracker.Goals[1].rejectionReason, Is.EqualTo("insufficient"));
        }

        [Test]
        public void CompletionRate_UsesConfirmedOnly()
        {
            var tracker = Tracker();
            for (var i = 0; i < 2; i++) { var id = tracker.ActiveGoal.goalId; var turnId = "rate-" + i; tracker.SubmitGoalCandidate(id, "manual", new GoalEvidence { turnId = turnId }, out _); tracker.ConfirmGoal(id, "exp", "", out _); AdvanceAfterConfirmedGoal(tracker, turnId); }
            tracker.SubmitGoalCandidate(tracker.Goals[2].goalId, "llm", null, out _);
            Assert.That(tracker.GetCompletionRate(), Is.EqualTo(0.5f));
        }

        [Test]
        public void ResetGoals_ClearsPreviousTaskState()
        {
            var tracker = Tracker(); tracker.SubmitGoalCandidate(tracker.Goals[0].goalId, "manual", null, out _);
            tracker.ResetGoals(Task("new_task"));
            Assert.That(tracker.Goals.All(x => x.state == GoalProgressState.NotStarted), Is.True);
            Assert.That(tracker.Goals.All(x => x.goalId.StartsWith("new_task")), Is.True);
        }

        [Test]
        public void Lifecycle_LoadsAssignedTask_AndResetRunsBeforeCondition()
        {
            using var fixture = new LifecycleFixture("lifecycle-load");
            fixture.Manager.BeginTurn();
            Assert.That(fixture.Coordinator.PrepareCondition(0, false, out var error), Is.True, error);
            Assert.That(fixture.Manager.CurrentTurnIndex, Is.Zero);
            Assert.That(fixture.Manager.CurrentTask.taskId, Is.EqualTo(fixture.Assignment.conditions[0].task.taskId));
            Assert.That(fixture.ResetProbe.Count, Is.GreaterThan(0));
        }

        [Test]
        public void Lifecycle_CreatesFreshGoalsAndAwaitingQuestionnaireBoundary()
        {
            using var fixture = new LifecycleFixture("lifecycle-goals");
            fixture.Coordinator.PrepareCondition(0, false, out _);
            Assert.That(fixture.Coordinator.GoalTracker.Goals.Count, Is.EqualTo(4));
            fixture.Coordinator.CompleteTask("experimenter_ended");
            Assert.That(fixture.Coordinator.CurrentConditionAssignment.status, Is.EqualTo(ConditionRunStatus.AwaitingQuestionnaire));
        }

        [Test]
        public void CompletedCondition_CannotRunTwice()
        {
            using var fixture = new LifecycleFixture("no-duplicate");
            fixture.Coordinator.PrepareCondition(0, false, out _); fixture.Coordinator.CompleteTask("done"); fixture.Coordinator.CompleteQuestionnaireBoundary();
            Assert.That(fixture.Coordinator.PrepareCondition(0, false, out var error), Is.False);
            Assert.That(error, Is.EqualTo("condition_already_completed"));
        }

        [Test]
        public void TechnicalInvalidRetry_GetsNewRunIdAndRequiresExplicitFlag()
        {
            using var fixture = new LifecycleFixture("retry");
            fixture.Coordinator.PrepareCondition(0, false, out _); var first = fixture.Coordinator.ConditionRunId; fixture.Coordinator.MarkTechnicalInvalid("tts_failure");
            Assert.That(fixture.Coordinator.PrepareCondition(0, false, out _), Is.False);
            Assert.That(fixture.Coordinator.PrepareCondition(0, true, out var error), Is.True, error);
            Assert.That(fixture.Coordinator.ConditionRunId, Is.Not.EqualTo(first));
        }

        [Test]
        public void MaximumTurns_CanEndWithoutAllGoals()
        {
            using var fixture = new LifecycleFixture("max-turns");
            Set(fixture.Coordinator, "maxTurns", 1); fixture.Coordinator.PrepareCondition(0, false, out _); fixture.Manager.BeginTurn();
            Assert.That(fixture.Coordinator.ShouldEndForLimit(out var reason), Is.True);
            Assert.That(reason, Is.EqualTo("max_turns"));
        }

        [Test]
        public void MaximumDuration_CanEndWithoutAllGoals()
        {
            using var fixture = new LifecycleFixture("max-duration");
            fixture.Coordinator.PrepareCondition(0, false, out _);
            Set(fixture.Coordinator, "maxDurationMinutes", 0.1f);
            Set(fixture.Coordinator, "conditionStartedUtc", DateTime.UtcNow.AddMinutes(-1));
            Assert.That(fixture.Coordinator.ShouldEndForLimit(out var reason), Is.True);
            Assert.That(reason, Is.EqualTo("max_duration"));
        }

        [Test]
        public void GoalConfirmAndReject_WriteIndependentStudyEvents()
        {
            var participant = "events-" + Guid.NewGuid().ToString("N");
            using var fixture = new LifecycleFixture(participant);
            fixture.Coordinator.PrepareCondition(0, false, out _);
            var first = fixture.Coordinator.GoalTracker.Goals[0].goalId;
            var second = fixture.Coordinator.GoalTracker.Goals[1].goalId;
            fixture.Coordinator.SubmitGoalCandidate(first, "fake_llm", "turn-1", "evidence", out _);
            fixture.Coordinator.ConfirmGoalByExperimenter(first, "exp-1", "confirmed", out _);
            AdvanceAfterConfirmedGoal(fixture.Coordinator.GoalTracker, "turn-1");
            fixture.Coordinator.SubmitGoalCandidate(second, "manual", "turn-2", "evidence", out _);
            fixture.Coordinator.RejectGoalByExperimenter(second, "exp-1", "not enough", out _);
            var path = Path.Combine(Application.persistentDataPath, "SceneTalkVR", "ExperimentLogs", participant + "_session_study_events_v1.jsonl");
            var text = File.ReadAllText(path);
            StringAssert.Contains("GoalCandidateSubmitted", text);
            StringAssert.Contains("GoalConfirmed", text);
            StringAssert.Contains("GoalRejected", text);
        }

        [Test]
        public void FormalMode_RejectsDeveloperTestAssignment()
        {
            var go = new GameObject("formal-reject");
            try
            {
                var manager = go.AddComponent<ExperimentConditionManager>(); Set(manager, "formalExperiment", true);
                var coordinator = manager.LifecycleCoordinator ?? go.AddComponent<ExperimentLifecycleCoordinator>(); coordinator.Configure(manager);
                Assert.That(coordinator.LoadAssignment(Create("developer"), out var error), Is.False);
                Assert.That(error, Is.EqualTo("formal_mode_rejects_developer_assignment"));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void TwoConditions_RunSequentially_WithFreshRunTaskAndGoals()
        {
            using var fixture = new LifecycleFixture("two-conditions");
            Assert.That(fixture.Coordinator.PrepareCondition(0, false, out var firstError), Is.True, firstError);
            var firstRun = fixture.Coordinator.ConditionRunId;
            var firstTask = fixture.Coordinator.CurrentConditionAssignment.task.taskId;
            var firstGoal = fixture.Coordinator.GoalTracker.Goals[0].goalId;
            fixture.Coordinator.SubmitGoalCandidate(firstGoal, "manual", "turn-1", "evidence", out _);
            fixture.Coordinator.CompleteTask("experimenter_ended"); fixture.Coordinator.CompleteQuestionnaireBoundary();
            Assert.That(fixture.Coordinator.PrepareCondition(1, false, out var secondError), Is.True, secondError);
            Assert.That(fixture.Coordinator.ConditionRunId, Is.Not.EqualTo(firstRun));
            Assert.That(fixture.Coordinator.CurrentConditionAssignment.task.taskId, Is.Not.EqualTo(firstTask));
            Assert.That(fixture.Coordinator.GoalTracker.Goals.All(g => g.state == GoalProgressState.NotStarted), Is.True);
            Assert.That(fixture.Manager.CurrentTurnIndex, Is.Zero);
        }

        private static ExperimentAssignment Create(string participant)
        {
            Assert.That(new ExperimentAssignmentAllocator().TryCreateForTesting(participant, "session", "test-protocol", "test-catalog",
                Sequences, Tasks, AssignmentPolicy.StrictWithoutReplacement, out var value, out var error), Is.True, error);
            return value;
        }
        private static AssignmentSequence Seq(string id, params FormalConditionCode[] values) => new AssignmentSequence { sequenceId = id, conditions = values };
        private static GoalProgressTracker Tracker() { var value = new GoalProgressTracker(); value.ResetGoals(Task("task")); return value; }
        private static void AdvanceAfterConfirmedGoal(GoalProgressTracker tracker, string evidenceTurnId)
        {
            if (tracker.SequenceState == GoalSequenceState.AwaitingParticipantTurn)
            {
                var unlockTurnId = evidenceTurnId + "-unlock";
                Assert.That(tracker.NotifyParticipantTurnSubmitted(unlockTurnId), Is.True);
                Assert.That(tracker.NotifyDialogueTurnCompleted(unlockTurnId), Is.True);
                return;
            }
            Assert.That(tracker.NotifyDialogueTurnCompleted(evidenceTurnId), Is.True);
        }
        private static ExperimentTaskDefinition Task(string id) => new ExperimentTaskDefinition
        {
            taskId = id, goals = Enumerable.Range(1, 4).Select(i => new ExperimentTaskGoal { text = "Goal " + i }).ToArray()
        };
        private static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(target, value);

        private sealed class ResetProbe : MonoBehaviour, ISceneTalkSessionReset { public int Count; public void ResetSession() => Count++; }
        private sealed class LifecycleFixture : IDisposable
        {
            public readonly GameObject Go = new GameObject("stage4-fixture");
            public ExperimentConditionManager Manager { get; }
            public ExperimentLifecycleCoordinator Coordinator { get; }
            public ExperimentAssignment Assignment { get; }
            public ResetProbe ResetProbe { get; }
            public LifecycleFixture(string participant)
            {
                Manager = Go.AddComponent<ExperimentConditionManager>();
                Coordinator = Manager.LifecycleCoordinator ?? Go.AddComponent<ExperimentLifecycleCoordinator>();
                Coordinator.Configure(Manager);
                ResetProbe = Go.AddComponent<ResetProbe>();
                var protocol = AssetDatabase.LoadAssetAtPath<ExperimentV11ProtocolConfig>("Assets/SceneTalkVR/ExperimentProtocol/ExperimentV11Protocol.asset");
                var catalog = AssetDatabase.LoadAssetAtPath<ExperimentTaskCatalog>("Assets/SceneTalkVR/ExperimentProtocol/ExperimentTaskCatalog.asset");
                Set(Manager, "experimentProtocol", protocol); Set(Manager, "taskCatalog", catalog);
                Assignment = Create(participant); Assignment.protocolVersion = protocol.ProtocolVersion; Assignment.taskCatalogVersion = catalog.CatalogVersion;
                Assert.That(Coordinator.LoadAssignment(Assignment, out var error), Is.True, error);
            }
            public void Dispose() => UnityEngine.Object.DestroyImmediate(Go);
        }
    }
}
