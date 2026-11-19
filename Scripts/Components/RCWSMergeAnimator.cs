#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

/// <summary>
/// See RCWSProcessor.Merge 
/// </summary>
public class RCWSMergeAnimator : RCWSComponent
{
    public string UniqueString;

    [SerializeField]
    public AnimatorMergeTarget[] Animators;
    public VRCExpressionsMenu[] Menus;
    public VRCExpressionParameters[] Parameters;

    public string[] GlobalParameters;
    public string[] GlobalTags;

    [Serializable]
    public class AnimatorMergeTarget
    {
        public AnimatorController Controller;
        public VRCAvatarDescriptor.AnimLayerType Layer = VRCAvatarDescriptor.AnimLayerType.FX;
    }

    void OnValidate()
    {
        EnsureUniqueString();
    }

    void Reset()
    {
        EnsureUniqueString();
    }

    public void EnsureUniqueString()
    {
        if(string.IsNullOrEmpty(UniqueString))
        {
            UniqueString = GenerateUniqueString();
            return;
        }

        var avatar = GetComponentInParent<VRCAvatarDescriptor>();
        if(avatar == null) return;
        
        var mergers = avatar.GetComponentsInChildren<RCWSMergeAnimator>().ToList();
        mergers.Remove(this);

        if(mergers.Any(m=>m.UniqueString == UniqueString))
            UniqueString = GenerateUniqueString();
    }

    static string GenerateUniqueString()
    {
        int length = 6;
        string allowedChars = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
        string s = "";
        var rand = new System.Random();
        for (int i = 0; i < length; i++)
            s += allowedChars[rand.Next(allowedChars.Length-1)];

        return s;
    }
}
#endif