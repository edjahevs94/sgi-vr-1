using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace SgiVr.Horror
{
    /// <summary>
    /// Pistola: cógela con el grip y dispara con el gatillo. Muestra un láser mientras la sujetas.
    /// El cañón se calcula a partir del eje más largo del modelo; si el láser sale hacia atrás,
    /// marca "Invertir cañón" en el Inspector.
    /// </summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    public class PistolGun : MonoBehaviour
    {
        public enum BarrelAxis { Automatico, X, Y, Z }

        [Header("Dirección del cañón")]
        public BarrelAxis ejeCanon = BarrelAxis.Automatico;
        [Tooltip("Marca esta casilla si el láser sale por la parte de atrás de la pistola.")]
        public bool invertirCanon;

        [Header("Disparo")]
        public int damage = 1;
        public float range = 40f;
        public float fireCooldown = 0.3f;

        [Header("Láser")]
        public bool showLaser = true;
        public Material laserMaterial;

        XRGrabInteractable grab;
        LineRenderer laser;
        Light muzzleFlash;
        Bounds localBounds;
        float lastShotTime = -999f;
        float flashTimer;

        void Awake()
        {
            grab = GetComponent<XRGrabInteractable>();
            localBounds = BoundsUtil.LocalBounds(transform);

            var flashGo = new GameObject("Fogonazo");
            flashGo.transform.SetParent(transform, false);
            muzzleFlash = flashGo.AddComponent<Light>();
            muzzleFlash.type = LightType.Point;
            muzzleFlash.color = new Color(1f, 0.7f, 0.3f);
            muzzleFlash.range = 5f;
            muzzleFlash.intensity = 4f;
            muzzleFlash.shadows = LightShadows.None;
            muzzleFlash.enabled = false;

            if (laserMaterial)
            {
                var laserGo = new GameObject("Laser");
                laserGo.transform.SetParent(transform, false);
                laser = laserGo.AddComponent<LineRenderer>();
                laser.useWorldSpace = true;
                laser.positionCount = 2;
                laser.widthMultiplier = 0.004f;
                laser.sharedMaterial = laserMaterial;
                laser.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                laser.receiveShadows = false;
                laser.enabled = false;
            }
        }

        void OnEnable()
        {
            grab.activated.AddListener(OnActivated);
        }

        void OnDisable()
        {
            grab.activated.RemoveListener(OnActivated);
        }

        void OnActivated(ActivateEventArgs args) => Shoot();

        void GetMuzzle(out Vector3 position, out Vector3 direction)
        {
            Vector3 size = localBounds.size;
            int axis;
            switch (ejeCanon)
            {
                case BarrelAxis.X: axis = 0; break;
                case BarrelAxis.Y: axis = 1; break;
                case BarrelAxis.Z: axis = 2; break;
                default: axis = size.x >= size.y && size.x >= size.z ? 0 : (size.y >= size.z ? 1 : 2); break;
            }
            Vector3 localDir = Vector3.zero;
            localDir[axis] = invertirCanon ? -1f : 1f;
            Vector3 localPos = localBounds.center + Vector3.Scale(localDir, localBounds.extents) + localDir * 0.01f;
            position = transform.TransformPoint(localPos);
            direction = transform.TransformDirection(localDir).normalized;
        }

        public void Shoot()
        {
            if (Time.time - lastShotTime < fireCooldown) return;
            lastShotTime = Time.time;

            GetMuzzle(out Vector3 origin, out Vector3 dir);
            muzzleFlash.transform.position = origin;
            muzzleFlash.enabled = true;
            flashTimer = 0.06f;

            if (Physics.Raycast(origin, dir, out RaycastHit hit, range, ~0, QueryTriggerInteraction.Ignore))
            {
                var target = hit.collider.GetComponentInParent<IDamageable>();
                if (target != null) target.TakeDamage(damage, hit.point);
            }
        }

        void Update()
        {
            if (flashTimer > 0f)
            {
                flashTimer -= Time.deltaTime;
                if (flashTimer <= 0f) muzzleFlash.enabled = false;
            }

            if (!laser) return;
            bool on = showLaser && grab.isSelected;
            laser.enabled = on;
            if (!on) return;
            GetMuzzle(out Vector3 origin, out Vector3 dir);
            Vector3 end = Physics.Raycast(origin, dir, out RaycastHit hit, range, ~0, QueryTriggerInteraction.Ignore)
                ? hit.point
                : origin + dir * range;
            laser.SetPosition(0, origin);
            laser.SetPosition(1, end);
        }
    }
}
