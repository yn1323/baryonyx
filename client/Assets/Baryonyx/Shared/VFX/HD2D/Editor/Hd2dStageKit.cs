using System;
using System.Collections.Generic;
using System.IO;
using Baryonyx.Editor;
using Baryonyx.Editor.Art;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Baryonyx.Vfx.Hd2d.Editor
{
    /// <summary>
    /// Parts for building a 3D stage (HD-2D) from code: a floor cut into tiles, walls, boxes and
    /// cut-out boards textured with pixel art, flickering lights, flames and embers. The meshes
    /// carry world-scaled UVs, so one material per texture keeps the dots the same size on every
    /// face. A stage collects its meshes in one asset (<see cref="MeshStore"/>) because a prefab
    /// can only refer to saved meshes. The shared materials of the stage's characters and
    /// effects are made here too. What a stage looks like is decided by its own builder
    /// (<c>Baryonyx.Stages.Editor.StageSetAssets</c>).
    /// </summary>
    public static class Hd2dStageKit
    {
        private const string MaterialDirectory = "Assets/Baryonyx/Shared/VFX/HD2D/Materials";
        private const string ShaderDirectory = "Assets/Baryonyx/Shared/VFX/HD2D/Shaders";
        public const string SpriteMaterialPath = MaterialDirectory + "/Hd2dStageSprite.mat";
        public const string ContactShadowMaterialPath =
            MaterialDirectory + "/Hd2dContactShadow.mat";
        public const string FlameMaterialPath = MaterialDirectory + "/Hd2dFlame.mat";
        public const string EmberMaterialPath = MaterialDirectory + "/Hd2dEmberGlow.mat";
        public const string LightBeamMaterialPath = MaterialDirectory + "/Hd2dLightBeam.mat";
        public const string MoteMaterialPath = MaterialDirectory + "/Hd2dMoteGlow.mat";
        public const string PropShadeMaterialPath = MaterialDirectory + "/Hd2dPropShade.mat";
        public const string LeafCookiePath =
            "Assets/Baryonyx/Shared/VFX/HD2D/Textures/Hd2dLeafCookie.png";

        private const string SimpleLitShader = "Universal Render Pipeline/Simple Lit";

        /// <summary>The materials of the characters' boards, their shadows, flames and embers.</summary>
        public static void EnsureSharedMaterials()
        {
            AssetFolders.Ensure(MaterialDirectory);
            // The characters keep most of their own colours: a lift under the stage's light keeps
            // them readable in the dark, and the lights tint them on top.
            EnsureMaterial(
                SpriteMaterialPath,
                ShaderDirectory + "/Hd2dStageSprite.shader",
                material =>
                {
                    material.SetFloat("_AmbientScale", 1.2f);
                    material.SetFloat("_MainLightScale", 0.6f);
                    material.SetFloat("_PointLightScale", 1f);
                    material.SetFloat("_Directionality", 0.45f);
                    material.SetColor("_Lift", new Color(0.4f, 0.38f, 0.36f, 0f));
                }
            );
            EnsureMaterial(
                ContactShadowMaterialPath,
                ShaderDirectory + "/Hd2dContactShadow.shader",
                null
            );
            // The soft shade under the scenery (Shades): wide and faint, the sky's light hidden
            // where a rock or a crate stands, not a shadow cast by a light.
            EnsureMaterial(
                PropShadeMaterialPath,
                ShaderDirectory + "/Hd2dContactShadow.shader",
                material =>
                {
                    material.SetColor("_ShadowColor", new Color(0.03f, 0.03f, 0.05f, 1f));
                    material.SetFloat("_Strength", 0.5f);
                    material.SetFloat("_Softness", 0.85f);
                }
            );
            // The flame's layout on its board (root height, flame width and height) is read back by
            // Flame to size the board, so the stages only give the size of the fire itself.
            EnsureMaterial(
                FlameMaterialPath,
                ShaderDirectory + "/Hd2dFlame.shader",
                material =>
                {
                    material.SetFloat("_Intensity", 1.6f);
                    material.SetFloat("_Halo", 0.3f);
                    material.SetFloat("_Turbulence", 1f);
                    material.SetFloat("_Speed", 2.2f);
                    material.SetFloat("_Puff", 1.6f);
                    material.SetFloat("_Base", 0.1f);
                    material.SetFloat("_FlameWidth", 0.5f);
                    material.SetFloat("_FlameHeight", 0.5f);
                }
            );
            EnsureMaterial(
                EmberMaterialPath,
                ShaderDirectory + "/Hd2dGlowParticle.shader",
                material =>
                {
                    material.SetColor("_Color", new Color(1f, 0.62f, 0.25f, 1f));
                    material.SetFloat("_Intensity", 2.5f);
                }
            );
            // Sunlight through the leaves: warm, soft beams and the dust and pollen floating in
            // them, both added on top of the stage.
            EnsureMaterial(
                LightBeamMaterialPath,
                ShaderDirectory + "/Hd2dLightBeam.shader",
                material =>
                {
                    material.SetColor("_Color", new Color(1f, 0.9f, 0.66f, 1f));
                    material.SetFloat("_Intensity", 0.6f);
                    material.SetFloat("_EdgeSoftness", 0.7f);
                    material.SetFloat("_StripeScale", 3f);
                    material.SetFloat("_StripeAmount", 0.5f);
                    material.SetFloat("_Speed", 0.1f);
                }
            );
            EnsureMaterial(
                MoteMaterialPath,
                ShaderDirectory + "/Hd2dGlowParticle.shader",
                material =>
                {
                    material.SetColor("_Color", new Color(1f, 0.95f, 0.75f, 1f));
                    material.SetFloat("_Intensity", 2f);
                }
            );
            EnsureLeafCookie();
        }

        // A tiling mask of light and leaf shade for the sun's cookie: soft clusters of leaves
        // over full light, so the ground and scenery under the trees catch dappled sunlight.
        private static void EnsureLeafCookie()
        {
            const int size = 128;
            if (!File.Exists(LeafCookiePath))
            {
                var random = new System.Random(2718);
                var blobs = new List<Vector4>();
                for (int i = 0; i < 70; i++)
                    blobs.Add(
                        new Vector4(
                            (float)random.NextDouble() * size,
                            (float)random.NextDouble() * size,
                            8f + (float)random.NextDouble() * 18f,
                            0.35f + (float)random.NextDouble() * 0.5f
                        )
                    );
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                try
                {
                    for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float shade = 0f;
                        foreach (var blob in blobs)
                        {
                            // The shortest way round the tile, so the mask repeats seamlessly.
                            float dx = Mathf.Abs(x - blob.x);
                            float dy = Mathf.Abs(y - blob.y);
                            dx = Mathf.Min(dx, size - dx);
                            dy = Mathf.Min(dy, size - dy);
                            float distance = Mathf.Sqrt(dx * dx + dy * dy * 1.6f) / blob.z;
                            shade += blob.w * (1f - Mathf.SmoothStep(0.55f, 1f, distance));
                        }
                        float light = 1f - Mathf.Clamp01(shade) * 0.6f;
                        texture.SetPixel(x, y, new Color(light, light, light, light));
                    }
                    texture.Apply();
                    File.WriteAllBytes(LeafCookiePath, texture.EncodeToPNG());
                }
                finally
                {
                    Object.DestroyImmediate(texture);
                }
            }
            AssetDatabase.ImportAsset(LeafCookiePath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(LeafCookiePath);
            importer.textureType = TextureImporterType.Default;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = true;
            importer.sRGBTexture = false;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        // Made once, then set again on every build so a changed value in code reaches the asset.
        private static void EnsureMaterial(string path, string shaderPath, Action<Material> setup)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
                if (shader == null)
                    throw new InvalidOperationException("Shader not found: " + shaderPath);
                material = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
                AssetDatabase.CreateAsset(material, path);
            }
            setup?.Invoke(material);
            EditorUtility.SetDirty(material);
        }

        /// <summary>
        /// Loads a pixel-art texture (.aseprite) with sharp dots. Tiles repeat; boards clamp so
        /// their edges do not pick up the opposite side.
        /// </summary>
        public static Texture2D PixelTexture(string path, bool repeat)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(path) is UnityEditor.U2D.Aseprite.AsepriteImporter importer)
            {
                var wrap = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                if (importer.wrapMode != wrap)
                {
                    importer.wrapMode = wrap;
                    importer.SaveAndReimport();
                }
            }
            ArtAssets.ImportDrawn(path, mipmaps: false);
            return ArtAssets.LoadTexture(path);
        }

        /// <summary>
        /// Makes the normal map of a stage texture (<see cref="Hd2dRelief"/>) and saves it beside
        /// the texture as <c>&lt;name&gt;Normal.png</c>, one normal per dot, read sharp like the
        /// texture. It is worked out from the picture on every build, so a redrawn texture or a
        /// changed strength in code reaches it.
        /// </summary>
        public static Texture2D ReliefMap(Texture2D texture, float relief, float bevel = 0f)
        {
            var source = AssetDatabase.GetAssetPath(texture);
            var path =
                Path.GetDirectoryName(source).Replace('\\', '/')
                + "/"
                + Path.GetFileNameWithoutExtension(source)
                + "Normal.png";
            bool repeat = texture.wrapMode == TextureWrapMode.Repeat;
            var pixels = ReadPixels(texture);
            var normals = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            try
            {
                normals.SetPixels32(
                    Hd2dRelief.Normals(pixels, texture.width, texture.height, relief, bevel, repeat)
                );
                normals.Apply();
                var bytes = normals.EncodeToPNG();
                if (!File.Exists(path) || !File.ReadAllBytes(path).AsSpan().SequenceEqual(bytes))
                    File.WriteAllBytes(path, bytes);
            }
            finally
            {
                Object.DestroyImmediate(normals);
            }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            var wrap = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            if (
                importer.textureType != TextureImporterType.NormalMap
                || importer.filterMode != FilterMode.Point
                || importer.mipmapEnabled
                || importer.wrapMode != wrap
                || importer.textureCompression != TextureImporterCompression.Uncompressed
                || importer.npotScale != TextureImporterNPOTScale.None
            )
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.wrapMode = wrap;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // The texture's dots as drawn (sRGB values, rows from the bottom), read through a render
        // texture because the imported texture is not readable.
        private static Color32[] ReadPixels(Texture2D texture)
        {
            var target = RenderTexture.GetTemporary(
                texture.width,
                texture.height,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB
            );
            var previous = RenderTexture.active;
            var copy = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            try
            {
                Graphics.Blit(texture, target);
                RenderTexture.active = target;
                copy.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
                copy.Apply();
                return copy.GetPixels32();
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                Object.DestroyImmediate(copy);
            }
        }

        /// <summary>
        /// A plain lit material (URP Simple Lit) for the stage: the texture's colour lit by the
        /// stage's lights, no shine. Cut-outs drop the see-through parts and cast shaped shadows.
        /// With <paramref name="relief"/> (a normal map from <see cref="ReliefMap"/>), the stones'
        /// joints, the bark and the leaves turn to the lights like real bumps.
        /// </summary>
        public static Material LitMaterial(
            string path,
            Texture2D texture,
            Color tint,
            bool cutout = false,
            Color? glow = null,
            Texture2D relief = null
        )
        {
            var shader = Shader.Find(SimpleLitShader);
            if (shader == null)
                throw new InvalidOperationException("Shader not found: " + SimpleLitShader);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = material == null;
            if (created)
                material = new Material(shader);
            material.shader = shader;
            material.name = Path.GetFileNameWithoutExtension(path);
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", tint);
            // URP derives the keywords from these values when it checks the material again:
            // in Simple Lit, 1 means no shine, and emission needs an emissive GI flag.
            material.SetFloat("_SpecularHighlights", 1f);
            material.DisableKeyword("_SPECULAR_COLOR");
            material.SetFloat("_EnvironmentReflections", 0f);
            material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            material.SetFloat("_Smoothness", 0f);
            material.SetFloat("_ReceiveShadows", 1f);
            material.DisableKeyword("_RECEIVE_SHADOWS_OFF");
            material.SetFloat("_AlphaClip", cutout ? 1f : 0f);
            material.SetFloat("_Cutoff", 0.5f);
            material.SetFloat("_Cull", cutout ? (float)CullMode.Off : (float)CullMode.Back);
            // The bumps of the picture (ReliefMap) catch the torches and the low sun across them.
            material.SetTexture("_BumpMap", relief);
            if (relief != null)
                material.EnableKeyword("_NORMALMAP");
            else
                material.DisableKeyword("_NORMALMAP");
            // A far backdrop faces the camera and away from the key light, so it lights itself
            // a little (its own texture as emission) to read like the painted distance it is.
            if (glow is Color emission)
            {
                material.EnableKeyword("_EMISSION");
                material.SetTexture("_EmissionMap", texture);
                material.SetColor("_EmissionColor", emission);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
            {
                material.DisableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", Color.black);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }
            if (cutout)
            {
                material.EnableKeyword("_ALPHATEST_ON");
                material.SetOverrideTag("RenderType", "TransparentCutout");
                material.renderQueue = (int)RenderQueue.AlphaTest;
            }
            else
            {
                material.DisableKeyword("_ALPHATEST_ON");
                material.SetOverrideTag("RenderType", "Opaque");
                material.renderQueue = (int)RenderQueue.Geometry;
            }
            if (created)
                AssetDatabase.CreateAsset(material, path);
            else
                EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// A material for the leaves and grass of the stage (tufts, ferns, bushes, the canopy):
        /// a lit cut-out like <see cref="LitMaterial"/>, but the light from behind shows through
        /// the leaves (<paramref name="translucency"/>) and the tips sway in the wind by
        /// <paramref name="sway"/> m. <paramref name="hangsDown"/> sways the bottom of a board of
        /// leaves hanging from above instead of its top.
        /// </summary>
        public static Material FoliageMaterial(
            string path,
            Texture2D texture,
            Color tint,
            float sway,
            float translucency = 0.5f,
            bool hangsDown = false
        )
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(
                ShaderDirectory + "/Hd2dStageFoliage.shader"
            );
            if (shader == null)
                throw new InvalidOperationException("Foliage shader not found.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = material == null;
            if (created)
                material = new Material(shader);
            // A material that was Simple Lit before keeps none of its keywords or values.
            if (material.shader != shader)
            {
                material.shader = shader;
                material.shaderKeywords = Array.Empty<string>();
            }
            material.name = Path.GetFileNameWithoutExtension(path);
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", tint);
            material.SetFloat("_Cutoff", 0.5f);
            material.SetFloat("_Translucency", translucency);
            material.SetFloat("_Sway", sway);
            material.SetFloat("_SwaySpeed", 1.3f);
            material.SetFloat("_HangDown", hangsDown ? 1f : 0f);
            material.renderQueue = (int)RenderQueue.AlphaTest;
            if (created)
                AssetDatabase.CreateAsset(material, path);
            else
                EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// A material for a painted distance (the sky, far mountains): the picture's own colours,
        /// unlit and without fog, cut out where it is see-through. With <paramref name="starBoost"/>
        /// above 1, dots brighter than their neighbours (the stars) shine past white and bloom,
        /// and <paramref name="twinkle"/> makes them twinkle; with <paramref name="glowBoost"/> above 1,
        /// broad bright parts (the moon) shine past white too.
        /// </summary>
        public static Material PaintedMaterial(
            string path,
            Texture2D texture,
            Color tint,
            float starBoost = 1f,
            float twinkle = 0f,
            float glowBoost = 1f
        )
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(
                ShaderDirectory + "/Hd2dPaintedDistance.shader"
            );
            if (shader == null)
                throw new InvalidOperationException("Painted distance shader not found.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = material == null;
            if (created)
                material = new Material(shader);
            material.shader = shader;
            material.name = Path.GetFileNameWithoutExtension(path);
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", tint);
            material.SetFloat("_StarBoost", starBoost);
            material.SetFloat("_Twinkle", twinkle);
            material.SetFloat("_GlowBoost", glowBoost);
            if (created)
                AssetDatabase.CreateAsset(material, path);
            else
                EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// Collects the meshes of one stage in a single asset, made anew on every build so no
        /// mesh of an older layout is left behind.
        /// </summary>
        public sealed class MeshStore
        {
            private readonly string path;
            private readonly List<Mesh> meshes = new();

            public MeshStore(string path)
            {
                this.path = path;
                if (File.Exists(path))
                    AssetDatabase.DeleteAsset(path);
            }

            public Mesh Add(Mesh mesh)
            {
                mesh.RecalculateBounds();
                // Normal maps turn their bumps along the texture's own axes, which the tangents give.
                if (mesh.normals.Length > 0 && mesh.uv.Length > 0)
                    mesh.RecalculateTangents();
                if (meshes.Count == 0)
                    AssetDatabase.CreateAsset(mesh, path);
                else
                    AssetDatabase.AddObjectToAsset(mesh, path);
                meshes.Add(mesh);
                return mesh;
            }
        }

        /// <summary>
        /// A level floor centred on <paramref name="center"/>, cut into tiles of about
        /// <paramref name="chunk"/> m so that each tile picks its own nearby lights (the mobile
        /// renderer lights each object with a few lights only). One texture tile covers
        /// <paramref name="tileSize"/> m.
        /// </summary>
        public static GameObject Floor(
            Transform parent,
            MeshStore store,
            string name,
            Material material,
            Vector3 center,
            Vector2 size,
            float tileSize,
            float chunk
        )
        {
            var floor = Group(parent, name, Vector3.zero);
            int columns = Mathf.Max(1, Mathf.RoundToInt(size.x / chunk));
            int rows = Mathf.Max(1, Mathf.RoundToInt(size.y / chunk));
            var cell = new Vector2(size.x / columns, size.y / rows);
            var origin = center - new Vector3(size.x, 0f, size.y) * 0.5f;
            for (int z = 0; z < rows; z++)
            for (int x = 0; x < columns; x++)
            {
                var min = origin + new Vector3(x * cell.x, 0f, z * cell.y);
                var mesh = new Mesh { name = $"{name}_{x}_{z}" };
                mesh.vertices = new[]
                {
                    min,
                    min + new Vector3(0f, 0f, cell.y),
                    min + new Vector3(cell.x, 0f, cell.y),
                    min + new Vector3(cell.x, 0f, 0f),
                };
                mesh.uv = Array.ConvertAll(mesh.vertices, v => new Vector2(v.x, v.z) / tileSize);
                mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
                mesh.triangles = new[] { 0, 1, 2, 2, 3, 0 };
                Renderer(floor.transform, $"{name}_{x}_{z}", store.Add(mesh), material, false);
            }
            return floor;
        }

        /// <summary>
        /// A floor painted with one picture stretched over it (a stage with a pattern in the
        /// middle), cut into tiles like <see cref="Floor"/>.
        /// </summary>
        public static GameObject PaintedFloor(
            Transform parent,
            MeshStore store,
            string name,
            Material material,
            Vector3 center,
            Vector2 size,
            float chunk
        )
        {
            var floor = Group(parent, name, Vector3.zero);
            int columns = Mathf.Max(1, Mathf.RoundToInt(size.x / chunk));
            int rows = Mathf.Max(1, Mathf.RoundToInt(size.y / chunk));
            var cell = new Vector2(size.x / columns, size.y / rows);
            var origin = center - new Vector3(size.x, 0f, size.y) * 0.5f;
            for (int z = 0; z < rows; z++)
            for (int x = 0; x < columns; x++)
            {
                var min = origin + new Vector3(x * cell.x, 0f, z * cell.y);
                var mesh = new Mesh { name = $"{name}_{x}_{z}" };
                mesh.vertices = new[]
                {
                    min,
                    min + new Vector3(0f, 0f, cell.y),
                    min + new Vector3(cell.x, 0f, cell.y),
                    min + new Vector3(cell.x, 0f, 0f),
                };
                mesh.uv = Array.ConvertAll(
                    mesh.vertices,
                    v => new Vector2((v.x - origin.x) / size.x, (v.z - origin.z) / size.y)
                );
                mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
                mesh.triangles = new[] { 0, 1, 2, 2, 3, 0 };
                Renderer(floor.transform, $"{name}_{x}_{z}", store.Add(mesh), material, false);
            }
            return floor;
        }

        /// <summary>
        /// A wall standing on <paramref name="bottomCenter"/>, facing <paramref name="facing"/>
        /// (towards the room). One texture tile covers <paramref name="tileSize"/> m.
        /// </summary>
        public static GameObject Wall(
            Transform parent,
            MeshStore store,
            string name,
            Material material,
            Vector3 bottomCenter,
            Vector2 size,
            Vector3 facing,
            float tileSize,
            bool castShadows = false
        )
        {
            var normal = new Vector3(facing.x, 0f, facing.z).normalized;
            // Seen from the front, the texture runs left to right.
            var across = Vector3.Cross(normal, Vector3.up);
            var mesh = new Mesh { name = name };
            var left = bottomCenter - across * size.x * 0.5f;
            mesh.vertices = new[]
            {
                left,
                left + Vector3.up * size.y,
                left + across * size.x + Vector3.up * size.y,
                left + across * size.x,
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(0f, size.y / tileSize),
                new Vector2(size.x / tileSize, size.y / tileSize),
                new Vector2(size.x / tileSize, 0f),
            };
            mesh.normals = new[] { normal, normal, normal, normal };
            mesh.triangles = new[] { 0, 1, 2, 2, 3, 0 };
            return Renderer(parent, name, store.Add(mesh), material, castShadows);
        }

        /// <summary>
        /// A box (pillar, step, block) standing on <paramref name="bottomCenter"/> with every face
        /// textured at <paramref name="tileSize"/> m per tile. The bottom face is left out.
        /// </summary>
        public static GameObject Box(
            Transform parent,
            MeshStore store,
            string name,
            Material material,
            Vector3 bottomCenter,
            Vector3 size,
            float tileSize,
            bool castShadows = true
        )
        {
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            var half = new Vector3(size.x, 0f, size.z) * 0.5f;
            var min = bottomCenter - half;
            void Face(Vector3 origin, Vector3 u, Vector3 v, Vector3 normal)
            {
                int start = vertices.Count;
                vertices.Add(origin);
                vertices.Add(origin + v);
                vertices.Add(origin + u + v);
                vertices.Add(origin + u);
                float width = u.magnitude / tileSize;
                float height = v.magnitude / tileSize;
                uvs.Add(new Vector2(0f, 0f));
                uvs.Add(new Vector2(0f, height));
                uvs.Add(new Vector2(width, height));
                uvs.Add(new Vector2(width, 0f));
                for (int i = 0; i < 4; i++)
                    normals.Add(normal);
                triangles.AddRange(
                    new[] { start, start + 1, start + 2, start + 2, start + 3, start }
                );
            }
            var x = Vector3.right * size.x;
            var y = Vector3.up * size.y;
            var z = Vector3.forward * size.z;
            Face(min, x, y, Vector3.back);
            Face(min + x + z, -x, y, Vector3.forward);
            Face(min + z, -z, y, Vector3.left);
            Face(min + x, z, y, Vector3.right);
            Face(min + y, x, z, Vector3.up);
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            return Renderer(parent, name, store.Add(mesh), material, castShadows);
        }

        /// <summary>
        /// A cut-out board standing on <paramref name="bottomCenter"/> (a gate, a banner, a tree),
        /// facing <paramref name="facing"/>. Like a flat on a theatre stage, it keeps its own
        /// direction; set it square to the camera. <paramref name="mirror"/> flips the picture.
        /// </summary>
        public static GameObject Flat(
            Transform parent,
            MeshStore store,
            string name,
            Material material,
            Vector3 bottomCenter,
            Vector2 size,
            Vector3 facing,
            bool castShadows = true,
            bool mirror = false
        )
        {
            var normal = facing.normalized;
            var across = Vector3.Cross(new Vector3(normal.x, 0f, normal.z).normalized, Vector3.up);
            var up = Vector3.Cross(across, normal).normalized;
            var mesh = new Mesh { name = name };
            var left = bottomCenter - across * size.x * 0.5f;
            mesh.vertices = new[]
            {
                left,
                left + up * size.y,
                left + across * size.x + up * size.y,
                left + across * size.x,
            };
            float u0 = mirror ? 1f : 0f;
            float u1 = mirror ? 0f : 1f;
            mesh.uv = new[]
            {
                new Vector2(u0, 0f),
                new Vector2(u0, 1f),
                new Vector2(u1, 1f),
                new Vector2(u1, 0f),
            };
            mesh.normals = new[] { normal, normal, normal, normal };
            mesh.triangles = new[] { 0, 1, 2, 2, 3, 0 };
            return Renderer(parent, name, store.Add(mesh), material, castShadows);
        }

        /// <summary>
        /// One of many small things laid over a stage with one material (<see cref="Boards"/>,
        /// <see cref="Decals"/>, <see cref="Shades"/>): where it stands or lies, how large it is,
        /// whether its picture is mirrored and, lying on the ground, how many quarter turns it is
        /// turned. <see cref="Strength"/> is how dark a shade is.
        /// </summary>
        public readonly struct Piece
        {
            public readonly Vector3 Position;
            public readonly Vector2 Size;
            public readonly bool Mirror;
            public readonly int Turns;
            public readonly float Strength;

            public Piece(
                Vector3 position,
                Vector2 size,
                bool mirror = false,
                int turns = 0,
                float strength = 1f
            )
            {
                Position = position;
                Size = size;
                Mirror = mirror;
                Turns = turns;
                Strength = strength;
            }
        }

        /// <summary>
        /// Many cut-out boards of one picture (tufts of grass, small rocks) standing on their
        /// <see cref="Piece.Position"/> and facing the camera like <see cref="Flat"/>, merged into
        /// one mesh for every <paramref name="chunk"/> m of ground, so a field of them costs a few
        /// draws and each part still picks its own nearby lights.
        /// </summary>
        public static GameObject Boards(
            Transform parent,
            MeshStore store,
            string name,
            Material material,
            IReadOnlyList<Piece> pieces,
            float chunk,
            bool castShadows = true
        ) =>
            Merged(
                parent,
                store,
                name,
                material,
                pieces,
                chunk,
                castShadows,
                (piece, quad) =>
                {
                    var left = piece.Position - Vector3.right * piece.Size.x * 0.5f;
                    quad[0] = left;
                    quad[1] = left + Vector3.up * piece.Size.y;
                    quad[2] = left + new Vector3(piece.Size.x, piece.Size.y, 0f);
                    quad[3] = left + Vector3.right * piece.Size.x;
                },
                Vector3.back
            );

        /// <summary>
        /// Pictures laid flat on the ground (fallen leaves, moss, drifted sand), a few millimetres
        /// above it, to break up the repeating tiles. They are turned only by quarter turns, so
        /// their dots stay on the same grid as the ground's. Merged like <see cref="Boards"/>.
        /// </summary>
        public static GameObject Decals(
            Transform parent,
            MeshStore store,
            string name,
            Material material,
            IReadOnlyList<Piece> pieces,
            float chunk
        ) => Merged(parent, store, name, material, pieces, chunk, false, GroundQuad, Vector3.up);

        /// <summary>
        /// Soft dark ellipses on the ground under the scenery (with the contact shadow material),
        /// where the sky's light is hidden by the thing standing there, so it sits on the ground
        /// instead of touching it along a sharp line. Merged like <see cref="Boards"/>; each
        /// piece's <see cref="Piece.Strength"/> goes to the vertex colour's alpha.
        /// </summary>
        public static GameObject Shades(
            Transform parent,
            MeshStore store,
            string name,
            Material material,
            IReadOnlyList<Piece> pieces,
            float chunk
        ) => Merged(parent, store, name, material, pieces, chunk, false, GroundQuad, Vector3.up);

        private static void GroundQuad(Piece piece, Vector3[] quad)
        {
            var half = new Vector3(piece.Size.x, 0f, piece.Size.y) * 0.5f;
            quad[0] = piece.Position + new Vector3(-half.x, 0f, -half.z);
            quad[1] = piece.Position + new Vector3(-half.x, 0f, half.z);
            quad[2] = piece.Position + new Vector3(half.x, 0f, half.z);
            quad[3] = piece.Position + new Vector3(half.x, 0f, -half.z);
        }

        private static GameObject Merged(
            Transform parent,
            MeshStore store,
            string name,
            Material material,
            IReadOnlyList<Piece> pieces,
            float chunk,
            bool castShadows,
            Action<Piece, Vector3[]> corners,
            Vector3 normal
        )
        {
            var group = Group(parent, name, Vector3.zero);
            var cells = new SortedDictionary<(int, int), List<Piece>>();
            foreach (var piece in pieces)
            {
                var key = (
                    Mathf.FloorToInt(piece.Position.x / chunk),
                    Mathf.FloorToInt(piece.Position.z / chunk)
                );
                if (!cells.TryGetValue(key, out var list))
                    cells[key] = list = new List<Piece>();
                list.Add(piece);
            }
            var quad = new Vector3[4];
            foreach (var ((cx, cz), list) in cells)
            {
                var vertices = new List<Vector3>();
                var uvs = new List<Vector2>();
                var normals = new List<Vector3>();
                var colors = new List<Color>();
                var triangles = new List<int>();
                foreach (var piece in list)
                {
                    corners(piece, quad);
                    int start = vertices.Count;
                    vertices.AddRange(quad);
                    var corner = new[]
                    {
                        new Vector2(0f, 0f),
                        new Vector2(0f, 1f),
                        new Vector2(1f, 1f),
                        new Vector2(1f, 0f),
                    };
                    for (int i = 0; i < 4; i++)
                    {
                        // Quarter turns of the picture move each corner's UV one corner round.
                        var uv = corner[(i + piece.Turns % 4 + 4) % 4];
                        if (piece.Mirror)
                            uv.x = 1f - uv.x;
                        uvs.Add(uv);
                        normals.Add(normal);
                        colors.Add(new Color(1f, 1f, 1f, piece.Strength));
                    }
                    triangles.AddRange(
                        new[] { start, start + 1, start + 2, start + 2, start + 3, start }
                    );
                }
                var mesh = new Mesh { name = $"{name}_{cx}_{cz}" };
                mesh.SetVertices(vertices);
                mesh.SetUVs(0, uvs);
                mesh.SetNormals(normals);
                mesh.SetColors(colors);
                mesh.SetTriangles(triangles, 0);
                Renderer(group.transform, mesh.name, store.Add(mesh), material, castShadows);
            }
            return group;
        }

        /// <summary>
        /// Places up to <paramref name="count"/> points at random (the same ones for the same
        /// <paramref name="seed"/>) in <paramref name="area"/> (x and z), at least
        /// <paramref name="spacing"/> m apart and only where <paramref name="allowed"/> says so.
        /// </summary>
        public static List<Vector2> Scatter(
            int seed,
            int count,
            Rect area,
            float spacing,
            Func<Vector2, bool> allowed
        )
        {
            var random = new System.Random(seed);
            var points = new List<Vector2>();
            for (int attempt = 0; attempt < count * 30 && points.Count < count; attempt++)
            {
                var point = new Vector2(
                    area.xMin + (float)random.NextDouble() * area.width,
                    area.yMin + (float)random.NextDouble() * area.height
                );
                if (!allowed(point))
                    continue;
                bool crowded = false;
                foreach (var other in points)
                    if ((other - point).sqrMagnitude < spacing * spacing)
                    {
                        crowded = true;
                        break;
                    }
                if (!crowded)
                    points.Add(point);
            }
            return points;
        }

        /// <summary>A point light, flickering like a flame when <paramref name="flicker"/> is above 0.</summary>
        public static Light PointLight(
            Transform parent,
            string name,
            Vector3 position,
            Color color,
            float intensity,
            float range,
            bool shadows = false,
            float flicker = 0f,
            float seed = 0f
        )
        {
            var light = Group(parent, name, position).AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = shadows ? LightShadows.Hard : LightShadows.None;
            light.shadowStrength = 0.85f;
            light.renderMode = LightRenderMode.ForcePixel;
            light.lightmapBakeType = LightmapBakeType.Realtime;
            if (flicker > 0f)
            {
                var wobble = light.gameObject.AddComponent<Hd2dLightFlicker>();
                wobble.IntensityAmount = flicker;
                wobble.Seed = seed;
            }
            return light;
        }

        /// <summary>
        /// The key light: a directional light casting the stage's main shadows (the characters'
        /// shadow boards turn to it).
        /// </summary>
        public static Light KeyLight(
            Transform parent,
            string name,
            Vector3 euler,
            Color color,
            float intensity,
            float shadowStrength
        )
        {
            var light = Group(parent, name, Vector3.zero).AddComponent<Light>();
            light.transform.rotation = Quaternion.Euler(euler);
            light.type = LightType.Directional;
            light.color = color;
            light.intensity = intensity;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = shadowStrength;
            light.shadowBias = 0.05f;
            light.shadowNormalBias = 0.4f;
            light.lightmapBakeType = LightmapBakeType.Realtime;
            return light;
        }

        /// <summary>
        /// A flame drawn by the flame shader on a board that turns to the camera.
        /// <paramref name="root"/> is where the fire burns from and <paramref name="size"/> the
        /// width and height of the fire itself; the board is made larger by the material's layout
        /// to hold the glow, the tips breaking off and the smoke. Put the root a little below the
        /// rim of its bowl and just behind the bowl's board, so the bowl hides the base of the fire
        /// and the flames rise out of it.
        /// </summary>
        public static GameObject Flame(
            Transform parent,
            string name,
            Vector3 root,
            Vector2 size,
            Material material
        )
        {
            float rootHeight = material.GetFloat("_Base");
            var board = new Vector2(
                size.x / material.GetFloat("_FlameWidth"),
                size.y / material.GetFloat("_FlameHeight")
            );
            var quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            var flame = Renderer(parent, name, quad, material, false);
            flame.transform.position = root + Vector3.up * board.y * (0.5f - rootHeight);
            flame.transform.localScale = new Vector3(board.x, board.y, 1f);
            return flame;
        }

        /// <summary>Embers rising from a flame: a few glowing dots drifting up and fading.</summary>
        public static ParticleSystem Embers(
            Transform parent,
            string name,
            Vector3 position,
            Material material,
            float rate,
            float spread,
            float size
        )
        {
            var embers = Group(parent, name, position).AddComponent<ParticleSystem>();
            embers.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            var main = embers.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 2.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.85f, 0.5f, 1f),
                new Color(1f, 0.5f, 0.2f, 1f)
            );
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 48;
            main.gravityModifier = -0.04f;
            var emission = embers.emission;
            emission.rateOverTime = rate;
            var shape = embers.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 18f;
            shape.radius = spread;
            var noise = embers.noise;
            noise.enabled = true;
            noise.strength = 0.35f;
            noise.frequency = 0.6f;
            noise.scrollSpeed = 0.4f;
            var color = embers.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.9f, 0.6f), 0f),
                    new GradientColorKey(new Color(1f, 0.35f, 0.1f), 1f),
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.15f),
                    new GradientAlphaKey(0f, 1f),
                }
            );
            color.color = gradient;
            var renderer = embers.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return embers;
        }

        /// <summary>
        /// Lays the leaf mask (<see cref="LeafCookiePath"/>) over a directional light, one tile
        /// every <paramref name="size"/> m, for sunlight dappled by the trees above.
        /// </summary>
        public static void LeafCookie(Light sun, float size)
        {
            sun.cookie = AssetDatabase.LoadAssetAtPath<Texture2D>(LeafCookiePath);
            var data = sun.GetUniversalAdditionalLightData();
            data.lightCookieSize = new Vector2(size, size);
            data.lightCookieOffset = Vector2.zero;
        }

        /// <summary>
        /// A beam of light from <paramref name="top"/> down towards <paramref name="bottom"/>,
        /// <paramref name="width"/> m wide, drawn by the light beam shader on a board that turns
        /// round the beam to the camera.
        /// </summary>
        public static GameObject LightBeam(
            Transform parent,
            string name,
            Vector3 top,
            Vector3 bottom,
            float width,
            Material material
        )
        {
            var quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            var beam = Renderer(parent, name, quad, material, false);
            beam.GetComponent<MeshRenderer>().receiveShadows = false;
            var axis = top - bottom;
            beam.transform.position = (top + bottom) * 0.5f;
            beam.transform.rotation = Quaternion.FromToRotation(Vector3.up, axis.normalized);
            beam.transform.localScale = new Vector3(width, axis.magnitude, 1f);
            return beam;
        }

        /// <summary>
        /// Dust and pollen floating in sunlight (or fireflies at night): soft specks drifting
        /// slowly in a box of <paramref name="size"/> m around <paramref name="center"/>, fading in
        /// and out. <paramref name="tint"/> colours them (warm white when left out).
        /// </summary>
        public static ParticleSystem Motes(
            Transform parent,
            string name,
            Vector3 center,
            Vector3 size,
            Material material,
            int count,
            Color? tint = null
        )
        {
            var motes = Group(parent, name, center).AddComponent<ParticleSystem>();
            var main = motes.main;
            main.loop = true;
            main.playOnAwake = true;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.05f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
            main.startColor = tint is Color speck
                ? new ParticleSystem.MinMaxGradient(speck, speck * 0.8f)
                : new ParticleSystem.MinMaxGradient(
                    new Color(1f, 0.97f, 0.85f, 1f),
                    new Color(1f, 0.85f, 0.55f, 1f)
                );
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = count * 2;
            main.gravityModifier = -0.002f;
            var emission = motes.emission;
            emission.rateOverTime = count / 7f;
            var shape = motes.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = size;
            var noise = motes.noise;
            noise.enabled = true;
            noise.strength = 0.18f;
            noise.frequency = 0.25f;
            noise.scrollSpeed = 0.15f;
            var color = motes.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f),
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.3f),
                    new GradientAlphaKey(1f, 0.7f),
                    new GradientAlphaKey(0f, 1f),
                }
            );
            color.color = gradient;
            var renderer = motes.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return motes;
        }

        /// <summary>
        /// A post-processing profile (the "look" of a stage), made once and then filled again
        /// from code on every build, so a changed value in code reaches the asset.
        /// </summary>
        public static VolumeProfile Profile(string path, Action<VolumeProfile> setup)
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }
            foreach (var component in profile.components.ToArray())
            {
                profile.components.Remove(component);
                Object.DestroyImmediate(component, true);
            }
            setup(profile);
            foreach (var component in profile.components)
            {
                component.name = component.GetType().Name;
                component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(component, profile);
            }
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        public static GameObject Group(Transform parent, string name, Vector3 position)
        {
            var group = new GameObject(name);
            group.transform.SetParent(parent, false);
            group.transform.position = position;
            return group;
        }

        private static GameObject Renderer(
            Transform parent,
            string name,
            Mesh mesh,
            Material material,
            bool castShadows
        )
        {
            var item = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            item.transform.SetParent(parent, false);
            item.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = item.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return item;
        }

        /// <summary>The sorting order of the characters' boards (<see cref="Hd2dUiBillboard"/>).</summary>
        public const int BoardOrder = 10;

        /// <summary>
        /// Lets a UI picture stand on the 3D stage when the screen has one: the picture's owner
        /// gets a <see cref="Hd2dUiBillboard"/>, with the 2D shadow under its feet.
        /// </summary>
        public static Hd2dUiBillboard Stand(
            GameObject owner,
            UnityEngine.UI.Graphic picture,
            UnityEngine.UI.Graphic footShadow
        )
        {
            var board = owner.GetComponent<Hd2dUiBillboard>();
            if (board == null)
                board = owner.AddComponent<Hd2dUiBillboard>();
            board.Picture = picture;
            board.FootShadow = footShadow;
            board.SortingOrder = BoardOrder;
            return board;
        }

        /// <summary>
        /// Draws a layer of a camera canvas in front of the characters' boards (an order above
        /// <see cref="BoardOrder"/>) or behind them (below it), with a canvas of its own.
        /// </summary>
        public static Canvas SortLayer(RectTransform layer, int order)
        {
            var canvas = layer.GetComponent<Canvas>();
            if (canvas == null)
                canvas = layer.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = order;
            // The effects' shapes take their seed and moment from the second UV.
            canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1;
            return canvas;
        }

        /// <summary>Marks a part as belonging to the screen's 2D look only (<see cref="Hd2dFlatOnly"/>).</summary>
        public static void FlatOnly(GameObject part)
        {
            if (part.GetComponent<Hd2dFlatOnly>() == null)
                part.AddComponent<Hd2dFlatOnly>();
        }

        /// <summary>Destroys a built object without leaving it in the scene.</summary>
        public static void Discard(Object target)
        {
            if (target != null)
                Object.DestroyImmediate(target);
        }
    }
}
