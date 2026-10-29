using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

public class AmbientColorSlider : MonoBehaviour
{
    [SerializeField] Slider slider;          // Min 0, Max 1
    [SerializeField] float startValue = 0.2f;

    bool dirty;
    float nextUpdate;

    void Start()
    {
        // Ambient Color solo se usa si Source está en "Color"
        RenderSettings.ambientMode = AmbientMode.Flat;

        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.SetValueWithoutNotify(startValue);
        slider.onValueChanged.AddListener(Apply);

        Apply(startValue);
    }

    void Apply(float value)
    {
        RenderSettings.ambientLight = Color.Lerp(Color.black, Color.white, value);
        dirty = true;
    }

    void Update()
    {
        // Refresca la iluminación ambiente como máximo 5 veces por segundo
        if (dirty && Time.time >= nextUpdate)
        {
            DynamicGI.UpdateEnvironment();
            dirty = false;
            nextUpdate = Time.time + 0.2f;
        }
    }
}