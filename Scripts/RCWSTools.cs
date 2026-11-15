#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase.Editor.BuildPipeline;

namespace RCWS
{
    public class RCWSTools
    {
        [MenuItem("GameObject/RCWS/Run only RCWS preprocessor on clone", false, 50 )]
        static void RCWSProcessSelectedAvatar()
        {
            var avatar = Selection.activeGameObject.GetComponent<VRCAvatarDescriptor>();
            if(avatar == null)
                avatar = Selection.activeGameObject.GetComponentInParent<VRCAvatarDescriptor>();

            var clone = GameObject.Instantiate(avatar.gameObject);
            clone.transform.parent = avatar.transform.parent;
            clone.transform.localPosition += new Vector3(-2, 0, 0);

            new RCWSProcessor().OnPreprocessAvatar(clone);
        }

        [MenuItem("GameObject/RCWS/Run only RCWS preprocessor on clone", true)]
        static bool CanRCWSProcessSelectedAvatar()
            => CanProcessSelectedAvatar();
            
        [MenuItem("GameObject/RCWS/Run all preprocessors on clone", false, 50 )]
        static void ProcessSelectedAvatar()
        {
            var avatar = Selection.activeGameObject.GetComponent<VRCAvatarDescriptor>();
            if(avatar == null)
                avatar = Selection.activeGameObject.GetComponentInParent<VRCAvatarDescriptor>();

            var clone = GameObject.Instantiate(avatar.gameObject);
            clone.transform.parent = avatar.transform.parent;
            clone.transform.localPosition += new Vector3(-2, 0, 0);

            VRCBuildPipelineCallbacks.OnPreprocessAvatar(clone);
        }

        [MenuItem("GameObject/RCWS/Run all preprocessors on clone", true)]
        static bool CanProcessSelectedAvatar()
            => Selection.activeGameObject != null && 
                (Selection.activeGameObject.GetComponent<VRCAvatarDescriptor>() != null || Selection.activeGameObject.GetComponentInParent<VRCAvatarDescriptor>() != null);

        [MenuItem("Tools/RCWS/Pause Running Preprocessor On Play")]
        static void PauseRunningPreprocessorOnPlay()
        {
            RunPreprocessorInPlayMode.Pause = !RunPreprocessorInPlayMode.Pause;    
        }
            

        [MenuItem("Tools/RCWS/Pause Running Preprocessor On Play", true)]
        static bool PauseRunningPreprocessorOnPlayCheck()
        {
            Menu.SetChecked("Tools/RCWS/Pause Running Preprocessor On Play", RunPreprocessorInPlayMode.Pause);
            return true;
        }
        
        [MenuItem("Tools/RCWS/Clear Temp Folder")]
        static void ClearTempFolder()
        {
            RCWSProcessor.ClearTempFiles();
        }
    }
}

#endif