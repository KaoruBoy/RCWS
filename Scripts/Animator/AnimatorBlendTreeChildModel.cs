#if UNITY_EDITOR
using UnityEngine;

namespace RCWS
{
    public class AnimatorBlendTreeChildModel
    {
        public AnimatorMotionModel Motion;
        public float Threshold;
        public Vector2 Position;
        public float TimeScale;
        public float CycleOffset;
        public bool Mirror;
        public AnimatorParameterModel DirectBlendParameter;
    }
}
#endif
