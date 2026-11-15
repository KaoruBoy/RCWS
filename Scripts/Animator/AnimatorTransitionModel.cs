#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor.Animations;

namespace RCWS
{
    public class AnimatorTransitionModel
    {
        public AnimatorStateModel DestinationState;
        public AnimatorStateMachineModel DestinationStateMachine;
        public bool IsExit;
        public bool Mute;
        public bool Solo;
        public float Duration;
        public float ExitTime;
        public bool HasExitTime;
        public bool HasFixedDuration;
        public float Offset;
        public TransitionInterruptionSource InterruptionSource;
        public bool OrderedInterruption;
        public bool CanTransitionToSelf;
        public readonly List<AnimatorConditionModel> Conditions = new List<AnimatorConditionModel>();

        internal static AnimatorTransitionModel FromTransition(
            AnimatorTransitionBase transition,
            IDictionary<AnimatorState, AnimatorStateModel> states,
            IDictionary<AnimatorStateMachine, AnimatorStateMachineModel> stateMachines,
            IDictionary<string, AnimatorParameterModel> parameters)
        {
            var model = new AnimatorTransitionModel
            {
                IsExit = transition.isExit,
                Mute = transition.mute,
                Solo = transition.solo
            };

            if (transition.destinationState != null)
                model.DestinationState = states[transition.destinationState];
            if (transition.destinationStateMachine != null)
                model.DestinationStateMachine = stateMachines[transition.destinationStateMachine];

            foreach (var condition in transition.conditions)
                model.Conditions.Add(new AnimatorConditionModel(
                    condition,
                    AnimatorControllerModel.FindParameter(condition.parameter, parameters)));

            if (transition is AnimatorStateTransition stateTransition)
            {
                model.Duration = stateTransition.duration;
                model.ExitTime = stateTransition.exitTime;
                model.HasExitTime = stateTransition.hasExitTime;
                model.HasFixedDuration = stateTransition.hasFixedDuration;
                model.Offset = stateTransition.offset;
                model.InterruptionSource = stateTransition.interruptionSource;
                model.OrderedInterruption = stateTransition.orderedInterruption;
                model.CanTransitionToSelf = stateTransition.canTransitionToSelf;
            }

            return model;
        }
    }
}
#endif
