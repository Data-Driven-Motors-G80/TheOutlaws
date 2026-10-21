using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PickupSpawner : MonoBehaviour
{
    [SerializeField] private InfiniteRoad road;
    [SerializeField] private Transform car;
    [SerializeField] private PickupItem pickupPrefab;
    [SerializeField] private ObstacleSpawner obstacles;
    [SerializeField, Min(0.1f)] private float effectDuration = 5f;
    [SerializeField, Min(1f)] private float firstRowSeconds = 5f;
    [SerializeField, Min(5f)] private float rowIntervalSeconds = 20f;
    public Func<float> RunTime;
    public Func<float> RunSpeed;
    public float NextRowAt { get; private set; } = 5f;
    public bool Suppressed { get; set; }
    private readonly List<PickupItem> active = new List<PickupItem>();
    private bool claimed;
    private Vector3 rowPosition;
    private CarPickupEffects effects;
    private void Start()
    {
        if (car == null || road == null || pickupPrefab == null) { enabled = false; return; }
        effects = car.GetComponent<CarPickupEffects>();
    }
    public void RestoreSchedule(float at) { NextRowAt = at; }
    public void BeginRealRun() { Suppressed = false; NextRowAt = (RunTime?.Invoke() ?? 0f) + firstRowSeconds; }
    public void ClearAndSuppress() { Suppressed = true; ClearRow(); }
    private void OnDisable() { ClearRow(); }
    private void ClearRow()
    {
        if (obstacles != null) obstacles.ReleaseSafeGap(this);
        foreach (var item in active) if (item != null) { item.gameObject.SetActive(false); Destroy(item.gameObject); }
        active.Clear();
    }
    private void Update()
    {
        if (Suppressed || Time.timeScale <= 0f || effects == null || effects.RunState.IsGameOver) return;
        float now = RunTime?.Invoke() ?? 0f;
        if (active.Count > 0)
        {
            if (road.IsBehind(rowPosition, car.position, 4f))
            {
                GameEvents.RaisePickupMissed(PickupKind.Mystery);
                ClearRow(); NextRowAt = now + rowIntervalSeconds;
            }
            return;
        }
        float speed = Mathf.Max(1f, RunSpeed?.Invoke() ?? 25f);
        float remaining = Mathf.Max(0f, NextRowAt - now);
        if (remaining > firstRowSeconds) return;
        float lead = Mathf.Max(PickupLookAhead.Distance, remaining * speed);
        // Reserve before checking: existing rows pass naturally while new ones wait.
        if (obstacles == null || !obstacles.TryReserveSafeGap(this)) return;
        if (!road.TryGetPointAhead(car.position, lead, out RoadPathPoint point)) return;
        if (!obstacles.IsSafeGap(point.Position, 14f)) return;
        claimed = false; rowPosition = point.Position;
        float laneWidth = point.Width / 3f;
        for (int lane = 0; lane < 3; lane++)
        {
            var item = Instantiate(pickupPrefab, point.Position + point.Right * ((lane - 1) * laneWidth) + point.Up * 0.6f,
                Quaternion.LookRotation(point.Forward, point.Up), transform);
            item.Configure(PickupEffectType.RandomFuelOrReverse, effectDuration);
            item.GetComponent<SphereCollider>().enabled = false;
            var trigger = item.gameObject.AddComponent<BoxCollider>();
            trigger.isTrigger = true; trigger.size = new Vector3(laneWidth / item.transform.lossyScale.x, 3f / item.transform.lossyScale.y, 2f / item.transform.lossyScale.z);
            item.ClaimRow = () => { if (claimed) return false; claimed = true; return true; };
            item.CollectedCallback = () => { ClearRow(); NextRowAt = (RunTime?.Invoke() ?? now) + rowIntervalSeconds; };
            var renderer = item.GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                Vector3 scale = renderer.transform.localScale;
                scale.x *= laneWidth * 0.95f / Mathf.Max(0.01f, renderer.bounds.size.x);
                renderer.transform.localScale = scale;
                WorldLabel.Attach(renderer, "?", new Color(1f, 0.85f, 0.2f));
            }
            active.Add(item);
        }
        GameEvents.RaisePickupSpawned(PickupKind.Mystery);
    }
    public PickupItem SpawnTutorialNitro(float distance, float lateral)
    {
        return SpawnTutorialMystery(distance, lateral, PickupEffectType.Boost);
    }

    public PickupItem SpawnTutorialMystery(float distance, float lateral, PickupEffectType outcome)
    {
        if (pickupPrefab == null || !TryGetPointAhead(distance, out RoadPathPoint point)) return null;
        float limit = Mathf.Max(0f, point.Width * 0.5f - 1.2f);
        Vector3 position = point.Position + point.Right * Mathf.Clamp(lateral, -limit, limit)
            + point.Up * 0.6f;
        PickupItem item = Instantiate(pickupPrefab, position, Quaternion.identity, transform);
        item.Configure(outcome, effectDuration);
        Renderer box = item.GetComponentInChildren<Renderer>();
        if (box != null) WorldLabel.Attach(box, "?", new Color(1f, 0.85f, 0.2f));
        return item;
    }

    private bool TryGetPointAhead(float distance, out RoadPathPoint point) => road.TryGetPointAhead(car.position, distance, out point);
}
