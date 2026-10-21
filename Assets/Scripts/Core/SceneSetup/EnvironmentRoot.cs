using UnityEngine;

/// <summary>The explicit scene references and configuration shared by environment systems.</summary>
[DisallowMultipleComponent]
public sealed class EnvironmentRoot : MonoBehaviour
{
    [SerializeField] private AutoDriveCar player;
    [SerializeField] private InfiniteRoad road;
    [SerializeField] private ObstacleSpawner obstacles;
    [SerializeField] private FuelTankSpawner fuelSpawner;
    [SerializeField] private PickupSpawner mysterySpawner;
    [SerializeField] private ChaseCar[] policeCars;
    [SerializeField] private ScoreMeter score;
    [SerializeField] private RewardTable rewards;
    [SerializeField] private bool snow;

    public AutoDriveCar Player => player;
    public InfiniteRoad Road => road;
    public ObstacleSpawner Obstacles => obstacles;
    public FuelTankSpawner FuelSpawner => fuelSpawner;
    public PickupSpawner MysterySpawner => mysterySpawner;
    public ChaseCar[] PoliceCars => policeCars;
    public ScoreMeter Score => score;
    public RewardTable Rewards => rewards;
    public bool IsSnow => snow;

    public bool ValidateSetup()
    {
        bool valid = player != null && road != null && obstacles != null &&
            fuelSpawner != null && mysterySpawner != null && score != null && rewards != null &&
            policeCars != null && policeCars.Length > 0;
        if (policeCars != null)
            foreach (ChaseCar police in policeCars) valid &= police != null;
        if (!valid)
            Debug.LogError("EnvironmentRoot needs its player, road, spawners, police, score and reward table assigned.", this);
        return valid;
    }
}
