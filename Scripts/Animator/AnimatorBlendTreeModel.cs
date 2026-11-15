#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor.Animations;

namespace RCWS
{
    public class AnimatorBlendTreeModel
    {
        public string Name;
        public BlendTreeType BlendType;
        public AnimatorParameterModel BlendParameter;
        public AnimatorParameterModel BlendParameterY;
        public float MinThreshold;
        public float MaxThreshold;
        public bool UseAutomaticThresholds;
        public readonly List<AnimatorBlendTreeChildModel> Children = new List<AnimatorBlendTreeChildModel>();

        internal static AnimatorBlendTreeModel FromBlendTree(
            BlendTree tree,
            IDictionary<string, AnimatorParameterModel> parameters)
        {
            var model = new AnimatorBlendTreeModel
            {
                Name = tree.name,
                BlendType = tree.blendType,
                BlendParameter = AnimatorControllerModel.FindParameter(tree.blendParameter, parameters),
                BlendParameterY = AnimatorControllerModel.FindParameter(tree.blendParameterY, parameters),
                MinThreshold = tree.minThreshold,
                MaxThreshold = tree.maxThreshold,
                UseAutomaticThresholds = tree.useAutomaticThresholds,
            };

            foreach (var child in tree.children)
            {
                model.Children.Add(new AnimatorBlendTreeChildModel
                {
                    Motion = AnimatorMotionModel.FromMotion(child.motion, parameters),
                    Threshold = child.threshold,
                    Position = child.position,
                    TimeScale = child.timeScale,
                    CycleOffset = child.cycleOffset,
                    Mirror = child.mirror,
                    DirectBlendParameter = AnimatorControllerModel.FindParameter(child.directBlendParameter, parameters)
                });
            }

            return model;
        }
    }
}
#endif
