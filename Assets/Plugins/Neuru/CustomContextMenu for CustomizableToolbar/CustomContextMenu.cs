using UnityEditor;
using UnityEngine;
using System;
using System.Linq;
using System.Collections.Generic;

namespace Neuru
{
    public class CustomContextMenu
    {
        [MenuItem("CONTEXT/MA")]
        private static void ShowMAComponentsMenu(MenuCommand command)
        {
            FindCustomComponents("Modular Avatar");
        }

        [MenuItem("CONTEXT/AO")]
        private static void ShowAOComponentsMenu(MenuCommand command)
        {
            FindCustomComponents("Avatar Optimizer");
        }

        private static void FindCustomComponents(string filterString)
        {
            // Get all script asset paths in the project
            string[] scriptAssetPaths = AssetDatabase.FindAssets("t:Script")
                .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
                .ToArray();

            List<string> filteredMenuPaths = new List<string>();

            foreach (string assetPath in scriptAssetPaths)
            {
                // Load the script asset
                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(assetPath);

                if (script != null)
                {
                    Type scriptType = script.GetClass();

                    // Check if the script has the AddComponentMenu attribute
                    if (scriptType != null && Attribute.IsDefined(scriptType, typeof(AddComponentMenu)))
                    {
                        AddComponentMenu attribute = (AddComponentMenu)Attribute.GetCustomAttribute(scriptType, typeof(AddComponentMenu));

                        string menuPath = attribute.componentMenu;

                        if (menuPath.StartsWith(filterString) && menuPath.Length > filterString.Length)
                        {
                            filteredMenuPaths.Add(menuPath);
                        }
                    }
                }
            }

            // Convert the List to a string array
            string[] filteredMenuPathsArray = filteredMenuPaths.ToArray();

            // Make context menu with the resulting array
            GenericMenu menu = new GenericMenu();
            foreach (string menuPath in filteredMenuPathsArray)
            {
                menu.AddItem(new GUIContent(menuPath.Substring(filterString.Length + 1)), false, Execute, menuPath);
            }
            menu.ShowAsContext();
        }
        private static void Execute(object context)
        {
            string m_Context = (string)context;
            EditorApplication.ExecuteMenuItem("Component/" + m_Context);
        }
    }
}