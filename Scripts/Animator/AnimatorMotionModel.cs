#if UNITY_EDITOR
using UnityEditor.Animations;
using UnityEngine;
using System.Collections.Generic;

namespace RCWS
{
    public class AnimatorMotionModel
    {
        public Motion Motion;
        public AnimatorBlendTreeModel BlendTree;

        internal static AnimatorMotionModel FromMotion(
            Motion motion,
            IDictionary<string, AnimatorParameterModel> parameters)
        {
            if (motion == null) return null;
            var model = new AnimatorMotionModel();
            if (motion is BlendTree blendTree)
                model.BlendTree = AnimatorBlendTreeModel.FromBlendTree(blendTree, parameters);
            else
                model.Motion = motion;
            return model;
        }
    }
}
#endif
