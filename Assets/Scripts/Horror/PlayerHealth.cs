using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SgiVr.Horror
{
    /// <summary>
    /// Vida del jugador. Va en la cámara del XR Origin. Al recibir daño la vista se tiñe de rojo;
    /// al morir, la escena se reinicia.
    /// </summary>
    public class PlayerHealth : MonoBehaviour
    {
        public static PlayerHealth Instance { get; private set; }

        public int maxHealth = 100;
        [Tooltip("Vida que se recupera por segundo si no te golpean durante un rato.")]
        public float regenPerSecond = 4f;
        public float regenDelay = 5f;
        [Tooltip("Plano rojo delante de la cámara.")]
        public Renderer damageOverlay;
        public float restartDelay = 3f;

        float health;
        float flash;
        float lastHitTime = -999f;
        bool dead;
        MaterialPropertyBlock block;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        public bool IsDead => dead;
        public float Health => health;

        void Awake()
        {
            Instance = this;
            health = maxHealth;
            block = new MaterialPropertyBlock();
            UpdateOverlay();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void TakeDamage(int amount)
        {
            if (dead) return;
            health -= amount;
            lastHitTime = Time.time;
            flash = 1f;
            if (health <= 0f)
            {
                health = 0f;
                dead = true;
                Debug.Log("Has muerto. Reiniciando la escena...");
                StartCoroutine(RestartAfterDelay());
            }
        }

        void Update()
        {
            if (!dead && Time.time - lastHitTime > regenDelay)
                health = Mathf.Min(maxHealth, health + regenPerSecond * Time.deltaTime);
            flash = Mathf.MoveTowards(flash, 0f, Time.deltaTime * 1.5f);
            UpdateOverlay();
        }

        void UpdateOverlay()
        {
            if (!damageOverlay) return;
            float lowHealth = 1f - health / Mathf.Max(1, maxHealth);
            float alpha = dead ? 0.85f : Mathf.Clamp01(flash * 0.55f + lowHealth * 0.25f);
            damageOverlay.enabled = alpha > 0.01f;
            damageOverlay.GetPropertyBlock(block);
            block.SetColor(BaseColorId, new Color(0.5f, 0f, 0f, alpha));
            damageOverlay.SetPropertyBlock(block);
        }

        IEnumerator RestartAfterDelay()
        {
            yield return new WaitForSeconds(restartDelay);
            Scene scene = SceneManager.GetActiveScene();
            if (scene.buildIndex >= 0) SceneManager.LoadScene(scene.buildIndex);
            else SceneManager.LoadScene(scene.name);
        }
    }
}
