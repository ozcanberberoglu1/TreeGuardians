using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.U2D.Animation;

namespace TreeGuardians.Editor.Animals
{
    /// Rigs a body-part sheet (body, head, arms, legs on one 1024x1024 texture) with the exact 15-bone skeleton used by
    /// yabandomuzu/ördek, so the shared Idle/Walk/Jump clips and YabanDomuzuController drive it unchanged.
    /// Pipeline mirrors the Skinning Editor: importer settings copied from the template, bones fitted per body part,
    /// Auto Geometry (outline + triangulation) and Auto Weights (bounded biharmonic) via the 2D Animation package code,
    /// depth-sorted triangles, then a prefab with SpriteRenderer + SpriteSkin + Animator.
    public static class AnimalRigBuilder
    {
        public const string ArtRoot = "Assets/Art";
        public const string TemplateName = "yabandomuzu";
        public const string ControllerPath = ArtRoot + "/animation/YabanDomuzuController.controller";
        public const string LitSpriteMaterialGuid = "a97c105638bdf8b4a8650670310a4cd3";
        public static readonly string[] NewAnimals = { "kartal", "kedi", "maymun" };
        /// Hand-rigged references that must never be re-rigged by the tool.
        public static readonly string[] HandRigged = { "yabandomuzu", "ördek" };

        /// Folder names come back from the file system in NFD on macOS ("ördek"); compare in NFC.
        public static bool IsHandRigged(string name)
        {
            string n = name.Normalize(System.Text.NormalizationForm.FormC);
            foreach (var h in HandRigged) if (h.Normalize(System.Text.NormalizationForm.FormC) == n) return true;
            return false;
        }

        /// Every Assets/Art/<name>/<name>.png sheet (hand-rigged references first).
        public static List<string> DiscoverAnimals()
        {
            var list = new List<string>();
            foreach (var dir in System.IO.Directory.GetDirectories(ArtRoot))
            {
                string name = System.IO.Path.GetFileName(dir);
                if (name.StartsWith("_") || name == "animation") continue;
                if (System.IO.File.Exists(System.IO.Path.Combine(dir, name + ".png"))) list.Add(name);
            }
            list.Sort((a, b) =>
            {
                int ia = IsHandRigged(a) ? System.Array.FindIndex(HandRigged, h => h.Normalize(System.Text.NormalizationForm.FormC) == a.Normalize(System.Text.NormalizationForm.FormC)) : -1;
                int ib = IsHandRigged(b) ? System.Array.FindIndex(HandRigged, h => h.Normalize(System.Text.NormalizationForm.FormC) == b.Normalize(System.Text.NormalizationForm.FormC)) : -1;
                if (ia >= 0 || ib >= 0) return (ia >= 0 ? ia : 99).CompareTo(ib >= 0 ? ib : 99);
                return string.Compare(a, b, StringComparison.Ordinal);
            });
            return list;
        }

        [MenuItem("Tree Guardians/Animals/Rig All Sheets In Assets-Art (auto-discover)", priority = 99)]
        public static void RigAllDiscovered()
        {
            foreach (var name in DiscoverAnimals())
            {
                if (IsHandRigged(name)) continue;
                Rig(name);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        const int AlphaTolerance = 10;
        const float OutlineDetail = 0.10f;
        const float WeightTolerance = 0.01f;
        const int MinComponentArea = 400;
        const float MatchDistance = 320f;

        [MenuItem("Tree Guardians/Animals/Rig New Animals (kartal, kedi, maymun)", priority = 100)]
        public static void RigAllNew()
        {
            foreach (var name in NewAnimals) Rig(name);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("Tree Guardians/Animals/Rig kartal", priority = 110)] static void RigKartal() => Rig("kartal");
        [MenuItem("Tree Guardians/Animals/Rig kedi", priority = 111)] static void RigKedi() => Rig("kedi");
        [MenuItem("Tree Guardians/Animals/Rig maymun", priority = 112)] static void RigMaymun() => Rig("maymun");

        public static string TexturePath(string name) => $"{ArtRoot}/{name}/{name}.png";
        public static string PrefabPath(string name) => $"{ArtRoot}/{name}/{name}.prefab";

        // ------------------------------------------------------------------ entry
        public static void Rig(string name)
        {
            string texPath = TexturePath(name);
            var importer = AssetImporter.GetAtPath(texPath) as TextureImporter;
            var templateImporter = AssetImporter.GetAtPath(TexturePath(TemplateName)) as TextureImporter;
            if (importer == null || templateImporter == null)
            {
                Debug.LogError($"[TG] Rig '{name}': importer missing ({texPath} / template).");
                return;
            }

            try
            {
                EditorUtility.DisplayProgressBar("Rig " + name, "Importer settings", 0.05f);
                CopyImporterSettings(templateImporter, importer);

                EditorUtility.DisplayProgressBar("Rig " + name, "Reading template skeleton", 0.15f);
                var templateProvider = OpenProvider(templateImporter, out var templateRect);
                var templateBones = templateProvider.GetDataProvider<ISpriteBoneDataProvider>().GetBones(templateRect.spriteID);
                var templateTex = templateProvider.GetDataProvider<ITextureDataProvider>();
                var templateParts = FindParts(templateTex.GetReadableTexture2D());

                var provider = OpenProvider(importer, out var rect);
                var tex = provider.GetDataProvider<ITextureDataProvider>();
                var readable = tex.GetReadableTexture2D();
                var parts = FindParts(readable);
                float ppu = importer.spritePixelsPerUnit;

                EditorUtility.DisplayProgressBar("Rig " + name, "Fitting bones to body parts", 0.3f);
                var bones = FitSkeleton(templateBones, templateParts, parts, out var unmatchedChains, out var log);

                EditorUtility.DisplayProgressBar("Rig " + name, "Auto geometry", 0.45f);
                GenerateGeometry(tex, rect.rect, out var vertices, out var edges, out var indices);

                EditorUtility.DisplayProgressBar("Rig " + name, "Auto weights", 0.6f);
                var weights = GenerateWeights(name, bones, vertices, indices, edges);

                EditorUtility.DisplayProgressBar("Rig " + name, "Mirroring single arm to back arm", 0.75f);
                string armNote = DuplicateMissingLimb(bones, unmatchedChains, "kol", "kol2", ref vertices, ref indices, ref edges, ref weights);

                SortTrianglesByDepth(bones, vertices, weights, ref indices);

                EditorUtility.DisplayProgressBar("Rig " + name, "Writing sprite data", 0.85f);
                WriteSkinning(provider, rect.spriteID, bones, vertices, weights, indices, edges);
                importer.SaveAndReimport();

                EditorUtility.DisplayProgressBar("Rig " + name, "Building prefab", 0.95f);
                BuildPrefab(name, bones, ppu);

                Debug.Log($"[TG] Rigged '{name}': {bones.Count} bones, {vertices.Length} vertices, {indices.Length / 3} triangles, parts={parts.Count}.\n{log}{armNote}");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        // ------------------------------------------------------------------ importer
        static void CopyImporterSettings(TextureImporter from, TextureImporter to)
        {
            var settings = new TextureImporterSettings();
            from.ReadTextureSettings(settings);
            settings.spriteMode = (int)SpriteImportMode.Single;
            to.SetTextureSettings(settings);
            to.spriteImportMode = SpriteImportMode.Single;
            to.spritePixelsPerUnit = from.spritePixelsPerUnit;
            to.spritePivot = from.spritePivot;
            to.textureType = from.textureType;
            to.maxTextureSize = from.maxTextureSize;
            to.isReadable = from.isReadable;
            to.mipmapEnabled = from.mipmapEnabled;
            to.filterMode = from.filterMode;
            to.alphaIsTransparency = from.alphaIsTransparency;
            to.textureCompression = from.textureCompression;
            to.SetPlatformTextureSettings(from.GetDefaultPlatformTextureSettings());
            foreach (var platform in new[] { "Standalone", "Android", "iPhone" })
            {
                var ps = from.GetPlatformTextureSettings(platform);
                if (ps != null && ps.overridden) to.SetPlatformTextureSettings(ps);
            }
            EditorUtility.SetDirty(to);
            to.SaveAndReimport();
        }

        static ISpriteEditorDataProvider OpenProvider(TextureImporter importer, out SpriteRect rect)
        {
            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            var rects = provider.GetSpriteRects();
            if (rects == null || rects.Length == 0) throw new InvalidOperationException("No sprite rect on " + importer.assetPath);
            rect = rects[0];
            if (rect.spriteID == default)
            {
                rect.spriteID = GUID.Generate();
                provider.SetSpriteRects(rects);
                provider.Apply();
                importer.SaveAndReimport();
                provider = factory.GetSpriteEditorDataProviderFromObject(importer);
                provider.InitSpriteEditorDataProvider();
                rect = provider.GetSpriteRects()[0];
            }
            return provider;
        }

        // ------------------------------------------------------------------ body parts
        public sealed class Part
        {
            public int area;
            public int minX, minY, maxX, maxY;
            public Vector2 Center => new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
            public float Height => maxY - minY + 1;
            public float Width => maxX - minX + 1;
            public Vector2 TopCenter => new Vector2(Center.x, maxY);
            public Vector2 BottomCenter => new Vector2(Center.x, minY);
            public bool Contains(Vector2 p, float pad) => p.x >= minX - pad && p.x <= maxX + pad && p.y >= minY - pad && p.y <= maxY + pad;
            public override string ToString() => $"[{minX},{minY}]-[{maxX},{maxY}] area={area}";
        }

        /// Connected opaque regions (y-up pixel space, like sprite bone/mesh data).
        public static List<Part> FindParts(Texture2D readable)
        {
            int w = readable.width, h = readable.height;
            var px = readable.GetPixels32();
            var seen = new bool[w * h];
            var stack = new Stack<int>();
            var parts = new List<Part>();
            for (int i = 0; i < px.Length; i++)
            {
                if (seen[i] || px[i].a <= AlphaTolerance) continue;
                var part = new Part { minX = w, minY = h, maxX = -1, maxY = -1 };
                stack.Push(i);
                seen[i] = true;
                while (stack.Count > 0)
                {
                    int p = stack.Pop();
                    int x = p % w, y = p / w;
                    part.area++;
                    if (x < part.minX) part.minX = x;
                    if (x > part.maxX) part.maxX = x;
                    if (y < part.minY) part.minY = y;
                    if (y > part.maxY) part.maxY = y;
                    if (x > 0) Visit(p - 1);
                    if (x < w - 1) Visit(p + 1);
                    if (y > 0) Visit(p - w);
                    if (y < h - 1) Visit(p + w);
                }
                if (part.area >= MinComponentArea) parts.Add(part);
            }
            parts.Sort((a, b) => b.area.CompareTo(a.area));
            return parts;

            void Visit(int n)
            {
                if (!seen[n] && px[n].a > AlphaTolerance) { seen[n] = true; stack.Push(n); }
            }
        }

        // ------------------------------------------------------------------ skeleton fitting
        struct BoneWorld { public Vector2 pos; public Quaternion rot; public Vector2 end; }

        static BoneWorld[] ComputeWorld(IList<SpriteBone> bones)
        {
            var world = new BoneWorld[bones.Count];
            for (int i = 0; i < bones.Count; i++)
            {
                var b = bones[i];
                if (b.parentId < 0)
                {
                    world[i].pos = b.position;
                    world[i].rot = b.rotation;
                }
                else
                {
                    var p = world[b.parentId];
                    world[i].pos = p.pos + (Vector2)(p.rot * (Vector3)(Vector2)b.position);
                    world[i].rot = p.rot * b.rotation;
                }
                world[i].end = world[i].pos + (Vector2)(world[i].rot * new Vector3(b.length, 0f, 0f));
            }
            return world;
        }

        static List<int> Chain(IList<SpriteBone> bones, int root)
        {
            var list = new List<int> { root };
            for (int i = 0; i < list.Count; i++)
                for (int j = 0; j < bones.Count; j++)
                    if (bones[j].parentId == list[i]) list.Add(j);
            return list;
        }

        static int IndexOf(IList<SpriteBone> bones, string boneName)
        {
            for (int i = 0; i < bones.Count; i++) if (bones[i].name == boneName) return i;
            return -1;
        }

        static Part NearestPart(List<Part> parts, Vector2 p)
        {
            Part best = null;
            float bestD = float.MaxValue;
            foreach (var part in parts)
            {
                if (part.Contains(p, 24f)) return part;
                float d = Vector2.Distance(part.Center, p);
                if (d < bestD) { bestD = d; best = part; }
            }
            return best;
        }

        /// Clones the template skeleton and moves/scales each limb chain onto the matching body part of the new sheet.
        static List<SpriteBone> FitSkeleton(List<SpriteBone> template, List<Part> templateParts, List<Part> parts, out List<string> unmatchedChains, out string log)
        {
            var bones = new List<SpriteBone>(template.Count);
            foreach (var b in template)
                bones.Add(new SpriteBone { name = b.name, guid = GUID.Generate().ToString(), position = b.position, rotation = b.rotation, length = b.length, parentId = b.parentId, color = b.color });

            var world = ComputeWorld(template);
            int root = bones.FindIndex(b => b.parentId < 0);
            var chainRoots = new List<int>();
            for (int i = 0; i < bones.Count; i++) if (bones[i].parentId == root) chainRoots.Add(i);

            // Template part per chain root (and body for the root bone).
            var templatePartOf = new Dictionary<int, Part> { [root] = NearestPart(templateParts, world[root].pos) };
            foreach (int c in chainRoots) templatePartOf[c] = NearestPart(templateParts, world[c].pos);
            // Body bone shares the body part with the root.
            int bodyBone = IndexOf(bones, "Body");
            if (bodyBone >= 0) templatePartOf[bodyBone] = templatePartOf[root];

            // Greedy one-to-one match of new parts to template parts by center distance.
            var match = new Dictionary<Part, Part>();
            var pairs = new List<(float d, Part t, Part n)>();
            foreach (var t in templateParts) foreach (var n in parts) pairs.Add((Vector2.Distance(t.Center, n.Center), t, n));
            pairs.Sort((a, b) => a.d.CompareTo(b.d));
            var usedNew = new HashSet<Part>();
            foreach (var (d, t, n) in pairs)
            {
                if (d > MatchDistance || match.ContainsKey(t) || usedNew.Contains(n)) continue;
                match[t] = n;
                usedNew.Add(n);
            }

            var sb = new System.Text.StringBuilder();
            unmatchedChains = new List<string>();

            // Root (BodyCenter): translate by the body part's bottom-center delta; scale its own and Body's length by body height.
            var rootPart = templatePartOf[root];
            Vector2 rootWorldNew = world[root].pos;
            float bodyScale = 1f;
            if (rootPart != null && match.TryGetValue(rootPart, out var newBody))
            {
                rootWorldNew += newBody.BottomCenter - rootPart.BottomCenter;
                bodyScale = Mathf.Clamp(newBody.Height / rootPart.Height, 0.6f, 1.6f);
                sb.AppendLine($"  body: {rootPart} -> {newBody} scale={bodyScale:0.00}");
            }
            var rb = bones[root]; rb.position = new Vector3(rootWorldNew.x, rootWorldNew.y, rb.position.z); rb.length *= bodyScale; bones[root] = rb;
            if (bodyBone >= 0)
            {
                var bb = bones[bodyBone]; bb.position = new Vector3(bb.position.x * bodyScale, bb.position.y * bodyScale, bb.position.z); bb.length *= bodyScale; bones[bodyBone] = bb;
            }
            Quaternion rootRotInv = Quaternion.Inverse(world[root].rot);

            foreach (int c in chainRoots)
            {
                if (c == bodyBone) continue;
                var chain = Chain(bones, c);
                var tPart = templatePartOf[c];
                bool isHead = bones[c].name == "head";
                Vector2 rootWorld = world[c].pos;
                float scale = 1f;
                if (tPart != null && match.TryGetValue(tPart, out var nPart))
                {
                    Vector2 anchorOld = isHead ? tPart.BottomCenter : tPart.TopCenter;
                    Vector2 anchorNew = isHead ? nPart.BottomCenter : nPart.TopCenter;
                    rootWorld += anchorNew - anchorOld;
                    scale = Mathf.Clamp(nPart.Height / tPart.Height, 0.6f, 1.6f);
                    sb.AppendLine($"  {bones[c].name}: {tPart} -> {nPart} scale={scale:0.00}");
                }
                else
                {
                    unmatchedChains.Add(bones[c].name);
                    sb.AppendLine($"  {bones[c].name}: no matching part on this sheet (kept template placement)");
                }
                // Root of chain: convert new world position into BodyCenter-local space.
                Vector2 local = rootRotInv * (rootWorld - rootWorldNew);
                var cb = bones[c]; cb.position = new Vector3(local.x, local.y, cb.position.z); cb.length *= scale; bones[c] = cb;
                for (int k = 1; k < chain.Count; k++)
                {
                    var child = bones[chain[k]];
                    child.position = new Vector3(child.position.x * scale, child.position.y * scale, child.position.z);
                    child.length *= scale;
                    bones[chain[k]] = child;
                }
            }
            log = sb.ToString();
            return bones;
        }

        // ------------------------------------------------------------------ geometry (package code via reflection)
        static Assembly AnimationEditorAssembly =>
            AppDomain.CurrentDomain.GetAssemblies().First(a => a.GetName().Name == "Unity.2D.Animation.Editor");

        static void GenerateGeometry(ITextureDataProvider tex, Rect frame, out float2[] vertices, out int2[] edges, out int[] indices)
        {
            tex.GetTextureActualWidthAndHeight(out int aw, out int ah);
            var scale = new Vector2(tex.texture.width / (float)aw, tex.texture.height / (float)ah);
            var scaledRect = new Rect(Vector2.Scale(frame.min, scale), Vector2.Scale(frame.size, scale));
            Vector2 rectOffset = frame.size * 0.5f;

            var asm = AnimationEditorAssembly;
            var genType = asm.GetType("UnityEditor.U2D.Animation.OutlineGenerator");
            var gen = Activator.CreateInstance(genType);
            var m = genType.GetMethod("GenerateOutline", BindingFlags.Public | BindingFlags.Instance);
            object[] args = { tex, scaledRect, OutlineDetail, (byte)AlphaTolerance, false, null };
            m.Invoke(gen, args);
            var paths = (Vector2[][])args[5] ?? Array.Empty<Vector2[]>();

            var verts = new List<float2>();
            var edgeList = new List<int2>();
            int baseIndex = 0;
            foreach (var path in paths)
            {
                int n = path.Length;
                if (n < 3) continue;
                for (int j = 0; j <= n; j++)
                {
                    if (j < n) { var p = new Vector2(path[j].x / scale.x, path[j].y / scale.y) + rectOffset; verts.Add(new float2(p.x, p.y)); }
                    if (j > 0) edgeList.Add(new int2(baseIndex + j - 1, baseIndex + j % n));
                }
                baseIndex += n;
            }
            vertices = verts.ToArray();
            edges = edgeList.ToArray();

            var triType = asm.GetType("UnityEditor.U2D.Animation.Triangulator");
            var tri = Activator.CreateInstance(triType);
            var tm = triType.GetMethod("Triangulate", BindingFlags.Public | BindingFlags.Instance);
            object[] targs = { edges, vertices, null };
            tm.Invoke(tri, targs);
            edges = (int2[])targs[0];
            vertices = (float2[])targs[1];
            indices = (int[])targs[2] ?? Array.Empty<int>();
            if (vertices.Length == 0 || indices.Length == 0) throw new InvalidOperationException("Outline/triangulation produced no geometry.");
        }

        static void GetControlPoints(IList<SpriteBone> bones, out float2[] points, out int2[] boneEdges, out int[] pins)
        {
            var world = ComputeWorld(bones);
            var pointList = new List<Vector2>();
            var edgeList = new List<int2>();
            var pinList = new List<int>();
            for (int i = 0; i < bones.Count; i++)
            {
                float length = (world[i].end - world[i].pos).magnitude;
                if (length > 0f)
                {
                    int i1 = Find(pointList, world[i].pos);
                    int i2 = Find(pointList, world[i].end);
                    if (i1 < 0) { pointList.Add(world[i].pos); i1 = pointList.Count - 1; }
                    if (i2 < 0) { pointList.Add(world[i].end); i2 = pointList.Count - 1; }
                    edgeList.Add(new int2(i1, i2));
                }
                else if (bones[i].length == 0f)
                {
                    pointList.Add(world[i].pos);
                    pinList.Add(pointList.Count - 1);
                }
            }
            points = pointList.Select(p => new float2(p.x, p.y)).ToArray();
            boneEdges = edgeList.ToArray();
            pins = pinList.ToArray();

            static int Find(List<Vector2> list, Vector2 p)
            {
                for (int i = 0; i < list.Count; i++) if ((list[i] - p).sqrMagnitude < 0.0001f) return i;
                return -1;
            }
        }

        static BoneWeight[] GenerateWeights(string name, IList<SpriteBone> bones, float2[] vertices, int[] indices, int2[] edges)
        {
            GetControlPoints(bones, out var controlPoints, out var boneEdges, out var pins);
            BoneWeight[] weights = null;
            try
            {
                var type = AnimationEditorAssembly.GetType("UnityEditor.U2D.Animation.BoundedBiharmonicWeightsGenerator");
                var gen = Activator.CreateInstance(type);
                var m = type.GetMethod("Calculate", BindingFlags.Public | BindingFlags.Instance);
                object[] args = { name, vertices, indices, edges, controlPoints, boneEdges, pins };
                weights = (BoneWeight[])m.Invoke(gen, args);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[TG] Biharmonic weights failed ({e.GetBaseException().Message}); using nearest-bone fallback.");
            }
            if (weights == null || weights.Length != vertices.Length || !AnyWeight(weights))
                weights = NearestBoneWeights(bones, vertices);
            for (int i = 0; i < weights.Length; i++) weights[i] = FilterAndNormalize(weights[i], WeightTolerance);
            // Vertices the solver left empty (e.g. islands without bones inside) fall back to the nearest bone.
            var fallback = NearestBoneWeights(bones, vertices);
            for (int i = 0; i < weights.Length; i++)
                if (Sum(weights[i]) <= 0f) weights[i] = fallback[i];
            return weights;
        }

        static bool AnyWeight(BoneWeight[] w)
        {
            for (int i = 0; i < w.Length; i++) if (Sum(w[i]) > 0f && !float.IsNaN(Sum(w[i]))) return true;
            return false;
        }

        static float Sum(BoneWeight w) => w.weight0 + w.weight1 + w.weight2 + w.weight3;

        static BoneWeight FilterAndNormalize(BoneWeight w, float tolerance)
        {
            var idx = new[] { w.boneIndex0, w.boneIndex1, w.boneIndex2, w.boneIndex3 };
            var wt = new[] { w.weight0, w.weight1, w.weight2, w.weight3 };
            float sum = 0f;
            for (int i = 0; i < 4; i++)
            {
                if (float.IsNaN(wt[i]) || wt[i] <= tolerance) { wt[i] = 0f; idx[i] = 0; }
                sum += wt[i];
            }
            if (sum > 0f) for (int i = 0; i < 4; i++) wt[i] /= sum;
            Array.Sort(wt, idx, Comparer<float>.Create((a, b) => b.CompareTo(a)));
            return new BoneWeight { boneIndex0 = idx[0], weight0 = wt[0], boneIndex1 = idx[1], weight1 = wt[1], boneIndex2 = idx[2], weight2 = wt[2], boneIndex3 = idx[3], weight3 = wt[3] };
        }

        /// Fallback: nearest bone segment gets the vertex, blended with the neighbour bone near shared joints.
        static BoneWeight[] NearestBoneWeights(IList<SpriteBone> bones, float2[] vertices)
        {
            var world = ComputeWorld(bones);
            var result = new BoneWeight[vertices.Length];
            for (int v = 0; v < vertices.Length; v++)
            {
                var p = new Vector2(vertices[v].x, vertices[v].y);
                int best = -1, second = -1;
                float bd = float.MaxValue, sd = float.MaxValue;
                for (int i = 0; i < bones.Count; i++)
                {
                    float d = DistanceToSegment(p, world[i].pos, world[i].end);
                    if (d < bd) { second = best; sd = bd; best = i; bd = d; }
                    else if (d < sd) { second = i; sd = d; }
                }
                if (best < 0) { result[v] = new BoneWeight { weight0 = 1f }; continue; }
                float blend = 0f;
                if (second >= 0 && (bones[second].parentId == best || bones[best].parentId == second))
                    blend = Mathf.Clamp01(1f - (sd - bd) / 60f) * 0.5f;
                result[v] = new BoneWeight { boneIndex0 = best, weight0 = 1f - blend, boneIndex1 = blend > 0f ? second : 0, weight1 = blend };
            }
            return result;
        }

        static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float len2 = ab.sqrMagnitude;
            float t = len2 > 0f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2) : 0f;
            return Vector2.Distance(p, a + ab * t);
        }

        // ------------------------------------------------------------------ single arm -> back arm
        /// When the sheet has only one arm, copies that arm's mesh island and binds the copy to the other arm chain.
        static string DuplicateMissingLimb(List<SpriteBone> bones, List<string> unmatched, string sourceRoot, string targetRoot,
            ref float2[] vertices, ref int[] indices, ref int2[] edges, ref BoneWeight[] weights)
        {
            if (!unmatched.Contains(targetRoot) || unmatched.Contains(sourceRoot)) return "";
            int src = IndexOf(bones, sourceRoot), dst = IndexOf(bones, targetRoot);
            if (src < 0 || dst < 0) return "";
            var srcChain = Chain(bones, src);
            var dstChain = Chain(bones, dst);
            if (srcChain.Count != dstChain.Count) return "";
            var remap = new Dictionary<int, int>();
            for (int i = 0; i < srcChain.Count; i++) remap[srcChain[i]] = dstChain[i];

            // Bind pose of the target chain = source chain (same pixels), keeping the target chain's depth.
            for (int i = 0; i < srcChain.Count; i++)
            {
                var s = bones[srcChain[i]];
                var d = bones[dstChain[i]];
                d.position = new Vector3(s.position.x, s.position.y, d.position.z);
                d.rotation = s.rotation;
                d.length = s.length;
                bones[dstChain[i]] = d;
            }

            // Connected components of the triangle mesh; pick those dominated by the source chain.
            int vc = vertices.Length;
            var parent = new int[vc];
            for (int i = 0; i < vc; i++) parent[i] = i;
            int FindRoot(int x) { while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; } return x; }
            for (int t = 0; t < indices.Length; t += 3)
            {
                int a = FindRoot(indices[t]), b = FindRoot(indices[t + 1]), c = FindRoot(indices[t + 2]);
                parent[b] = a; parent[FindRoot(c)] = FindRoot(a);
            }
            var srcSet = new HashSet<int>(srcChain);
            var islandVotes = new Dictionary<int, (int src, int total)>();
            for (int v = 0; v < vc; v++)
            {
                int r = FindRoot(v);
                islandVotes.TryGetValue(r, out var votes);
                votes.total++;
                if (srcSet.Contains(weights[v].boneIndex0) && weights[v].weight0 > 0f) votes.src++;
                islandVotes[r] = votes;
            }
            var islands = new HashSet<int>(islandVotes.Where(kv => kv.Value.total > 0 && kv.Value.src * 2 > kv.Value.total).Select(kv => kv.Key));
            if (islands.Count == 0) return "  arm mirror: no mesh island bound to " + sourceRoot + "\n";

            var newIndexOf = new int[vc];
            for (int i = 0; i < vc; i++) newIndexOf[i] = -1;
            var vList = new List<float2>(vertices);
            var wList = new List<BoneWeight>(weights);
            for (int v = 0; v < vc; v++)
            {
                if (!islands.Contains(FindRoot(v))) continue;
                newIndexOf[v] = vList.Count;
                vList.Add(vertices[v]);
                var w = weights[v];
                w.boneIndex0 = Remap(w.boneIndex0, w.weight0); w.boneIndex1 = Remap(w.boneIndex1, w.weight1);
                w.boneIndex2 = Remap(w.boneIndex2, w.weight2); w.boneIndex3 = Remap(w.boneIndex3, w.weight3);
                wList.Add(w);
            }
            var iList = new List<int>(indices);
            for (int t = 0; t < indices.Length; t += 3)
            {
                if (newIndexOf[indices[t]] < 0) continue;
                iList.Add(newIndexOf[indices[t]]); iList.Add(newIndexOf[indices[t + 1]]); iList.Add(newIndexOf[indices[t + 2]]);
            }
            var eList = new List<int2>(edges);
            foreach (var e in edges)
                if (newIndexOf[e.x] >= 0 && newIndexOf[e.y] >= 0) eList.Add(new int2(newIndexOf[e.x], newIndexOf[e.y]));

            int added = vList.Count - vc;
            vertices = vList.ToArray(); weights = wList.ToArray(); indices = iList.ToArray(); edges = eList.ToArray();
            return $"  arm mirror: sheet has a single arm; copied its mesh ({added} vertices) onto the '{targetRoot}' chain (drawn behind the body by bone depth).\n";

            int Remap(int boneIndex, float weight) => weight > 0f && remap.TryGetValue(boneIndex, out var to) ? to : boneIndex;
        }

        // ------------------------------------------------------------------ depth sort (mirrors SpriteMeshDataController.SortTrianglesByDepth)
        static void SortTrianglesByDepth(IList<SpriteBone> bones, float2[] vertices, BoneWeight[] weights, ref int[] indices)
        {
            var order = new float[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                var w = weights[i];
                order[i] = Depth(w.boneIndex0) * w.weight0 + Depth(w.boneIndex1) * w.weight1 + Depth(w.boneIndex2) * w.weight2 + Depth(w.boneIndex3) * w.weight3;
            }
            int triCount = indices.Length / 3;
            var tris = new (float weight, int seq, int a, int b, int c)[triCount];
            for (int t = 0; t < triCount; t++)
            {
                int a = indices[t * 3], b = indices[t * 3 + 1], c = indices[t * 3 + 2];
                tris[t] = ((order[a] + order[b] + order[c]) / 3f, t, a, b, c);
            }
            Array.Sort(tris, (x, y) => x.weight != y.weight ? x.weight.CompareTo(y.weight) : x.seq.CompareTo(y.seq));
            var sorted = new int[indices.Length];
            for (int t = 0; t < triCount; t++) { sorted[t * 3] = tris[t].a; sorted[t * 3 + 1] = tris[t].b; sorted[t * 3 + 2] = tris[t].c; }
            indices = sorted;

            float Depth(int bone) => bone >= 0 && bone < bones.Count ? bones[bone].position.z : 0f;
        }

        // ------------------------------------------------------------------ write
        static void WriteSkinning(ISpriteEditorDataProvider provider, GUID spriteId, List<SpriteBone> bones, float2[] vertices, BoneWeight[] weights, int[] indices, int2[] edges)
        {
            provider.GetDataProvider<ISpriteBoneDataProvider>().SetBones(spriteId, bones);
            var mesh = provider.GetDataProvider<ISpriteMeshDataProvider>();
            var verts = new Vertex2DMetaData[vertices.Length];
            for (int i = 0; i < verts.Length; i++) verts[i] = new Vertex2DMetaData { position = new Vector2(vertices[i].x, vertices[i].y), boneWeight = weights[i] };
            mesh.SetVertices(spriteId, verts);
            mesh.SetIndices(spriteId, indices);
            mesh.SetEdges(spriteId, edges.Select(e => new Vector2Int(e.x, e.y)).ToArray());
            provider.Apply();
            EditorUtility.SetDirty(provider.targetObject);
        }

        // ------------------------------------------------------------------ prefab
        static void BuildPrefab(string name, List<SpriteBone> bones, float ppu)
        {
            string texPath = TexturePath(name);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(texPath);
            var templatePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(TemplateName));
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(LitSpriteMaterialGuid));
            if (material == null && templatePrefab != null) material = templatePrefab.GetComponent<SpriteRenderer>()?.sharedMaterial;

            var root = new GameObject(name);
            try
            {
                var sr = root.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                if (material != null) sr.sharedMaterial = material;
                var skin = root.AddComponent<SpriteSkin>();

                var transforms = new Transform[bones.Count];
                Vector2 pivotPx = sprite != null ? sprite.pivot : new Vector2(512f, 512f);
                for (int i = 0; i < bones.Count; i++)
                {
                    var b = bones[i];
                    var go = new GameObject(b.name);
                    go.transform.SetParent(b.parentId < 0 ? root.transform : transforms[b.parentId], false);
                    go.transform.localPosition = b.parentId < 0
                        ? new Vector3((b.position.x - pivotPx.x) / ppu, (b.position.y - pivotPx.y) / ppu, 0f)
                        : new Vector3(b.position.x / ppu, b.position.y / ppu, 0f);
                    go.transform.localRotation = b.rotation;
                    go.transform.localScale = Vector3.one;
                    transforms[i] = go.transform;
                }

                // Assembled pose: same layout as the template prefab (limb roots pulled onto the body).
                if (templatePrefab != null)
                {
                    var templateRoot = templatePrefab.GetComponent<SpriteSkin>()?.rootBone;
                    for (int i = 0; i < bones.Count; i++)
                    {
                        var t = FindDeep(templatePrefab.transform, bones[i].name);
                        if (t == null) continue;
                        bool isRoot = bones[i].parentId < 0;
                        bool isChainRoot = !isRoot && bones[bones[i].parentId].parentId < 0;
                        if (isRoot) transforms[i].localPosition = Vector3.zero;
                        else if (isChainRoot) transforms[i].localPosition = t.localPosition;
                        transforms[i].localRotation = t.localRotation;
                    }
                    if (templateRoot != null) root.transform.localScale = templateRoot.parent != null ? templateRoot.parent.localScale : Vector3.one;
                }
                root.transform.localPosition = Vector3.zero;
                if (root.transform.localScale == Vector3.one) root.transform.localScale = Vector3.one * 0.3f;

                int rootIndex = bones.FindIndex(b => b.parentId < 0);
                skin.SetRootBone(transforms[rootIndex]);
                skin.SetBoneTransforms(transforms);
                skin.alwaysUpdate = true;
                skin.autoRebind = false;

                // Generous local bounds around every bone (deformed mesh never leaves this box).
                var min = new Vector3(float.MaxValue, float.MaxValue, 0f);
                var max = new Vector3(float.MinValue, float.MinValue, 0f);
                for (int i = 0; i < bones.Count; i++)
                {
                    var p = root.transform.InverseTransformPoint(transforms[i].position);
                    var e = root.transform.InverseTransformPoint(transforms[i].TransformPoint(new Vector3(bones[i].length / ppu, 0f, 0f)));
                    min = Vector3.Min(min, Vector3.Min(p, e));
                    max = Vector3.Max(max, Vector3.Max(p, e));
                }
                var pad = new Vector3(2.5f, 2.5f, 0.1f);
                var so = new SerializedObject(skin);
                var bp = so.FindProperty("m_Bounds");
                if (bp != null)
                {
                    bp.FindPropertyRelative("m_Center").vector3Value = (min + max) * 0.5f;
                    bp.FindPropertyRelative("m_Extent").vector3Value = (max - min) * 0.5f + pad;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                var animator = root.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath(name));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        static Transform FindDeep(Transform t, string childName)
        {
            if (t.name == childName) return t;
            for (int i = 0; i < t.childCount; i++)
            {
                var r = FindDeep(t.GetChild(i), childName);
                if (r != null) return r;
            }
            return null;
        }
    }
}
