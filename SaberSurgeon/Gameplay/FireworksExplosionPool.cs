using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SaberSurgeon.Gameplay
{
    public class FireworksExplosionPool : MonoBehaviour
    {
        private static FireworksExplosionPool _instance;
        private static GameObject _go;

        private Material _particleMaterial;
        private readonly Queue<GameObject> _pool = new Queue<GameObject>();

        public static FireworksExplosionPool Instance
        {
            get
            {
                if (_instance != null) return _instance;
                _go = new GameObject("SaberSurgeonFireworksExplosionPool");
                DontDestroyOnLoad(_go);
                _instance = _go.AddComponent<FireworksExplosionPool>();
                return _instance;
            }
        }

        public void SetParticleMaterial(Material mat)
        {
            if (_particleMaterial == null && mat != null)
                _particleMaterial = mat;
        }

        public void Spawn(Vector3 position, Color baseColor, int burstCount = 220, float life = 1.6f)
        {
            // ← CHANGE: Call this instead of checking if null
            var mat = GetOrInitializeParticleMaterial();
            if (mat == null)
            {
                Plugin.Log.Warn("FireworksExplosionPool: Cannot spawn – material is null");
                return;
            }

            var root = GetOrCreateInstance();
            root.transform.position = position;
            root.SetActive(true);

            // Randomize a little so it feels like fireworks (optional).
            var color = Color.HSVToRGB(Random.value, 0.85f, 1.0f);
            color.a = 1f;
            color = Color.Lerp(color, baseColor, 0.35f);

            var burst = root.transform.Find("Burst").GetComponent<ParticleSystem>();
            var sparks = root.transform.Find("Sparks").GetComponent<ParticleSystem>();

            ConfigureBurst(burst, color, burstCount, life);
            ConfigureSparks(sparks, color, Mathf.RoundToInt(burstCount * 0.9f), life * 0.9f);

            burst.Play(true);
            sparks.Play(true);

            // Return to pool after it's done.
            StartCoroutine(DespawnAfter(root, Mathf.Max(life, 0.1f) + 0.25f));
        }



        private GameObject GetOrCreateInstance()
        {
            if (_pool.Count > 0)
                return _pool.Dequeue();

            var mat = GetOrInitializeParticleMaterial();

            var root = new GameObject("SaberSurgeonFireworksExplosion");
            root.SetActive(false);

            CreateParticleChild(root.transform, "Burst", mat);
            CreateParticleChild(root.transform, "Sparks", mat);

            return root;
        }

        private static void CreateParticleChild(Transform parent, string name, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var ps = go.AddComponent<ParticleSystem>();
            var psr = go.GetComponent<ParticleSystemRenderer>();
            psr.sharedMaterial = mat;

            // Basic defaults; real shaping is done per-spawn in Configure*.
            var main = ps.main;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 0f;

            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private static void ConfigureBurst(ParticleSystem ps, Color color, int count, float life)
        {
            var main = ps.main;
            main.startColor = color;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.55f, life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(6f, 14f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
            main.gravityModifier = 0.35f;
            main.maxParticles = 4000;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.03f;

            var emission = ps.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Clamp(count, 1, 2000)) });

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
            );
            col.color = new ParticleSystem.MinMaxGradient(grad);

            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private static void ConfigureSparks(ParticleSystem ps, Color color, int count, float life)
        {
            var main = ps.main;
            main.startColor = Color.Lerp(color, Color.white, 0.35f);
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.35f, life * 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 7f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.06f);
            main.gravityModifier = 0.9f;
            main.maxParticles = 6000;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.08f;

            var emission = ps.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0.02f, (short)Mathf.Clamp(count, 1, 4000)) });

            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private System.Collections.IEnumerator DespawnAfter(GameObject go, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            go.SetActive(false);
            _pool.Enqueue(go);
        }
        private void LogAllParticleTextures()
        {
            var textures = Resources.FindObjectsOfTypeAll<Texture2D>();
            var particleTextures = textures.Where(t =>
                t.name.Contains("Particle") ||
                t.name.Contains("particle") ||
                t.name.Contains("Star") ||
                t.name.Contains("Heart") ||
                t.name.Contains("Spark")
            ).ToList();

            Plugin.Log.Info($"Found {particleTextures.Count} particle-related textures:");
            foreach (var tex in particleTextures)
            {
                Plugin.Log.Info($"  - {tex.name} ({tex.width}x{tex.height})");
            }
        }

        private Material GetOrInitializeParticleMaterial()
        {
            if (_particleMaterial != null) return _particleMaterial;

            // Find the default particle material from Beat Saber
            var particle = Resources.FindObjectsOfTypeAll<ParticleSystemRenderer>()
                .FirstOrDefault(ps => ps.sharedMaterial != null && ps.sharedMaterial.name.Contains("Particle"));

            if (particle != null)
            {
                _particleMaterial = particle.sharedMaterial;

                LogAllParticleTextures();

                // DEBUG: Log what texture is being used
                if (_particleMaterial.mainTexture != null)
                {
                    Plugin.Log.Info($"FireworksExplosionPool: Particle material texture = '{_particleMaterial.mainTexture.name}'");
                }
                else
                {
                    Plugin.Log.Warn("FireworksExplosionPool: Particle material has NO texture!");
                }

                Plugin.Log.Info("FireworksExplosionPool: Initialized material from game ParticleSystemRenderer");
            }
            else
            {
                // Fallback: create a simple material from standard shader
                var shader = Shader.Find("Standard");
                if (shader != null)
                {
                    _particleMaterial = new Material(shader);
                    _particleMaterial.name = "FireworksParticleMaterial";
                    Plugin.Log.Info("FireworksExplosionPool: Created fallback material from Standard shader");
                }
                else
                {
                    Plugin.Log.Warn("FireworksExplosionPool: Failed to create particle material (no shader found)");
                }
            }

            return _particleMaterial;
        }

    }
}
