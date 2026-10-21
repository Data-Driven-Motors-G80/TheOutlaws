using UnityEngine;

/// <summary>Preserves existing snow scenes and their update order; behaviour is composed on the base spawner.</summary>
[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public sealed class SnowObstacleSpawner : ObstacleSpawner
{
}
