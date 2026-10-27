using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace SgiVr.Horror
{
    /// <summary>
    /// Pone un contorno brillante (que late suavemente) alrededor del objeto para encontrarlo en la oscuridad.
    /// Al empezar crea una copia de cada malla con el material de contorno. Se oculta mientras lo sujetas.
    /// </summary>
    [DisallowMultipleComponent]
    public class GlowOutline : MonoBehaviour
    {
        [Tooltip("Material con el shader SgiVr/Outline.")]
        public Material outlineMaterial;
        public Color color = new Color(1f, 0.85f, 0.1f, 1f);
        [Tooltip("Grosor del contorno en píxeles de pantalla (se ve igual de cerca que de lejos).")]
        public float width = 6f;
        public float pulseSpeed = 2.5f;
        [Range(0f, 1f)] public float pulseAmount = 0.4f;
        public bool hideWhenHeld = true;

        readonly List<Renderer> outlines = new List<Renderer>();
        XRBaseInteractable interactable;
        MaterialPropertyBlock block;
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int WidthId = Shader.PropertyToID("_Width");

        void Awake()
        {
            if (!outlineMaterial)
            {
                Debug.LogWarning("GlowOutline sin material de contorno en " + name, this);
                enabled = false;
                return;
            }
            interactable = GetComponent<XRBaseInteractable>();
            block = new MaterialPropertyBlock();

            // Se recogen antes de crear las copias para no copiar las copias.
            MeshFilter[] filters = GetComponentsInChildren<MeshFilter>(true);
            SkinnedMeshRenderer[] skinned = GetComponentsInChildren<SkinnedMeshRenderer>(true);

            foreach (MeshFilter mf in filters)
            {
                if (!mf.sharedMesh || !mf.GetComponent<MeshRenderer>()) continue;
                GameObject go = CreateChild(mf.transform, mf.name);
                go.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                Setup(go.AddComponent<MeshRenderer>(), mf.sharedMesh.subMeshCount);
            }

            foreach (SkinnedMeshRenderer smr in skinned)
            {
                if (!smr.sharedMesh) continue;
                GameObject go = CreateChild(smr.transform, smr.name);
                var copy = go.AddComponent<SkinnedMeshRenderer>();
                copy.sharedMesh = smr.sharedMesh;
                copy.bones = smr.bones;
                copy.rootBone = smr.rootBone;
                copy.localBounds = smr.localBounds;
                Setup(copy, smr.sharedMesh.subMeshCount);
            }
        }

        static GameObject CreateChild(Transform parent, string baseName)
        {
            var go = new GameObject(baseName + "_Contorno");
            go.transform.SetParent(parent, false);
            return go;
        }

        void Setup(Renderer r, int subMeshCount)
        {
            var mats = new Material[Mathf.Max(1, subMeshCount)];
            for (int i = 0; i < mats.Length; i++) mats[i] = outlineMaterial;
            r.sharedMaterials = mats;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            outlines.Add(r);
        }

        void Update()
        {
            bool show = !(hideWhenHeld && interactable && interactable.isSelected);
            float pulse = 1f - pulseAmount * 0.5f * (1f + Mathf.Sin(Time.time * pulseSpeed * Mathf.PI));
            block.SetColor(ColorId, color * pulse);
            block.SetFloat(WidthId, width);

            foreach (Renderer r in outlines)
            {
                if (!r) continue;
                r.enabled = show;
                if (show) r.SetPropertyBlock(block);
            }
        }
    }
}
