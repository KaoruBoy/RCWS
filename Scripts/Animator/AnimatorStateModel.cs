#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor.Animations;
using UnityEngine;

namespace RCWS
{
    public class AnimatorStateModel
    {
        public string Name;
        public string Tag;
        public float Speed;
        public float CycleOffset;
        public bool Mirror;
        public bool IKOnFeet;
        public bool WriteDefaultValues;
        public bool SpeedParameterActive;
        public AnimatorParameterModel SpeedParameter;
        public bool CycleOffsetParameterActive;
        public AnimatorParameterModel CycleOffsetParameter;
        public bool MirrorParameterActive;
        public AnimatorParameterModel MirrorParameter;
        public bool TimeParameterActive;
        public AnimatorParameterModel TimeParameter;
        public Vector3 Position;
        public AnimatorMotionModel Motion;
        public StateMachineBehaviour[] Behaviours = Array.Empty<StateMachineBehaviour>();
        public readonly List<AnimatorTransitionModel> Transitions = new List<AnimatorTransitionModel>();
    }
}
#endif
