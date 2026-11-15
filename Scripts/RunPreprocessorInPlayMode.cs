#if UNITY_EDITOR

using System.Linq;
using Collections.Pooled;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase.Editor.BuildPipeline;

namespace RCWS
{
    [InitializeOnLoad]
    public class RunPreprocessorInPlayMode : IProcessSceneWithReport
    {
        public static bool Pause = false;

        static RunPreprocessorInPlayMode()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }


        static void OnPlayModeStateChanged(PlayModeStateChange obj)
        {
            if (obj == PlayModeStateChange.EnteredEditMode)
            {
                RCWSProcessor.ClearTempFiles();
            }
        }

        public int callbackOrder => int.MinValue + 99;
        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (!Application.isPlaying) return;

            if (Pause)
            {
                Debug.Log("Skipping avatar preprocessing. Paused in tools menu");
                return;
            }

            // Check if any known package runs the preprocessor on play to avoid double processing
            {
                var pkgs = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages();

                string[] packagesThatRunPreprocessorOnPlay = new string[]
                {
                        "com.vrcfury.vrcfury",
                        "nadena.dev.ndmf"
                };

                var preprocessorPackages = pkgs.Where(p => packagesThatRunPreprocessorOnPlay.Any(pp => p.packageId.StartsWith(pp + "@")));
                if (preprocessorPackages.Any())
                {
                    Debug.Log("Skipping avatar preprocessing. Found package(s) that run the preprocessor on play: " + string.Join(", ", preprocessorPackages.Select(p => p.packageId.Split("@")[0])));
                    return;
                }
            }

            Debug.Log("Running avatar preprocessors on play mode enter");
            foreach (var avatar in GameObject.FindObjectsOfType<VRCAvatarDescriptor>())
            {
                VRCBuildPipelineCallbacks.OnPreprocessAvatar(avatar.gameObject);
            }
        }
    }
}

#endif