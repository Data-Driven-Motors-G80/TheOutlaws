using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ScoreMeter : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform car;
    [SerializeField] private TMP_Text label;

    [Header("Scoring")]
    [SerializeField, Min(0f)] private float pointsPerUnit = 1f;

    private Vector3 previousCarPosition;
    private int displayedScore = -1;
    private OutlawGameManager game;
    private string shownStatus;

    public float Score { get; private set; }

    private void Reset()
    {
        label = GetComponent<TMP_Text>();
    }

    private void Awake()
    {
        if (car == null || label == null)
        {
            Debug.LogError($"{nameof(ScoreMeter)}: the car and the text label must be assigned.", this);
            enabled = false;
            return;
        }

        previousCarPosition = car.position;
        game = Object.FindFirstObjectByType<OutlawGameManager>();
        Refresh();
    }

    private void Start()
    {
        if (!RunSession.TryGetPending(out RunSnapshot carried)) return;
        Score = carried.Score;
        previousCarPosition = car.position;
        Refresh();
    }

    private void Update()
    {
        if (game != null)
        {
            // The run's distance is the score; the tutorial earns nothing.
            Score = game.DistanceTravelled * pointsPerUnit;
            previousCarPosition = car.position;
            if (game.IsTutorial || game.State == OutlawGameState.Ready)
            {
                // Blank during the tutorial: its instruction box sits in this spot.
                string status = "";
                if (shownStatus != status) label.SetText(status);
                shownStatus = status;
                displayedScore = -1;
                return;
            }
            shownStatus = null;
        }
        else
        {
            Score += Vector3.Distance(car.position, previousCarPosition) * pointsPerUnit;
            previousCarPosition = car.position;
        }

        Refresh();
    }

    private void Refresh()
    {
        int wholeScore = Mathf.FloorToInt(Score);

        if (wholeScore == displayedScore)
        {
            return;
        }

        displayedScore = wholeScore;
        label.SetText(game != null ? "{0:0} m" : "Score: {0:0}", wholeScore);
    }
}