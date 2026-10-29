using SgiVr.Horror;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace SgiVr.Horror.EditorTools
{
    /// <summary>
    /// Añade el HUD a la escena abierta como objetos normales (Canvas + Images + textos), dentro de Main Camera,
    /// y pone la pila (FlashlightBattery) en la linterna. No toca nada más de la escena.
    /// Se usa una sola vez; después el HUD se edita a mano como cualquier objeto. Se puede deshacer con Ctrl/Cmd+Z.
    /// </summary>
    public static class HorrorHUDBuilder
    {
        [MenuItem("SGI VR/Añadir HUD a la escena abierta")]
        static void AddFromMenu()
        {
            PlayerHealth player = Object.FindAnyObjectByType<PlayerHealth>(FindObjectsInactive.Include);
            if (!player)
            {
                EditorUtility.DisplayDialog("HUD", "No encuentro PlayerHealth en la escena abierta (debería estar en Main Camera).\nAbre CasaTerror y vuelve a probar.", "Vale");
                return;
            }

            HorrorHUD existing = player.GetComponentInChildren<HorrorHUD>(true);
            if (existing && !EditorUtility.DisplayDialog("HUD", "Ya hay un HUD en la cámara. ¿Quieres borrarlo y crear uno nuevo?", "Reemplazar", "Cancelar"))
                return;

            GameObject hud = AddTo(player);
            Selection.activeGameObject = hud;
            EditorGUIUtility.PingObject(hud);
            Debug.Log("[Casa de terror] HUD añadido dentro de " + player.name + ". Guarda la escena (Cmd+S) para conservarlo.");
        }

        /// <summary>Crea el HUD dentro de la cámara del jugador (borra el anterior si lo hay). Lo usa también el generador.</summary>
        public static GameObject AddTo(PlayerHealth player)
        {
            foreach (HorrorHUD old in player.GetComponentsInChildren<HorrorHUD>(true))
                Undo.DestroyObjectImmediate(old.gameObject);

            AddBatteryToFlashlights();

            // Objeto raíz: un Canvas en el espacio del mundo, pegado a la cámara, arriba a la derecha.
            var root = new GameObject("HUD", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(root, "Añadir HUD");
            var rect = (RectTransform)root.transform;
            rect.SetParent(player.transform, false);
            var position = new Vector3(0.31f, 0.22f, 0.55f);
            rect.localPosition = position;
            rect.localRotation = Quaternion.LookRotation(position.normalized, Vector3.up); // mirando a los ojos
            rect.localScale = Vector3.one * 0.0007f;
            rect.sizeDelta = new Vector2(240f, 170f);

            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 100;
            root.AddComponent<CanvasScaler>();

            var hud = root.AddComponent<HorrorHUD>();
            hud.player = player;

            CreateBar(rect, "Vida", new Vector2(0f, 45f), hud.healthFull, "VIDA 100", out hud.playerFill, out hud.playerText);
            CreateBar(rect, "Linterna", new Vector2(0f, -10f), hud.batteryColor, "LINTERNA 100%", out hud.batteryFill, out hud.batteryText);
            hud.clownGroup = CreateBar(rect, "Payaso", new Vector2(0f, -65f), hud.clownColor, "PAYASO", out hud.clownFill, out hud.clownText);

            hud.battery = Object.FindAnyObjectByType<FlashlightBattery>(FindObjectsInactive.Include);
            hud.clown = Object.FindAnyObjectByType<ClownEnemy>(FindObjectsInactive.Include);

            EditorSceneManager.MarkSceneDirty(root.scene);
            return root;
        }

        static void AddBatteryToFlashlights()
        {
            foreach (FlashlightToggle toggle in Object.FindObjectsByType<FlashlightToggle>(FindObjectsInactive.Include))
                if (!toggle.GetComponent<FlashlightBattery>())
                    Undo.AddComponent<FlashlightBattery>(toggle.gameObject);
        }

        /// <summary>Una barra: borde + fondo + relleno (Image Filled) + texto encima.</summary>
        static GameObject CreateBar(RectTransform parent, string name, Vector2 position, Color color, string label,
                                    out Image fill, out TMP_Text text)
        {
            Sprite sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            var group = new GameObject(name, typeof(RectTransform));
            var groupRect = (RectTransform)group.transform;
            groupRect.SetParent(parent, false);
            groupRect.anchoredPosition = position;
            groupRect.sizeDelta = new Vector2(220f, 16f);

            Image border = CreateImage("Borde", groupRect, new Color(0f, 0f, 0f, 0.85f), null);
            Stretch(border.rectTransform, -3f);
            Image back = CreateImage("Fondo", groupRect, new Color(0.15f, 0.15f, 0.15f, 0.85f), null);
            Stretch(back.rectTransform, 0f);

            fill = CreateImage("Relleno", groupRect, color, sprite);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;
            Stretch(fill.rectTransform, 0f);

            var textGo = new GameObject("Texto", typeof(RectTransform));
            var textRect = (RectTransform)textGo.transform;
            textRect.SetParent(groupRect, false);
            textRect.anchorMin = new Vector2(0f, 1f);
            textRect.anchorMax = new Vector2(1f, 1f);
            textRect.pivot = new Vector2(0.5f, 0f);
            textRect.anchoredPosition = new Vector2(0f, 4f);
            textRect.sizeDelta = new Vector2(0f, 30f);
            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = 22f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Right;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            tmp.text = label;
            text = tmp;

            return group;
        }

        static Image CreateImage(string name, RectTransform parent, Color color, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        static void Stretch(RectTransform rect, float grow)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(-grow, -grow);
            rect.offsetMax = new Vector2(grow, grow);
        }
    }
}
