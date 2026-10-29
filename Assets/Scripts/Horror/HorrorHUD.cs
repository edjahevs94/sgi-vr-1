using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace SgiVr.Horror
{
    /// <summary>
    /// HUD de la casa de terror. Va en el objeto "HUD" (un Canvas en World Space dentro de Main Camera)
    /// y actualiza sus barras: tu vida, la pila de la linterna y la vida del payaso.
    /// Las barras son objetos normales de la escena: muévelos, cámbiales el tamaño o el color en el editor.
    /// Cada barra es una Image en modo "Filled" (horizontal): el script solo cambia su Fill Amount y su texto.
    /// </summary>
    public class HorrorHUD : MonoBehaviour
    {
        [Header("Barras (Image en modo Filled) y textos")]
        public Image playerFill;
        public TMP_Text playerText;
        public Image batteryFill;
        public TMP_Text batteryText;
        [Tooltip("Objeto de la barra del payaso: se oculta si en la escena no hay payaso.")]
        public GameObject clownGroup;
        public Image clownFill;
        public TMP_Text clownText;

        [Header("De quién lee los datos (si se dejan vacíos, los busca solo)")]
        public PlayerHealth player;
        public FlashlightBattery battery;
        public ClownEnemy clown;

        [Header("Colores")]
        public Color healthFull = new Color(0.2f, 0.85f, 0.3f);
        public Color healthLow = new Color(0.9f, 0.1f, 0.1f);
        public Color batteryColor = new Color(1f, 0.85f, 0.1f);
        public Color clownColor = new Color(0.85f, 0.05f, 0.1f);
        public Color clownDownColor = new Color(0.45f, 0.3f, 0.6f);

        [Tooltip("Dibuja el HUD por encima de todo para que no se meta dentro de las paredes al acercarte a ellas.")]
        public bool drawOnTop = true;

        [Tooltip("Velocidad a la que se mueven las barras (fracción por segundo).")]
        public float barSpeed = 1.5f;

        void Awake()
        {
            if (drawOnTop) MakeDrawOnTop();
        }

        void Start()
        {
            if (!player) player = PlayerHealth.Instance ? PlayerHealth.Instance : FindAnyObjectByType<PlayerHealth>();
            if (!battery) battery = FindAnyObjectByType<FlashlightBattery>();
            if (!clown) clown = FindAnyObjectByType<ClownEnemy>();
            if (clownGroup) clownGroup.SetActive(clown);
        }

        void Update()
        {
            if (player)
            {
                float v = player.Health / Mathf.Max(1, player.maxHealth);
                SetBar(playerFill, playerText, v, Color.Lerp(healthLow, healthFull, v), "VIDA " + Mathf.CeilToInt(player.Health));
            }

            if (battery)
            {
                float v = battery.Percent / 100f;
                string text = battery.Percent <= 0f ? "SIN PILA" : "LINTERNA " + Mathf.CeilToInt(battery.Percent) + "%";
                SetBar(batteryFill, batteryText, v, v < 0.2f ? healthLow : batteryColor, text);
            }

            if (clown)
            {
                if (clown.IsDown)
                {
                    // En el suelo: la barra se va llenando hasta que se levanta.
                    int seconds = Mathf.CeilToInt(clown.SecondsToRevive);
                    SetBar(clownFill, clownText, clown.ReviveProgress, clownDownColor, "PAYASO EN EL SUELO " + seconds + " s");
                }
                else
                {
                    SetBar(clownFill, clownText, (float)clown.Health / clown.MaxHealth, clownColor, "PAYASO");
                }
            }
        }

        void SetBar(Image fill, TMP_Text label, float value, Color color, string text)
        {
            if (fill)
            {
                fill.fillAmount = Mathf.MoveTowards(fill.fillAmount, Mathf.Clamp01(value), Time.deltaTime * barSpeed);
                fill.color = color;
            }
            if (label && label.text != text) label.text = text;
        }

        /// <summary>
        /// Cambia el material de las imágenes y textos para que ignoren la profundidad (ZTest Always).
        /// Se hace al empezar la partida; en la escena guardada no cambia nada.
        /// </summary>
        void MakeDrawOnTop()
        {
            var uiMaterial = new Material(Shader.Find("UI/Default"));
            uiMaterial.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);
            foreach (Image image in GetComponentsInChildren<Image>(true))
                if (!image.material || image.material == image.defaultMaterial) image.material = uiMaterial;

            foreach (TMP_Text text in GetComponentsInChildren<TMP_Text>(true))
                if (text.fontSharedMaterial) text.fontMaterial.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);
        }
    }
}
