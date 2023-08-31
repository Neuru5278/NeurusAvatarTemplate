using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace UnityToolbarExtender.Neuru
{
    [InitializeOnLoad]
    public static class QuickPlay
    {
        #region ToolbarGUI
        // Toggle states for the custom toolbar buttons
        static bool quickToggle = EditorPrefs.GetBool("QuickToggle", false);
        static bool simpleToggle = EditorPrefs.GetBool("SimpleToggle", false);
        static bool sceneToggle = EditorPrefs.GetBool("SceneToggle", false);

        static QuickPlay()
        {
            // Add the custom toolbar buttons to the left and right side of the Unity toolbar
            ToolbarExtender.LeftToolbarGUI.Add(OnToolbarGUILeft);
            ToolbarExtender.RightToolbarGUI.Add(OnToolbarGUIRight);

            // Subscribe to the play mode state change event to handle entering and exiting play mode
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.pauseStateChanged += OnPauseStateChanged;

        }

        // Custom GUI for the toolbar buttons
        private static void OnToolbarGUILeft()
        {
            // Ensure GUI changed is reset to false
            GUI.changed = false;
            // Quick Toggle: Enter Play Mode without reloading domain and scene
            GUILayout.FlexibleSpace();
            quickToggle = GUILayout.Toggle(quickToggle, new GUIContent("Quick", "Quickly enter Play Mode without reloading domain and scene"), QuickPlayButtonStyle);
            if (GUI.changed)
            {
                // Save the state of the Quick Toggle in EditorPrefs
                EditorPrefs.SetBool("QuickToggle", quickToggle);
            }

            // Simple Toggle: Enter Play Mode without any "Apply On Play" features
            GUI.changed = false;
            simpleToggle = GUILayout.Toggle(simpleToggle, new GUIContent("Simple", "Enter Play Mode without any \"Apply On Play\" features"),QuickPlayButtonStyle);
            if (GUI.changed)
            {
                // Save the state of the Simple Toggle in EditorPrefs
                EditorPrefs.SetBool("SimpleToggle", simpleToggle);
            }
        }

        static void OnToolbarGUIRight()
        {
            GUI.changed = false;
            sceneToggle = GUILayout.Toggle(sceneToggle, new GUIContent("Scene", "Focus SceneView when entering play mode"), QuickPlayButtonStyle);
            if (GUI.changed)
            {
                EditorPrefs.SetBool("SceneToggle", sceneToggle);
            }
        }
        #endregion

        #region GUIStyle

        private static GUIStyle quickPlayButtonStyle;
        private static GUIStyleState normalStyleState;
        private static GUIStyleState onNormalStyleState;
        private static GUIStyle QuickPlayButtonStyle
        {
            get
            {
                if (quickPlayButtonStyle == null)
                {
                    normalStyleState = new GUIStyleState()
                    {
                        background = MakeTex(2, 2, new Color32(56, 56, 56, 255)),
                        textColor = new Color32(192, 192, 192, 255)
                    };
                    onNormalStyleState = new GUIStyleState()
                    {
                        background = MakeTex(2, 2, new Color32(35, 74, 108, 255)),
                        textColor = new Color32(192, 192, 192, 255)
                    };
                    quickPlayButtonStyle = new GUIStyle("Command")
                    {
                        normal = normalStyleState,
                        onNormal = onNormalStyleState,
                        fontSize = 12,
                        alignment = TextAnchor.MiddleCenter,
                        imagePosition = ImagePosition.TextOnly,
                        fontStyle = FontStyle.Bold,
                        fixedWidth = 50
                    };
                }
                else if (normalStyleState.background == null && onNormalStyleState.background == null)
                {
                    normalStyleState = new GUIStyleState()
                    {
                        background = MakeTex(2, 2, new Color32(56, 56, 56, 255)),
                        textColor = new Color32(192, 192, 192, 255)
                    };
                    onNormalStyleState = new GUIStyleState()
                    {
                        background = MakeTex(2, 2, new Color32(35, 74, 108, 255)),
                        textColor = new Color32(192, 192, 192, 255)
                    };
                    quickPlayButtonStyle.normal = normalStyleState;
                    quickPlayButtonStyle.onNormal = onNormalStyleState;
                }

                return quickPlayButtonStyle;
            }
        }

        private static Texture2D MakeTex(int width, int height, Color color)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; ++i)
            {
                pix[i] = color;
            }
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }
        #endregion

        #region PlayModeMethods
        // Enter Play Mode with the options for quick play
        private static void QuickPlayMode()
        {
            if (Application.isPlaying) return;
            SaveAndSetPlayModeOptions();
        }

        // Enter Play Mode with the options for simple play
        private static void SimplePlayMode()
        {
            if (Application.isPlaying) return;
            SaveAndSetPrefs();
            SaveAndSetAOPrefs();
        }
        private static void PlayModeSceneViewFocuser()
        {
            if (sceneToggle)
            {
                EditorWindow.FocusWindowIfItsOpen<SceneView>();
            }
        }
        #endregion

        #region PlayModeAndPauseStateChanged
        // Event handler for the play mode state change event
        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            switch (state)
            {
                // Exiting Edit Mode: Entering Play Mode
                case PlayModeStateChange.ExitingEditMode:
                    if (quickToggle)
                    {
                        // Enter Play Mode with quick options
                        QuickPlayMode();
                    }
                    if (simpleToggle)
                    {
                        // Enter Play Mode with simple options
                        SimplePlayMode();
                    }
                    break;
                // Entered Play Mode: Exiting Play Mode
                case PlayModeStateChange.EnteredPlayMode:
                    // Restore the original play mode options and preferences
                    RestorePlayModeOptions();
                    RestorePrefs();
                    RestoreAOPrefs();
                    PlayModeSceneViewFocuser();
                    break;
            }
        }
        static void OnPauseStateChanged(PauseState state)
        {
            if (sceneToggle && state == PauseState.Unpaused)
            {
                // Not sure why, but this must be delayed
                EditorApplication.delayCall += EditorWindow.FocusWindowIfItsOpen<SceneView>;
            }
        }
        #endregion

        #region PlayModeOptionsMethods
        // Original play mode options and flag to track if options are saved
        private static EnterPlayModeOptions originalPlayModeOptions;
        private static bool optionsSaved = false;

        // Save and set the play mode options for quick play
        private static void SaveAndSetPlayModeOptions()
        {
            originalPlayModeOptions = EditorSettings.enterPlayModeOptions;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload;
            optionsSaved = true;
        }

        // Restore the original play mode options
        private static void RestorePlayModeOptions()
        {
            if (optionsSaved)
            {
                EditorSettings.enterPlayModeOptions = originalPlayModeOptions;
                optionsSaved = false;
            }
        }
        #endregion

        #region PrefsMethods
        // Original preferences and keys to save and restore
        private static bool[] originalPrefsBool;
        private static readonly string[] prefsKey = new string[]
        {
            "com.vrcfury.playMode",
            "nadena.dev.modular-avatar.applyOnPlay"
            // Add more keys here if needed
        };
        private static bool arePrefsSaved = false;

        // Save and set the preferences for simple play
        private static void SaveAndSetPrefs()
        {
            originalPrefsBool = new bool[prefsKey.Length];
            for (int i = 0; i < prefsKey.Length; i++)
            {
                originalPrefsBool[i] = EditorPrefs.GetBool(prefsKey[i], false);
                EditorPrefs.SetBool($"OriginalPrefsKey{i}", originalPrefsBool[i]);
                EditorPrefs.SetBool(prefsKey[i], false);
            }
            arePrefsSaved = true;
            EditorPrefs.SetBool("ArePrefsSaved", arePrefsSaved);
        }

        // Restore the original preferences
        private static void RestorePrefs()
        {
            originalPrefsBool = new bool[prefsKey.Length];
            arePrefsSaved = EditorPrefs.GetBool("ArePrefsSaved", false);
            if (arePrefsSaved)
            {
                for (int i = 0; i < prefsKey.Length; i++)
                {
                    originalPrefsBool[i] = EditorPrefs.GetBool($"OriginalPrefsKey{i}", true);
                    EditorPrefs.SetBool(prefsKey[i], originalPrefsBool[i]);
                }
                originalPrefsBool = null;
                arePrefsSaved = false;
                EditorPrefs.SetBool("ArePrefsSaved", arePrefsSaved);
            }
        }
        #endregion

        #region AOPrefsMethods
        // Original preferences for "Apply On Play" and flag to track if preferences are saved
        private static bool[] originalAOPrefsBool;
        private static bool areAOPrefsSaved = false;

        // Save and set the "Apply On Play" preferences for simple play
        private static void SaveAndSetAOPrefs()
        {
            FindAOAssembly();
            GetApplyOnPlayCallbacks();
            if (callbacks != null)
            {
                originalAOPrefsBool = new bool[callbacks.Length];
                for (int i = 0; i < originalAOPrefsBool.Length; i++)
                {
                    originalAOPrefsBool[i] = GetApplyOnPlayCallbackState(i);
                    EditorPrefs.SetBool($"OriginalAOPrefsKey{i}", originalAOPrefsBool[i]);
                    SetApplyOnPlayCallbackState(i, false);
                }
                areAOPrefsSaved = true;
                EditorPrefs.SetBool("AreAOPrefsSaved", areAOPrefsSaved);
            }
            else
            {
                Debug.Log("callbacks is null.");
            }
        }

        // Restore the original "Apply On Play" preferences
        private static void RestoreAOPrefs()
        {
            FindAOAssembly();
            GetApplyOnPlayCallbacks();
            originalAOPrefsBool = new bool[callbacks.Length];
            areAOPrefsSaved = EditorPrefs.GetBool("AreAOPrefsSaved", false);
            if (areAOPrefsSaved && callbacks != null)
            {
                for (int i = 0; i < callbacks.Length; i++)
                {
                    originalAOPrefsBool[i] = EditorPrefs.GetBool($"OriginalAOPrefsKey{i}", true);
                    SetApplyOnPlayCallbackState(i, originalAOPrefsBool[i]);
                }
                originalAOPrefsBool = null;
                areAOPrefsSaved = false;
                EditorPrefs.SetBool("AreAOPrefsSaved", areAOPrefsSaved);
            }
        }
        #endregion

        #region AOAssemblyMethods
        // Assembly and types related to "Apply On Play" feature
        private static Assembly assemblyAO;
        private static Type applyOnPlayConfigType;
        private static (string, string)[] callbacks;

        // Find the assembly containing "Apply On Play" feature
        private static void FindAOAssembly()
        {
            if (assemblyAO == null)
            {
                Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
                foreach (Assembly assembly in assemblies)
                {
                    if (assembly.FullName.StartsWith("com.anatawa12.avatar-optimizer.internal.apply-on-play.editor"))
                    {
                        assemblyAO = assembly;
                        Debug.Log("Anatawa12's Avatar Optimizer found.");
                        return;
                    }
                }
                Debug.Log("Anatawa12's Avatar Optimizer not found.");
            }
        }

        // Get the callbacks for "Apply On Play" feature
        private static void GetApplyOnPlayCallbacks()
        {
            if (callbacks == null && assemblyAO != null)
            {
                applyOnPlayConfigType = assemblyAO.GetType("Anatawa12.ApplyOnPlay.ApplyOnPlayConfiguration");
                if (applyOnPlayConfigType != null)
                {
                    var applyOnPlayConfig = ScriptableObject.CreateInstance(applyOnPlayConfigType) as EditorWindow;
                    var callbacksField = applyOnPlayConfigType.GetField("_callbacks", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (callbacksField != null)
                    {
                        callbacks = callbacksField.GetValue(applyOnPlayConfig) as (string, string)[];
                    }
                }
                Debug.Log("GetApplyOnPlayCallbacks - callbacks: " + (callbacks == null ? "null" : callbacks.Length.ToString()));
            }
        }

        // Get the state of "Apply On Play" callback at the specified index
        private static bool GetApplyOnPlayCallbackState(int index)
        {
            if (callbacks != null && index >= 0 && index < callbacks.Length)
            {
                string callbackId = callbacks[index].Item1;
                return EditorPrefs.GetBool("com.anatawa12.apply-on-play.enabled." + callbackId, false);
            }
            return false;
        }

        // Set the state of "Apply On Play" callback at the specified index
        private static void SetApplyOnPlayCallbackState(int index, bool state)
        {
            if (callbacks != null && index >= 0 && index < callbacks.Length)
            {
                string callbackId = callbacks[index].Item1;
                EditorPrefs.SetBool("com.anatawa12.apply-on-play.enabled." + callbackId, state);
            }
        }
        #endregion
    }
}
