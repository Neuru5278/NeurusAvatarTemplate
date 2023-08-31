﻿using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Animations;

namespace Hai.ComboGesture.Scripts.Editor.Internal
{
    internal class CgeBlendTreeAutoWeightCorrector : List<CgeManifestBinding>
    {
        public const string AutoGestureWeightParam = "_AutoGestureWeight";
        public const string AutoGestureWeightParam_UniversalLeft = "_AutoGestureLeftWeight";
        public const string AutoGestureWeightParam_UniversalRight = "_AutoGestureRightWeight";
        private readonly List<CgeManifestBinding> _activityManifests;
        private readonly bool _useGestureWeightCorrection;
        private readonly bool _useSmoothing;
        private readonly CgeAssetContainer _assetContainer;

        public CgeBlendTreeAutoWeightCorrector(List<CgeManifestBinding> activityManifests, bool useGestureWeightCorrection, bool useSmoothing, CgeAssetContainer assetContainer)
        {
            _activityManifests = activityManifests;
            _useGestureWeightCorrection = useGestureWeightCorrection;
            _useSmoothing = useSmoothing;
            _assetContainer = assetContainer;
        }

        public List<CgeManifestBinding> MutateAndCorrectExistingBlendTrees()
        {
            var remappables = new HashSet<CgeManifestKind>(new[] {CgeManifestKind.Permutation, CgeManifestKind.Massive, CgeManifestKind.OneHand});
            var mappings = _activityManifests
                .Where(binding => remappables.Contains(binding.Manifest.Kind()))
                .SelectMany(binding => binding.Manifest.AllBlendTreesFoundRecursively())
                .Distinct()
                .Where(tree =>
                {
                    switch (tree.blendType)
                    {
                        case BlendTreeType.Simple1D:
                            return tree.blendParameter == AutoGestureWeightParam;
                        case BlendTreeType.SimpleDirectional2D:
                        case BlendTreeType.FreeformDirectional2D:
                        case BlendTreeType.FreeformCartesian2D:
                            return tree.blendParameter == AutoGestureWeightParam || tree.blendParameterY == AutoGestureWeightParam;
                        case BlendTreeType.Direct:
                            return tree.children.Any(motion => motion.directBlendParameter == AutoGestureWeightParam);
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                })
                .Select(originalTree =>
                {
                    var newTreeForLeftSide = CopyTreeIdentically(originalTree, CgeSide.Left);
                    var newTreeForRightSide = CopyTreeIdentically(originalTree, CgeSide.Right);
                    _assetContainer.AddBlendTree(newTreeForLeftSide);
                    _assetContainer.AddBlendTree(newTreeForRightSide);
                    return new CgeAutoWeightTreeMapping(originalTree, newTreeForLeftSide, newTreeForRightSide);
                })
                .ToDictionary(mapping => mapping.Original, mapping => mapping);


            return _activityManifests
                .Select(binding =>
                {
                    if (!remappables.Contains(binding.Manifest.Kind()))
                    {
                        return binding;
                    }

                    return RemapManifest(binding, mappings);
                }).ToList();
        }

        private static CgeManifestBinding RemapManifest(CgeManifestBinding manifestBinding, Dictionary<BlendTree, CgeAutoWeightTreeMapping> autoWeightRemapping)
        {
            var remappedManifest = manifestBinding.Manifest.UsingRemappedWeights(autoWeightRemapping);
            return CgeManifestBinding.Remapping(manifestBinding, remappedManifest);
        }

        private BlendTree CopyTreeIdentically(BlendTree originalTree, CgeSide side)
        {
            var newTree = new BlendTree();

            // Object.Instantiate(...) is triggering some weird issues about assertions failures.
            // Copy the blend tree manually
            newTree.name = "zAutogeneratedPup_" + originalTree.name + "_DO_NOT_EDIT";
            newTree.blendType = originalTree.blendType;
            newTree.blendParameter = HandleWeightCorrection(
                RemapAutoWeightOrElse(side, originalTree.blendParameter)
            );
            newTree.blendParameterY = HandleWeightCorrection(
                RemapAutoWeightOrElse(side, originalTree.blendParameterY)
            );
            newTree.minThreshold = originalTree.minThreshold;
            newTree.maxThreshold = originalTree.maxThreshold;
            newTree.useAutomaticThresholds = originalTree.useAutomaticThresholds;

            var copyOfChildren = originalTree.children;
            while (newTree.children.Length > 0) {
                newTree.RemoveChild(0);
            }

            newTree.children = copyOfChildren
                .Select(childMotion => new ChildMotion
                {
                    motion = childMotion.motion,
                    threshold = childMotion.threshold,
                    position = childMotion.position,
                    timeScale = childMotion.timeScale,
                    cycleOffset = childMotion.cycleOffset,
                    directBlendParameter = HandleWeightCorrection(RemapAutoWeightOrElse(side, childMotion.directBlendParameter)),
                    mirror = childMotion.mirror
                })
                .ToArray();

            return newTree;
        }

        private static string RemapAutoWeightOrElse(CgeSide side, string originalParameterName)
        {
            switch (originalParameterName)
            {
                case AutoGestureWeightParam:
                    return side == CgeSide.Left ? "GestureLeftWeight" : "GestureRightWeight";
                case AutoGestureWeightParam_UniversalLeft:
                    return "GestureLeftWeight";
                case AutoGestureWeightParam_UniversalRight:
                    return "GestureRightWeight";
                default:
                    return originalParameterName;
            }
        }

        private string HandleWeightCorrection(string originalTreeBlendParameter)
        {
            // FIXME this is duplicate code
            if (!_useGestureWeightCorrection)
            {
                return originalTreeBlendParameter;
            }

            switch (originalTreeBlendParameter)
            {
                case "GestureLeftWeight":
                    return _useSmoothing ? CgeSharedLayerUtils.HaiGestureComboLeftWeightSmoothing : CgeSharedLayerUtils.HaiGestureComboLeftWeightProxy;
                case "GestureRightWeight":
                    return _useSmoothing ? CgeSharedLayerUtils.HaiGestureComboRightWeightSmoothing : CgeSharedLayerUtils.HaiGestureComboRightWeightProxy;
                default:
                    return originalTreeBlendParameter;
            }
        }
    }

    internal enum CgeSide
    {
        Left, Right
    }
}
