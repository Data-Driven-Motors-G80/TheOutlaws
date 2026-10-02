using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SphereCollider))]
public sealed class PickupItem : MonoBehaviour
{
    [SerializeField] private PickupEffectType effect = PickupEffectType.ReverseSteering;
    [SerializeField, Min(0.1f)] private float duration = 5f;
    private bool collected;

    public void Configure(PickupEffectType type, float seconds)
    {
        effect = type;
        duration = seconds;
    }

    private void Reset()
    {
        GetComponent<SphereCollider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (collected) return;
        CarPickupEffects receiver = other.GetComponentInParent<CarPickupEffects>();
        if (receiver == null || !receiver.isActiveAndEnabled) return;
        if (!receiver.TryApply(effect, duration)) return;

        collected = true;
        GetComponent<SphereCollider>().enabled = false;
        gameObject.SetActive(false);
        Destroy(gameObject);
    }
}
