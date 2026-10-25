using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SphereCollider))]
public sealed class PickupItem : MonoBehaviour
{
    [SerializeField] private PickupEffectType effect = PickupEffectType.ReverseSteering;
    [SerializeField, Min(0.1f)] private float duration = 5f;
    private bool collected;
    private bool retiring;
    public System.Func<bool> ClaimRow;
    public System.Action CollectedCallback;

    public bool Collected => collected;

    public void Configure(PickupEffectType type, float seconds)
    {
        effect = type;
        duration = seconds;
    }

    // A resolved row remains visible briefly, but can no longer grant rewards.
    public void Retire()
    {
        if (collected) return;
        if (!retiring)
        {
            retiring = true;
            foreach (var collider in GetComponentsInChildren<Collider>()) collider.enabled = false;
        }

    }

    private void Reset()
    {
        GetComponent<SphereCollider>().isTrigger = true;
    }

    private void OnTriggerStay(Collider other)
    {
        if (ClaimRow != null) OnTriggerEnter(other);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (collected || retiring) return;
        CarPickupEffects receiver = other.GetComponentInParent<CarPickupEffects>();
        if (receiver == null || !receiver.isActiveAndEnabled) return;
        if (ClaimRow != null)
        {
            var box = GetComponent<BoxCollider>();
            if (box == null) return;
            float center = transform.InverseTransformPoint(receiver.transform.position).x;
            float forgiveness = 0.1f / Mathf.Max(0.001f, Mathf.Abs(transform.lossyScale.x));
            if (Mathf.Abs(center - box.center.x) > box.size.x * 0.5f + forgiveness) return;
            if (!ClaimRow()) return;
        }
        if (!receiver.TryApply(effect, duration)) return;
        CollectedCallback?.Invoke();

        collected = true;
        GetComponent<SphereCollider>().enabled = false;
        gameObject.SetActive(false);
        Destroy(gameObject);
    }
}
