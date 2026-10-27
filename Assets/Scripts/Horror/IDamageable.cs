using UnityEngine;

namespace SgiVr.Horror
{
    /// <summary>Cualquier cosa que pueda recibir un disparo (los enemigos).</summary>
    public interface IDamageable
    {
        void TakeDamage(int amount, Vector3 hitPoint);
    }

    public static class BoundsUtil
    {
        /// <summary>
        /// Caja que envuelve todas las mallas de 'root', expresada en coordenadas locales de 'root'.
        /// </summary>
        public static Bounds LocalBounds(Transform root)
        {
            bool hasAny = false;
            Bounds result = new Bounds(Vector3.zero, Vector3.zero);
            Matrix4x4 toRoot = root.worldToLocalMatrix;

            void Add(Mesh mesh, Transform t)
            {
                if (!mesh) return;
                Bounds mb = mesh.bounds;
                Matrix4x4 m = toRoot * t.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var corner = mb.center + Vector3.Scale(mb.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 p = m.MultiplyPoint3x4(corner);
                    if (!hasAny) { result = new Bounds(p, Vector3.zero); hasAny = true; }
                    else result.Encapsulate(p);
                }
            }

            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true)) Add(mf.sharedMesh, mf.transform);
            foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true)) Add(smr.sharedMesh, smr.transform);
            return result;
        }
    }
}
