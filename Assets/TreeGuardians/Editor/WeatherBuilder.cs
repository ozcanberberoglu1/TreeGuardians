using System.Linq;
using TreeGuardians.FX;
using UnityEditor;
using UnityEngine;
using F = TreeGuardians.Editor.UIFactory;

namespace TreeGuardians.Editor
{
    /// Builds the pre-authored weather rig (rain streaks, ground splashes, wind-blown leaves, darkening overlay + WeatherController)
    /// and the shared particle materials. Used by the Battle scene builder and by the live Main Menu setup.
    public static class WeatherBuilder
    {
        public const string MaterialFolder = "Assets/TreeGuardians/Art/Materials";

        public static Material ParticleMaterial(string name, Texture texture)
        {
            string path = $"{MaterialFolder}/TG_FX_{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("Sprites/Default");
            if (mat == null)
            {
                ContentBuilder.EnsureFolder(MaterialFolder);
                mat = new Material(shader) { name = "TG_FX_" + name };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            mat.mainTexture = texture;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        public static Sprite[] LeafSprites() => AssetDatabase.LoadAllAssetsAtPath(UserArtSlicer.PlatformsPath).OfType<Sprite>().Where(s => s.name.StartsWith("leaf_")).OrderBy(s => s.name).ToArray();

        static Gradient Fade(float inT, float outT, float peak = 1f)
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(peak, inT), new GradientAlphaKey(peak, outT), new GradientAlphaKey(0f, 1f) });
            return g;
        }

        /// groundY: world Y where rain splashes; top: world Y the rain starts from; width: horizontal coverage.
        /// splashSurfaces: optional (centerX, width) pairs; when given, splashes only land on those surfaces (e.g. island tops).
        public static WeatherController Build(Transform parent, Camera cam, float groundY, float top, float width, int overlayOrder, bool gustSound, Vector2[] splashSurfaces = null)
        {
            var root = F.Child("Weather", parent);
            var controller = root.AddComponent<WeatherController>();

            // rain streaks (stretched billboards following velocity)
            var rainGo = F.Child("Rain", root.transform, new Vector3(0f, top, 0f));
            var rain = rainGo.AddComponent<ParticleSystem>();
            var m = rain.main;
            m.playOnAwake = false; m.loop = true; m.simulationSpace = ParticleSystemSimulationSpace.World;
            float fall = Mathf.Max(4f, top - groundY);
            m.startLifetime = new ParticleSystem.MinMaxCurve(fall / 19f, fall / 16f);
            m.startSpeed = 0f;
            m.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.08f);
            m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.78f, 0.86f, 1f, 0.6f), new Color(0.92f, 0.96f, 1f, 0.9f));
            m.maxParticles = 900;
            var em = rain.emission; em.rateOverTime = 0f;
            var sh = rain.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(width + 6f, 0.2f, 1f); sh.position = new Vector3(2f, 0f, 0f);
            var vel = rain.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-1.2f, -0.6f); vel.y = new ParticleSystem.MinMaxCurve(-19f, -16f); vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            var rr = rainGo.GetComponent<ParticleSystemRenderer>();
            rr.renderMode = ParticleSystemRenderMode.Stretch; rr.velocityScale = 0.06f; rr.lengthScale = 2f;
            rr.sharedMaterial = ParticleMaterial("Rain", AssetDatabase.LoadAssetAtPath<Texture2D>(VfxArtGenerator.PathOf("rain_streak")));
            rr.sortingOrder = overlayOrder + 1;

            // ground splashes
            bool surfaces = splashSurfaces != null && splashSurfaces.Length > 0;
            var splashGo = F.Child("RainSplashes", root.transform, new Vector3(surfaces ? splashSurfaces[0].x : 0f, groundY + 0.02f, 0f));
            var splash = splashGo.AddComponent<ParticleSystem>();
            var sm = splash.main;
            sm.playOnAwake = false; sm.loop = true; sm.simulationSpace = ParticleSystemSimulationSpace.World;
            sm.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.3f); sm.startSpeed = 0f;
            sm.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.55f);
            sm.startColor = new Color(0.85f, 0.92f, 1f, 0.75f);
            sm.maxParticles = 120;
            var sem = splash.emission; sem.rateOverTime = 0f;
            var ssh = splash.shape; ssh.shapeType = ParticleSystemShapeType.Box; ssh.scale = new Vector3(surfaces ? splashSurfaces[0].y : width, 0.05f, 1f);
            var ssz = splash.sizeOverLifetime; ssz.enabled = true; ssz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.25f));
            var scol = splash.colorOverLifetime; scol.enabled = true; scol.color = Fade(0.1f, 0.4f);
            var stsa = splash.textureSheetAnimation; stsa.enabled = true; stsa.mode = ParticleSystemAnimationMode.Sprites;
            var splashSprite = AssetDatabase.LoadAssetAtPath<Sprite>(VfxArtGenerator.PathOf("rain_splash"));
            if (splashSprite != null) stsa.AddSprite(splashSprite);
            var sr = splashGo.GetComponent<ParticleSystemRenderer>();
            sr.sharedMaterial = ParticleMaterial("RainSplash", splashSprite != null ? splashSprite.texture : null);
            sr.sortingOrder = overlayOrder + 1;

            // wind-blown leaves (user's leaf sprites)
            var leavesGo = F.Child("WindLeaves", root.transform, new Vector3(width * 0.2f, top - 0.6f, 0f));
            var leaves = leavesGo.AddComponent<ParticleSystem>();
            var lm = leaves.main;
            lm.playOnAwake = false; lm.loop = true; lm.simulationSpace = ParticleSystemSimulationSpace.World;
            lm.startLifetime = new ParticleSystem.MinMaxCurve(7f, 10f); lm.startSpeed = 0f;
            lm.startSize = new ParticleSystem.MinMaxCurve(0.26f, 0.42f);
            lm.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            lm.maxParticles = 70;
            var lem = leaves.emission; lem.rateOverTime = 1.2f;
            var lsh = leaves.shape; lsh.shapeType = ParticleSystemShapeType.Box; lsh.scale = new Vector3(width + 4f, 1f, 1f);
            var lvel = leaves.velocityOverLifetime; lvel.enabled = true; lvel.space = ParticleSystemSimulationSpace.World;
            lvel.x = new ParticleSystem.MinMaxCurve(-1.0f, -0.3f); lvel.y = new ParticleSystem.MinMaxCurve(-1.1f, -0.55f); lvel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            var lrot = leaves.rotationOverLifetime; lrot.enabled = true; lrot.z = new ParticleSystem.MinMaxCurve(-2.2f, 2.2f);
            var noise = leaves.noise; noise.enabled = true; noise.strength = 0.55f; noise.frequency = 0.35f; noise.scrollSpeed = 0.25f; noise.quality = ParticleSystemNoiseQuality.Low;
            var lcol = leaves.colorOverLifetime; lcol.enabled = true; lcol.color = Fade(0.06f, 0.85f);
            var ltsa = leaves.textureSheetAnimation; ltsa.enabled = true; ltsa.mode = ParticleSystemAnimationMode.Sprites;
            var leafSprites = LeafSprites();
            foreach (var s in leafSprites) ltsa.AddSprite(s);
            ltsa.frameOverTime = new ParticleSystem.MinMaxCurve(0f);
            ltsa.startFrame = new ParticleSystem.MinMaxCurve(0f, Mathf.Max(1, leafSprites.Length) - 0.01f);
            var lr = leavesGo.GetComponent<ParticleSystemRenderer>();
            lr.sharedMaterial = ParticleMaterial("Leaves", leafSprites.Length > 0 ? leafSprites[0].texture : null);
            lr.sortingOrder = overlayOrder - 2;

            // darkening + lightning overlay
            var white = F.Ui("white");
            var overlay = F.WorldSprite("Overlay", root.transform, white, new Color(0.1f, 0.14f, 0.22f, 0f), overlayOrder, new Vector3(0f, 0f, -0.5f));
            if (white != null)
            {
                var b = white.bounds.size;
                overlay.transform.localScale = new Vector3(60f / Mathf.Max(0.001f, b.x), 40f / Mathf.Max(0.001f, b.y), 1f);
            }

            F.Set(controller, "rain", rain);
            F.Set(controller, "splashes", splash);
            if (surfaces && splashSurfaces.Length > 1)
            {
                var extra = new Object[splashSurfaces.Length - 1];
                for (int i = 1; i < splashSurfaces.Length; i++)
                {
                    var copy = Object.Instantiate(splashGo, root.transform);
                    copy.name = "RainSplashes_" + i;
                    copy.transform.localPosition = new Vector3(splashSurfaces[i].x, groundY + 0.02f, 0f);
                    var cps = copy.GetComponent<ParticleSystem>();
                    var csh = cps.shape; csh.scale = new Vector3(splashSurfaces[i].y, 0.05f, 1f);
                    extra[i - 1] = cps;
                }
                F.SetArray(controller, "extraSplashes", extra);
            }
            F.Set(controller, "leaves", leaves);
            F.Set(controller, "overlay", overlay);
            F.Set(controller, "followCamera", cam);
            F.Set(controller, "gustSound", gustSound);
            return controller;
        }
    }
}
