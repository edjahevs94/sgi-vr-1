using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using SgiVr.Horror;

[RequireComponent(typeof(XRGrabInteractable))]
public class BatteryPickup : MonoBehaviour
{
    [Header("Linterna")]
    [SerializeField] FlashlightBattery battery;
    [Tooltip("Collider de la linterna. Si está vacío, usa el primero que encuentre en la linterna.")]
    [SerializeField] Collider flashlightCollider;

    [Header("Recarga")]
    [SerializeField] float rechargeAmount = 20f;
    [SerializeField] bool destroyAfterUse = true;
    [Tooltip("Solo recarga mientras la pila está agarrada")]
    [SerializeField] bool requireGrabbed = true;

    [Header("Feedback (opcional)")]
    [SerializeField] AudioClip useSound;

    XRGrabInteractable grab;
    Collider[] myColliders;

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        myColliders = GetComponentsInChildren<Collider>();

        if (battery == null) battery = FindFirstObjectByType<FlashlightBattery>();
        if (flashlightCollider == null && battery != null)
            flashlightCollider = battery.GetComponentInChildren<Collider>();
    }

    void OnEnable()
    {
        grab.selectEntered.AddListener(OnGrab);
        grab.selectExited.AddListener(OnRelease);
    }

    void OnDisable()
    {
        grab.selectEntered.RemoveListener(OnGrab);
        grab.selectExited.RemoveListener(OnRelease);
    }

    // Agarrada: trigger para que los raycasts la ignoren
    void OnGrab(SelectEnterEventArgs args) => SetTrigger(true);
    // Soltada: vuelve a ser sólida
    void OnRelease(SelectExitEventArgs args) => SetTrigger(false);

    void SetTrigger(bool value)
    {
        foreach (var c in myColliders)
            if (c != null) c.isTrigger = value;
    }

    void Update()
    {
        if (battery == null || flashlightCollider == null) return;
        if (requireGrabbed && !grab.isSelected) return;

        foreach (var c in myColliders)
        {
            if (c == null || !c.enabled) continue;
            if (c.bounds.Intersects(flashlightCollider.bounds))
            {
                Recharge();
                return;
            }
        }
    }

    void Recharge()
    {
        battery.Recharge(rechargeAmount);

        if (useSound != null)
            AudioSource.PlayClipAtPoint(useSound, transform.position);

        if (destroyAfterUse)
            Destroy(gameObject);
        else
            enabled = false; // evita recargar en cada frame; reactívalo si quieres reutilizarla
    }
}