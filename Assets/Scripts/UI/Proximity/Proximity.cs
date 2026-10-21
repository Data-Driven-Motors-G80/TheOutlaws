using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ProximityHUD : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ChaseMeter meter;
    [SerializeField] private Image fillImage;

    [Header("Colors")]
    [SerializeField] private Color safeColor = Color.green;
    [SerializeField] private Color dangerColor = Color.red;

    private void Awake()
    {
        if (meter == null || fillImage == null)
        {
            Debug.LogError($"{nameof(ProximityHUD)}: chase meter and fill image must be assigned.", this);
            enabled = false;
        }
    }

    private void Update()
    {
        fillImage.fillAmount = meter.Value;
        fillImage.color = Color.Lerp(dangerColor, safeColor, meter.Value);
    }
}