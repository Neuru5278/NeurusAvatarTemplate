#if UNITY_EDITOR

using UnityEngine;
using UnityEditor;
using System.Reflection;
using System;

namespace Neuru.CustomTransformEditor
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(Transform))]
    public class TransformEditor : UnityEditor.Editor
    {
        private SerializedProperty m_LocalPosition;
        private SerializedProperty m_LocalRotation;
        private SerializedProperty m_LocalScale;

        private static bool globalFoldOut;

        private static Vector3[] copiedValues = new Vector3[3];
        private static bool[] hasCopiedValues = new bool[3] {false, false, false};
        private static int contextChoice;

        private static System.Type RotationGUIType;
        private static MethodInfo RotationGUIEnableMethod;
        private static MethodInfo DrawRotationGUIMethod;
        private object rotationGUI;

        private GUIContent localPositionContent = new GUIContent("Position", "The local position of this GameObject relative to the parent.");
        private GUIContent localRotationContent = new GUIContent("Rotation", "The local rotation of this GameObject relative to the parent.");
        private GUIContent localScaleContent = new GUIContent("Scale", "The local scaling of this GameObject relative to the parent.");

        private const string GlobalPrefName = "TE_GlobalFoldOut";

        public static void SaveGlobalFoldOutPref(bool value)
        {
            EditorPrefs.SetBool(GlobalPrefName, value);
        }

        private void OnEnable()
        {
            if (RotationGUIType == null)
                RotationGUIType = System.Type.GetType("UnityEditor.TransformRotationGUI, UnityEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null");
            if (RotationGUIEnableMethod == null)
                RotationGUIEnableMethod = RotationGUIType.GetMethod("OnEnable", BindingFlags.Public | BindingFlags.Instance);
            if (DrawRotationGUIMethod == null)
                DrawRotationGUIMethod = RotationGUIType.GetMethod("RotationField", Array.Empty<System.Type>());

            // SerializedProperty 초기화
            m_LocalPosition = serializedObject.FindProperty("m_LocalPosition");
            m_LocalRotation = serializedObject.FindProperty("m_LocalRotation");
            m_LocalScale = serializedObject.FindProperty("m_LocalScale");

            if (rotationGUI == null)
                rotationGUI = Activator.CreateInstance(RotationGUIType);
            RotationGUIEnableMethod.Invoke(rotationGUI, new object[] { m_LocalRotation, localRotationContent });
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUI.BeginChangeCheck();

            // 로컬 변환 정보 표시
            DrawDefaultTransformInspector();

            // 글로벌 변환 정보 펼침/접기
            globalFoldOut = EditorGUILayout.Foldout(globalFoldOut, "Global");

            DrawSeparator();

            if (globalFoldOut)
            {
                // 글로벌 변환 정보 표시
                DrawGlobalTransformInspector();
            }

            if (EditorGUI.EndChangeCheck())
            {
                // 변화가 있을 경우 저장 및 적용
                SaveGlobalFoldOutPref(globalFoldOut);
                Undo.RecordObjects(targets, "Transform");
                serializedObject.ApplyModifiedProperties();
                foreach (var targetObject in targets)
                {
                    EditorUtility.SetDirty(targetObject);
                }
            }
        }

        private void DrawDefaultTransformInspector()
        {
            DrawSeparator();

            EditorGUILayout.BeginHorizontal();

            // 로컬 전체 복사, 붙여넣기, 리셋 버튼 표시
            DrawIntegratedButton(Color.green, 7);

            EditorGUILayout.EndHorizontal();

            DrawSeparator();

            EditorGUILayout.BeginHorizontal();

            // 로컬 위치 표시
            EditorGUILayout.PropertyField(m_LocalPosition, localPositionContent);

            // 복사, 붙여넣기, 리셋 버튼 표시
            DrawAllButton(Color.green, 1);

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();

            // 로컬 회전 표시
            DrawRotationGUIMethod.Invoke(rotationGUI, null);

            // 복사, 붙여넣기, 리셋 버튼 표시
            DrawAllButton(Color.green, 2);

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();

            // 로컬 스케일 표시
            EditorGUILayout.PropertyField(m_LocalScale, localScaleContent);

            // 복사, 붙여넣기, 리셋 버튼 표시
            DrawAllButton(Color.green, 3);

            EditorGUILayout.EndHorizontal();

            DrawSeparator();
        }

        private void DrawGlobalTransformInspector()
        {
            Undo.RecordObjects(targets, "Transform");

            EditorGUILayout.BeginHorizontal();

            // 로컬 전체 복사, 붙여넣기, 리셋 버튼 표시
            DrawIntegratedButton(Color.blue, 8);

            EditorGUILayout.EndHorizontal();

            DrawSeparator();

            EditorGUILayout.BeginHorizontal();

            // 글로벌 위치 표시
            Vector3 beforePosition = RoundVector3(((Transform)target).position);
            Vector3 changedPosition = EditorGUILayout.Vector3Field("Global Position", RoundVector3(((Transform)target).position));

            if (beforePosition != changedPosition)
            {
                foreach (var targetObject in targets)
                {
                    ((Transform)targetObject).position = changedPosition;
                }
            }

            // 복사, 붙여넣기, 리셋 버튼 표시
            DrawAllButton(Color.blue, 4);

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();

            // 글로벌 회전 표시
            Vector3 beforeRotation = RoundVector3(((Transform)target).eulerAngles);
            Vector3 changedRotation = EditorGUILayout.Vector3Field("Global Rotation", RoundVector3(((Transform)target).eulerAngles));

            if (beforeRotation != changedRotation)
            {
                foreach (var targetObject in targets)
                {
                    ((Transform)targetObject).eulerAngles = changedRotation;
                }
            }

            // 복사, 붙여넣기, 리셋 버튼 표시
            DrawAllButton(Color.blue, 5);

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();

            // 글로벌 스케일 표시
            Vector3 beforeScale = RoundVector3(((Transform)target).lossyScale);
            Vector3 changedScale = EditorGUILayout.Vector3Field("Global Scale", RoundVector3(((Transform)target).lossyScale));

            if (beforeScale != changedScale)
            {
                foreach (var targetObject in targets)
                {
                    ChangeGlobalScale((Transform)targetObject, changedScale);
                }
            }

            // 복사, 붙여넣기, 리셋 버튼 표시
            DrawAllButton(Color.blue, 6);

            EditorGUILayout.EndHorizontal();

            DrawSeparator();
        }

        private void DrawAllButton(Color color, int contextChoice)
        {
            // 복사 버튼 표시
            DrawButton(color, () => CopyFieldValues(contextChoice), "C");

            // 붙여넣기 버튼 표시
            DrawButton(color, () => PasteFieldValues(contextChoice), "P");

            // 리셋 버튼 표시
            DrawButton(color, () => ResetFieldValues(contextChoice), "R");
        }

        private void DrawIntegratedButton(Color color, int contextChoice)
        {
            Color oldBgColor = GUI.backgroundColor;
            GUI.backgroundColor = color;

            if (GUILayout.Button("Copy"))
            {
                CopyFieldValues(contextChoice);
            }

            if (GUILayout.Button("Paste"))
            {
                PasteFieldValues(contextChoice);
            }

            if (GUILayout.Button("Reset"))
            {
                ResetFieldValues(contextChoice);
            }

            GUI.backgroundColor = oldBgColor;
        }

        private void DrawButton(Color color, System.Action action, string text)
        {
            Color oldBgColor = GUI.backgroundColor;
            GUI.backgroundColor = color;

            if (GUILayout.Button(text, GUILayout.Width(20f), GUILayout.Height(18f)))
            {
                action();
            }

            GUI.backgroundColor = oldBgColor;
        }

        private static void DrawSeparator(int thickness = 1, int padding = 10)
        {
            Rect r = EditorGUILayout.GetControlRect(GUILayout.Height(thickness + padding));
            r.height = thickness;
            r.y += padding / 2f;
            r.x -= 2;
            r.width += 6;
            ColorUtility.TryParseHtmlString(EditorGUIUtility.isProSkin ? "#595959" : "#858585", out Color lineColor);
            EditorGUI.DrawRect(r, lineColor);
        }

        private void ChangeGlobalScale(Transform transform, Vector3 globalScale)
        {
            transform.localScale = Vector3.one;
            transform.localScale = new Vector3(
                globalScale.x / transform.lossyScale.x,
                globalScale.y / transform.lossyScale.y,
                globalScale.z / transform.lossyScale.z
            );
        }

        private Vector3 RoundVector3(Vector3 vector)
        {
            vector.x = Mathf.Round(vector.x * 10000f) * 0.0001f;
            vector.y = Mathf.Round(vector.y * 10000f) * 0.0001f;
            vector.z = Mathf.Round(vector.z * 10000f) * 0.0001f;
            return vector;
        }

        private void ResetFieldValues(int contextChoice)
        {
            serializedObject.Update();
            Undo.RecordObjects(targets, "Transform");
            switch (contextChoice)
            {
                case 1:
                    m_LocalPosition.vector3Value = Vector3.zero;
                    break;
                case 2:
                    m_LocalRotation.quaternionValue = Quaternion.identity;
                    break;
                case 3:
                    m_LocalScale.vector3Value = Vector3.one;
                    break;
                case 4:
                    foreach (var targetObject in targets)
                    {
                        ((Transform)targetObject).position = Vector3.zero;
                    }
                    break;
                case 5:
                    foreach (var targetObject in targets)
                    {
                        ((Transform)targetObject).rotation = Quaternion.identity;
                    }
                    break;
                case 6:
                    foreach (var targetObject in targets)
                    {
                        ChangeGlobalScale((Transform)targetObject, Vector3.one);
                    }
                    break;
                case 7:
                    m_LocalPosition.vector3Value = Vector3.zero;
                    m_LocalRotation.quaternionValue = Quaternion.identity;
                    m_LocalScale.vector3Value = Vector3.one;
                    break;
                case 8:
                    foreach (var targetObject in targets)
                    {
                        ((Transform)targetObject).position = Vector3.zero;
                        ((Transform)targetObject).rotation = Quaternion.identity;
                        ChangeGlobalScale((Transform)targetObject, Vector3.one);
                    }
                    break;
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void CopyFieldValues(int contextChoice)
        {
            for (int i = 0; i < hasCopiedValues.Length; i++) { hasCopiedValues[i] = false; }

            switch (contextChoice)
            {
                case 1:
                    copiedValues[0] = m_LocalPosition.vector3Value;
                    hasCopiedValues[0] = true;
                    break;
                case 2:
                    copiedValues[1] = m_LocalRotation.quaternionValue.eulerAngles;
                    hasCopiedValues[1] = true;
                    break;
                case 3:
                    copiedValues[2] = m_LocalScale.vector3Value;
                    hasCopiedValues[2] = true;
                    break;
                case 4:
                    copiedValues[0] = ((Transform)target).position;
                    hasCopiedValues[0] = true;
                    break;
                case 5:
                    copiedValues[1] = ((Transform)target).rotation.eulerAngles;
                    hasCopiedValues[1] = true;
                    break;
                case 6:
                    copiedValues[2] = ((Transform)target).lossyScale;
                    hasCopiedValues[2] = true;
                    break;
                case 7:
                    copiedValues[0] = m_LocalPosition.vector3Value;
                    hasCopiedValues[0] = true;
                    copiedValues[1] = m_LocalRotation.quaternionValue.eulerAngles;
                    hasCopiedValues[1] = true;
                    copiedValues[2] = m_LocalScale.vector3Value;
                    hasCopiedValues[2] = true;
                    break;
                case 8:
                    copiedValues[0] = ((Transform)target).position;
                    hasCopiedValues[0] = true;
                    copiedValues[1] = ((Transform)target).rotation.eulerAngles;
                    hasCopiedValues[1] = true;
                    copiedValues[2] = ((Transform)target).lossyScale;
                    hasCopiedValues[2] = true;
                    break;
            }
        }

        private void PasteFieldValues(int contextChoice)
        {
            serializedObject.Update();
            Undo.RecordObjects(targets, "Transform");
            switch (contextChoice)
            {
                case 1:
                    if (hasCopiedValues[0] == true)
                    {
                        m_LocalPosition.vector3Value = RoundVector3(copiedValues[0]);

                    }
                    break;
                case 2:
                    if (hasCopiedValues[1] == true)
                    {
                        Quaternion tempQuaternion1 = Quaternion.Euler(RoundVector3(copiedValues[1]));
                        m_LocalRotation.quaternionValue = tempQuaternion1;
                    }
                    break;
                case 3:
                    if (hasCopiedValues[2] == true)
                    {
                        m_LocalScale.vector3Value = RoundVector3(copiedValues[2]);
                    }
                    break;
                case 4:
                    if (hasCopiedValues[0] == true)
                    {
                        foreach (var targetObject in targets)
                        {
                            ((Transform)targetObject).position = RoundVector3(copiedValues[0]);
                        }
                    }
                    break;
                case 5:
                    if (hasCopiedValues[1] == true)
                    {
                        Quaternion tempQuaternion2 = Quaternion.Euler(RoundVector3(copiedValues[1]));
                        foreach (var targetObject in targets)
                        {
                            ((Transform)targetObject).rotation = tempQuaternion2;
                        }
                    }
                    break;
                case 6:
                    if (hasCopiedValues[2] == true)
                    {
                        foreach (var targetObject in targets)
                        {
                            ChangeGlobalScale((Transform)targetObject, RoundVector3(copiedValues[2]));
                        }
                    }
                    break;
                case 7:
                    if (hasCopiedValues[0] == true)
                    {
                        m_LocalPosition.vector3Value = RoundVector3(copiedValues[0]);
                    }
                    if (hasCopiedValues[1] == true)
                    {
                        m_LocalRotation.quaternionValue = Quaternion.Euler(RoundVector3(copiedValues[1]));
                    }
                    if (hasCopiedValues[2] == true)
                    {
                        m_LocalScale.vector3Value = RoundVector3(copiedValues[2]);
                    }
                    break;
                case 8:
                    foreach (var targetObject in targets)
                    {
                        if (hasCopiedValues[0] == true)
                        {
                            ((Transform)targetObject).position = RoundVector3(copiedValues[0]);
                        }
                        if (hasCopiedValues[1] == true)
                        {
                            ((Transform)targetObject).rotation = Quaternion.Euler(RoundVector3(copiedValues[1]));
                        }
                        if (hasCopiedValues[2] == true)
                        {
                            ChangeGlobalScale((Transform)targetObject, RoundVector3(copiedValues[2]));
                        }
                    }
                    break;
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}

#endif