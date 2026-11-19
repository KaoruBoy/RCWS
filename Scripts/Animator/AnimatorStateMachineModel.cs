#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace RCWS
{
    public class AnimatorStateMachineModel
    {
        public string Name;
        public Vector3 Position;
        public Vector3 AnyStatePosition;
        public Vector3 EntryPosition;
        public Vector3 ExitPosition;
        public Vector3 ParentStateMachinePosition;
        public AnimatorStateModel DefaultState;
        public StateMachineBehaviour[] Behaviours = Array.Empty<StateMachineBehaviour>();
        public readonly List<AnimatorStateModel> States = new List<AnimatorStateModel>();
        public readonly List<AnimatorStateMachineModel> StateMachines = new List<AnimatorStateMachineModel>();
        public readonly List<AnimatorTransitionModel> AnyStateTransitions = new List<AnimatorTransitionModel>();
        public readonly List<AnimatorTransitionModel> EntryTransitions = new List<AnimatorTransitionModel>();
        public readonly List<AnimatorStateMachineTransitionModel> StateMachineTransitions =
            new List<AnimatorStateMachineTransitionModel>();
    }
}
#endif
