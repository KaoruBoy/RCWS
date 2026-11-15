#if UNITY_EDITOR

using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace RCWS
{
    public class RCWSProgressWindow : EditorWindow
    {
        List<object> log = new List<object>();
        public VRCAvatarDescriptor avatar;

        public static RCWSProgressWindow Create(VRCAvatarDescriptor avatar)
        {
            var win = CreateInstance<RCWSProgressWindow>();
            win.avatar = avatar;

            var editorWindow = EditorGUIUtility.GetMainWindowPosition();
            var size = new Vector2(300, 200);
            win.ShowPopup();
            win.position = new Rect(editorWindow.xMin, editorWindow.yMax - size.y - 24, size.x, size.y);
            //win.position = new Rect(editorWindow.yMin + editorWindow.height - size.y, editorWindow.xMin, size.x, size.y);

            EditorApplication.delayCall += () => win.Close();

            return win;
        }

        void OnGUI()
        {
            var labelStyle = new GUIStyle(EditorStyles.label) { wordWrap = true };
            var width = position.width;
            var y = position.height;
            var renderedHeight = 0f;

            for (var i = log.Count - 1; i >= 0; i--)
            {
                var l = log[i];

                float height = 0;
                if (l is ProgressState ps)
                {
                    height = 16;
                    Rect r = new Rect(0,y-height, position.width, height);
                    EditorGUI.ProgressBar(r, ps.progressPercent, ps.text + " - " + ps.progressCurrent + " / " + ps.progressTarget + " (" + Mathf.Floor(ps.progressPercent * 100) + "%)");
                }
                else
                {
                    var text = Convert.ToString(log[i]);
                    height = labelStyle.CalcHeight(new GUIContent(text), width);
                    GUI.Label(new Rect(0, y - height, width, height), text, labelStyle);
                }

                if (renderedHeight + height > position.height)
                    break;

                y -= height;
                renderedHeight += height;
            }
        }

        public void Log(string newText)
        {
            log.Add(newText);
            Debug.Log(newText);
        }

        public ProgressState Progress()
        {
            var state = new ProgressState();
            log.Add(state);
            return state;
        }

        public void RenderNow()
        {
            var repaintImmediately = GetType().GetMethod("RepaintImmediately", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (repaintImmediately != null)
                repaintImmediately.Invoke(this, new object[] { });
            else Repaint();
        }

        public class ProgressState
        {
            public string text;
            public int progressCurrent, progressTarget;
            public float progressPercent => progressCurrent / (float)progressTarget;
        }
    }
}

#endif