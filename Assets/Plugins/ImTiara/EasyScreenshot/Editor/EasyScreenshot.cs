#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace ImTiara
{
    [CustomEditor(typeof(Camera))]
    public class EasyScreenshot : Editor
    {
        private Type _cameraEditor;
        private MethodInfo _onSceneGUI;
        private Editor _instance;
        private int _customWidth = 1920, _customHeight = 1080;
        private bool _allowTransparency;

        private void OnEnable()
        {
            _cameraEditor = AppDomain.CurrentDomain.GetAssemblies().SelectMany(assembly
                => assembly.GetTypes()).FirstOrDefault(type
                => type.Name == "CameraEditor");

            _onSceneGUI = _cameraEditor.GetMethod("OnSceneGUI");

            _instance = CreateEditor(targets, _cameraEditor);
        }

        private void OnDisable()
            => DestroyImmediate(_instance);

        public void OnSceneGUI()
            => _onSceneGUI.Invoke(_instance, null);

        public override void OnInspectorGUI()
        {
            _instance.OnInspectorGUI();

            GUILayout.BeginVertical("GroupBox");

            if (GUILayout.Button("1920x1080 (Default) Screenshot")) CaptureImage(1920, 1080);
            if (GUILayout.Button("3840x2160 (4K) Screenshot")) CaptureImage(3840, 2160);
            if (GUILayout.Button("7680x4320 (8K) Screenshot")) CaptureImage(7680, 4320);

            GUILayout.Space(5);

            _allowTransparency = EditorGUILayout.Toggle("Allow Transparency", _allowTransparency);

            GUILayout.Space(5);

            GUILayout.BeginHorizontal();

            _customWidth = EditorGUILayout.IntField(_customWidth, GUILayout.MaxWidth(43));

            GUILayout.Label("X", new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 14
            });

            _customHeight = EditorGUILayout.IntField(_customHeight, GUILayout.MaxWidth(43));

            GUI.enabled = _customHeight > 0 && _customWidth > 0;
            if (GUILayout.Button("Take Screenshot")) CaptureImage(_customWidth, _customHeight);
            GUI.enabled = true;
            GUILayout.FlexibleSpace();

            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }

        private void CaptureImage(int width, int height)
        {
            string output = EditorUtility.SaveFilePanel("Save Screenshot", "", $"Unity_{width}x{height}_{DateTime.Now:yyyy-MM-dd-HH-mm-ss.fff}.png", "png");
            if (string.IsNullOrEmpty(output)) return;

            Camera camera = (Camera)_instance.target;

            RenderTexture renderTexture = new RenderTexture(width, height, 24);
            camera.targetTexture = renderTexture;
            Texture2D tex = new Texture2D(width, height, _allowTransparency ? TextureFormat.RGBA32 : TextureFormat.RGB24, false);
            camera.Render();
            RenderTexture.active = renderTexture;
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            camera.targetTexture = null;
            RenderTexture.active = null;
            DestroyImmediate(renderTexture);

            byte[] bytes = ImageConversion.EncodeToPNG(tex);
            using (StreamWriter streamWriter = new StreamWriter(output))
            {
                streamWriter.BaseStream.Write(bytes, 0, bytes.Length);
                streamWriter.Close();
            }
        }
    }
}
#endif