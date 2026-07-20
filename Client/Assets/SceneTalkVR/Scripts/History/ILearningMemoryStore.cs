using System;
using System.Collections.Generic;

namespace SceneTalkVR.History
{
    public interface ILearningMemoryStore : IDisposable
    {
        void Initialize();
        int CountSessions();
        IReadOnlyList<LearningSessionSummary> ListSessions(int offset, int limit);
        LearningSessionDetail GetSession(string sessionId);
        IReadOnlyCollection<string> ListSessionIds();
        void CreateSession(LearningSessionDetail detail);
        void UpdateSession(LearningSessionDetail detail);
        void AppendTurn(string sessionId, DialogueTurnRecord turn);
        bool DeleteSession(string sessionId);
    }
}
