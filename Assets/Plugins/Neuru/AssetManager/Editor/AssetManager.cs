using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Presets;
using UnityEngine;
using Object = UnityEngine.Object;

//This work, "AssetManager", is forked from "AssetOrganizer" by Dreadrith, used under MIT Licsence. "AssetManager" is licensed under MIT Licsence by Neuru5278.

//Made by Dreadrith#3238
//Discord: https://discord.gg/ZsPfrGn
//Github: https://github.com/Dreadrith/DreadScripts

//Forked by Neuru#3455 or neuru5278
//Github : https://github.com/Neuru5278/AssetManager

namespace Neuru.AssetManager
{
    public class AssetManager : EditorWindow
    {
        #region Declarations
        #region Constants
        /// <summary>
        /// YAML 형식의 Encoding
        /// </summary>
        private static Encoding Encoding { get { return Encoding.GetEncoding("UTF-8"); } }
        private const string PrefsKey = "AvatarAssetManagerSettings";
        private static readonly ManageType[] ManageTypes =
        {
            new ManageType(0, "Animation", typeof(AnimationClip), typeof(BlendTree)),
            new ManageType(1, "Controller", typeof(AnimatorController), typeof(AnimatorOverrideController)),
            new ManageType(2, "Texture", typeof(Texture)),
            new ManageType(3, "Material", typeof(Material)),
            new ManageType(4, "Model", new string[] {".fbx",".obj", ".dae", ".3ds", ".dxf"}, typeof(Mesh)),
            new ManageType(5, "Prefab", new string[] {".prefab"}, typeof(GameObject)),
            new ManageType(6, "Audio", typeof(AudioClip)),
            new ManageType(7, "Mask", typeof(AvatarMask)),
            new ManageType(8, "Scene", typeof(SceneAsset)),
            new ManageType(9, "Preset", typeof(Preset)),
            new ManageType(10, "VRC", "VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters, VRCSDK3A","VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu, VRCSDK3A"),
            new ManageType(11, "Shader", typeof(Shader)),
            new ManageType(12, "Script", new string[] {".dll"}, typeof(MonoScript)),
            new ManageType(13, "Other", typeof(ScriptableObject))
        };
        private static readonly string[] mainTabs = { "Manager", "Options" };
        private static readonly string[] optionTabs = { "Folders", "Types" };


        private enum ManageAction
        {
            Skip,
            Move,
            Copy
        }

        private enum SortOptions
        {
            AlphabeticalPath,
            AlphabeticalAsset,
            AssetType
        }
        #endregion
        #region Automated Variables
        private static int mainToolbarIndex;
        private static int optionsToolbarIndex;
        private static DependencyAsset[] assets;
        private static List<string> createdFolders = new List<string>();
        private Vector2 scrollview;
        #endregion
        #region Input
        private static Object mainAsset;
        private static string destinationPath;
        private static string copySuffix;

        [SerializeField] private List<CustomFolder> specialFolders;
        [SerializeField] private ManageAction[] typeActions;
        [SerializeField] private SortOptions sortByOption;
        [SerializeField] private bool deleteEmptyFolders = true;
        #endregion
        #endregion

        #region Methods
        #region Main Methods
        private void OnGUI()
        {
            scrollview = EditorGUILayout.BeginScrollView(scrollview);
            mainToolbarIndex = GUILayout.Toolbar(mainToolbarIndex, mainTabs);

            switch (mainToolbarIndex)
            {
                case 0:
                    DrawManagerTab();
                    break;
                case 1:
                    DrawOptionsTab();
                    break;
            }


            DrawSeparator();
            Credit();
            EditorGUILayout.EndScrollView();
        }
        private void GetDependencyAssets()
        {
            destinationPath = AssetDatabase.GetAssetPath(mainAsset);
            bool isFolder = AssetDatabase.IsValidFolder(destinationPath);
            string[] assetsPath = isFolder ? GetAssetPathsInFolder(destinationPath).ToArray() : AssetDatabase.GetDependencies(destinationPath);
            assets = assetsPath.Select(p => new DependencyAsset(p)).ToArray();

            if (!isFolder) destinationPath = destinationPath.Replace('\\', '/').Substring(0, destinationPath.LastIndexOf('/'));

            foreach (var a in assets)
            {
                string[] subFolders = a.path.Split('/');

                bool setByFolder = false;
                foreach (var f in specialFolders)
                {
                    if (!f.active) continue;
                    if (subFolders.All(s => s != f.name)) continue;

                    a.action = f.action;
                    setByFolder = true;
                    break;

                }

                if (setByFolder) continue;

                if (!TrySetAction(a))
                    a.associatedType = ManageTypes.Last();
            }

            switch (sortByOption)
            {
                case SortOptions.AlphabeticalPath:
                    assets = assets.OrderBy(a => a.path).ToArray();
                    break;
                case SortOptions.AlphabeticalAsset:
                    assets = assets.OrderBy(a => a.asset.name).ToArray();
                    break;
                case SortOptions.AssetType:
                    assets = assets.OrderBy(a => a.type.Name).ToArray();
                    break;
            }

        }
        private void Organize()
        {
            CheckFolders();
            List<string> affectedFolders = new List<string>();
            var assetPathMap = new Dictionary<string, string>();
            var assetPathGUIDMap = new Dictionary<string, string>();
            var GUIDMap = new Dictionary<string, string>();
            try
            {
                AssetDatabase.StartAssetEditing();
                int count = assets.Length;
                float progressPerAsset = 1f / count;
                for (var i = 0; i < count; i++)
                {
                    EditorUtility.DisplayProgressBar("Organizing", $"Organizing Assets ({i + 1}/{count})", (i + 1) * progressPerAsset);
                    var a = assets[i];
                    string newPath = AssetDatabase.GenerateUniqueAssetPath($"{destinationPath}/{a.associatedType.name}/{Path.GetFileName(a.path)}");
                    string copyPath = AssetDatabase.GenerateUniqueAssetPath($"{destinationPath}/{a.associatedType.name}/{Path.GetFileNameWithoutExtension(a.path)}{copySuffix}{Path.GetExtension(a.path)}");
                    switch (a.action)
                    {
                        default: case ManageAction.Skip: continue;
                        case ManageAction.Move:
                            AssetDatabase.MoveAsset(a.path, newPath);
                            affectedFolders.Add(Path.GetDirectoryName(a.path));
                            break;
                        case ManageAction.Copy:
                            AssetDatabase.CopyAsset(a.path, copyPath);
                            assetPathMap.Add(a.path, copyPath);
                            assetPathGUIDMap.Add(a.path, a.guid);
                            break;
                    }
                }

            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.StopAssetEditing();
            }
            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (var kvp in assetPathMap)
                {
                    var assetPath = kvp.Key;
                    var copyPath = kvp.Value;
                    GUIDMap.Add(AssetDatabase.AssetPathToGUID(assetPath), AssetDatabase.AssetPathToGUID(copyPath));
                }
                foreach (var kvp in assetPathMap)
                {
                    var assetPath = kvp.Key;
                    var copyPath = kvp.Value;

                    // YAML형식의 경우만 참조처의 GUID 재작성 처리
                    using (StreamReader sr = new StreamReader(assetPath, Encoding))
                    {
                        string s = sr.ReadToEnd();
                        if (s.StartsWith("%YAML"))
                        {
                            foreach (var originalAssetPath in assetPathMap.Keys)
                            {
                                var originalAssetGUID = assetPathGUIDMap[originalAssetPath];
                                var copyAssetGUID = GUIDMap[originalAssetGUID];
                                s = s.Replace(originalAssetGUID, copyAssetGUID);
                            }

                            Debug.Log(string.Format("[Replace Dependencies] {0}", copyPath));
                            using (StreamWriter sw = new StreamWriter(copyPath, false, Encoding))
                            {
                                sw.Write(s);
                            }
                        }
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
            try
            {
                AssetDatabase.StartAssetEditing();

                foreach (var folderPath in createdFolders.Concat(affectedFolders).Distinct().Where(DirectoryIsEmpty))
                    AssetDatabase.DeleteAsset(folderPath);
            }
            finally { AssetDatabase.StopAssetEditing(); }
            AssetDatabase.Refresh();
            EditorGUIUtility.PingObject(AssetDatabase.LoadMainAssetAtPath(destinationPath));

            assets = null;
            destinationPath = null;
        }
        private void Work()
        {
            string dirMainAsset = AssetDatabase.GetAssetPath(mainAsset);
            CheckFolders2(dirMainAsset);
            List<string> affectedFolders = new List<string>();
            var assetPathMap = new Dictionary<string, string>();
            var assetPathGUIDMap = new Dictionary<string, string>();
            var GUIDMap = new Dictionary<string, string>();
            try
            {
                AssetDatabase.StartAssetEditing();
                int count = assets.Length;
                float progressPerAsset = 1f / count;
                for (var i = 0; i < count; i++)
                {
                    EditorUtility.DisplayProgressBar("working", $"working Assets ({i + 1}/{count})", (i + 1) * progressPerAsset);
                    var a = assets[i];
                    string dirA = Path.GetDirectoryName(a.path).Replace('\\', '/');
                    string dirMiddle = dirA.Replace(dirMainAsset, "");
                    Debug.Log(mainAsset.name);
                    string newPath = AssetDatabase.GenerateUniqueAssetPath($"{destinationPath}/{dirMiddle}/{Path.GetFileName(a.path)}");
                    string copyPath = AssetDatabase.GenerateUniqueAssetPath($"{destinationPath}/{dirMiddle}/{Path.GetFileNameWithoutExtension(a.path)}{copySuffix}{Path.GetExtension(a.path)}");
                    switch (a.action)
                    {
                        default: case ManageAction.Skip: continue;
                        case ManageAction.Move:
                            AssetDatabase.MoveAsset(a.path, newPath);
                            affectedFolders.Add(Path.GetDirectoryName(a.path));
                            break;
                        case ManageAction.Copy:
                            AssetDatabase.CopyAsset(a.path, copyPath);
                            assetPathMap.Add(a.path, copyPath);
                            assetPathGUIDMap.Add(a.path, a.guid);
                            break;
                    }
                }

            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.StopAssetEditing();
            }
            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (var kvp in assetPathMap)
                {
                    var assetPath = kvp.Key;
                    var copyPath = kvp.Value;
                    GUIDMap.Add(AssetDatabase.AssetPathToGUID(assetPath), AssetDatabase.AssetPathToGUID(copyPath));
                }
                foreach (var kvp in assetPathMap)
                {
                    var assetPath = kvp.Key;
                    var copyPath = kvp.Value;

                    // YAML형식의 경우만 참조처의 GUID 재작성 처리
                    using (StreamReader sr = new StreamReader(assetPath, Encoding))
                    {
                        string s = sr.ReadToEnd();
                        if (s.StartsWith("%YAML"))
                        {
                            foreach (var originalAssetPath in assetPathMap.Keys)
                            {
                                var originalAssetGUID = assetPathGUIDMap[originalAssetPath];
                                var copyAssetGUID = GUIDMap[originalAssetGUID];
                                s = s.Replace(originalAssetGUID, copyAssetGUID);
                            }

                            Debug.Log(string.Format("[Replace Dependencies] {0}", copyPath));
                            using (StreamWriter sw = new StreamWriter(copyPath, false, Encoding))
                            {
                                sw.Write(s);
                            }
                        }
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
            try
            {
                AssetDatabase.StartAssetEditing();

                foreach (var folderPath in createdFolders.Concat(affectedFolders).Distinct().Where(DirectoryIsEmpty))
                    AssetDatabase.DeleteAsset(folderPath);
            }
            finally { AssetDatabase.StopAssetEditing(); }
            AssetDatabase.Refresh();
            EditorGUIUtility.PingObject(AssetDatabase.LoadMainAssetAtPath(destinationPath));

            assets = null;
            destinationPath = null;
        }
        #endregion
        #region GUI Methods

        private void DrawManagerTab()
        {
            GUIStyle boxStyle = GUI.skin.GetStyle("box");

            using (new GUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                mainAsset = EditorGUILayout.ObjectField("Main Asset", mainAsset, typeof(Object), false);
                if (EditorGUI.EndChangeCheck())
                {
                    if (mainAsset)
                    {
                        destinationPath = AssetDatabase.GetAssetPath(mainAsset);
                        if (!AssetDatabase.IsValidFolder(destinationPath)) destinationPath = Path.GetDirectoryName(destinationPath).Replace('\\', '/');
                    }
                    assets = null;
                }

                using (new EditorGUI.DisabledScope(!mainAsset))
                    if (GUILayout.Button("Get Assets", GUILayout.Width(80)))
                        GetDependencyAssets();
            }

            using (new GUILayout.HorizontalScope())
            {
                copySuffix = EditorGUILayout.TextField("Copy Suffix", copySuffix);
            }

            destinationPath = AssetFolderPath(destinationPath, "Destination Folder");

            if (assets != null)
            {
                DrawSeparator(4, 20);

                var h = EditorGUIUtility.singleLineHeight;
                var squareOptions = new GUILayoutOption[] { GUILayout.Width(h), GUILayout.Height(h) };
                foreach (var a in assets)
                {
                    using (new GUILayout.HorizontalScope(boxStyle))
                    {
                        GUILayout.Label(a.icon, squareOptions);
                        if (GUILayout.Button($"| {a.path}", GUI.skin.label))
                            EditorGUIUtility.PingObject(a.asset);

                        a.action = (ManageAction)EditorGUILayout.EnumPopup(a.action, GUILayout.Width(60));
                    }

                }
                using (new GUILayout.VerticalScope("helpbox"))
                {
                    using (new GUILayout.HorizontalScope())
                    {
                        using (new GUILayout.HorizontalScope("helpbox"))
                        {
                            if (GUILayout.Button("Move => Copy"))
                            {
                                foreach (var a in assets)
                                {
                                    if (a.action == ManageAction.Move)
                                        a.action = ManageAction.Copy;
                                }
                            }
                        }
                        using (new GUILayout.HorizontalScope("helpbox"))
                        {
                            if (GUILayout.Button("Copy => Move"))
                            {
                                foreach (var a in assets)
                                {
                                    if (a.action == ManageAction.Copy)
                                        a.action = ManageAction.Move;
                                }
                            }
                        }
                    }
                    DrawSeparator(0, 2);
                    if (GUILayout.Button("Work with Organization"))
                        Organize();
                    bool isFolder = AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(mainAsset));
                    if (isFolder)
                    {
                        DrawSeparator(0, 2);
                        if (GUILayout.Button("Work As Is"))
                            Work();
                    }
                }
            }
        }

        private void DrawOptionsTab()
        {
            optionsToolbarIndex = GUILayout.Toolbar(optionsToolbarIndex, optionTabs);
            switch (optionsToolbarIndex)
            {
                case 0:
                    DrawFolderOptions();
                    DrawSeparator();
                    using (new GUILayout.VerticalScope("helpbox"))
                    {
                        using (new GUILayout.HorizontalScope())
                        {
                            using (new GUILayout.HorizontalScope("helpbox"))
                            {
                                if (GUILayout.Button("Move => Copy"))
                                {
                                    foreach (var f in specialFolders)
                                    {
                                        if (f.action == ManageAction.Move)
                                            f.action = ManageAction.Copy;
                                    }
                                }
                            }
                            using (new GUILayout.HorizontalScope("helpbox"))
                            {
                                if (GUILayout.Button("Copy => Move"))
                                {
                                    foreach (var f in specialFolders)
                                    {
                                        if (f.action == ManageAction.Copy)
                                            f.action = ManageAction.Move;
                                    }
                                }
                            }
                        }
                        DrawSeparator(0, 2);
                        using (new GUILayout.HorizontalScope())
                        {
                            deleteEmptyFolders = EditorGUILayout.Toggle(new GUIContent("Delete Empty Folders", "After moving assets, delete source folders if they're empty"), deleteEmptyFolders);
                            sortByOption = (SortOptions)EditorGUILayout.EnumPopup("Sort Search By", sortByOption);
                        }
                    }
                    break;
                case 1:
                    DrawTypeOptions();
                    DrawSeparator();
                    using (new GUILayout.VerticalScope("helpbox"))
                    {
                        using (new GUILayout.HorizontalScope())
                        {
                            using (new GUILayout.HorizontalScope("helpbox"))
                            {
                                if (GUILayout.Button("Move => Copy"))
                                {
                                    for (int i = 0; i < ManageTypes.Length; i++)
                                    {
                                        if (typeActions[ManageTypes[i].actionIndex] == ManageAction.Move)
                                        {
                                            typeActions[ManageTypes[i].actionIndex] = ManageAction.Copy;
                                        }
                                    }
                                }
                            }
                            using (new GUILayout.HorizontalScope("helpbox"))
                            {
                                if (GUILayout.Button("Copy => Move"))
                                {
                                    for (int i = 0; i < ManageTypes.Length; i++)
                                    {
                                        if (typeActions[ManageTypes[i].actionIndex] == ManageAction.Copy)
                                        {
                                            typeActions[ManageTypes[i].actionIndex] = ManageAction.Move;
                                        }
                                    }
                                }
                            }
                        }
                        DrawSeparator(0, 2);
                        using (new GUILayout.HorizontalScope())
                        {
                            deleteEmptyFolders = EditorGUILayout.Toggle(new GUIContent("Delete Empty Folders", "After moving assets, delete source folders if they're empty"), deleteEmptyFolders);
                            sortByOption = (SortOptions)EditorGUILayout.EnumPopup("Sort Search By", sortByOption);
                        }
                    }
                    break;
            }
        }

        private void DrawFolderOptions()
        {
            for (var i = 0; i < specialFolders.Count; i++)
            {
                var f = specialFolders[i];
                using (new GUILayout.HorizontalScope("helpbox"))
                {
                    using (new BGColoredScope(Color.green, Color.grey, f.active))
                        f.active = GUILayout.Toggle(f.active, f.active ? "Enabled" : "Disabled", GUI.skin.button, GUILayout.Width(100), GUILayout.Height(18));
                    using (new EditorGUI.DisabledScope(!f.active))
                    {
                        f.name = GUILayout.TextField(f.name);
                        f.action = (ManageAction)EditorGUILayout.EnumPopup(f.action, GUILayout.Width(60));
                        if (GUILayout.Button("X", GUILayout.Width(18), GUILayout.Height(18)))
                            specialFolders.RemoveAt(i);
                    }
                }
            }

            if (GUILayout.Button("Add"))
                specialFolders.Add(new CustomFolder());
        }

        private void DrawTypeOptions()
        {
            using (new GUILayout.HorizontalScope())
            {
                void DrawTypeGUI(ManageType t)
                {
                    var icon = GUIContent.none;
                    if (t.associatedTypes.Length > 0)
                        icon = new GUIContent(AssetPreview.GetMiniTypeThumbnail(t.associatedTypes[0]));

                    using (new GUILayout.HorizontalScope())
                    {
                        GUILayout.Label(icon, GUILayout.Height(18), GUILayout.Width(18));
                        GUILayout.Label($"| {t.name}");
                        if (TryGetTypeAction(t, out _))
                            typeActions[t.actionIndex] = (ManageAction)EditorGUILayout.EnumPopup(typeActions[t.actionIndex], GUILayout.Width(60));
                    }
                }

                using (new GUILayout.VerticalScope("helpbox"))
                {
                    for (int i = 0; i < ManageTypes.Length; i += 2)
                        DrawTypeGUI(ManageTypes[i]);
                }
                using (new GUILayout.VerticalScope("helpbox"))
                {
                    for (int i = 1; i < ManageTypes.Length; i += 2)
                        DrawTypeGUI(ManageTypes[i]);
                }
            }
        }

        private static string AssetFolderPath(string variable, string title)
        {
            using (new GUILayout.HorizontalScope())
            {
                EditorGUILayout.TextField(title, variable);

                if (GUILayout.Button("...", GUILayout.Width(30)))
                {
                    var dummyPath = EditorUtility.OpenFolderPanel(title, AssetDatabase.IsValidFolder(variable) ? variable : "Assets", string.Empty);
                    if (string.IsNullOrEmpty(dummyPath))
                        return variable;
                    string newPath = FileUtil.GetProjectRelativePath(dummyPath);

                    if (!newPath.StartsWith("Assets"))
                    {
                        Debug.LogWarning("New Path must be a folder within Assets!");
                        return variable;
                    }

                    variable = newPath;
                }
            }

            return variable;
        }

        private static void DrawSeparator(int thickness = 2, int padding = 10)
        {
            Rect r = EditorGUILayout.GetControlRect(GUILayout.Height(thickness + padding));
            r.height = thickness;
            r.y += padding / 2f;
            r.x -= 2;
            r.width += 6;
            ColorUtility.TryParseHtmlString(EditorGUIUtility.isProSkin ? "#595959" : "#858585", out Color lineColor);
            EditorGUI.DrawRect(r, lineColor);
        }
        #endregion

        #region Sub-Main Methods

        [MenuItem("Tools/Neuru/Asset Manager", false, 36)]
        private static void ShowWindow() => GetWindow<AssetManager>(false, "Asset Manager", true);
        private bool TrySetAction(DependencyAsset a)
        {
            for (int i = 0; i < ManageTypes.Length; i++)
            {
                if (!ManageTypes[i].IsAppliedTo(a)) continue;

                if (TryGetTypeAction(ManageTypes[i], out var action))
                {
                    a.action = action;
                    a.associatedType = ManageTypes[i];
                    return true;
                }

            }

            return false;
        }

        private bool TryGetTypeAction(ManageType type, out ManageAction action)
        {
            bool hasDoubleTried = false;
        TryAgain:
            try
            {
                action = typeActions[type.actionIndex];
                return true;
            }
            catch (Exception)
            {
                if (hasDoubleTried) throw;

                ManageAction[] newArray = new ManageAction[ManageTypes.Length];
                for (int j = 0; j < newArray.Length; j++)
                {
                    try { newArray[j] = typeActions[j]; }
                    catch { newArray[j] = ManageAction.Skip; }
                }

                Debug.LogWarning("Type Actions re-initialized due to a loading/serialization.");
                typeActions = newArray;
                hasDoubleTried = true;
                goto TryAgain;
            }
        }

        private static void CheckFolders()
        {
            if (!destinationPath.StartsWith("Assets/"))
                destinationPath = "Assets/" + destinationPath;
            ReadyPath(destinationPath);

            createdFolders.Clear();

            void CheckFolder(string name)
            {
                string path = $"{destinationPath}/{name}";
                if (ReadyPath(path)) createdFolders.Add(path);
            }

            try
            {
                AssetDatabase.StartAssetEditing();
                for (int i = 0; i < ManageTypes.Length; i++)
                    CheckFolder(ManageTypes[i].name);
            }
            finally { AssetDatabase.StopAssetEditing(); }
        }

        private static void CheckFolders2(string dirMainAsset)
        {
            if (!destinationPath.StartsWith("Assets/"))
                destinationPath = "Assets/" + destinationPath;
            ReadyPath(destinationPath);
            createdFolders.Clear();

            void CheckFolder2(string name)
            {
                string path = $"{destinationPath}/{name}";
                if (ReadyPath(path)) createdFolders.Add(path);
            }

            try
            {
                AssetDatabase.StartAssetEditing();
                int count = assets.Length;
                float progressPerAsset = 1f / count;
                for (var i = 0; i < count; i++)
                {
                    EditorUtility.DisplayProgressBar("checking", $"checking Assets ({i + 1}/{count})", (i + 1) * progressPerAsset);
                    var a = assets[i];
                    string dirA = Path.GetDirectoryName(a.path).Replace('\\', '/');
                    string dirMiddle = dirA.Replace(dirMainAsset, "");
                    CheckFolder2(dirMiddle);
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
        }
        public static void DeleteIfEmptyFolder(string folderPath)
        {
            if (!AssetDatabase.IsValidFolder(folderPath))
                folderPath = Path.GetDirectoryName(folderPath);
            while (DirectoryIsEmpty(folderPath) && folderPath != "Assets")
            {
                var parentDirectory = Path.GetDirectoryName(folderPath);
                FileUtil.DeleteFileOrDirectory(folderPath);
                FileUtil.DeleteFileOrDirectory(folderPath + ".meta");
                folderPath = parentDirectory;
            }
        }
        public static bool DirectoryIsEmpty(string path) => !Directory.EnumerateFileSystemEntries(path).Any();
        #endregion
        #region Automated Methods
        private void OnEnable()
        {
            string data = EditorPrefs.GetString(PrefsKey, JsonUtility.ToJson(this, false));
            JsonUtility.FromJsonOverwrite(data, this);
            if (!EditorPrefs.HasKey(PrefsKey))
            {
                //Default Folder based actions. Based on usual VRC assets.
                specialFolders = new List<CustomFolder>
                {
                    new CustomFolder("VRCSDK"),
                    new CustomFolder("Packages"),
                    new CustomFolder("Plugins"),
                    new CustomFolder("Editor")
                };

                //Default Type based Actions
                typeActions = new ManageAction[]
                {
                    ManageAction.Copy,
                    ManageAction.Copy,
                    ManageAction.Copy,
                    ManageAction.Copy,
                    ManageAction.Copy,
                    ManageAction.Copy,
                    ManageAction.Copy,
                    ManageAction.Copy,
                    ManageAction.Copy,
                    ManageAction.Skip,
                    ManageAction.Copy,
                    ManageAction.Skip,
                    ManageAction.Skip,
                    ManageAction.Skip,
                };
            }

            createdFolders = new List<string>();
        }

        private void OnDisable()
        {
            string data = JsonUtility.ToJson(this, false);
            EditorPrefs.SetString(PrefsKey, data);
        }
        #endregion
        #region Helper Methods
        private static void Credit()
        {
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Made By Dreadrith#3238", "boldlabel"))
                    Application.OpenURL("https://linktr.ee/Dreadrith");
            }
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                GUILayout.Button("Modified By Neuru#3455", "boldlabel");
            }
        }

        private static bool ReadyPath(string folderPath)
        {
            if (Directory.Exists(folderPath)) return false;

            Directory.CreateDirectory(folderPath);
            AssetDatabase.ImportAsset(folderPath);
            return true;
        }
        public static List<string> GetAssetPathsInFolder(string path, bool deep = true)
        {
            string[] fileEntries = Directory.GetFiles(path);
            string[] subDirectories = deep ? AssetDatabase.GetSubFolders(path) : null;

            List<string> list =
                (from fileName in fileEntries
                 where !fileName.EndsWith(".meta")
                       && !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(fileName.Replace('\\', '/')))
                       //지원하는 에셋 타입 여부 확인
                 select fileName.Replace('\\', '/')).ToList();


            if (deep)
                foreach (var sd in subDirectories)
                    list.AddRange(GetAssetPathsInFolder(sd));


            return list;
        }
        #endregion
        #endregion

        #region Classes & Structs

        [System.Serializable]
        private class CustomFolder
        {
            public string name;
            public bool active = true;
            public ManageAction action;
            public CustomFolder() { }
            public CustomFolder(string newName, ManageAction action = ManageAction.Skip)
            {
                name = newName;
                this.action = action;
            }
        }


        private class DependencyAsset
        {
            public readonly Object asset;
            public readonly string path;
            public readonly string guid;
            public readonly Type type;
            public readonly GUIContent icon;
            public ManageAction action;
            public ManageType associatedType;

            public DependencyAsset(string path)
            {
                this.path = path;
                asset = AssetDatabase.LoadAssetAtPath<Object>(path);
                guid = AssetDatabase.AssetPathToGUID(path);
                action = ManageAction.Skip;
                type = asset.GetType();
                icon = new GUIContent(AssetPreview.GetMiniTypeThumbnail(type), type.Name);
            }
        }
        private readonly struct ManageType
        {
            public readonly int actionIndex;
            public readonly string name;
            public readonly Type[] associatedTypes;
            private readonly string[] associatedExtensions;

            public ManageType(int actionIndex, string name)
            {
                this.actionIndex = actionIndex;
                this.name = name;
                this.associatedTypes = Array.Empty<Type>();
                this.associatedExtensions = Array.Empty<string>();
            }
            public ManageType(int actionIndex, string name, params string[] associatedTypes)
            {
                this.actionIndex = actionIndex;
                this.name = name;
                this.associatedTypes = new Type[associatedTypes.Length];
                for (int i = 0; i < associatedTypes.Length; i++)
                    this.associatedTypes[i] = System.Type.GetType(associatedTypes[i]);

                this.associatedExtensions = Array.Empty<string>();
            }

            public ManageType(int actionIndex, string name, params Type[] associatedTypes)
            {
                this.actionIndex = actionIndex;
                this.name = name;

                this.associatedTypes = associatedTypes;
                this.associatedExtensions = Array.Empty<string>();
            }

            public ManageType(int actionIndex, string name, string[] associatedExtensions, params string[] associatedTypes)
            {
                this.actionIndex = actionIndex;
                this.name = name;

                this.associatedTypes = new Type[associatedTypes.Length];
                for (int i = 0; i < associatedTypes.Length; i++)
                    this.associatedTypes[i] = System.Type.GetType(associatedTypes[i]);

                this.associatedExtensions = associatedExtensions;
            }

            public ManageType(int actionIndex, string name, string[] associatedExtensions, params Type[] associatedTypes)
            {
                this.actionIndex = actionIndex;
                this.name = name;

                int count = associatedTypes.Length;
                this.associatedTypes = associatedTypes;
                this.associatedExtensions = associatedExtensions;
            }

            public bool IsAppliedTo(DependencyAsset a)
            {
                bool applies = a.type != null &&
                    (associatedTypes.Any(t => t != null && (a.type == t || a.type.IsSubclassOf(t)))
                     || associatedExtensions.Any(e => !string.IsNullOrWhiteSpace(e) && a.path.EndsWith(e)));

                return applies;
            }

        }

        private class BGColoredScope : System.IDisposable
        {
            private readonly Color ogColor;
            public BGColoredScope(Color setColor)
            {
                ogColor = GUI.backgroundColor;
                GUI.backgroundColor = setColor;
            }
            public BGColoredScope(Color setColor, bool isActive)
            {
                ogColor = GUI.backgroundColor;
                GUI.backgroundColor = isActive ? setColor : ogColor;
            }
            public BGColoredScope(Color active, Color inactive, bool isActive)
            {
                ogColor = GUI.backgroundColor;
                GUI.backgroundColor = isActive ? active : inactive;
            }

            public BGColoredScope(int selectedIndex, params Color[] colors)
            {
                ogColor = GUI.backgroundColor;
                GUI.backgroundColor = colors[selectedIndex];
            }
            public void Dispose()
            {
                GUI.backgroundColor = ogColor;
            }
        }
        #endregion
    }
}
