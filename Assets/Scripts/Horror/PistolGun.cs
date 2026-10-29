using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace SgiVr.Horror
{
    [RequireComponent(typeof(XRGrabInteractable))]
    public class PistolGun : MonoBehaviour
    {
        public enum BarrelAxis { Automatico, X, Y, Z }

        [Header("Dirección del cañón")]
        [Tooltip("Objeto vacío en la punta del cañón con su eje Z (azul) apuntando hacia delante. Recomendado.")]
        public Transform puntoCanon;   // NUEVO
        public BarrelAxis ejeCanon = BarrelAxis.Automatico;
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

        void OnEnable() => grab.activated.AddListener(OnActivated);
        void OnDisable() => grab.activated.RemoveListener(OnActivated);
        void OnActivated(ActivateEventArgs args) => Shoot();

        void GetMuzzle(out Vector3 position, out Vector3 direction)
        {
            // NUEVO: si hay punto de cañón, se usa directamente
            if (puntoCanon != null)
            {
                position = puntoCanon.position;
                direction = (invertirCanon ? -puntoCanon.forward : puntoCanon.forward).normalized;
                return;
            }

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

        // NUEVO: raycast que ignora los colliders de la propia pistola
        bool CastRay(Vector3 origin, Vector3 dir, out RaycastHit best)
        {
            best = default;
            var hits = Physics.RaycastAll(origin, dir, range, ~0, QueryTriggerInteraction.Ignore);
            float min = float.MaxValue;
            bool found = false;
            foreach (var h in hits)
            {
                if (h.collider.transform.IsChildOf(transform)) continue;
                if (h.distance < min) { min = h.distance; best = h; found = true; }
            }
            return found;
        }

        public void Shoot()
        {
            if (Time.time - lastShotTime < fireCooldown) return;
            lastShotTime = Time.time;

            GetMuzzle(out Vector3 origin, out Vector3 dir);
            muzzleFlash.transform.position = origin;
            muzzleFlash.enabled = true;
            flashTimer = 0.06f;

            if (CastRay(origin, dir, out RaycastHit hit))
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
            Vector3 end = CastRay(origin, dir, out RaycastHit hit) ? hit.point : origin + dir * range;
            laser.SetPosition(0, origin);
            laser.SetPosition(1, end);
        }

        // NUEVO: línea en la Scene view para ver hacia dónde apunta el cañón
        void OnDrawGizmosSelected()
        {
            if (puntoCanon == null) return;
            Gizmos.color = Color.red;
            Vector3 d = invertirCanon ? -puntoCanon.forward : puntoCanon.forward;
            Gizmos.DrawRay(puntoCanon.position, d * 0.3f);
            Gizmos.DrawSphere(puntoCanon.position, 0.005f);
        }
    }
}