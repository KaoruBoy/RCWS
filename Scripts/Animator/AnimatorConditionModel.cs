#if UNITY_EDITOR
using UnityEditor.Animations;

namespace RCWS
{
    public class AnimatorConditionModel
    {
        public AnimatorParameterModel Parameter;
        public AnimatorConditionMode Mode;
        public float Threshold;

        public AnimatorConditionModel() { }

        public AnimatorConditionModel(AnimatorCondition condition, AnimatorParameterModel parameter)
        {
            Parameter = parameter;
            Mode = condition.mode;
            Threshold = condition.threshold;
        }
    }
}
#endif
