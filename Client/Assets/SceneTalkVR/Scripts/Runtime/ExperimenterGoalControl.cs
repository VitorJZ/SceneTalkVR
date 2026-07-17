using SceneTalkVR.Core;
using UnityEngine;

namespace SceneTalkVR.Runtime
{
    // Deliberately has no participant-facing VR buttons. Use this component's
    // protected Editor/desktop inspector or a future authenticated console.
    public sealed class ExperimenterGoalControl : MonoBehaviour
    {
        [SerializeField] private ExperimentLifecycleCoordinator lifecycle;
        [SerializeField] private string experimenterId;
        [SerializeField] private string goalId;
        [TextArea, SerializeField] private string note;

        private ExperimentLifecycleCoordinator Resolve() => lifecycle != null
            ? lifecycle : FindFirstObjectByType<ExperimentLifecycleCoordinator>(FindObjectsInactive.Include);

        [ContextMenu("Experiment/Confirm Goal")]
        public void ConfirmSelectedGoal()
        {
            var target = Resolve();
            var error = target == null ? "lifecycle_missing" : string.Empty;
            if (target == null || !target.ConfirmGoalByExperimenter(goalId, experimenterId, note, out error))
                Debug.LogWarning($"[ExperimenterGoalControl] Confirm rejected: {error}", this);
        }

        [ContextMenu("Experiment/Reject Goal")]
        public void RejectSelectedGoal()
        {
            var target = Resolve();
            var error = target == null ? "lifecycle_missing" : string.Empty;
            if (target == null || !target.RejectGoalByExperimenter(goalId, experimenterId, note, out error))
                Debug.LogWarning($"[ExperimenterGoalControl] Reject rejected: {error}", this);
        }

        [ContextMenu("Experiment/Complete Task")]
        public void CompleteTask() => Resolve()?.CompleteTask(note, experimenterId);

        [ContextMenu("Experiment/Mark Technical Invalid")]
        public void MarkTechnicalInvalid() => Resolve()?.MarkTechnicalInvalid(note);

        [ContextMenu("Experiment/Abort Condition")]
        public void AbortCondition() => Resolve()?.Abort(note);

        [ContextMenu("Experiment/Continue After Questionnaire Placeholder")]
        public void CompleteQuestionnaireBoundary() => Resolve()?.CompleteQuestionnaireBoundary(experimenterId);
    }
}
