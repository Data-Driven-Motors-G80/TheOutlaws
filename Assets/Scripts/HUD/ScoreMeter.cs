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
        Refresh();
    }

    private void Update()
    {
        Score += Vector3.Distance(car.position, previousCarPosition) * pointsPerUnit;
        previousCarPosition = car.position;

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
        label.SetText("Score: {0:0}", wholeScore);
    }
}