#if UNITY_EDITOR
using UnityEditor.Animations;
using UnityEngine;

namespace RCWS
{
    public class AnimatorLayerModel
    {
        public string Name;
        public AvatarMask AvatarMask;
        public AnimatorLayerBlendingMode BlendingMode;
        public float DefaultWeight;
        public bool IKPass;
        public int SyncedLayerIndex = -1;
        public bool SyncedLayerAffectsTiming;
        public AnimatorStateMachineModel StateMachine;
    }
}
#endif
