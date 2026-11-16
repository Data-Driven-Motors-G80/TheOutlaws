using System.Collections.Generic;
using UnityEngine;

namespace Outlaws.WorldShift
{
    /// <summary>Presentation-only crash and landing feedback: debris bursts, landing puffs and a short
    /// camera kick. Never changes simulation, collision or retry timing.</summary>
    internal sealed class WorldShiftImpactMoments : MonoBehaviour
    {
        private sealed class Piece { public Transform Body; public Vector3 Velocity, Spin; public float Age, Life, Scale; public bool Puff; }
        private readonly List<Piece> pieces = new List<Piece>();
        private WorldShiftCity game;
        private Mesh cube;
        private float shake, shakeDuration, shakeAge;
        private System.Random random = new System.Random(4127);

        public void Bind(WorldShiftCity city)
        {
            game = city;
            var primitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube = primitive.GetComponent<MeshFilter>().sharedMesh;
            if (Application.isPlaying) Destroy(primitive); else DestroyImmediate(primitive);
        }

        private float R(float a, float b) => a + (float)random.NextDouble() * (b - a);

        // Shared cached materials: never destroyed here.
        private static Material Mat(Color c) => RuntimeMaterials.Coloured(c);

        private void Spawn(Vector3 at, Vector3 velocity, float size, float life, Material material, bool puff)
        {
            var obj = new GameObject(puff ? "Landing puff" : "Crash debris", typeof(MeshFilter), typeof(MeshRenderer));
            obj.transform.SetParent(transform, true);
            obj.transform.position = at;
            obj.transform.rotation = Random.rotationUniform;
            obj.transform.localScale = Vector3.one * size;
            obj.GetComponent<MeshFilter>().sharedMesh = cube;
            var renderer = obj.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            pieces.Add(new Piece { Body = obj.transform, Velocity = velocity, Spin = new Vector3(R(-400, 400), R(-400, 400), R(-400, 400)),
                Life = life, Scale = size, Puff = puff });
        }

        public void Kick(float strength, float duration) { shake = Mathf.Max(shake, strength); shakeDuration = duration; shakeAge = 0f; }

        /// <summary>A collision crash: sparks and body debris thrown forward, plus a firm camera kick.</summary>
        public void Crash(Vector3 at, Vector3 forward)
        {
            Kick(.42f, .5f);
            var spark = Mat(new Color(1f, .78f, .25f));
            var hot = Mat(new Color(1f, .45f, .12f));
            var body = Mat(new Color(.42f, .1f, .14f));
            var dark = Mat(new Color(.08f, .09f, .1f));
            for (int i = 0; i < 26; i++)
            {
                Vector3 v = forward * R(3f, 9f) + Vector3.up * R(2f, 7f) + Vector3.Cross(Vector3.up, forward) * R(-6f, 6f);
                var m = i < 12 ? spark : i < 17 ? hot : i < 22 ? body : dark;
                Spawn(at + Vector3.up * .7f + forward * 1.6f, v, i < 17 ? R(.06f, .12f) : R(.14f, .26f), R(.45f, .85f), m, false);
            }
        }

        /// <summary>A fall or missed landing: a lighter kick, so the drop reads as a stumble rather than an impact.</summary>
        public void Fall() => Kick(.18f, .35f);

        /// <summary>A successful landing: an environment-coloured puff at both rear wheels and a short dip.</summary>
        public void Land(Vector3 at, Vector3 forward, ShiftLevel environment, ShiftWorld world)
        {
            Kick(.16f, .28f);
            Color c = environment switch
            {
                ShiftLevel.Desert => new Color(.86f, .7f, .47f),
                ShiftLevel.Ocean => new Color(.82f, .92f, .96f),
                ShiftLevel.Snow => new Color(.95f, .97f, 1f),
                ShiftLevel.Mountain => new Color(.55f, .5f, .44f),
                _ => new Color(.62f, .62f, .6f)
            };
            if (world == ShiftWorld.Echo) c *= .7f;
            c.a = 1f;
            var mat = Mat(c);
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            foreach (float side in new[] { -1f, 1f })
                for (int i = 0; i < 7; i++)
                {
                    bool splash = environment == ShiftLevel.Ocean; // sea spray rather than dust
                    Vector3 v = right * side * R(1.5f, 4f) - forward * R(1f, 3f) + Vector3.up * (splash ? R(2.5f, 4.5f) : R(.5f, 1.8f));
                    Spawn(at + right * side * 1.1f - forward * 1.2f + Vector3.up * .15f, v, R(.25f, .45f), R(.35f, .6f), mat, true);
                }
        }

        /// <summary>Camera offset for this frame; added after the normal follow camera.</summary>
        public Vector3 ShakeOffset
        {
            get
            {
                if (shake <= 0f || shakeAge >= shakeDuration) return Vector3.zero;
                float fade = 1f - shakeAge / shakeDuration;
                float t = shakeAge * 38f;
                return new Vector3(Mathf.PerlinNoise(t, .3f) - .5f, Mathf.PerlinNoise(.7f, t) - .5f, 0f) * 2f * shake * fade * fade;
            }
        }

        private void Update()
        {
            if (game != null && game.State == ShiftRunState.Paused) return;
            float dt = Time.deltaTime;
            shakeAge += dt;
            for (int i = pieces.Count - 1; i >= 0; i--)
            {
                var p = pieces[i];
                p.Age += dt;
                if (p.Age >= p.Life || p.Body == null)
                {
                    if (p.Body != null) Destroy(p.Body.gameObject);
                    pieces.RemoveAt(i);
                    continue;
                }
                float k = p.Age / p.Life;
                if (p.Puff)
                {
                    p.Velocity *= Mathf.Exp(-4f * dt);
                    p.Body.localScale = Vector3.one * p.Scale * (1f + 1.6f * k) * (1f - k * k);
                }
                else
                {
                    p.Velocity += Vector3.down * 18f * dt;
                    p.Body.localScale = Vector3.one * p.Scale * (1f - k * k);
                }
                p.Body.position += p.Velocity * dt;
                p.Body.rotation *= Quaternion.Euler(p.Spin * dt);
            }
        }

        public void Clear()
        {
            foreach (var p in pieces) if (p.Body != null) Destroy(p.Body.gameObject);
            pieces.Clear();
            shake = 0f;
        }

        private void OnDestroy()
        {
            Clear();
        }
    }
}
