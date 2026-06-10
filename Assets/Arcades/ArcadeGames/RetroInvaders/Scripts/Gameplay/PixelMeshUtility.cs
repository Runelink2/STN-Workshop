using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RetroInvaders {
    public static class PixelMeshUtility {
        private const float MinimumPixelSize = 0.001f;
        private const float GapSealScale = 1.005f;
        private static Mesh flatQuadMesh;

        private struct PixelSource {
            public GameObject GameObject;
            public Renderer Renderer;
            public Vector3 LocalPosition;
            public Vector3 LocalScale;
            public Material Material;
        }

        public static void OptimizePixelChildren(Transform visualRoot, string pixelNamePrefix) {
            if (visualRoot == null || string.IsNullOrEmpty(pixelNamePrefix)) {
                return;
            }

            if (visualRoot.Find(GetCombinedRootName(pixelNamePrefix)) != null) {
                return;
            }

            List<PixelSource> sources = CollectPixelSources(visualRoot, pixelNamePrefix);
            if (sources.Count == 0) {
                return;
            }

            float pixelWidth = InferAxisSize(sources, 0);
            float pixelHeight = InferAxisSize(sources, 1);
            float zOffset = -InferAxisSize(sources, 2) * 0.5f;
            Dictionary<Material, List<PixelSource>> groups = GroupByMaterial(sources);

            Transform combinedRoot = new GameObject(GetCombinedRootName(pixelNamePrefix)).transform;
            combinedRoot.SetParent(visualRoot, false);

            int meshIndex = 0;
            foreach (KeyValuePair<Material, List<PixelSource>> group in groups) {
                GameObject meshObject = new GameObject("PixelMesh_" + meshIndex.ToString("00"));
                meshObject.transform.SetParent(combinedRoot, false);

                MeshFilter filter = meshObject.AddComponent<MeshFilter>();
                filter.sharedMesh = BuildPixelMesh(pixelNamePrefix, group.Value, pixelWidth, pixelHeight, zOffset);

                MeshRenderer renderer = meshObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = group.Key;
                ConfigureRendererForPixelArt(renderer);
                meshIndex++;
            }

            DisableSourcePixels(sources);
        }

        public static void ApplyFlatQuadVisual(GameObject target) {
            if (target == null) {
                return;
            }

            MeshFilter filter = target.GetComponent<MeshFilter>();
            if (filter != null) {
                filter.sharedMesh = GetFlatQuadMesh();
            }

            Renderer renderer = target.GetComponent<Renderer>();
            ConfigureRendererForPixelArt(renderer);
        }

        public static void ConfigureRendererForPixelArt(Renderer renderer) {
            if (renderer == null) {
                return;
            }

            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            renderer.allowOcclusionWhenDynamic = false;
        }

        private static List<PixelSource> CollectPixelSources(Transform visualRoot, string pixelNamePrefix) {
            MeshRenderer[] renderers = visualRoot.GetComponentsInChildren<MeshRenderer>(true);
            List<PixelSource> sources = new List<PixelSource>(renderers.Length);

            for (int i = 0; i < renderers.Length; i++) {
                MeshRenderer renderer = renderers[i];
                if (renderer == null || renderer.transform.parent != visualRoot || !renderer.gameObject.name.StartsWith(pixelNamePrefix)) {
                    continue;
                }

                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || renderer.sharedMaterial == null) {
                    continue;
                }

                sources.Add(new PixelSource {
                    GameObject = renderer.gameObject,
                    Renderer = renderer,
                    LocalPosition = renderer.transform.localPosition,
                    LocalScale = renderer.transform.localScale,
                    Material = renderer.sharedMaterial
                });
            }

            return sources;
        }

        private static Dictionary<Material, List<PixelSource>> GroupByMaterial(List<PixelSource> sources) {
            Dictionary<Material, List<PixelSource>> groups = new Dictionary<Material, List<PixelSource>>();

            for (int i = 0; i < sources.Count; i++) {
                PixelSource source = sources[i];
                if (!groups.TryGetValue(source.Material, out List<PixelSource> group)) {
                    group = new List<PixelSource>();
                    groups.Add(source.Material, group);
                }

                group.Add(source);
            }

            return groups;
        }

        private static Mesh BuildPixelMesh(string prefix, List<PixelSource> sources, float pixelWidth, float pixelHeight, float zOffset) {
            List<Vector3> vertices = new List<Vector3>(sources.Count * 4);
            List<Vector2> uvs = new List<Vector2>(sources.Count * 4);
            List<Vector3> normals = new List<Vector3>(sources.Count * 4);
            List<int> triangles = new List<int>(sources.Count * 6);
            float halfWidth = pixelWidth * 0.5f;
            float halfHeight = pixelHeight * 0.5f;

            for (int i = 0; i < sources.Count; i++) {
                Vector3 center = sources[i].LocalPosition;
                int vertexStart = vertices.Count;

                vertices.Add(new Vector3(center.x - halfWidth, center.y - halfHeight, zOffset));
                vertices.Add(new Vector3(center.x - halfWidth, center.y + halfHeight, zOffset));
                vertices.Add(new Vector3(center.x + halfWidth, center.y + halfHeight, zOffset));
                vertices.Add(new Vector3(center.x + halfWidth, center.y - halfHeight, zOffset));

                uvs.Add(new Vector2(0.0f, 0.0f));
                uvs.Add(new Vector2(0.0f, 1.0f));
                uvs.Add(new Vector2(1.0f, 1.0f));
                uvs.Add(new Vector2(1.0f, 0.0f));

                normals.Add(Vector3.back);
                normals.Add(Vector3.back);
                normals.Add(Vector3.back);
                normals.Add(Vector3.back);

                triangles.Add(vertexStart);
                triangles.Add(vertexStart + 1);
                triangles.Add(vertexStart + 2);
                triangles.Add(vertexStart);
                triangles.Add(vertexStart + 2);
                triangles.Add(vertexStart + 3);
            }

            Mesh mesh = new Mesh {
                name = prefix + "_CombinedPixels",
                hideFlags = HideFlags.DontSave
            };

            if (vertices.Count > 65535) {
                mesh.indexFormat = IndexFormat.UInt32;
            }

            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static float InferAxisSize(List<PixelSource> sources, int axis) {
            float largestScale = 0.0f;
            float smallestStep = float.PositiveInfinity;

            for (int i = 0; i < sources.Count; i++) {
                largestScale = Mathf.Max(largestScale, Mathf.Abs(GetAxis(sources[i].LocalScale, axis)));

                for (int j = i + 1; j < sources.Count; j++) {
                    float step = Mathf.Abs(GetAxis(sources[i].LocalPosition, axis) - GetAxis(sources[j].LocalPosition, axis));
                    if (step > MinimumPixelSize && step < smallestStep) {
                        smallestStep = step;
                    }
                }
            }

            float size = float.IsPositiveInfinity(smallestStep) ? largestScale : Mathf.Max(largestScale, smallestStep);
            return Mathf.Max(MinimumPixelSize, size * GapSealScale);
        }

        private static float GetAxis(Vector3 value, int axis) {
            switch (axis) {
                case 0:
                    return value.x;
                case 1:
                    return value.y;
                default:
                    return value.z;
            }
        }

        private static Mesh GetFlatQuadMesh() {
            if (flatQuadMesh != null) {
                return flatQuadMesh;
            }

            flatQuadMesh = new Mesh {
                name = "RetroInvaders_FlatPixelQuad",
                hideFlags = HideFlags.DontSave
            };

            flatQuadMesh.vertices = new[] {
                new Vector3(-0.5f, -0.5f, 0.0f),
                new Vector3(-0.5f, 0.5f, 0.0f),
                new Vector3(0.5f, 0.5f, 0.0f),
                new Vector3(0.5f, -0.5f, 0.0f)
            };
            flatQuadMesh.uv = new[] {
                new Vector2(0.0f, 0.0f),
                new Vector2(0.0f, 1.0f),
                new Vector2(1.0f, 1.0f),
                new Vector2(1.0f, 0.0f)
            };
            flatQuadMesh.normals = new[] {
                Vector3.back,
                Vector3.back,
                Vector3.back,
                Vector3.back
            };
            flatQuadMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            flatQuadMesh.RecalculateBounds();
            return flatQuadMesh;
        }

        private static void DisableSourcePixels(List<PixelSource> sources) {
            for (int i = 0; i < sources.Count; i++) {
                PixelSource source = sources[i];

                if (source.Renderer != null) {
                    source.Renderer.enabled = false;
                }

                if (source.GameObject == null) {
                    continue;
                }

                source.GameObject.SetActive(false);

                if (Application.isPlaying) {
                    Object.Destroy(source.GameObject);
                } else {
                    Object.DestroyImmediate(source.GameObject);
                }
            }
        }

        private static string GetCombinedRootName(string pixelNamePrefix) {
            return "__CombinedPixels_" + pixelNamePrefix;
        }
    }
}
