using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using VRC.SDK3.Avatars.Components;
using System.Linq;
using UnityEditor.Animations;
using HarmonyLib;
using System.Reflection;

//Made by Dreadrith#3238

namespace DreadScripts
{
    public class ControllerFix : EditorWindow
    {
        public static VRCAvatarDescriptor mainAvatar;
        public static AnimatorController mainController;

        public static bool autoBuffer = true, autoFrame = true, autoLoop = true, autoTransition = true, autoFlag = true;

        public static AnimationClip buffer;

        [MenuItem("DreadTools/Utilities/Controller Fix",false,4500)]
        private static void showWindow()
        {
            GetWindow<ControllerFix>(false, "Controller Fix", true);
        }

        public void OnGUI()
        {
            using (new GUILayout.HorizontalScope("box"))
            {
                EditorGUI.BeginChangeCheck();

                Object dummy = EditorGUILayout.ObjectField("Target", (Object)mainAvatar ?? mainController ?? null, typeof(Object), true);
                
                if (EditorGUI.EndChangeCheck())
                {
                    if (dummy is GameObject o)
                    {
                        VRCAvatarDescriptor descriptor = o.GetComponent<VRCAvatarDescriptor>();
                        mainAvatar = descriptor ?? mainAvatar;
                        
                        if (!descriptor)
                        {
                            Animator ani = o.GetComponent<Animator>();
                            if (ani && ani.runtimeAnimatorController)
                            {
                                mainController = AssetDatabase.LoadAssetAtPath<AnimatorController>(AssetDatabase.GetAssetPath(ani.runtimeAnimatorController));
                                mainAvatar = null;
                            }
                            else
                                Debug.LogWarning("Target GameObject must have a VRCAvatarDescriptor or an Animator with a Controller");
                        }
                        else
                            mainController = null;
                    }
                    else
                    {
                        if (dummy is AnimatorController c)
                        {
                            mainController = c;
                            mainAvatar = null;
                        }
                        else
                        if (!dummy)
                        {
                            mainAvatar = null;
                            mainController = null;
                        }
                        else
                            Debug.LogWarning("Target must be a VRCAvatarDescriptor or an AnimatorController!");
                    }
                }
            }
            
            if (autoBuffer)
            {
                using (new GUILayout.HorizontalScope("box"))
                    buffer = (AnimationClip)EditorGUILayout.ObjectField("Buffer Clip", buffer, typeof(AnimationClip), true);
            }

            DrawSeperator();
            using (new GUILayout.HorizontalScope())
            {
                using (new GUILayout.VerticalScope())
                {
                    ColoredToggle(ref autoBuffer, "Add Buffer", "Fill empty states with a buffer clip");
                    ColoredToggle(ref autoFrame, "Auto Two Frame", "Make constant clips with one start frame have two frames");
                }
                using (new GUILayout.VerticalScope())
                {
                    ColoredToggle(ref autoLoop, "Auto Loop Time", "Set constant clips to loop time Off");
                    ColoredToggle(ref autoTransition, "Auto Transition", "Turn off 'Can Transition To Self' on any state transitions to a state with a constant clip or tree");
                }
            }
            ColoredToggle(ref autoFlag, "Fix HideFlags", "Unhides Controller objects from the inspector. Unity 2019 Controllers Fix.");

            DrawSeperator();

            EditorGUI.BeginDisabledGroup((!mainAvatar && !mainController) || (autoBuffer && !buffer) || (!(autoBuffer || autoFrame || autoLoop || autoTransition || autoFlag)));
            if (GUILayout.Button("Fix", "toolbarbutton"))
            {
                if (mainAvatar)
                    FixAvatar(mainAvatar, autoFrame, autoLoop, autoTransition, autoBuffer, buffer);
                else
                    FixController(mainController, autoFrame, autoLoop, autoTransition, autoBuffer, buffer);
            }
            EditorGUI.EndDisabledGroup();
        }

        /// <summary>
        /// Applies the Controller Fix to all of the Animator Controllers that exist on the target Avatar
        /// </summary>
        /// <param name="avatar">The target Avatar</param>
        public static void FixAvatar(VRCAvatarDescriptor avatar, bool autoFrame, bool autoLoop, bool autoTransition, bool autoBuffer, AnimationClip bufferClip)
        {
            HashSet<AnimatorController> fixedControllers = new HashSet<AnimatorController>();
            foreach (var layer in avatar.baseAnimationLayers.Concat(avatar.specialAnimationLayers))
            {
                if (layer.animatorController)
                {
                    AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(AssetDatabase.GetAssetPath(layer.animatorController));
                    if (controller && !fixedControllers.Contains(controller))
                    {
                        FixController(controller, autoFrame, autoLoop, autoTransition, autoBuffer, buffer);
                        fixedControllers.Add(controller);
                    }
                }
            }
            foreach (var runtimeController in avatar.GetComponentsInChildren<Animator>().Select(a => a.runtimeAnimatorController))
            {
                if (runtimeController)
                {
                    AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(AssetDatabase.GetAssetPath(runtimeController));
                    if (controller && !fixedControllers.Contains(controller))
                    {
                        FixController(controller, autoFrame, autoLoop, autoTransition, autoBuffer, buffer);
                        fixedControllers.Add(controller);
                    }
                }
            }
        }

 

        /// <summary>
        /// Applies several edits to fix issues with controller settings and animations.
        /// </summary>
        /// <param name="controller">The target Controller</param>
        /// <param name="autoFrame">If true, makes sure that constant clips are at least 2 frames long</param>
        /// <param name="autoLoop">If true, sets constant animation clips to loop time Off</param>
        /// <param name="autoTransition">If true, sets 'Can Transition To Self' Off on Any State transitions to states with a constant motion</param>
        /// <param name="autoBuffer">If true, fills states with no motion with the buffer clip </param>
        /// <param name="bufferClip">The Buffer Clip to fill states with.</param>
        public static void FixController(AnimatorController controller, bool autoFrame, bool autoLoop, bool autoTransition, bool autoBuffer, AnimationClip bufferClip)
        {
            for (int i = 0; i < controller.layers.Length; i++)
            {
                if (autoFlag)
                    FixMachineFlag(controller.layers[i].stateMachine);
                HashSet<AnimatorState> constantStates = new HashSet<AnimatorState>();

                IterateStates(controller.layers[i].stateMachine, s =>
                {
                    if (autoBuffer && !s.motion)
                    {
                        s.motion = bufferClip;
                        EditorUtility.SetDirty(s);
                    }
                    else
                    {
                        if (s.motion is AnimationClip clip)
                        {
                            if (IsConstant(clip, out bool oneStartFrame))
                            {
                                constantStates.Add(s);
                                FixConstantClip(clip, oneStartFrame, autoFrame, autoLoop);
                            }
                        }
                        else
                        {
                            if (s.motion is BlendTree tree)
                            {

                                bool constantTree = IsConstant(tree, out List<AnimationClip> treeConstantClips, out List<AnimationClip> treeOneStartFrameClips);
                                if (constantTree)
                                    constantStates.Add(s);

                                treeConstantClips.ForEach(c => FixConstantClip(c, false, autoFrame, autoLoop));
                                treeOneStartFrameClips.ForEach(c => FixConstantClip(c, true, autoFrame, autoLoop));
                            }
                        }
                    }
                }, true);

                if (autoTransition)
                {
                    AnimatorStateTransition[] anyTransitions = controller.layers[i].stateMachine.anyStateTransitions;
                    for (int j = 0; j < anyTransitions.Length; j++)
                    {
                        //If the destination state has a constant motion, set 'Can Transition To Self' Off
                        if (constantStates.Contains(anyTransitions[j].destinationState))
                        {
                            anyTransitions[j].canTransitionToSelf = false;
                            EditorUtility.SetDirty(anyTransitions[j]);
                        }
                    }
                }

            }
        }

        private static void FixConstantClip(AnimationClip clip, bool isOneStartFrame, bool autoFrame, bool autoLoop)
        {
            if (autoFrame && isOneStartFrame)
            {
                //Sets the animation curve to be 2 frames.

                EditorCurveBinding[] allCurves = AnimationUtility.GetCurveBindings(clip).Concat(AnimationUtility.GetObjectReferenceCurveBindings(clip)).ToArray();
                for (int i = 0; i < allCurves.Length; i++)
                {
                    EditorCurveBinding c = allCurves[i];

                    AnimationCurve floatCurve = AnimationUtility.GetEditorCurve(clip, c);
                    ObjectReferenceKeyframe[] objectCurve = AnimationUtility.GetObjectReferenceCurve(clip, c);
                    bool isFloatCurve = floatCurve != null;

                    if (isFloatCurve)
                    {
                        AnimationUtility.SetEditorCurve(clip, allCurves[i], null);
                        AnimationUtility.SetEditorCurve(clip, allCurves[i], ACurve(floatCurve.keys[0].value, clip.frameRate));
                    }
                    else
                    {
                        AnimationUtility.SetObjectReferenceCurve(clip, allCurves[i], null);
                        AnimationUtility.SetObjectReferenceCurve(clip, allCurves[i], OCurve(objectCurve, clip.frameRate));
                    }
                        

                }
                EditorUtility.SetDirty(clip);
            }
            if (autoLoop)
            {
                //Sets Loop time Off on the animation clip
                SetLoopTime(clip, false);
            }
        }

        //Returns a two frame float curve of the given value.
        private static AnimationCurve ACurve(float value, float frameRate)
        {
            return new AnimationCurve(new Keyframe[] { new Keyframe(0, value), new Keyframe(1 / frameRate, value) });
        }

        //Returns a two frame object curve of the first frame of the given ObjectReferenceKeyframe array.
        private static ObjectReferenceKeyframe[] OCurve(ObjectReferenceKeyframe[] values, float frameRate)
        {
            return new ObjectReferenceKeyframe[] { new ObjectReferenceKeyframe() { time = 0, value = values[0].value }, new ObjectReferenceKeyframe() { time = 1 / frameRate, value = values[0].value } };
        }

        /// <summary>
        /// Apply a Method to each State in the Target StateMachine
        /// </summary>
        /// <param name="machine">The Target StateMachine</param>
        /// <param name="action">The Action Method that should be applied to each State</param>
        /// <param name="deep">If true, the same method will be iteratively applied to the Child StateMachines of the Target StateMachine</param>
        public static void IterateStates(AnimatorStateMachine machine, System.Action<AnimatorState> action, bool deep = true)
        {
            if (deep)
                foreach (var subMachine in machine.stateMachines.Select(c => c.stateMachine))
                {
                    IterateStates(subMachine, action);
                }

            foreach (var state in machine.states.Select(s => s.state))
            {
                action(state);
            }
        }

        /// <summary>
        /// Checks if the BlendTree has no clips that have varying values throughout their animations
        /// </summary>
        /// <param name="tree">The Target BlendTree</param>
        /// <param name="constantClips">Returns the clips that were flagged as Constant but not isOneStartFrame</param>
        /// <param name="oneStartFrameClips">Returns the clips that were flagged as Constant and isOneStartFrame</param>
        /// <returns></returns>
        public static bool IsConstant(BlendTree tree,out List<AnimationClip> constantClips, out List<AnimationClip> oneStartFrameClips)
        {
            bool constantTree = true;
            constantClips = new List<AnimationClip>();
            oneStartFrameClips = new List<AnimationClip>();

            for (int i = 0; i < tree.children.Length; i++)
            {
                Motion m = tree.children[i].motion;
                if (m is AnimationClip clip)
                {
                    if (IsConstant(clip, out bool oneStartFrame))
                    {
                        if (oneStartFrame)
                            oneStartFrameClips.Add(clip);
                        else
                            constantClips.Add(clip);
                    }
                    else
                        constantTree = false;
                }
                else
                {
                    if (m is BlendTree t)
                    {
                        bool constantSubTree = IsConstant(t, out List<AnimationClip> subTreeConstantClips, out List<AnimationClip> subTreeOneStartFrameClips);
                        constantClips.AddRange(subTreeConstantClips);
                        oneStartFrameClips.AddRange(subTreeOneStartFrameClips);

                        if (!constantSubTree)
                            constantTree = false;
                    }
                }
            }
            return constantTree;
        }

        /// <summary>
        /// Checks if the clip has no varying values throughout the animation
        /// </summary>
        /// <param name="clip">The Clip to check</param>
        /// <param name="isOneStartFrame">Returns true if the animation only has a single frame at time = 0</param>
        /// <returns></returns>
        public static bool IsConstant(AnimationClip clip, out bool isOneStartFrame)
        {
            isOneStartFrame = true;

            EditorCurveBinding[] allCurves = AnimationUtility.GetCurveBindings(clip).Concat(AnimationUtility.GetObjectReferenceCurveBindings(clip)).ToArray();

            for (int i = 0; i < allCurves.Length; i++)
            {
                EditorCurveBinding c = allCurves[i];

                AnimationCurve floatCurve = AnimationUtility.GetEditorCurve(clip, c);
                ObjectReferenceKeyframe[] objectCurve = AnimationUtility.GetObjectReferenceCurve(clip, c);
                bool isFloatCurve = floatCurve != null;

                bool oneStartKey = (isFloatCurve && floatCurve.keys.Length == 1 && floatCurve.keys[0].time == 0) || (!isFloatCurve && objectCurve.Length <= 1 && objectCurve[0].time == 0);
                if (oneStartKey)
                    continue;
                else
                    isOneStartFrame = false;

                if (isFloatCurve)
                {
                    float v1 = floatCurve.keys[0].value;
                    float t1 = floatCurve.keys[0].time;
                    float t2;
                    for (int j = 1; j < floatCurve.keys.Length; j++)
                    {
                        t2 = floatCurve.keys[j].time;
                        if (floatCurve.keys[j].value != v1 || floatCurve.Evaluate((t1 + t2) / 2f) != v1)
                            return false;
                        t1 = t2;
                    }
                }
                else
                {
                    Object v = objectCurve[0].value;
                    if (objectCurve.Any(o => o.value != v))
                        return false;
                }

            }
            return true;
        }

        /// <summary>
        /// Sets the Loop Time setting of an animation clip.
        /// </summary>
        /// <param name="clip">The target Animation Clip.</param>
        /// <param name="loop">The desired Loop Time setting.</param>
        public static void SetLoopTime(AnimationClip clip, bool loop)
        {
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
        }

        /// <summary>
        /// Makes sure that the Path is ready for use by creating it if it doesn't exist.
        /// </summary>
        /// <param name="path">The Path to ready up.</param>
        public static void ReadyPath(string path)
        {
            if (!System.IO.Directory.Exists(path))
                System.IO.Directory.CreateDirectory(path);
        }

        /// <summary>
        /// A Toggle that switches between Green when on and Grey when off
        /// </summary>
        /// <param name="myBool">The bool to use and modify</param>
        /// <param name="name">Label of the button</param>
        /// <param name="tooltip">Tooltip when hovering over the button</param>
        public static void ColoredToggle(ref bool myBool,string name, string tooltip)
        {
            Color oldColor = GUI.backgroundColor;
            GUI.backgroundColor = myBool ? Color.green : Color.grey;

            myBool = GUILayout.Toggle(myBool, new GUIContent(name, tooltip), "toolbarbutton");

            GUI.backgroundColor = oldColor;
        }


        // Draws a thin horizontal line between GUI elements
        private static void DrawSeperator()
        {
            Rect r = EditorGUILayout.GetControlRect(GUILayout.Height(1 + 2));
            r.height = 1;
            r.y += 1;
            r.x -= 2;
            r.width += 6;
            ColorUtility.TryParseHtmlString(EditorGUIUtility.isProSkin ? "#595959" : "#858585", out Color lineColor);
            EditorGUI.DrawRect(r, lineColor);
        }

        private void OnEnable()
        {
            if (!mainAvatar)
                mainAvatar = FindObjectOfType<VRCAvatarDescriptor>();

            string folderPath = "Assets/DreadScripts/Resources/Animation/Clip";
            ReadyPath(folderPath);

            if (!buffer)
                buffer = AssetDatabase.LoadAssetAtPath<AnimationClip>(folderPath + "/DS_Buffer.anim");
            if (!buffer)
            {
                buffer = new AnimationClip();
                buffer.SetCurve("!Buffer", typeof(GameObject), "Dummy Property", new AnimationCurve(new Keyframe[] { new Keyframe(0, 0), new Keyframe(1 / 60f, 0) }));
                AssetDatabase.CreateAsset(buffer, folderPath + "/DS_Buffer.anim");
            }
        }

        
        //Applies the Patch needed to fix any pasted or duplicated states, statemachines, transitions, and behaviors on the spot
        [InitializeOnLoadMethod]
        private static void InitializePatches()
        {
            Harmony harmony = new Harmony("com.dreadscripts.controllerfix.tool");
            MethodInfo pasteOriginal = typeof(Unsupported).GetMethod("PasteToStateMachineFromPasteboard", BindingFlags.Public | BindingFlags.Static);
            MethodInfo pastePost = typeof(ControllerFix).GetMethod("PastePost", BindingFlags.NonPublic | BindingFlags.Static);
            harmony.Patch(pasteOriginal, null, new HarmonyMethod(pastePost));
        }

        private static void PastePost(AnimatorStateMachine sm)
        {
            FixMachineFlag(sm);
        }

        public static void FixMachineFlag(AnimatorStateMachine m)
        {
            FixFlag(m);
            Iterate(m.entryTransitions, t => FixFlag(t));
            Iterate(m.anyStateTransitions, t => FixFlag(t));
            Iterate(m.behaviours, b => FixFlag(b));
            Iterate(m.states, s => FixStateFlag(s.state));
            Iterate(m.stateMachines, s => FixMachineFlag(s.stateMachine));
        }

        public static void FixStateFlag(AnimatorState s)
        {
            FixFlag(s);

            if (s.motion is BlendTree tree)
                FixTreeFlag(tree);
            Iterate(s.behaviours, b => FixFlag(b));
            Iterate(s.transitions, t => FixFlag(t));
        }

        public static void FixTreeFlag(BlendTree t)
        {
            FixFlag(t);
            Iterate(t.children, c =>
            {
                if (c.motion is BlendTree tree)
                    FixTreeFlag(tree);
            });
        }

        
        public static void FixFlag(Object o)
        {
            //If the Object is hidden in inspector
            if (o.hideFlags.HasFlag(HideFlags.HideInInspector))
            {
                //Unhide it
                o.hideFlags &= ~HideFlags.HideInInspector;
            }
            EditorUtility.SetDirty(o);
        }

        private static void Iterate<T>(IEnumerable<T> collection, System.Action<T> action)
        {

            using (IEnumerator<T> myNum = collection.GetEnumerator())
                while (myNum.MoveNext())
                {
                    action(myNum.Current);
                }
        }
    }
}