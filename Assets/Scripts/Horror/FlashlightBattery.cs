using UnityEngine;

namespace SgiVr.Horror
{
    /// <summary>
    /// Pila de la linterna: solo se gasta mientras la luz está encendida (apagada se queda igual).
    /// Sin pila, la luz no se puede encender. Con poca pila, parpadea.
    /// Va en la linterna de la escena, junto al script FlashlightToggle.
    /// </summary>
    [DisallowMultipleComponent]
    public class FlashlightBattery : MonoBehaviour
    {
        [Tooltip("Porcentaje de pila que se gasta por segundo con la luz encendida (0,5 = 200 s de luz).")]
        public float drainPerSecond = 0.5f;
        [Tooltip("Porcentaje que se recupera por segundo con la luz apagada (0 = no se recarga nunca).")]
        public float rechargePerSecond = 0f;
        [Tooltip("Por debajo de este porcentaje la luz parpadea.")]
        public float flickerBelow = 10f;

        public static FlashlightBattery Instance { get; private set; }
        [Tooltip("1 = velocidad normal, 0 = no gasta, 2 = el doble de rápido")]
        public float drainMultiplier = 1f;

        /// <summary>Pila restante, de 0 a 100.</summary>
        public float Percent { get;  set; } = 100f;
        public bool IsOn { get; set; }

        Light[] lights;
        float[] baseIntensity;
        float flickerTimer;
        float flickerFactor = 1f;

        void Awake()
        {
            Instance = this;
            lights = GetComponentsInChildren<Light>(true);
            baseIntensity = new float[lights.Length];
            for (int i = 0; i < lights.Length; i++) baseIntensity[i] = lights[i].intensity;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Recharge(float amount)
        {
            Percent = Mathf.Clamp(Percent + amount, 0f, 100f);
        }

        // LateUpdate: va después de que el botón encienda o apague la luz en el mismo frame.
        void LateUpdate()
        {
            IsOn = false;
            foreach (Light l in lights)
                if (l && l.enabled) IsOn = true;

            if (IsOn)
            {
                Percent = Mathf.Max(0f, Percent - drainPerSecond * drainMultiplier * Time.deltaTime);
                if (Percent <= 0f)
                {
                    foreach (Light l in lights) if (l) l.enabled = false;
                    IsOn = false;
                }
            }
            else
            {
                Percent = Mathf.Min(100f, Percent + rechargePerSecond * Time.deltaTime);
            }

            // Parpadeo con poca pila.
            if (IsOn && Percent < flickerBelow)
            {
                flickerTimer -= Time.deltaTime;
                if (flickerTimer <= 0f)
                {
                    flickerFactor = Random.value < 0.25f ? Random.Range(0.05f, 0.4f) : 1f;
                    flickerTimer = Random.Range(0.04f, 0.25f);
                }
            }
            else
            {
                flickerFactor = 1f;
            }

            for (int i = 0; i < lights.Length; i++)
                if (lights[i]) lights[i].intensity = baseIntensity[i] * flickerFactor;
        }
    }
}
