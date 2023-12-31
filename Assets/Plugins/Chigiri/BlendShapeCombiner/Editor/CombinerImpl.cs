﻿using UnityEngine;
using UnityEditor;
using System.IO;

namespace Chigiri.BlendShapeCombiner.Editor
{
    public class CombinerImpl
    {

        public static void Process(BlendShapeCombiner p)
        {
            var resultMesh = MergeBlendShapes(p);
            if (resultMesh == null) return;
            resultMesh.name = p.sourceMesh.name + ".BlendShapeAdded";

            // 保存ダイアログを表示
            string dir = AssetDatabase.GetAssetPath(p.sourceMesh);
            dir = dir == "" ? "Assets" : Path.GetDirectoryName(dir);
            string path = EditorUtility.SaveFilePanel("Save the new mesh as", dir, Helper.SanitizeFileName(resultMesh.name), "asset");
            if (path.Length == 0) return;

            // 保存
            if (!path.StartsWith(Application.dataPath))
            {
                Debug.LogError("Invalid path: Path must be under " + Application.dataPath);
                return;
            }
            path = path.Replace(Application.dataPath, "Assets");
            AssetDatabase.CreateAsset(resultMesh, path);
            Debug.Log("Asset exported: " + path);

            // Targetのメッシュを差し替えてシェイプキーのウェイトを設定
            Undo.RecordObject(p.targetRenderer, "Process (MeshHoleShrinker)");
            p.targetRenderer.sharedMesh = resultMesh;
            // Selection.activeGameObject = self.targetRenderer.gameObject;
        }

        static Mesh MergeBlendShapes(BlendShapeCombiner p)
        {
            Mesh ret = Object.Instantiate(p.sourceMesh);
            ret.name = p.sourceMesh.name;
            var src = Object.Instantiate(ret);

            foreach (var newKey in p.newKeys)
            {
                var n = newKey.sourceKeys.Count;
                var newFrames = 0;
                for (var i = 0; i < n; i++)
                {
                    var key = newKey.sourceKeys[i];
                    int index = src.GetBlendShapeIndex(key.name);
                    int numFrames = src.GetBlendShapeFrameCount(index);
                    if (i == 0 || numFrames < newFrames) newFrames = numFrames;
                }

                var tempVertices = new Vector3[p.sourceMesh.vertexCount];
                var tempNormals = new Vector3[p.sourceMesh.vertexCount];
                var tempTangents = new Vector3[p.sourceMesh.vertexCount];

                for (var frame = 0; frame < newFrames; frame++)
                {
                    float weight = 0.0f;
                    var vertices = new Vector3[p.sourceMesh.vertexCount];
                    var normals = new Vector3[p.sourceMesh.vertexCount];
                    var tangents = new Vector3[p.sourceMesh.vertexCount];
                    for (var i = 0; i < n; i++)
                    {
                        var key = newKey.sourceKeys[i];
                        int index = src.GetBlendShapeIndex(key.name);
                        weight += src.GetBlendShapeFrameWeight(index, frame);
                        p.sourceMesh.GetBlendShapeFrameVertices(index, frame, tempVertices, tempNormals, tempTangents);
                        vertices = Helper.AddVector3(vertices, tempVertices, key.scale);
                        normals = Helper.AddVector3(normals, tempNormals, key.scale);
                        tangents = Helper.AddVector3(tangents, tempTangents, key.scale);
                    }
                    ret.AddBlendShapeFrame(newKey.name, weight/n, vertices, normals, tangents);
                }
            }
            return ret;
        }

    }
}
