using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SgiVr.Horror;

public class BatteryDrainSlider : MonoBehaviour
{
    [SerializeField] Slider slider;
    [SerializeField] FlashlightBattery battery;   // si está vacío, la busca sola
    [SerializeField] TMP_Text label;              // opcional: muestra "Gasto x1.5"

    [Header("Rango")]
    [SerializeField] float minMultiplier = 0f;
    [SerializeField] float maxMultiplier = 3f;

    void Start()
    {
        if (battery == null) battery = FindFirstObjectByType<FlashlightBattery>();
        if (battery == null || slider == null) { enabled = false; return; }

        slider.minValue = minMultiplier;
        slider.maxValue = maxMultiplier;
        slider.wholeNumbers = false;

        // Arranca con el valor que ya tenga la linterna
        slider.SetValueWithoutNotify(battery.drainMultiplier);
        Apply(battery.drainMultiplier);

        slider.onValueChanged.AddListener(Apply);
    }

    void OnDestroy()
    {
        if (slider != null) slider.onValueChanged.RemoveListener(Apply);
    }

    void Apply(float value)
    {
        battery.drainMultiplier = value;
        if (label != null) label.text = "Gasto x" + value.ToString("0.0");
    }
}