#if UNITY_EDITOR
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.Animations;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase.Editor.BuildPipeline;
using static VRC.SDK3.Avatars.Components.VRCAvatarDescriptor;
using System;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDKBase;
using System.Reflection;
using UnityEditor;
using System.IO;
using VRC.SDK3.Dynamics.PhysBone.Components;
using VRC.SDK3.Dynamics.Contact.Components;
using System.Diagnostics;
using Mono.Cecil.Cil;

namespace RCWS
{
    public class RCWSProcessor : IVRCSDKPreprocessAvatarCallback
    {
        const string MainFolderPath = "Packages/io.github.kaoruboy.rcws/";
        const string TempFolderPath = MainFolderPath + "Temp/";

        static private bool didRunInPlayMode = false;
        [InitializeOnEnterPlayMode]
        static void OnEnterPlaymodeInEditor(EnterPlayModeOptions options)
        {
            didRunInPlayMode = false;
        }

        public int callbackOrder => -1025;

        public bool OnPreprocessAvatar(GameObject avatar)
        {
            // Prevent running it twice
            if (didRunInPlayMode) return true;

            CreateTempFolder();
            AnimatorCache.Clear();

            try
            {
                return PreprocessAvatar(avatar.GetComponent<VRCAvatarDescriptor>());
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("Exception while processing avatar " + avatar.name + "\n" + e);
            }
            didRunInPlayMode = Application.isPlaying;
            return false;
        }

        static RCWSProgressWindow ProgressWindow;

        public bool PreprocessAvatar(VRCAvatarDescriptor avatar)
        {
            // Nothing to do
            if (!avatar.GetComponentsInChildren<RCWSComponent>().Any()) return true;

            AssetDatabase.StartAssetEditing();

            ProgressWindow = RCWSProgressWindow.Create(avatar);

            try
            {
                ProgressWindow.Log("Processing avatar " + avatar.name + "...");
                ProgressWindow.RenderNow();
                CreateTempFolder();

                ProgressWindow.Log("Copying parameter list...");
                ProgressWindow.RenderNow();
                CreateParameterListCopy(avatar);

                ProgressWindow.Log("Copying menus...");
                CreateMenuCopy(avatar);

                var reparenters = avatar.GetComponentsInChildren<RCWSReparent>(true);
                Dictionary<string, string> remappedPaths = new Dictionary<string, string>();
                Dictionary<Transform, string> originalPaths = reparenters.ToDictionary(r=>r.transform, r=>AnimationUtility.CalculateTransformPath(r.transform, avatar.transform));
                if (reparenters.Any())
                {
                    ProgressWindow.Log("Reparenting objects...");
                    var progress = ProgressWindow.Progress();
                    progress.progressTarget = reparenters.Length;

                    for (int i = 0; i < reparenters.Length; i++)
                    {
                        RCWSReparent reparenter = reparenters[i];
                        progress.progressCurrent = i;
                        progress.text = "Reparenting " + AnimationUtility.CalculateTransformPath(reparenter.transform, avatar.transform);
                        ProgressWindow.RenderNow();

                        Reparent(avatar, reparenter);
                    }

                    progress.progressCurrent += 1;
                    ProgressWindow.RenderNow();
                }

                foreach(var kv in originalPaths)
                    remappedPaths[kv.Value] = AnimationUtility.CalculateTransformPath(kv.Key, avatar.transform);

                var mergers = avatar.GetComponentsInChildren<RCWSMergeAnimator>(true);
                if (mergers.Any())
                {
                    ProgressWindow.Log("Merging animators...");
                    var progress = ProgressWindow.Progress();
                    progress.progressTarget = mergers.Length;
                    for (int i = 0; i < mergers.Length; i++)
                    {
                        RCWSMergeAnimator merger = mergers[i];
                        progress.progressCurrent = i;
                        progress.text = "Merging " + AnimationUtility.CalculateTransformPath(merger.gameObject.transform, avatar.transform);
                        ProgressWindow.RenderNow();

                        Merge(avatar, merger, remappedPaths);
                    }
                    progress.progressCurrent += 1;
                    ProgressWindow.RenderNow();
                }

                ProgressWindow.Log("Removing RCWS components...");
                ProgressWindow.RenderNow();
                RemoveAllRCWSComponents(avatar);
            }
            catch (Exception e)
            {
                ProgressWindow.RenderNow();
                ProgressWindow.Log("An exception has been thrown:" + e.ToString());
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                return false;
            }

            ProgressWindow.Log("Saving assets...");
            ProgressWindow.RenderNow();
            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
            ProgressWindow.Log("Finished");
            ProgressWindow.RenderNow();
            return true;
        }

        void CreateParameterListCopy(VRCAvatarDescriptor avatar)
        {
            if (avatar.expressionParameters == null)
                avatar.expressionParameters = new VRCExpressionParameters();

            var paramList = VRCExpressionParameters.Instantiate(avatar.expressionParameters);
            AssetDatabase.CreateAsset(paramList, AssetDatabase.GenerateUniqueAssetPath(TempFolderPath + avatar.name + "_Parameters.asset"));

            avatar.expressionParameters = paramList;
        }

        void CreateMenuCopy(VRCAvatarDescriptor avatar)
        {
            if (avatar.expressionsMenu == null)
            {
                avatar.expressionsMenu = new VRCExpressionsMenu();
                AssetDatabase.CreateAsset(avatar.expressionsMenu, AssetDatabase.GenerateUniqueAssetPath(TempFolderPath + avatar.name + "_Menu.asset"));
                return;
            }

            var copiedMenus = new Dictionary<VRCExpressionsMenu, VRCExpressionsMenu>();

            VRCExpressionsMenu CopyMenu(VRCExpressionsMenu srcMenu, VRCExpressionsMenu root = null)
            {
                if (copiedMenus.TryGetValue(srcMenu, out var existingMenu))
                    return existingMenu;

                var menu = VRCExpressionsMenu.Instantiate(srcMenu);
                copiedMenus.Add(srcMenu, menu);
                menu.Parameters = avatar.expressionParameters;

                if (root != null)
                    AssetDatabase.AddObjectToAsset(menu, AssetDatabase.GetAssetPath(root));
                else
                {
                    var path = AssetDatabase.GenerateUniqueAssetPath(TempFolderPath + avatar.name + "_Menu.asset");
                    AssetDatabase.CreateAsset(menu, path);
                }

                for (int i = 0; i < menu.controls.Count; i++)
                {
                    var control = menu.controls[i];

                    if (control.type == VRCExpressionsMenu.Control.ControlType.SubMenu)
                        control.subMenu = CopyMenu(menu.controls[i].subMenu, root ?? menu);
                }

                return menu;
            }

            avatar.expressionsMenu = CopyMenu(avatar.expressionsMenu);
        }

        Dictionary<VRCAvatarDescriptor, Dictionary<AnimLayerType, AnimatorController>> AnimatorCache = new Dictionary<VRCAvatarDescriptor, Dictionary<AnimLayerType, AnimatorController>>();

        AnimatorController GetAnimatorLayer(VRCAvatarDescriptor avatar, AnimLayerType type)
        {
            if (AnimatorCache.ContainsKey(avatar))
                if (AnimatorCache[avatar].ContainsKey(type))
                    return AnimatorCache[avatar][type];

            avatar.customizeAnimationLayers = true;
            var layerIndex = Array.FindIndex<CustomAnimLayer>(avatar.baseAnimationLayers, l => l.type == type);
            if (layerIndex == -1)
            {
                var newLayer = new CustomAnimLayer()
                {
                    isEnabled = true,
                    type = type,
                    animatorController = null,
                    mask = null,
                    isDefault = false
                };
                layerIndex = avatar.baseAnimationLayers.Length;
                avatar.baseAnimationLayers = avatar.baseAnimationLayers.Append(newLayer).ToArray();
            }
            CustomAnimLayer layer = avatar.baseAnimationLayers[layerIndex];
            AnimatorController animator = null;
            var animatorPathCopy = AssetDatabase.GenerateUniqueAssetPath(TempFolderPath + $"{avatar.name}_{type.ToString()}_Animator.controller");
            if (layer.animatorController == null)
            {
                animator = AnimatorController.CreateAnimatorControllerAtPath(animatorPathCopy);
                layer.animatorController = animator;
            }
            else
            {
                animator = (AnimatorController)layer.animatorController;
                animator = CopyController((AnimatorController)layer.animatorController, animatorPathCopy);
            }

            layer.animatorController = animator;
            avatar.baseAnimationLayers[layerIndex] = layer;

            if (!AnimatorCache.ContainsKey(avatar))
                AnimatorCache[avatar] = new Dictionary<AnimLayerType, AnimatorController>();
            AnimatorCache[avatar][type] = animator;

            return animator;
        }

        public static void ClearTempFiles()
        {
            CreateTempFolder();
            AssetDatabase.DeleteAssets(
                AssetDatabase.FindAssets("*", new string[] { TempFolderPath }).Select(guid => AssetDatabase.GUIDToAssetPath(guid)).ToArray(),
                new List<string>());
        }

        static void CreateTempFolder()
        {
            string[] tempFolderPathParts = TempFolderPath.Split('/');

            string parentFolder = tempFolderPathParts[0];
            for (int i = 1; i < tempFolderPathParts.Length; i++)
            {
                string folderPath = parentFolder + "/" + tempFolderPathParts[i];
                if (!AssetDatabase.IsValidFolder(folderPath))
                {
                    AssetDatabase.CreateFolder(parentFolder, tempFolderPathParts[i]);
                    AssetDatabase.ImportAsset(parentFolder + "/" + tempFolderPathParts[i]);
                }

                parentFolder = folderPath;
            }
        }

        AnimatorController CopyController(AnimatorController con, string path = null)
        {
            if (path == null) path = AssetDatabase.GenerateUniqueAssetPath(TempFolderPath + con.name + " Copy" + ".animator");
            var copy = AnimatorController.Instantiate(con);
            AssetDatabase.CreateAsset(copy, path);
            return copy;
        }

        void SetDefaultAnimatorInt(AnimatorController animator, string paramName, int value)
        {
            var parameters = animator.parameters;
            parameters.First(p => p.name == paramName).defaultInt = value;
            animator.parameters = parameters;
        }

        void SetDefaultAnimatorFloat(AnimatorController animator, string paramName, float value)
        {
            var parameters = animator.parameters;
            parameters.First(p => p.name == paramName).defaultFloat = value;
            animator.parameters = parameters;
        }

        void SetDefaultAnimatorBool(AnimatorController animator, string paramName, bool value)
        {
            var parameters = animator.parameters;
            parameters.First(p => p.name == paramName).defaultBool = value;
            animator.parameters = parameters;
        }

        void Reparent(VRCAvatarDescriptor avatar, RCWSReparent reparenter)
        {
            var animator = avatar.GetComponent<Animator>();

            var parent = reparenter.ParentOverride;
            if (parent == null)
                parent = animator.GetBoneTransform(reparenter.Bone);

            var pos = reparenter.transform.localPosition;
            var rot = reparenter.transform.localRotation;
            var scale = reparenter.transform.localScale;

            reparenter.transform.SetParent(parent);

            if (reparenter.KeepLocalPosition) reparenter.transform.localPosition = pos;
            if (reparenter.KeepLocalRotation) reparenter.transform.localRotation = rot;
            if (reparenter.KeepLocalScale) reparenter.transform.localScale = scale;
        }

        void Merge(VRCAvatarDescriptor avatar, RCWSMergeAnimator merger, Dictionary<string, string> remappedPaths)
        {
            merger.EnsureUniqueString();
            var uniqueName = merger.name + " " + merger.UniqueString;
            var parameterPrefix = "RCWS/Unique/" + uniqueName + "/";

            ProgressWindow.Log("Remapping dynamics parameters...");
            RemapDynamicsParameters(merger.gameObject, parameterPrefix);
            ProgressWindow.Log("Remapping collision tags...");
            Dictionary<string, string> collisionTagParameterRemaps = RemapContactTags(merger.gameObject, parameterPrefix).ToDictionary(e => e.Item1, e => e.Item2);

            MergeParameters(avatar, merger, collisionTagParameterRemaps, parameterPrefix);
            MergeMenus(avatar, merger, parameterPrefix);
            MergeAnimators(avatar, merger, collisionTagParameterRemaps, parameterPrefix, remappedPaths);
        }

        void MergeMenus(VRCAvatarDescriptor avatar, RCWSMergeAnimator merger, string parameterPrefix)
        {
            if (!merger.Menus.Any()) return;

            ProgressWindow.Log("Merging menus...");
            var paramProgress = ProgressWindow.Progress();
            paramProgress.progressTarget = merger.Parameters.Length;

            Dictionary<VRCExpressionsMenu, VRCExpressionsMenu> copiedMenus = new Dictionary<VRCExpressionsMenu, VRCExpressionsMenu>();

            for (int i = 0; i < merger.Menus.Length; i++)
            {
                VRCExpressionsMenu mergeMenu = merger.Menus[i];

                paramProgress.progressCurrent = i;
                paramProgress.text = "Merging " + mergeMenu.name;
                ProgressWindow.RenderNow();

                mergeMenu = CopyMenu(mergeMenu);

                foreach (var control in mergeMenu.controls)
                    avatar.expressionsMenu.controls.Add(control);

                VRCExpressionsMenu CopyMenu(VRCExpressionsMenu srcMenu)
                {
                    if (copiedMenus.TryGetValue(srcMenu, out var existingMenu))
                        return existingMenu;

                    var menu = VRCExpressionsMenu.Instantiate(srcMenu);
                    copiedMenus.Add(srcMenu, menu);
                    menu.Parameters = avatar.expressionParameters;

                    AssetDatabase.AddObjectToAsset(menu, AssetDatabase.GetAssetPath(avatar.expressionsMenu));

                    for (int i = 0; i < menu.controls.Count; i++)
                    {
                        var control = menu.controls[i];

                        if (control.parameter != null && ShouldParameterBeRemapped(control.parameter.name, parameterPrefix))
                            control.parameter.name = parameterPrefix + control.parameter.name;

                        if (control.subParameters != null)
                        {
                            foreach (var sp in control.subParameters)
                            {
                                if (sp == null) continue;
                                if (ShouldParameterBeRemapped(sp.name, parameterPrefix))
                                    sp.name = parameterPrefix + sp.name;
                            }
                        }

                        if (control.type == VRCExpressionsMenu.Control.ControlType.SubMenu)
                            control.subMenu = CopyMenu(menu.controls[i].subMenu);
                    }

                    return menu;
                }
            }

            paramProgress.progressCurrent += 1;
            ProgressWindow.RenderNow();
        }

        void MergeParameters(VRCAvatarDescriptor avatar, RCWSMergeAnimator merger, Dictionary<string, string> collisionTagParameterRemaps, string parameterPrefix)
        {
            if (!merger.Parameters.Any()) return;

            ProgressWindow.Log("Merging parameters...");
            var paramProgress = ProgressWindow.Progress();
            paramProgress.progressTarget = merger.Parameters.Length;

            VRCExpressionParameters.Parameter[] avatarParameters = avatar.expressionParameters.parameters;
            for (int i = 0; i < merger.Parameters.Length; i++)
            {
                VRCExpressionParameters mergeParameters = merger.Parameters[i];

                paramProgress.progressCurrent = i;
                paramProgress.text = "Merging " + mergeParameters.name;
                ProgressWindow.RenderNow();

                var uniqueParams = new VRCExpressionParameters.Parameter[mergeParameters.parameters.Length];
                for (int pi = 0; pi < mergeParameters.parameters.Length; pi++)
                {
                    var p = mergeParameters.parameters[pi];
                    var method = typeof(object).GetMethod(
                        nameof(MemberwiseClone),
                        BindingFlags.Instance | BindingFlags.NonPublic
                    );
                    p = (VRCExpressionParameters.Parameter)method.Invoke(p, null);

                    var originalName = p.name;

                    if (collisionTagParameterRemaps.ContainsKey(p.name))
                        p.name = collisionTagParameterRemaps[p.name];

                    if (ShouldParameterBeRemapped(originalName, parameterPrefix))
                        p.name = parameterPrefix + p.name;

                    uniqueParams[pi] = p;
                }

                avatarParameters = avatarParameters.Concat(uniqueParams).ToArray();
            }
            avatar.expressionParameters.parameters = avatarParameters;

            paramProgress.progressCurrent += 1;
            ProgressWindow.RenderNow();
        }

        void MergeAnimators(VRCAvatarDescriptor avatar, RCWSMergeAnimator merger, Dictionary<string, string> collisionTagParameterRemaps, string parameterPrefix, Dictionary<string, string> remappedPaths)
        {
            if (!merger.Animators.Any()) return;

            var clipNamePrefix = parameterPrefix;
            var pathPrefix = AnimationUtility.CalculateTransformPath(merger.transform, avatar.transform) + "/";

            ProgressWindow.Log("Merging animators...");
            var animatorProgress = ProgressWindow.Progress();
            animatorProgress.progressTarget = merger.Animators.Length;

            // Merge animator to avatar animator
            for (int i = 0; i < merger.Animators.Length; i++)
            {
                var mergeTarget = merger.Animators[i];
                var avatarAnimator = GetAnimatorLayer(avatar, mergeTarget.Layer);
                var animModel = AnimatorControllerModel.FromAnimatorController(mergeTarget.Controller);

                animatorProgress.progressCurrent = i;
                animatorProgress.text = "Merging " + mergeTarget.Controller.name + " to " + mergeTarget.Layer + " layer";
                ProgressWindow.RenderNow();

                // Remap parameters
                foreach (var p in animModel.Parameters)
                {
                    var originalName = p.Name;

                    if (collisionTagParameterRemaps.ContainsKey(p.Name))
                        p.Name = collisionTagParameterRemaps[p.Name];

                    if (ShouldParameterBeRemapped(originalName, parameterPrefix))
                        p.Name = parameterPrefix + p.Name;
                }

                // Remap animation paths
                {
                    Dictionary<AnimationClip, AnimationClip> clips = new Dictionary<AnimationClip, AnimationClip>();
                    AnimationClip RemapClip(AnimationClip c)
                    {
                        if (clips.ContainsKey(c))
                            return clips[c];

                        var remappedClip = new AnimationClip()
                        {
                            name = clipNamePrefix + c.name
                        };

                        var bindings = AnimationUtility.GetCurveBindings(c);
                        for (int ai = 0; ai < bindings.Length; ai++)
                        {
                            var bind = bindings[ai];
                            var curve = AnimationUtility.GetEditorCurve(c, bind);
                            bind.path = pathPrefix + bind.path;

                            if(remappedPaths.ContainsKey(bind.path))
                                bind.path = remappedPaths[bind.path];
                            else
                            {
                                var parent = remappedPaths.Keys.OrderByDescending(k=>k.Length).FirstOrDefault(p=>bind.path.StartsWith(p + "/"));
                                if(parent != null)
                                {
                                    bind.path = remappedPaths[parent] + bind.path.Substring(parent.Length);
                                }
                            }

                            if (bind.type == typeof(Animator))
                            {
                                bind.path = "";

                                var originalName = bind.propertyName;
                                if (collisionTagParameterRemaps.ContainsKey(bind.propertyName))
                                    bind.propertyName = collisionTagParameterRemaps[bind.propertyName];

                                if (ShouldParameterBeRemapped(originalName, parameterPrefix))
                                    bind.propertyName = parameterPrefix + bind.propertyName;
                            }

                            if(bind.path.EndsWith("/"))
                                bind.path = bind.path.Substring(0, bind.path.Length-1);


                            AnimationUtility.SetEditorCurve(remappedClip, bind, curve);
                        }

                        var objectReferenceBindings = AnimationUtility.GetObjectReferenceCurveBindings(c);
                        for (int ai = 0; ai < objectReferenceBindings.Length; ai++)
                        {
                            var bind = objectReferenceBindings[ai];
                            var keyframes = AnimationUtility.GetObjectReferenceCurve(c, bind);
                            bind.path = pathPrefix + bind.path;
                            AnimationUtility.SetObjectReferenceCurve(remappedClip, bind, keyframes);
                        }

                        AssetDatabase.AddObjectToAsset(remappedClip, avatarAnimator);

                        clips[c] = remappedClip;
                        return remappedClip;
                    }

                    foreach (var l in animModel.Layers)
                    {
                        RemapStateMachine(l.StateMachine);

                        void RemapStateMachine(AnimatorStateMachineModel sm)
                        {
                            foreach (var subsm in sm.StateMachines)
                                RemapStateMachine(subsm);

                            foreach (var s in sm.States)
                                RemapState(s);

                            void RemapState(AnimatorStateModel m)
                            {
                                if (m.Motion.BlendTree != null)
                                    RemapTree(m.Motion.BlendTree);

                                if (m.Motion.Motion is AnimationClip c)
                                    m.Motion.Motion = RemapClip(c);
                            }

                            void RemapTree(AnimatorBlendTreeModel t)
                            {
                                foreach (var child in t.Children)
                                {
                                    if (child.Motion.BlendTree != null)
                                        RemapTree(child.Motion.BlendTree);

                                    if (child.Motion.Motion is AnimationClip c)
                                        child.Motion.Motion = RemapClip(c);
                                }
                            }
                        }
                    }
                }

                // Fix state behaviors
                {
                    foreach (var l in animModel.Layers)
                        ProcessStateMachine(l.StateMachine);
                    void ProcessStateMachine(AnimatorStateMachineModel sm)
                    {
                        foreach (var state in sm.States)
                            foreach (var b in state.Behaviours)
                            {
                                if (b is VRCAnimatorPlayAudio p)
                                {
                                    if (p.PlaybackOrder == VRC_AnimatorPlayAudio.Order.Parameter)
                                    {
                                        var originalName = p.ParameterName;
                                        if (collisionTagParameterRemaps.ContainsKey(p.ParameterName))
                                            p.ParameterName = collisionTagParameterRemaps[p.ParameterName];

                                        if (ShouldParameterBeRemapped(originalName, parameterPrefix))
                                            p.ParameterName = parameterPrefix + p.ParameterName;
                                    }

                                    p.SourcePath = pathPrefix + p.SourcePath;
                                }
                                else if (b is VRCAvatarParameterDriver pd)
                                {
                                    foreach (var pa in pd.parameters)
                                    {
                                        {
                                            var originalName = pa.name;
                                            if (collisionTagParameterRemaps.ContainsKey(pa.name))
                                                pa.name = collisionTagParameterRemaps[pa.name];

                                            if (ShouldParameterBeRemapped(originalName, parameterPrefix))
                                                pa.name = parameterPrefix + pa.name;
                                        }

                                        if (pa.type == VRC_AvatarParameterDriver.ChangeType.Copy)
                                        {
                                            var originalName = pa.source;
                                            if (collisionTagParameterRemaps.ContainsKey(pa.source))
                                                pa.source = collisionTagParameterRemaps[pa.source];

                                            if (ShouldParameterBeRemapped(originalName, parameterPrefix))
                                                pa.source = parameterPrefix + pa.source;
                                        }
                                    }
                                }
                                else if (b is VRCAnimatorLayerControl alc)
                                {
                                    VRC_AnimatorLayerControl.BlendableLayer mergeLayer;
                                    switch (mergeTarget.Layer)
                                    {
                                        case AnimLayerType.Action:
                                            mergeLayer = VRC_AnimatorLayerControl.BlendableLayer.Action;
                                            break;
                                        case AnimLayerType.FX:
                                            mergeLayer = VRC_AnimatorLayerControl.BlendableLayer.FX;
                                            break;
                                        case AnimLayerType.Gesture:
                                            mergeLayer = VRC_AnimatorLayerControl.BlendableLayer.Gesture;
                                            break;
                                        case AnimLayerType.Additive:
                                            mergeLayer = VRC_AnimatorLayerControl.BlendableLayer.Additive;
                                            break;
                                        default:
                                            throw new Exception("VRCAnimatorLayerControl cannot be used on a " + mergeTarget.Layer + " layer. Allowed layer types are Action, FX, Gesture, and Additive");
                                    }
                                    if (alc.playable != mergeLayer)
                                        throw new Exception("VRCAnimatorLayerControl can only be used if the playable layer is the same as the one it's being merged to");

                                    alc.layer += avatarAnimator.layers.Length;
                                }
                                else if (b is VRCAnimatorFaceTrackingBlendshapesSet ftbs)
                                {
                                    foreach (var bs in ftbs.blendSets)
                                    {
                                        if (bs.type != VRC_AnimatorFaceTrackingBlendshapesSet.SourceType.Parameter) continue;

                                        var originalName = bs.sourceParameter;
                                        if (collisionTagParameterRemaps.ContainsKey(bs.sourceParameter))
                                            bs.sourceParameter = collisionTagParameterRemaps[bs.sourceParameter];

                                        if (ShouldParameterBeRemapped(originalName, parameterPrefix))
                                            bs.sourceParameter = parameterPrefix + bs.sourceParameter;
                                    }
                                }
                            }

                        foreach (var ssm in sm.StateMachines)
                            ProcessStateMachine(ssm);
                    }
                }

                animModel.AddLayersAndParametersTo(avatarAnimator);
            }
            animatorProgress.progressCurrent += 1;
            ProgressWindow.RenderNow();
        }

        void RemapDynamicsParameters(GameObject root, string parameterPrefix)
        {
            foreach (var pb in root.GetComponentsInChildren<VRCPhysBone>(true))
                if (ShouldParameterBeRemapped(pb.parameter, parameterPrefix))
                    pb.parameter = parameterPrefix + pb.parameter;

            foreach (var contact in root.GetComponentsInChildren<VRCContactReceiver>(true))
                if (ShouldParameterBeRemapped(contact.parameter, parameterPrefix))
                    contact.parameter = parameterPrefix + contact.parameter;
        }

        static HashSet<string> builtInCollisionTags = new HashSet<string>()
        {
            "Head",
            "Torso",
            "Hand",
            "HandL",
            "HandR",
            "Foot",
            "FootL",
            "FootR",
            "Finger",
            "FingerL",
            "FingerR",
            "FingerIndex",
            "FingerMiddle",
            "FingerRing",
            "FingerLittle",
            "FingerIndexL",
            "FingerMiddleL",
            "FingerRingL",
            "FingerLittleL",
            "FingerIndexR",
            "FingerMiddleR",
            "FingerRingR",
            "FingerLittleR",
            "HandLeft",
            "HandRight",
            "FingerIndexLeft",
            "FingerIndexRight",
            "FootLeft",
            "FootRight",
            "Hot",
            "Cold",
            "Fire",
            "Freezer",
            "Wet",
            "Water",
            "Wind",
            "Weapon",
            "Shield",
            "Damage",
            "DamageBlunt",
            "DamageSharp",
            "Ammunition",
            "Projectile",
            "Consumable",
            "ConsumableFood",
            "ConsumableDrink",
            "Brush",
            "Dye"
        };

        bool ShouldRemapCollisionTag(string tag, string prefix)
        {
            if (string.IsNullOrEmpty(tag)) return false;
            if (tag.StartsWith("RCWS/", StringComparison.Ordinal)) return false;
            if (tag.StartsWith("KB/RCWS/", StringComparison.Ordinal)) return false;
            if (tag.StartsWith(prefix, StringComparison.Ordinal)) return false;
            if (builtInCollisionTags.Contains(tag)) return false;

            return true;
        }

        IEnumerable<(string, string)> RemapContactTags(GameObject root, string tagPrefix)
        {
            foreach (var sender in root.GetComponentsInChildren<VRCContactSender>())
            {
                for (int i = 0; i < sender.collisionTags.Count; i++)
                {
                    if (!ShouldRemapCollisionTag(sender.collisionTags[i], tagPrefix)) continue;

                    sender.collisionTags[i] = tagPrefix + sender.collisionTags[i];
                }
            }

            foreach (var contact in root.GetComponentsInChildren<VRCContactReceiver>())
            {
                var hasCollisionTagParameters = (contact.advancedParameters & VRC.Dynamics.ContactReceiver.AdvancedParameter.CollisionTags) != 0;

                for (int i = 0; i < contact.collisionTags.Count; i++)
                {
                    if (!ShouldRemapCollisionTag(contact.collisionTags[i], tagPrefix)) continue;

                    // Return advanced collision tag parameters to be remapped 
                    if (hasCollisionTagParameters && !String.IsNullOrEmpty(contact.parameter))
                        yield return (contact.parameter + VRCContactReceiver.PARAM_COLLISION_TAG_PREFIX + contact.collisionTags[i],
                            contact.parameter + VRCContactReceiver.PARAM_COLLISION_TAG_PREFIX + tagPrefix + contact.collisionTags[i]);

                    contact.collisionTags[i] = tagPrefix + contact.collisionTags[i];
                }
            }
        }

        static HashSet<string> builtInParameters = new HashSet<string> {
            "IsLocal",
            "PreviewMode",
            "Viseme",
            "Voice",
            "GestureLeft",
            "GestureRight",
            "GestureLeftWeight",
            "GestureRightWeight",
            "AngularY",
            "VelocityX",
            "VelocityY",
            "VelocityZ",
            "VelocityMagnitude",
            "Upright",
            "Grounded",
            "Seated",
            "AFK",
            "TrackingType",
            "VRMode",
            "MuteSelf",
            "InStation",
            "Earmuffs",
            "IsOnFriendsList",
            "AvatarVersion",
            "IsAnimatorEnabled",
            "UserEyeTracking",
            "UserFaceTracking",
            "ScaleModified",
            "ScaleFactor",
            "ScaleFactorInverse",
            "EyeHeightAsMeters",
            "EyeHeightAsPercent",
        };

        bool ShouldParameterBeRemapped(string parameter, string prefix)
        {
            if (string.IsNullOrEmpty(parameter)) return false;
            if (parameter.StartsWith("RCWS/", StringComparison.Ordinal)) return false;
            if (parameter.StartsWith("KB/RCWS/", StringComparison.Ordinal)) return false;
            if (parameter.StartsWith(prefix, StringComparison.Ordinal)) return false;
            if (builtInParameters.Contains(parameter)) return false;

            return true;
        }

        void RemoveAllRCWSComponents(VRCAvatarDescriptor avatar)
        {
            foreach (var c in avatar.GetComponentsInChildren<RCWSComponent>(true))
                UnityEngine.Object.DestroyImmediate(c);
        }
    }
}
#endif