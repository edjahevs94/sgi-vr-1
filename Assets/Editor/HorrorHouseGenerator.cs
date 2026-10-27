using System.Collections.Generic;
using System.IO;
using System.Linq;
using SgiVr.Horror;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace SgiVr.Horror.EditorTools
{
    /// <summary>
    /// Genera la escena "CasaTerror": una casa vintage de 7 habitaciones + pasillo, totalmente a oscuras
    /// (la única luz es la linterna), con pistola y un payaso que patrulla.
    /// Menú: SGI VR > Generar casa de terror.
    /// </summary>
    public static class HorrorHouseGenerator
    {
        const string ScenePath = "Assets/Scenes/CasaTerror.unity";
        const string OutputFolder = "Assets/Horror";
        const string PiecesFolder = "Assets/Scenes/ZNS3D/Prefabs/";
        const string ClownPrefabPath = "Assets/Clown monster/prefab/base_mesh_1_set_1.prefab";
        const string PistolPrefabPath = "Assets/4K 3D Weapons Mega Pack/Pistol 2/Prefabs/Pistol 2.prefab";
        const string FlashlightModelPath = "Assets/Flashlight/Model/Flashlight.prefab";
        const string XROriginPath = "Assets/VRTemplateAssets/Prefabs/Setup/Complete XR Origin Set Up Hands Variant.prefab";
        const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
        const string FlashlightOutPath = OutputFolder + "/Prefabs/Linterna_VR.prefab";

        [MenuItem("SGI VR/Generar casa de terror")]
        public static void Generate()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            if (File.Exists(ScenePath) &&
                !EditorUtility.DisplayDialog("Casa de terror",
                    "Ya existe " + ScenePath + ".\n¿Quieres sobrescribirla con una casa nueva?", "Sobrescribir", "Cancelar"))
                return;

            try
            {
                EditorUtility.DisplayProgressBar("Casa de terror", "Preparando materiales y prefabs...", 0.1f);
                EnsureFolder(OutputFolder);
                EnsureFolder(OutputFolder + "/Prefabs");
                EnsureFolder(OutputFolder + "/Materials");
                ConvertMaterialsToUrp(false);
                GameObject flashlightPrefab = GetOrCreateFlashlightPrefab();

                EditorUtility.DisplayProgressBar("Casa de terror", "Construyendo la casa...", 0.4f);
                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var builder = new Builder(flashlightPrefab);
                builder.Build();

                EditorUtility.DisplayProgressBar("Casa de terror", "Guardando escena...", 0.9f);
                EditorSceneManager.SaveScene(scene, ScenePath);
                AddSceneToBuildSettings(ScenePath);
                Debug.Log("[Casa de terror] Escena generada en " + ScenePath + ". " + builder.Summary);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        [MenuItem("SGI VR/Convertir materiales de payaso, linterna y pistola a URP")]
        public static void ConvertMaterialsMenu() => ConvertMaterialsToUrp(true);

        // ------------------------------------------------------------------ Materiales

        /// <summary>
        /// La pistola usa el shader "Standard" (se ve morado en URP).
        /// Los pasamos a "Universal Render Pipeline/Lit" conservando sus texturas.
        /// </summary>
        static void ConvertMaterialsToUrp(bool showReport)
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (!lit)
            {
                Debug.LogError("[Casa de terror] No se encontró el shader URP/Lit.");
                return;
            }

            var converted = new List<string>();
            var prefabPaths = new[] { PistolPrefabPath, ClownPrefabPath, FlashlightModelPath };
            foreach (string matPath in prefabPaths.Where(File.Exists)
                         .SelectMany(p => AssetDatabase.GetDependencies(p, true))
                         .Where(p => p.EndsWith(".mat")).Distinct())
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (!mat) continue;
                string shaderName = mat.shader ? mat.shader.name : "";
                bool isUrp = (shaderName.StartsWith("Universal Render Pipeline/") && !shaderName.Contains("/2D/"))
                             || shaderName.StartsWith("Shader Graphs/");
                if (isUrp) continue;

                Undo.RecordObject(mat, "Convertir material a URP");
                Color color = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;
                mat.shader = lit;
                // Al cambiar de shader, Unity conserva las propiedades con el mismo nombre.
                if (!mat.GetTexture("_BaseMap") && mat.HasProperty("_MainTex") && mat.GetTexture("_MainTex"))
                    mat.SetTexture("_BaseMap", mat.GetTexture("_MainTex"));
                mat.SetColor("_BaseColor", color.a <= 0f ? Color.white : color);
                SetKeyword(mat, "_NORMALMAP", mat.GetTexture("_BumpMap"));
                SetKeyword(mat, "_METALLICSPECGLOSSMAP", mat.GetTexture("_MetallicGlossMap"));
                SetKeyword(mat, "_OCCLUSIONMAP", mat.GetTexture("_OcclusionMap"));
                EditorUtility.SetDirty(mat);
                converted.Add(matPath + " (antes: " + shaderName + ")");
            }

            AssetDatabase.SaveAssets();
            string report = converted.Count == 0
                ? "No había materiales que convertir."
                : "Materiales convertidos a URP/Lit:\n" + string.Join("\n", converted);
            Debug.Log("[Casa de terror] " + report);
            if (showReport) EditorUtility.DisplayDialog("Materiales URP", report, "OK");
        }

        static void SetKeyword(Material mat, string keyword, bool on)
        {
            if (on) mat.EnableKeyword(keyword);
            else mat.DisableKeyword(keyword);
        }

        static Material CreateUnlitMaterial(string path, Color color, bool transparent)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", color);
            if (transparent)
            {
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_Blend", 0f);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                mat.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_ZWrite", 0f);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = (int)RenderQueue.Transparent;
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ------------------------------------------------------------------ Linterna

        /// <summary>
        /// Reutiliza la linterna que ya está configurada en SampleScene (agarre + luz + FlashlightToggle)
        /// y la guarda como prefab. Si no la encuentra, monta una nueva.
        /// </summary>
        static GameObject GetOrCreateFlashlightPrefab()
        {
            GameObject result = null;
            if (File.Exists(SampleScenePath))
            {
                Scene sample = SceneManager.GetSceneByPath(SampleScenePath);
                bool openedHere = false;
                if (!sample.isLoaded)
                {
                    sample = EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Additive);
                    openedHere = true;
                }

                GameObject source = sample.GetRootGameObjects()
                    .SelectMany(r => r.GetComponentsInChildren<XRGrabInteractable>(true))
                    .Select(g => g.gameObject)
                    .FirstOrDefault(g => g.name.Contains("Flashlight"));
                if (source)
                {
                    GameObject copy = Object.Instantiate(source);
                    copy.name = "Linterna_VR";
                    copy.transform.position = Vector3.zero;
                    // Quita restos del tutorial de la plantilla VR.
                    foreach (Transform child in copy.GetComponentsInChildren<Transform>(true))
                        if (child && child != copy.transform && child.name.Contains("Step Indicator"))
                            Object.DestroyImmediate(child.gameObject);
                    // Quita componentes cuyo script ya no existe (p. ej. datos de luz de HDRP);
                    // Unity no permite guardar un prefab con scripts que faltan.
                    int removed = 0;
                    foreach (Transform child in copy.GetComponentsInChildren<Transform>(true))
                        removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(child.gameObject);
                    if (removed > 0)
                        Debug.Log($"[Casa de terror] Quitados {removed} componente(s) con script perdido de la linterna.");
                    result = PrefabUtility.SaveAsPrefabAsset(copy, FlashlightOutPath);
                    Object.DestroyImmediate(copy);
                    Debug.Log("[Casa de terror] Linterna copiada de SampleScene a " + FlashlightOutPath);
                }
                if (openedHere) EditorSceneManager.CloseScene(sample, true);
            }

            if (result) return result;
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(FlashlightOutPath);
            return existing ? existing : BuildFallbackFlashlight();
        }

        static GameObject BuildFallbackFlashlight()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(FlashlightModelPath);
            if (!model) return null;

            var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
            go.name = "Linterna_VR";
            Bounds lb = BoundsUtil.LocalBounds(go.transform);
            var box = go.AddComponent<BoxCollider>();
            box.center = lb.center;
            box.size = lb.size;
            go.AddComponent<Rigidbody>().mass = 0.4f;
            var grab = go.AddComponent<XRGrabInteractable>();
            grab.useDynamicAttach = true;

            // Luz en el extremo del eje más largo.
            Vector3 size = lb.size;
            int axis = size.x >= size.y && size.x >= size.z ? 0 : (size.y >= size.z ? 1 : 2);
            Vector3 dir = Vector3.zero;
            dir[axis] = 1f;
            var lightGo = new GameObject("Spot Light");
            lightGo.transform.SetParent(go.transform, false);
            lightGo.transform.localPosition = lb.center + Vector3.Scale(dir, lb.extents);
            lightGo.transform.localRotation = Quaternion.LookRotation(dir);
            var spot = lightGo.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.range = 15f;
            spot.spotAngle = 45f;
            spot.intensity = 3f;
            spot.color = new Color(1f, 0.95f, 0.85f);

            var toggle = go.AddComponent<FlashlightToggle>();
            var so = new SerializedObject(toggle);
            so.FindProperty("spotLight").objectReferenceValue = spot;
            so.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddVoidPersistentListener(grab.activated, toggle.Toggle);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, FlashlightOutPath);
            Object.DestroyImmediate(go);
            Debug.LogWarning("[Casa de terror] No encontré la linterna de SampleScene; he creado una nueva. " +
                             "Si la luz apunta hacia atrás, gira el hijo 'Spot Light' 180º en el prefab " + FlashlightOutPath);
            return prefab;
        }

        // ------------------------------------------------------------------ Utilidades

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static void AddSceneToBuildSettings(string path)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == path)) return;
            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ================================================================== Constructor de la casa

        enum Side { South, North, West, East }
        enum Segment { Plain, Window, Door }

        class RoomDef
        {
            public string name;
            public RectInt cells;          // en celdas de la cuadrícula
            public Rect area;              // en metros
            public Transform root;
            public readonly Dictionary<(Side, int), Segment> segments = new Dictionary<(Side, int), Segment>();
        }

        class DoorDef
        {
            public int a, b;
            public bool alongX;            // la pared va a lo largo del eje X
            public Vector2Int cell;        // inicio del segmento en celdas
            public bool big;
            public readonly List<GameObject> walls = new List<GameObject>();
            public Vector3 center;
            public Vector3 axis;
        }

        class Builder
        {
            readonly GameObject flashlightPrefab;
            readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();
            readonly List<RoomDef> rooms = new List<RoomDef>();
            readonly List<DoorDef> doors = new List<DoorDef>();
            readonly List<Bounds> colliderBoxes = new List<Bounds>();
            readonly List<GameObject> staticRoots = new List<GameObject>();
            Transform house, collisionRoot;
            float L, H, T; // largo de un módulo de pared, altura y grosor
            int windowCounter;
            public string Summary = "";

            public Builder(GameObject flashlightPrefab)
            {
                this.flashlightPrefab = flashlightPrefab;
            }

            GameObject P(string name)
            {
                if (prefabs.TryGetValue(name, out var cached)) return cached;
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(PiecesFolder + name + ".prefab");
                if (!go) Debug.LogWarning("[Casa de terror] No encuentro el prefab " + PiecesFolder + name + ".prefab");
                prefabs[name] = go;
                return go;
            }

            public void Build()
            {
                house = new GameObject("Casa").transform;
                collisionRoot = new GameObject("Colisiones").transform;
                collisionRoot.SetParent(house, false);

                MeasureModules();
                DefineLayout();
                foreach (var room in rooms) BuildRoomShell(room);
                foreach (var door in doors) ResolveDoor(door);
                Furnish();
                BuildLamps();
                BuildEnvironment();
                GameObject player = BuildPlayer();
                BuildEnemies();
                BuildCollisions();
                BuildHouseLayout();

                foreach (var root in staticRoots) MarkStatic(root);
                Summary = $"Módulo de pared: {L:0.00} m x {H:0.00} m (grosor {T:0.00} m). " +
                          $"{rooms.Count} espacios, {doors.Count} puertas." + (player ? "" : " ¡Falta el XR Origin!");
            }

            // ---------------------------------------------------------- Medidas

            void MeasureModules()
            {
                var probe = Spawn(P("wall_default 1"), null);
                Bounds b = WorldBounds(probe);
                Object.DestroyImmediate(probe);
                if (b.size.z > b.size.x)
                    Debug.LogWarning("[Casa de terror] La pared parece orientada en Z; puede que las paredes salgan giradas.");
                L = Mathf.Max(b.size.x, 0.5f);
                H = Mathf.Max(b.size.y, 2f);
                T = Mathf.Clamp(Mathf.Min(b.size.x, b.size.z), 0.02f, 0.5f);
            }

            // ---------------------------------------------------------- Distribución

            void DefineLayout()
            {
                // Cuadrícula en celdas de L x L metros. X = este, Z = norte.
                AddRoom("Recibidor", new RectInt(2, -2, 3, 2));   // 0
                AddRoom("Pasillo", new RectInt(3, 0, 1, 10));     // 1
                AddRoom("Salon", new RectInt(0, 0, 3, 4));        // 2
                AddRoom("Biblioteca", new RectInt(0, 4, 3, 3));   // 3
                AddRoom("Dormitorio", new RectInt(0, 7, 3, 3));   // 4
                AddRoom("Comedor", new RectInt(4, 0, 3, 3));      // 5
                AddRoom("SalaMusica", new RectInt(4, 3, 3, 3));   // 6
                AddRoom("Estudio", new RectInt(4, 6, 3, 4));      // 7

                AddDoor(0, 1, true, 3, 0, true);   // Recibidor - Pasillo
                AddDoor(2, 1, false, 3, 1, true);  // Salón - Pasillo
                AddDoor(3, 1, false, 3, 5, true);  // Biblioteca - Pasillo
                AddDoor(4, 1, false, 3, 8, true);  // Dormitorio - Pasillo
                AddDoor(1, 5, false, 4, 1, true);  // Pasillo - Comedor
                AddDoor(1, 6, false, 4, 4, true);  // Pasillo - Sala de música
                AddDoor(1, 7, false, 4, 8, true);  // Pasillo - Estudio
                AddDoor(2, 3, true, 1, 4, false);  // Salón - Biblioteca
                AddDoor(5, 6, true, 5, 3, false);  // Comedor - Sala de música
            }

            void AddRoom(string name, RectInt cells)
            {
                var room = new RoomDef
                {
                    name = name,
                    cells = cells,
                    area = new Rect(cells.x * L, cells.y * L, cells.width * L, cells.height * L)
                };
                room.root = new GameObject(name).transform;
                room.root.SetParent(house, false);
                staticRoots.Add(room.root.gameObject);
                rooms.Add(room);
            }

            void AddDoor(int a, int b, bool alongX, int x, int z, bool big)
            {
                var door = new DoorDef { a = a, b = b, alongX = alongX, cell = new Vector2Int(x, z), big = big };
                RectInt rb = rooms[b].cells;
                door.axis = alongX
                    ? (rb.yMin >= z ? Vector3.forward : Vector3.back)
                    : (rb.xMin >= x ? Vector3.right : Vector3.left);
                doors.Add(door);
            }

            bool CellInsideHouse(int x, int z) => rooms.Any(r => r.cells.Contains(new Vector2Int(x, z)));

            // ---------------------------------------------------------- Suelos, techos y paredes

            void BuildRoomShell(RoomDef room)
            {
                var floors = new GameObject("Suelo").transform;
                floors.SetParent(room.root, false);
                var ceilings = new GameObject("Techo").transform;
                ceilings.SetParent(room.root, false);
                var walls = new GameObject("Paredes").transform;
                walls.SetParent(room.root, false);

                for (int x = room.cells.xMin; x < room.cells.xMax; x++)
                for (int z = room.cells.yMin; z < room.cells.yMax; z++)
                {
                    var cellCenter = new Vector3((x + 0.5f) * L, 0f, (z + 0.5f) * L);
                    PlaceTile(P("floor_big"), floors, cellCenter, null, 0f);
                    PlaceTile(P("ceiling_big"), ceilings, cellCenter, H - 0.01f, null);
                }

                // Paredes del perímetro: cada habitación pone las suyas mirando hacia dentro.
                RectInt c = room.cells;
                for (int i = 0; i < c.width; i++)
                {
                    BuildSegment(room, walls, Side.South, i, true, c.xMin + i, c.yMin, +1, c.xMin + i, c.yMin - 1);
                    BuildSegment(room, walls, Side.North, i, true, c.xMin + i, c.yMax, -1, c.xMin + i, c.yMax);
                }
                for (int i = 0; i < c.height; i++)
                {
                    BuildSegment(room, walls, Side.West, i, false, c.xMin, c.yMin + i, +1, c.xMin - 1, c.yMin + i);
                    BuildSegment(room, walls, Side.East, i, false, c.xMax, c.yMin + i, -1, c.xMax, c.yMin + i);
                }
            }

            void BuildSegment(RoomDef room, Transform parent, Side side, int index, bool alongX,
                              int x, int z, int facing, int outsideX, int outsideZ)
            {
                Vector3 center = alongX ? new Vector3((x + 0.5f) * L, 0f, z * L) : new Vector3(x * L, 0f, (z + 0.5f) * L);
                DoorDef door = doors.FirstOrDefault(d => d.alongX == alongX && d.cell.x == x && d.cell.y == z);
                if (door != null)
                {
                    room.segments[(side, index)] = Segment.Door;
                    var prefab = P(door.big ? "wall_with_big_door" : "wall_with_small_door");
                    door.walls.Add(PlaceWall(prefab, parent, center, alongX, facing, L));
                    return;
                }

                bool exterior = !CellInsideHouse(outsideX, outsideZ);
                bool window = exterior && index % 2 == 1 && room.name != "Recibidor";
                room.segments[(side, index)] = window ? Segment.Window : Segment.Plain;
                string prefabName = window
                    ? (windowCounter++ % 2 == 0 ? "wall_with_one_window" : "wall_with_two_window")
                    : "wall_default 1";
                GameObject wall = PlaceWall(P(prefabName), parent, center, alongX, facing, L);
                colliderBoxes.Add(WorldBounds(wall));
            }

            /// <summary>
            /// Coloca una pieza de pared centrada en el segmento, con su cara decorada hacia 'facing'
            /// (+1 = hacia +Z o +X). Se usa un objeto contenedor para escalar/espejar sin tocar el prefab.
            /// </summary>
            GameObject PlaceWall(GameObject prefab, Transform parent, Vector3 lineCenter, bool alongX, int facing,
                                 float length, float minY = 0f, float heightScale = 1f)
            {
                GameObject holder = Spawn(prefab, parent);
                Bounds native = WorldBounds(holder);
                float nativeLength = Mathf.Max(native.size.x, 0.01f);
                holder.transform.localScale = new Vector3(length / nativeLength, heightScale, facing);
                holder.transform.rotation = Quaternion.Euler(0f, alongX ? 0f : 90f, 0f);
                Bounds b = WorldBounds(holder);
                float thickness = alongX ? b.size.z : b.size.x;
                Vector3 inward = (alongX ? Vector3.forward : Vector3.right) * facing;
                Align(holder, lineCenter + inward * (thickness * 0.5f), minY, null);
                return holder;
            }

            void PlaceTile(GameObject prefab, Transform parent, Vector3 cellCenter, float? minY, float? maxY)
            {
                GameObject holder = Spawn(prefab, parent);
                Bounds native = WorldBounds(holder);
                holder.transform.localScale = new Vector3(L / Mathf.Max(native.size.x, 0.01f), 1f,
                                                          L / Mathf.Max(native.size.z, 0.01f));
                Align(holder, cellCenter, minY, maxY);
            }

            /// <summary>
            /// Comprueba con rayos dónde está el hueco de la puerta. Si el modelo trae la puerta cerrada
            /// (no hay hueco), lo sustituye por una abertura construida con trozos de pared.
            /// </summary>
            void ResolveDoor(DoorDef door)
            {
                Vector3 along = door.alongX ? Vector3.right : Vector3.forward;
                Vector3 across = door.alongX ? Vector3.forward : Vector3.right;
                Vector3 segStart = new Vector3(door.cell.x * L, 0f, door.cell.y * L);

                var temp = new List<Collider>();
                foreach (var wall in door.walls)
                foreach (var mf in wall.GetComponentsInChildren<MeshFilter>())
                {
                    var mc = mf.gameObject.AddComponent<MeshCollider>();
                    mc.sharedMesh = mf.sharedMesh;
                    temp.Add(mc);
                }
                Physics.SyncTransforms();

                const int samples = 60;
                int bestStart = -1, bestLen = 0, runStart = -1;
                for (int i = 3; i <= samples - 3; i++)
                {
                    Vector3 p = segStart + along * (L * i / samples);
                    bool open = !BlockedAt(p + Vector3.up * 1.0f, across, temp) &&
                                !BlockedAt(p + Vector3.up * 1.8f, across, temp);
                    if (open && runStart < 0) runStart = i;
                    if ((!open || i == samples - 3) && runStart >= 0)
                    {
                        int end = open ? i : i - 1;
                        if (end - runStart + 1 > bestLen) { bestLen = end - runStart + 1; bestStart = runStart; }
                        runStart = -1;
                    }
                }
                foreach (var col in temp) Object.DestroyImmediate(col);

                float gapStart, gapEnd;
                float gapWidth = bestLen * L / samples;
                if (bestLen > 0 && gapWidth >= 0.7f)
                {
                    gapStart = L * bestStart / samples;
                    gapEnd = gapStart + gapWidth;
                }
                else
                {
                    // El modelo no deja pasar: construimos nuestra propia abertura.
                    Debug.Log($"[Casa de terror] La pared con puerta {rooms[door.a].name}-{rooms[door.b].name} no tiene hueco; creo una abertura.");
                    const float width = 1.1f;
                    gapStart = (L - width) * 0.5f;
                    gapEnd = gapStart + width;
                    float doorHeight = Mathf.Min(2.2f, H - 0.2f);
                    float jamb = gapStart;
                    var plain = P("wall_default 1");
                    var parents = door.walls.Select(w => w.transform.parent).ToList();
                    foreach (var w in door.walls) Object.DestroyImmediate(w);
                    door.walls.Clear();
                    for (int s = 0; s < 2; s++)
                    {
                        int facing = s == 0 ? 1 : -1;
                        Transform parent = parents.Count > s ? parents[s] : house;
                        Vector3 line = segStart + along * (jamb * 0.5f);
                        PlaceWall(plain, parent, line, door.alongX, facing, jamb);
                        line = segStart + along * (L - jamb * 0.5f);
                        PlaceWall(plain, parent, line, door.alongX, facing, jamb);
                        line = segStart + along * (L * 0.5f);
                        PlaceWall(plain, parent, line, door.alongX, facing, width, doorHeight, (H - doorHeight) / H);
                    }
                }

                door.center = segStart + along * ((gapStart + gapEnd) * 0.5f);

                // Colisiones: dos jambas a los lados del hueco (las paredes a ambos lados del segmento).
                float thick = T * 2f + 0.02f;
                AddSegmentBox(segStart, along, across, 0f, gapStart, thick);
                AddSegmentBox(segStart, along, across, gapEnd, L, thick);
            }

            static bool BlockedAt(Vector3 point, Vector3 across, List<Collider> colliders)
            {
                var ray = new Ray(point - across * 1.0f, across);
                foreach (var col in colliders)
                    if (col.Raycast(ray, out _, 2.0f)) return true;
                return false;
            }

            void AddSegmentBox(Vector3 segStart, Vector3 along, Vector3 across, float from, float to, float thickness)
            {
                if (to - from < 0.01f) return;
                Vector3 center = segStart + along * ((from + to) * 0.5f) + Vector3.up * (H * 0.5f);
                Vector3 size = along * (to - from) + across * thickness + Vector3.up * H;
                colliderBoxes.Add(new Bounds(center, size));
            }

            // ---------------------------------------------------------- Muebles

            void Furnish()
            {
                // Recibidor: punto de partida, con la linterna sobre una mesita.
                RoomDef r = rooms[0];
                PlaceFloor(r, "carpet_1", 0.5f, 0.5f, 0f, solid: false);
                GameObject table = PlaceFloor(r, "tea_table 1", 0.25f, 0.45f, 0f);
                PlaceOnWall(r, Side.West, 0.5f, "clock_wall", 1.9f);
                PlaceOnWall(r, Side.East, 0.5f, "painting_5", 1.6f);
                PlaceFloor(r, "light_tall", 0.9f, 0.2f, 0f);
                PlaceFloor(r, "vase_2", 0.1f, 0.15f, 0f);
                PlaceItemOnTop(flashlightPrefab, table, "Linterna");

                // Pasillo: cuadros torcidos y alfombras.
                r = rooms[1];
                for (int i = 0; i < 4; i++)
                    PlaceFloor(r, "carpet_2", 0.5f, 0.12f + i * 0.25f, 90f, solid: false);
                string[] paintings = { "painting_1", "painting_2", "painting_3", "painting_4", "painting_6", "painting_5" };
                for (int i = 0; i < 6; i++)
                    PlaceOnWall(r, i % 2 == 0 ? Side.West : Side.East, 0.08f + i * 0.16f, paintings[i], 1.5f + (i % 3) * 0.1f, tilt: i % 2 == 0 ? 8f : -6f);
                PlaceOnWall(r, Side.North, 0.5f, "clock_wall", 2.0f);

                // Salón: sofás, tele y la pistola sobre la mesa.
                r = rooms[2];
                PlaceFloor(r, "carpet_2", 0.5f, 0.55f, 0f, solid: false);
                PlaceAgainstWall(r, Side.West, 0.6f, "sofa_big", allowWindow: true);
                PlaceAgainstWall(r, Side.South, 0.35f, "sofa_small", allowWindow: true);
                GameObject salonTable = PlaceFloor(r, "tea_table 1", 0.45f, 0.55f, 0f);
                GameObject tvDesk = PlaceAgainstWall(r, Side.East, 0.85f, "tv_desk");
                PlaceOnTop(tvDesk, "tv", 0f);
                PlaceOnWall(r, Side.West, 0.85f, "painting_1", 1.7f);
                PlaceOnTop(salonTable, "ceramic_teapot", 0.7f);
                PlaceFloor(r, "light_tall", 0.1f, 0.08f, 0f);
                PlaceItemOnTop(AssetDatabase.LoadAssetAtPath<GameObject>(PistolPrefabPath), salonTable, "Pistola", isPistol: true);

                // Biblioteca: estanterías y libros tirados por el suelo.
                r = rooms[3];
                PlaceFloor(r, "carpet_3", 0.5f, 0.5f, 0f, solid: false);
                foreach (float t in new[] { 0.17f, 0.5f, 0.83f }) PlaceAgainstWall(r, Side.North, t, "bookshelf");
                PlaceOnWall(r, Side.East, 0.15f, "painting_2", 1.6f);
                PlaceFloor(r, "light_tall", 0.08f, 0.1f, 0f);
                for (int i = 1; i <= 9; i++)
                    PlaceFloor(r, "book_" + i, Random.Range(0.25f, 0.75f), Random.Range(0.45f, 0.8f), Random.Range(0f, 360f), solid: false);

                // Dormitorio: cortinas y una pared llena de fotos.
                r = rooms[4];
                PlaceFloor(r, "carpet_1", 0.5f, 0.5f, 0f, solid: false);
                PlaceAgainstWall(r, Side.North, 0.75f, "sofa_small", allowWindow: true);
                PlaceCurtains(r);
                for (int i = 1; i <= 5; i++)
                    PlaceOnWall(r, Side.South, 0.2f + i * 0.1f, "photo_" + i, 1.3f + (i % 2) * 0.3f, tilt: (i % 3 - 1) * 10f, anySegment: true);

                // Comedor.
                r = rooms[5];
                PlaceFloor(r, "carpet_2", 0.5f, 0.5f, 0f, solid: false);
                GameObject dining = PlaceFloor(r, "tea_table 1", 0.5f, 0.5f, 0f);
                PlaceOnTop(dining, "vase_1", 0f);
                PlaceOnTop(dining, "ceramic_tea_glass", 0.2f);
                PlaceFloor(r, "sofa_small", 0.5f, 0.2f, 0f);
                PlaceFloor(r, "sofa_small", 0.5f, 0.8f, 180f);
                PlaceOnWall(r, Side.East, 0.15f, "painting_3", 1.6f);
                PlaceOnWall(r, Side.South, 0.15f, "clock_wall", 2.0f);

                // Sala de música: gramófono. Aquí empieza el payaso.
                r = rooms[6];
                PlaceFloor(r, "carpet_3", 0.5f, 0.5f, 0f, solid: false);
                GameObject musicTable = PlaceAgainstWall(r, Side.East, 0.5f, "tea_table 1", allowWindow: true);
                PlaceOnTop(musicTable, "gramophone", 0f);
                PlaceOnWall(r, Side.North, 0.5f, "painting_4", 1.7f);
                PlaceFloor(r, "sofa_small", 0.3f, 0.5f, 90f);
                PlaceFloor(r, "phone", 0.15f, 0.85f, 30f, solid: false);

                // Estudio.
                r = rooms[7];
                PlaceFloor(r, "carpet_3", 0.5f, 0.5f, 0f, solid: false);
                GameObject studyDesk = PlaceAgainstWall(r, Side.East, 0.8f, "tv_desk");
                PlaceOnTop(studyDesk, "tv", 0f);
                PlaceFloor(r, "sofa_big", 0.35f, 0.75f, 90f);
                GameObject side = PlaceAgainstWall(r, Side.North, 0.2f, "tv_desk");
                PlaceOnTop(side, "light_desk", -0.25f);
                PlaceOnTop(side, "photo_frame_desk", 0.2f);
                PlaceOnWall(r, Side.West, 0.5f, "painting_6", 1.6f);
                PlaceFloor(r, "light_tall", 0.9f, 0.1f, 0f);
                PlaceFloor(r, "vase_3", 0.1f, 0.92f, 0f);
            }

            Rect Interior(RoomDef room)
            {
                Rect a = room.area;
                return new Rect(a.xMin + T, a.yMin + T, a.width - 2f * T, a.height - 2f * T);
            }

            GameObject PlaceFloor(RoomDef room, string prefabName, float u, float v, float yaw, bool solid = true)
            {
                var prefab = P(prefabName);
                if (!prefab) return null;
                Rect inside = Interior(room);
                GameObject holder = Spawn(prefab, room.root);
                holder.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                Bounds b = WorldBounds(holder);
                float x = Mathf.Clamp(Mathf.Lerp(inside.xMin, inside.xMax, u), inside.xMin + b.extents.x, inside.xMax - b.extents.x);
                float z = Mathf.Clamp(Mathf.Lerp(inside.yMin, inside.yMax, v), inside.yMin + b.extents.z, inside.yMax - b.extents.z);
                Align(holder, new Vector3(x, 0f, z), 0.005f, null);
                if (solid) AddFurnitureCollider(holder);
                return holder;
            }

            static float YawFacingInto(Side side)
            {
                switch (side)
                {
                    case Side.South: return 0f;    // la pared sur mira al norte (+Z)
                    case Side.North: return 180f;
                    case Side.West: return 90f;
                    default: return -90f;
                }
            }

            /// <summary>Busca el segmento de pared más cercano a 't' que no sea puerta (ni ventana, si no se permite).</summary>
            float FindFreeSpot(RoomDef room, Side side, float t, bool allowWindow, bool anySegment)
            {
                int count = side == Side.South || side == Side.North ? room.cells.width : room.cells.height;
                int wanted = Mathf.Clamp(Mathf.FloorToInt(t * count), 0, count - 1);
                for (int offset = 0; offset < count; offset++)
                foreach (int i in new[] { wanted - offset, wanted + offset })
                {
                    if (i < 0 || i >= count) continue;
                    room.segments.TryGetValue((side, i), out Segment seg);
                    if (seg == Segment.Door) continue;
                    if (seg == Segment.Window && !allowWindow) continue;
                    if (i == wanted) return anySegment ? t : (i + 0.5f) / count;
                    return (i + 0.5f) / count;
                }
                return -1f;
            }

            Vector3 WallPoint(RoomDef room, Side side, float t, out Vector3 inward)
            {
                Rect inside = Interior(room);
                switch (side)
                {
                    case Side.South: inward = Vector3.forward; return new Vector3(Mathf.Lerp(inside.xMin, inside.xMax, t), 0f, inside.yMin);
                    case Side.North: inward = Vector3.back; return new Vector3(Mathf.Lerp(inside.xMin, inside.xMax, t), 0f, inside.yMax);
                    case Side.West: inward = Vector3.right; return new Vector3(inside.xMin, 0f, Mathf.Lerp(inside.yMin, inside.yMax, t));
                    default: inward = Vector3.left; return new Vector3(inside.xMax, 0f, Mathf.Lerp(inside.yMin, inside.yMax, t));
                }
            }

            GameObject PlaceAgainstWall(RoomDef room, Side side, float t, string prefabName, bool allowWindow = false)
            {
                var prefab = P(prefabName);
                if (!prefab) return null;
                float spot = FindFreeSpot(room, side, t, allowWindow, false);
                if (spot < 0f) return null;
                GameObject holder = Spawn(prefab, room.root);
                holder.transform.rotation = Quaternion.Euler(0f, YawFacingInto(side), 0f);
                Bounds b = WorldBounds(holder);
                Vector3 p = WallPoint(room, side, spot, out Vector3 inward);
                float depth = Mathf.Abs(Vector3.Dot(b.size, inward));
                Align(holder, p + inward * (depth * 0.5f + 0.03f), 0.005f, null);
                AddFurnitureCollider(holder);
                return holder;
            }

            void PlaceOnWall(RoomDef room, Side side, float t, string prefabName, float centerHeight,
                             float tilt = 0f, bool anySegment = false)
            {
                var prefab = P(prefabName);
                if (!prefab) return;
                float spot = FindFreeSpot(room, side, t, false, anySegment);
                if (spot < 0f) return;
                GameObject holder = Spawn(prefab, room.root);
                holder.transform.rotation = Quaternion.Euler(0f, YawFacingInto(side), tilt);
                Bounds b = WorldBounds(holder);
                Vector3 p = WallPoint(room, side, spot, out Vector3 inward);
                float depth = Mathf.Abs(Vector3.Dot(b.size, inward));
                Vector3 target = p + inward * (depth * 0.5f + 0.01f);
                target.y = centerHeight;
                Align(holder, target, null, null);
            }

            void PlaceCurtains(RoomDef room)
            {
                int n = 0;
                foreach (var kv in room.segments.Where(s => s.Value == Segment.Window).ToList())
                {
                    Side side = kv.Key.Item1;
                    int count = side == Side.South || side == Side.North ? room.cells.width : room.cells.height;
                    float t = (kv.Key.Item2 + 0.5f) / count;
                    var prefab = P(n++ % 2 == 0 ? "curtain_1" : "curtain_2");
                    if (!prefab) continue;
                    GameObject holder = Spawn(prefab, room.root);
                    holder.transform.rotation = Quaternion.Euler(0f, YawFacingInto(side), 0f);
                    Bounds b = WorldBounds(holder);
                    Vector3 p = WallPoint(room, side, t, out Vector3 inward);
                    float depth = Mathf.Abs(Vector3.Dot(b.size, inward));
                    Align(holder, p + inward * (depth * 0.5f + 0.05f), Mathf.Max(0.02f, H - 0.15f - b.size.y), null);
                }
            }

            GameObject PlaceOnTop(GameObject baseHolder, string prefabName, float offset)
            {
                var prefab = P(prefabName);
                if (!prefab || !baseHolder) return null;
                Bounds top = WorldBounds(baseHolder);
                GameObject holder = Spawn(prefab, baseHolder.transform.parent);
                holder.transform.rotation = baseHolder.transform.rotation;
                Vector3 c = top.center + baseHolder.transform.right * (offset * top.extents.magnitude * 0.5f);
                Align(holder, new Vector3(c.x, 0f, c.z), top.max.y + 0.002f, null);
                return holder;
            }

            /// <summary>Objetos que se pueden coger (linterna, pistola): sin contenedor, encima de un mueble.</summary>
            void PlaceItemOnTop(GameObject prefab, GameObject baseHolder, string label, bool isPistol = false)
            {
                if (!prefab || !baseHolder)
                {
                    Debug.LogWarning("[Casa de terror] No se pudo colocar: " + label);
                    return;
                }
                Bounds top = WorldBounds(baseHolder);
                var item = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                item.name = label;

                if (isPistol) SetupPistol(item);

                Align(item, new Vector3(top.center.x, 0f, top.center.z), top.max.y + 0.01f, null);

                // Contorno amarillo brillante para encontrarlo a oscuras.
                Material outline = GetOutlineMaterial();
                if (outline)
                {
                    var glow = item.GetComponent<GlowOutline>() ? item.GetComponent<GlowOutline>() : item.AddComponent<GlowOutline>();
                    glow.outlineMaterial = outline;
                }
            }

            static Material GetOutlineMaterial()
            {
                const string path = OutputFolder + "/Materials/Contorno.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat) return mat;
                Shader shader = Shader.Find("SgiVr/Outline");
                if (!shader)
                {
                    Debug.LogWarning("[Casa de terror] No encuentro el shader SgiVr/Outline (Assets/Horror/Shaders/Outline.shader); los objetos irán sin contorno.");
                    return null;
                }
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
                return mat;
            }

            void SetupPistol(GameObject pistol)
            {
                // Tamaño razonable de pistola (~22 cm).
                Bounds wb = WorldBounds(pistol);
                float longest = Mathf.Max(wb.size.x, wb.size.y, wb.size.z);
                if (longest > 0.0001f && (longest < 0.12f || longest > 0.4f))
                    pistol.transform.localScale *= 0.22f / longest;

                Bounds lb = BoundsUtil.LocalBounds(pistol.transform);
                if (!pistol.GetComponent<Collider>())
                {
                    var box = pistol.AddComponent<BoxCollider>();
                    box.center = lb.center;
                    box.size = lb.size;
                }
                var rb = pistol.GetComponent<Rigidbody>() ? pistol.GetComponent<Rigidbody>() : pistol.AddComponent<Rigidbody>();
                rb.mass = 1f;
                var grab = pistol.GetComponent<XRGrabInteractable>() ? pistol.GetComponent<XRGrabInteractable>() : pistol.AddComponent<XRGrabInteractable>();
                grab.useDynamicAttach = true;
                var gun = pistol.AddComponent<PistolGun>();
                gun.laserMaterial = CreateUnlitMaterial(OutputFolder + "/Materials/Laser.mat", new Color(1f, 0.1f, 0.1f, 1f), false);
            }

            void AddFurnitureCollider(GameObject holder)
            {
                if (!holder) return;
                Bounds b = WorldBounds(holder);
                b.Expand(new Vector3(-0.04f, 0f, -0.04f));
                colliderBoxes.Add(b);
            }

            // ---------------------------------------------------------- Luces y ambiente

            /// <summary>Solo las lámparas de techo como decoración: están apagadas, no emiten luz.</summary>
            void BuildLamps()
            {
                var lampPrefab = P("antique_light");
                if (!lampPrefab) return;
                foreach (RoomDef room in rooms)
                {
                    var centers = new List<Vector3>();
                    if (room.name == "Pasillo")
                        for (int k = 0; k < 3; k++)
                            centers.Add(new Vector3(room.area.center.x, 0f, Mathf.Lerp(room.area.yMin, room.area.yMax, (k + 0.5f) / 3f)));
                    else
                        centers.Add(new Vector3(room.area.center.x, 0f, room.area.center.y));

                    foreach (Vector3 c in centers)
                    {
                        GameObject lamp = Spawn(lampPrefab, room.root);
                        Align(lamp, c, null, H - 0.01f);
                    }
                }
            }

            /// <summary>
            /// Oscuridad total: sin luces en la escena, sin luz ambiental ni reflejos.
            /// La única luz es la de la linterna (y el fogonazo de la pistola al disparar).
            /// </summary>
            void BuildEnvironment()
            {
                RenderSettings.skybox = null;
                RenderSettings.sun = null;
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = Color.black;
                RenderSettings.ambientIntensity = 0f;
                RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
                RenderSettings.customReflectionTexture = null;
                RenderSettings.reflectionIntensity = 0f;
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.Exponential;
                RenderSettings.fogDensity = 0.07f;
                RenderSettings.fogColor = Color.black;
            }

            // ---------------------------------------------------------- Jugador y enemigos

            GameObject BuildPlayer()
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(XROriginPath);
                if (!prefab)
                {
                    Debug.LogError("[Casa de terror] No encuentro el XR Origin en " + XROriginPath);
                    return null;
                }
                var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                Rect start = rooms[0].area;
                player.transform.position = new Vector3(start.center.x, 0f, start.yMin + start.height * 0.3f);
                player.transform.rotation = Quaternion.identity; // mirando al norte, hacia el pasillo

                Camera cam = player.GetComponentInChildren<Camera>(true);
                if (!cam) return player;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;

                var overlay = GameObject.CreatePrimitive(PrimitiveType.Quad);
                overlay.name = "Overlay Daño";
                Object.DestroyImmediate(overlay.GetComponent<Collider>());
                overlay.transform.SetParent(cam.transform, false);
                overlay.transform.localPosition = new Vector3(0f, 0f, 0.12f);
                overlay.transform.localScale = new Vector3(0.6f, 0.6f, 1f);
                var mr = overlay.GetComponent<MeshRenderer>();
                mr.sharedMaterial = CreateUnlitMaterial(OutputFolder + "/Materials/DamageOverlay.mat", new Color(0.5f, 0f, 0f, 0f), true);
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
                mr.enabled = false;

                var health = cam.gameObject.AddComponent<PlayerHealth>();
                health.damageOverlay = mr;
                return player;
            }

            void BuildEnemies()
            {
                var clownPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ClownPrefabPath);
                if (clownPrefab)
                {
                    GameObject clown = SpawnCharacter(clownPrefab, "Payaso", rooms[6], 0.6f, 0.5f, -90f, 2.1f);
                    clown.AddComponent<ClownEnemy>();
                }
                else Debug.LogWarning("[Casa de terror] No encuentro el payaso en " + ClownPrefabPath);
            }

            GameObject SpawnCharacter(GameObject prefab, string name, RoomDef room, float u, float v, float yaw, float wantedHeight)
            {
                var root = new GameObject(name);
                var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
                Bounds b = WorldBounds(root);
                if (b.size.y > 0.01f && (b.size.y < 1.4f || b.size.y > 2.8f))
                    model.transform.localScale *= wantedHeight / b.size.y;

                // Modelo con los pies en el origen del contenedor.
                b = WorldBounds(root);
                model.transform.position += new Vector3(-b.center.x, -b.min.y, -b.center.z);
                float height = Mathf.Max(b.size.y, 1f);

                var cc = root.AddComponent<CharacterController>();
                cc.height = height;
                cc.radius = 0.3f;
                cc.center = new Vector3(0f, height * 0.5f + 0.02f, 0f);

                Rect inside = Interior(room);
                root.transform.position = new Vector3(Mathf.Lerp(inside.xMin + 0.6f, inside.xMax - 0.6f, u), 0.02f,
                                                      Mathf.Lerp(inside.yMin + 0.6f, inside.yMax - 0.6f, v));
                root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                return root;
            }

            // ---------------------------------------------------------- Colisiones, teletransporte y mapa

            void BuildCollisions()
            {
                int i = 0;
                foreach (Bounds b in colliderBoxes)
                {
                    var go = new GameObject("Col_" + i++);
                    go.transform.SetParent(collisionRoot, false);
                    var box = go.AddComponent<BoxCollider>();
                    box.center = b.center;
                    box.size = b.size;
                }

                int teleportMask = InteractionLayerMask.GetMask("Teleport");
                foreach (RoomDef room in rooms)
                {
                    var floor = new GameObject("Suelo_" + room.name);
                    floor.transform.SetParent(collisionRoot, false);
                    var box = floor.AddComponent<BoxCollider>();
                    box.center = new Vector3(room.area.center.x, -0.1f, room.area.center.y);
                    box.size = new Vector3(room.area.width, 0.2f, room.area.height);
                    var area = floor.AddComponent<TeleportationArea>();
                    if (teleportMask != 0) area.interactionLayers = teleportMask;
                }
                staticRoots.Add(collisionRoot.gameObject);
            }

            void BuildHouseLayout()
            {
                var layout = house.gameObject.AddComponent<HouseLayout>();
                foreach (RoomDef room in rooms)
                    layout.rooms.Add(new HouseLayout.Room { name = room.name, area = room.area });
                foreach (DoorDef door in doors)
                    layout.doors.Add(new HouseLayout.Door { roomA = door.a, roomB = door.b, position = door.center, axis = door.axis });
            }

            // ---------------------------------------------------------- Ayudas generales

            static GameObject Spawn(GameObject prefab, Transform parent)
            {
                var holder = new GameObject(prefab ? prefab.name : "Falta");
                if (parent) holder.transform.SetParent(parent, false);
                if (prefab) PrefabUtility.InstantiatePrefab(prefab, holder.transform);
                return holder;
            }

            static Bounds WorldBounds(GameObject go)
            {
                var renderers = go.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
                Bounds b = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
                return b;
            }

            /// <summary>Mueve el objeto para que su caja quede centrada en 'center' (XZ) y apoyada en minY / colgada de maxY.</summary>
            static void Align(GameObject go, Vector3 center, float? minY, float? maxY)
            {
                Bounds b = WorldBounds(go);
                var delta = new Vector3(center.x - b.center.x, 0f, center.z - b.center.z);
                if (minY.HasValue) delta.y = minY.Value - b.min.y;
                else if (maxY.HasValue) delta.y = maxY.Value - b.max.y;
                else delta.y = center.y - b.center.y;
                go.transform.position += delta;
            }

            static void MarkStatic(GameObject root)
            {
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    GameObjectUtility.SetStaticEditorFlags(t.gameObject,
                        StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            }
        }
    }
}
