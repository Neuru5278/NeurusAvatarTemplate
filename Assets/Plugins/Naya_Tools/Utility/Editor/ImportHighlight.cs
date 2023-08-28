using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace NayaTools
{

    public class ImportHighlight : AssetPostprocessor
    {

        static List<string> importAssets;

        static Color _color = Color.red;

        static bool _enable = true;

        static float _time;

        public static bool enable
        {
            get
            {
                return _enable;
            }
            set
            {
                _enable = value;
            }
        }
        public static Color color
        {
            set
            {
                _color.r = value.r * 1.6f;
                _color.g = value.g * 1.6f;
                _color.b = value.b * 1.6f;
                _color.a = value.a;

                Debug.Log("SetColor_" + _color);
            }
        }

        [InitializeOnLoadMethod]
        static void SetEvent()
        {
            _enable = EditorPrefs.GetBool("Naya_Tools.ImportHighlightEnable", true);

            var initColorString = EditorPrefs.GetString("Naya_Tools.ImportHighlightColor");
            if (!string.IsNullOrEmpty(initColorString))
            {
                _color.r = float.Parse(initColorString.Split('_')[0]) * 1.6f;
                _color.g = float.Parse(initColorString.Split('_')[1]) * 1.6f;
                _color.b = float.Parse(initColorString.Split('_')[2]) * 1.6f;
                _color.a = float.Parse(initColorString.Split('_')[3]);
            }
            EditorApplication.projectWindowItemOnGUI += OnGUI;
        }

        static void OnGUI(string guid, Rect selectionRect)
        {
            if (!enable) return;
            if (importAssets == null) return;
            var assetPath = AssetDatabase.GUIDToAssetPath(guid);
            string path = Array.Find<string>(importAssets.ToArray(), _ => _.StartsWith(assetPath) && File.Exists(_));
            if (path != null)
            {
                if (string.IsNullOrEmpty(assetPath)) return;
                var originColor = GUI.color;
                GUI.color = _color;
                GUI.Box(selectionRect, string.Empty);
                GUI.color = originColor;
            }
        }

        public static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (importedAssets.Length != 0)
                foreach (string import in importedAssets)
                {
                    Debug.Log($"Filename:{import}_{_time}_{Time.time}");
                }


            if (_time == Time.time && importAssets != null)
                importAssets.AddRange(importedAssets);
            else
                importAssets = new List<string>(importedAssets);
            _time = Time.time;

            Debug.Log(importAssets.Count);
        }

    }

    public class ImportHighlight_ColorChanger : EditorWindow
    {
        private static Color color = Color.red;
        private static bool enable = true;

        [MenuItem("GameObject/Naya_Tools/Setings/ImportHighlight")]
        static void init()
        {
            enable = EditorPrefs.GetBool("Naya_Tools.ImportHighlightEnable", true);
            var initColorString = EditorPrefs.GetString("Naya_Tools.ImportHighlightColor");
            Debug.Log(initColorString);
            if (!string.IsNullOrEmpty(initColorString))
            {
                color.r = float.Parse(initColorString.Split('_')[0]);
                color.g = float.Parse(initColorString.Split('_')[1]);
                color.b = float.Parse(initColorString.Split('_')[2]);
                color.a = float.Parse(initColorString.Split('_')[3]);
            }
            EditorWindow.GetWindow<ImportHighlight_ColorChanger>();
        }


        public void OnGUI()
        {
            enable = EditorGUILayout.Toggle("有効", enable);

            if (enable != ImportHighlight.enable)
            {
                ImportHighlight.enable = enable;
                EditorPrefs.SetBool("Naya_Tools.ImportHighlightEnable", enable);
            }

            color = EditorGUILayout.ColorField("強調色", color);

            if (GUILayout.Button("設定を保存"))
            {
                ImportHighlight.color = color;
                EditorPrefs.SetString("Naya_Tools.ImportHighlightColor", color.r + "_" + color.g + "_" + color.b + "_" + color.a);
            }
        }

    }

}