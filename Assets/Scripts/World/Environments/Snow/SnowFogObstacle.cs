using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class SnowFogObstacle : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform car;

    [Header("Frequency")]
    [Tooltip("Metres driven in the real run before the first fog bank.")]
    [SerializeField, Min(0f)] private float firstFogDistance = 100f;
    [Tooltip("Metres driven between fog banks (random between min and max).")]
    [SerializeField, Min(1f)] private float minSpacing = 70f;
    [SerializeField, Min(1f)] private float maxSpacing = 120f;
    [Tooltip("Most fog banks alive at once.")]
    [SerializeField, Min(1)] private int maxActive = 1;

    [Header("Size")]
    [Tooltip("Length of the fog bank along the road, random each time. The car drives through it.")]
    [SerializeField, Min(1f)] private float minLength = 15f;
    [SerializeField, Min(1f)] private float maxLength = 45f;
    [Tooltip("How high the cloud puffs are placed above the road.")]
    [SerializeField, Min(0.5f)] private float fogHeight = 4f;
    [Tooltip("Size of each cloud puff. Bigger puffs = fewer of them = cheaper.")]
    [SerializeField, Min(1f)] private float puffSizeMin = 7f;
    [SerializeField, Min(1f)] private float puffSizeMax = 10f;
    [Tooltip("Distance between puffs. Smaller = denser cloud but more puffs.")]
    [SerializeField, Min(1f)] private float puffSpacing = 3.5f;
    [Tooltip("Stacked layers of puffs at different heights. More = denser, but heavier to draw.")]
    [SerializeField, Range(1, 3)] private int puffLayers = 1;

    [Header("Thickness (random per fog bank)")]
    [Tooltip("Opacity of the thickest fog. 1 = nothing behind the fog can be seen.")]
    [SerializeField, Range(0.1f, 1f)] private float denseOpacity = 1f;
    [Tooltip("Opacity of the thinnest fog. Each fog bank picks a random opacity between this and Dense Opacity.")]
    [SerializeField, Range(0.1f, 1f)] private float lightOpacity = 0.55f;

    [Header("Look")]
    [SerializeField] private Color fogColor = new Color(0.88f, 0.92f, 0.97f, 1f);
    [Tooltip("Despawn once the far end of the fog is this many metres behind the car.")]
    [SerializeField, Min(0f)] private float despawnDistance = 15f;

    [Header("Debug")]
    [SerializeField] private bool enableFog = true;

    private sealed class FogBlock
    {
        public GameObject Root;
        public Material[] Materials;
        public readonly List<Transform> Puffs = new List<Transform>();
        public readonly List<float> Rolls = new List<float>();
        public Vector3 End;
    }

    private readonly List<FogBlock> active = new List<FogBlock>();
    private readonly List<FogBlock> spare = new List<FogBlock>();
    private ObstacleSpawner spawner;
    private InfiniteRoad road;
    private Vector3 previousCarPosition;
    private float distanceUntilSpawn;
    private Texture2D[] cloudTextures;
    private CarPickupEffects pickupEffects;
    private bool blocksHidden;

    private void Awake()
    {
        spawner = GetComponent<ObstacleSpawner>();
        if (spawner == null) spawner = FindFirstObjectByType<ObstacleSpawner>();
        road = spawner != null ? spawner.RoadReference : FindFirstObjectByType<InfiniteRoad>();
        if (car == null)
        {
            AutoDriveCar driver = FindFirstObjectByType<AutoDriveCar>();
            if (driver != null) car = driver.transform;
        }
        distanceUntilSpawn = firstFogDistance;
        if (car != null)
        {
            previousCarPosition = car.position;
            pickupEffects = car.GetComponent<CarPickupEffects>();
        }
    }

    private void OnDisable()
    {
        blocksHidden = false;
        foreach (FogBlock block in active) block.Root.SetActive(false);
        spare.AddRange(active);
        active.Clear();
    }

    private void OnDestroy()
    {
        foreach (FogBlock block in active) DestroyBlock(block);
        foreach (FogBlock block in spare) DestroyBlock(block);
        if (cloudTextures == null) return;
        foreach (Texture2D texture in cloudTextures)
            if (texture != null) Destroy(texture);
    }

    private void DestroyBlock(FogBlock block)
    {
        if (block.Root != null) Destroy(block.Root);
        foreach (Material material in block.Materials)
            if (material != null) Destroy(material);
    }

    private void Update()
    {
        if (!enableFog || road == null || car == null) return;

        float travelled = Mathf.Min((car.position - previousCarPosition).magnitude, 10f);
        previousCarPosition = car.position;

        bool sunny = pickupEffects != null && pickupEffects.SunnyDayActive;
        if (sunny != blocksHidden)
        {
            blocksHidden = sunny;
            foreach (FogBlock block in active) block.Root.SetActive(!sunny);
        }

        bool suppressed = spawner != null && spawner.SuppressAutoSpawn;
        if (!suppressed && !sunny)
        {
            distanceUntilSpawn -= travelled;
            if (distanceUntilSpawn <= 0f && active.Count < maxActive && TrySpawn())
                distanceUntilSpawn = Random.Range(minSpacing, Mathf.Max(minSpacing, maxSpacing));
        }

        UpdateBlocks();
    }

    private bool TrySpawn()
    {
        if (!road.TryGetPointAhead(car.position, PickupLookAhead.Distance, out RoadPathPoint endPoint)) return false;
        if (!road.TryGetDistance(endPoint.Position, out float endDistance)) return false;

        float length = Random.Range(minLength, Mathf.Max(minLength, maxLength));
        float width = Mathf.Max(0.8f, endPoint.Width - 0.1f);
        int columns = Mathf.Max(2, Mathf.RoundToInt(width / puffSpacing));
        int rows = Mathf.Max(2, Mathf.RoundToInt(length / puffSpacing));

        FogBlock block = GetBlock();
        int perLayer = columns * rows;
        EnsurePuffs(block, perLayer * puffLayers);

        int index = 0;
        for (int row = 0; row < rows; row++)
        {
            float along = row / (rows - 1f) * length;
            bool onRoad = road.TrySample(endDistance - length + along, out RoadPathPoint point);
            for (int column = 0; column < columns; column++)
            {
                for (int layer = 0; layer < puffLayers; layer++, index++)
                {
                    Transform puff = block.Puffs[index];
                    if (!onRoad)
                    {
                        puff.gameObject.SetActive(false);
                        continue;
                    }

                    float lateral = (column / (columns - 1f) - 0.5f) * width
                        + Random.Range(-0.5f, 0.5f) * puffSpacing * 0.6f;
                    float lift = Mathf.Lerp(0.4f, fogHeight, (layer + Random.value) / puffLayers);
                    puff.position = point.Position + point.Right * lateral + point.Up * lift;
                    puff.localScale = Vector3.one * Random.Range(puffSizeMin, Mathf.Max(puffSizeMin, puffSizeMax));
                    block.Rolls[index] = Random.Range(0f, 360f);
                    puff.gameObject.SetActive(true);
                }
            }
        }

        Color colour = fogColor;
        colour.a = Random.Range(Mathf.Min(lightOpacity, denseOpacity), denseOpacity);
        foreach (Material material in block.Materials) material.color = colour;

        block.End = endPoint.Position;
        block.Root.SetActive(true);
        active.Add(block);
        return true;
    }

    private void UpdateBlocks()
    {
        Camera view = Camera.main;
        Quaternion face = view != null ? view.transform.rotation : Quaternion.identity;

        for (int i = active.Count - 1; i >= 0; i--)
        {
            FogBlock block = active[i];

            if (road.IsBehind(block.End, car.position, despawnDistance))
            {
                block.Root.SetActive(false);
                spare.Add(block);
                active.RemoveAt(i);
                continue;
            }

            for (int p = 0; p < block.Puffs.Count; p++)
            {
                Transform puff = block.Puffs[p];
                if (puff.gameObject.activeSelf)
                    puff.rotation = face * Quaternion.Euler(0f, 0f, block.Rolls[p]);
            }
        }
    }

    private FogBlock GetBlock()
    {
        if (spare.Count > 0)
        {
            FogBlock reused = spare[spare.Count - 1];
            spare.RemoveAt(spare.Count - 1);
            return reused;
        }

        Texture2D[] textures = GetCloudTextures();
        FogBlock block = new FogBlock
        {
            Root = new GameObject("Fog Bank"),
            Materials = new Material[textures.Length]
        };
        for (int i = 0; i < textures.Length; i++)
            block.Materials[i] = CreateMaterial(textures[i]);
        return block;
    }

    private void EnsurePuffs(FogBlock block, int count)
    {
        while (block.Puffs.Count < count)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Fog Puff";
            Collider collider = quad.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
                Destroy(collider);
            }

            Renderer renderer = quad.GetComponent<Renderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sharedMaterial = block.Materials[block.Puffs.Count % block.Materials.Length];

            quad.transform.SetParent(block.Root.transform, true);
            block.Puffs.Add(quad.transform);
            block.Rolls.Add(0f);
        }

        for (int i = 0; i < block.Puffs.Count; i++)
            block.Puffs[i].gameObject.SetActive(i < count);
    }

    private Material CreateMaterial(Texture2D texture)
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            Debug.LogWarning($"{nameof(SnowFogObstacle)}: Sprites/Default shader not found.", this);
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }

        Material material = new Material(shader);
        material.mainTexture = texture;
        material.renderQueue = 3000;
        return material;
    }

    private Texture2D[] GetCloudTextures()
    {
        if (cloudTextures != null) return cloudTextures;
        cloudTextures = new[] { MakeCloudTexture(11f), MakeCloudTexture(47f) };
        return cloudTextures;
    }

    private static Texture2D MakeCloudTexture(float seed)
    {
        const int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = x / (size - 1f);
                float v = y / (size - 1f);
                float dx = u * 2f - 1f;
                float dy = v * 2f - 1f;
                float radius = Mathf.Sqrt(dx * dx + dy * dy);
                float falloff = 1f - Mathf.SmoothStep(0.55f, 1f, radius);

                float noise = 0.65f * Mathf.PerlinNoise(seed + u * 3f, seed + v * 3f)
                    + 0.35f * Mathf.PerlinNoise(seed + u * 8f, seed + v * 8f);
                float alpha = Mathf.Clamp01(falloff * (0.9f + 0.6f * noise));
                float shade = Mathf.Lerp(0.82f, 1f, noise);
                texture.SetPixel(x, y, new Color(shade, shade, shade, alpha));
            }
        }
        texture.Apply(false, false);
        return texture;
    }
}