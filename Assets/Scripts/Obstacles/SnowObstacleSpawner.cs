using UnityEngine;

[DisallowMultipleComponent]
public sealed class SnowObstacleSpawner : ObstacleSpawner
{
    [Header("Snow: Frequency")]
    [Tooltip("Multiplies the gap between obstacle rows. 1 = same as city, 2 = half as many rows.")]
    [SerializeField, Min(1f)] private float rowSpacingMultiplier = 1.7f;
    [Tooltip("Multiplies the gap between roadblocks. 1 = same as city.")]
    [SerializeField, Min(1f)] private float roadblockSpacingMultiplier = 1.7f;

    public InfiniteRoad RoadReference => Road;

    protected override float RowSpacingMultiplier => rowSpacingMultiplier;
    protected override float RoadblockSpacingMultiplier => roadblockSpacingMultiplier;
}