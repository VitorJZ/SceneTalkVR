using System;
using System.Collections.Generic;

namespace SceneTalkVR.History
{
    public interface IExperimentHistoryStore : IDisposable
    {
        void Initialize();
        int CountExperiments();
        IReadOnlyList<ExperimentRecordSummary> ListExperiments(int offset, int limit);
        ExperimentRecordDetail GetExperiment(string experimentId);
        void CreateExperiment(ExperimentRecordDetail detail);
        void UpdateExperiment(ExperimentRecordSummary summary);
        void UpsertPhase(ExperimentPhaseRecord phase);
        void UpsertAttempt(ExperimentAttemptRecord attempt);
        void UpsertQuestionnaire(ExperimentQuestionnaireRecord questionnaire);
        void UpsertRanking(ExperimentRankingRecord ranking);
        bool DeleteExperiment(string experimentId);
    }
}
