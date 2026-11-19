using UnityEngine;

/// <summary>
/// See RCWSProcessor.Reparent
/// </summary>
public class RCWSReparent : RCWSComponent
{
    public HumanBodyBones Bone;
    public Transform ParentOverride;
    
    public bool KeepLocalPosition = false;
    public bool KeepLocalRotation = false;
    public bool KeepLocalScale = false;
}
