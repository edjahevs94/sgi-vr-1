# Contexto del proyecto sgi-vr-1

Este archivo resume una conversación previa con Claude en claude.ai. Responde siempre en **español**. El usuario (Edgard) está aprendiendo Git y Unity, así que explica los pasos con claridad y sin dar por sabidas cosas avanzadas.

## Entorno

- **Equipo:** MacBook Pro (macOS). El proyecto se creó originalmente en otro ordenador y se descargó en el Mac desde GitHub.
- **Unity:** 6000.5.0f1 (ver `ProjectSettings/ProjectVersion.txt`). Abrir siempre con esta versión desde Unity Hub.
- **Render pipeline:** URP. Configuración en `Assets/Settings/Project Configuration/` (`Quality URP Config.asset`, `Performance URP Config.asset`, `UniversalRenderPipelineGlobalSettings.asset`) y `Assets/URPDefaultResources/`.
- **VR:** XR Interaction Toolkit 3.5.1, XR Hands 1.8.1, OpenXR, Meta XR (carpeta `Assets/Oculus`). Basado en la plantilla VR de Unity (`Assets/VRTemplateAssets`). Plataforma objetivo: Meta Quest (Android).
- **Control de versiones:** Git + GitHub + **Git LFS** (instalado en el Mac con Homebrew).

## Historial de problemas resueltos

1. **Error "load asset path file may be corrupted" al abrir el proyecto en el Mac.** Causa: Git LFS no estaba instalado (`git: 'lfs' is not a git command`), así que se descargaron solo los punteros de LFS en lugar de los archivos reales. Solución: `brew install git-lfs`, `git lfs install`, `git lfs pull`.
2. **Todo se veía morado (magenta).** Causa: al abrir Unity con los punteros de LFS, Unity reescribió varios archivos de `ProjectSettings/` (entre ellos `GraphicsSettings.asset` y `QualitySettings.asset`) con valores por defecto sin URP. `git lfs ls-files` los mostraba con `-` en lugar de `*`. Solución: `git restore` de `ProjectSettings`, `Assets/XR`, `Assets/DefaultVolumeProfile.asset` y `Assets/Resources/DevAgentSettings.asset`, después `git lfs pull`, borrar `Library/` y reabrir. Verificado: ya no quedan archivos con `-`.
3. **Simulador VR.** Activado en *Project Settings → XR Interaction Toolkit → Use XR Interaction Simulator in scenes* (con *Instantiate In Editor Only* marcado). Ya funciona. Si aparece un error de OpenXR en el editor, desmarcar OpenXR solo en la pestaña de escritorio de *XR Plug-in Management*.

## Problema pendiente de configuración (recomendado arreglar)

El `.gitattributes` actual mete en LFS los archivos `.asset`, incluidos los de `ProjectSettings/`. Son archivos YAML de texto pequeños y deberían ir en Git normal; tenerlos en LFS es lo que causó el problema 2. Propuesta: limitar LFS a binarios grandes (`*.png`, `*.jpg`, `*.psd`, `*.exr`, `*.tga`, `*.fbx`, `*.wav`, `*.mp3`, etc.) y migrar los `.asset` fuera de LFS. Explicárselo al usuario y hacerlo con cuidado (commit previo, y coordinar con el otro ordenador).

## Assets disponibles (principales)

- **Vintage Living Room Game Pack** (`Assets/Scenes/ZNS3D/Vintage Living Room Game Pack/`): piezas modulares (`wall_default`, `wall_default_2`, `wall_corner`, `wall_corner_2`, `wall_corner_double`, `wall_with_big_door`, `wall_with_small_door`, `wall_with_one_window`, `wall_with_two_window`, `floor_big`, `floor_rectangle`, `floor_small`, `ceiling_big`, `ceiling_rectangle`, `ceiling_small`) y muebles/decoración (sofás, librería, libros, alfombras, cortinas, cuadros, fotos, lámparas, TV, gramófono, teléfono, reloj, jarrones, tetera). Incluye una demo con HDRI `moonlit_golf_4k.exr` y volume profiles.
- **Enemigos:** `Assets/ZombieMale_AAB/` (zombi; ojo, tiene una escena de test HDRP que no aplica) y `Assets/Clown monster/` (payaso con muchas animaciones: walk, run, attack, hit, death, rage, idle).
- **Objetos:** `Assets/Flashlight/` (linterna con cookies de luz), `Assets/4K 3D Weapons Mega Pack/Pistol 2/`.
- **Manos VR:** `Assets/PolyOne/Free VR Hands/`.

## Generador de la casa de terror (hecho el 27/10/2026)

El usuario eligió: **casa de terror**, **6-8 habitaciones**.

- Menú en Unity: **SGI VR > Generar casa de terror** → crea `Assets/Scenes/CasaTerror.unity` (y la añade a Build Settings).
- Generador: `Assets/Editor/HorrorHouseGenerator.cs`. Scripts de juego: `Assets/Scripts/Horror/` (namespace `SgiVr.Horror`):
  `HouseLayout` (habitaciones + puertas; los enemigos lo usan como "NavMesh" simple, no hay paquete AI Navigation),
  `ClownEnemy` (patrulla, persigue y ataca usando los estados del Animator del payaso),
  `PistolGun`, `PlayerHealth`. (El usuario pidió quitar el zombi y todas las luces; sus scripts se borraron.)
- Distribución (celdas de ~2,4 m = largo de `wall_default`): Recibidor (inicio, linterna), Pasillo central, Salón (pistola),
  Biblioteca, Dormitorio, Comedor, Sala de música (payaso), Estudio.
- Medidas reales de las piezas: el generador las mide con los bounds al generar. Paredes: largo en X, cara decorada en +Z local.
  Muebles: frente en +Z local. Las puertas se detectan con rayos; si el modelo no tiene hueco, se construye una abertura.
- Oscuridad total: sin luces, sin luz ambiental ni reflejos; la única luz es la linterna.
- Linterna y pistola llevan un contorno amarillo que late (`GlowOutline` + shader, grosor en píxeles de pantalla, `Assets/Horror/Shaders/Outline.shader`,
  material `Assets/Horror/Materials/Contorno.mat`); se oculta al cogerlas. Grosor/color/pulso editables en el Inspector.
- El usuario edita `CasaTerror.unity` a mano: **no regenerar la escena**; los cambios nuevos se hacen en los scripts de juego.
- HUD tradicional (lo pidió el usuario): objeto `HUD` guardado en la escena dentro de Main Camera (Canvas World Space +
  Images Filled + TextMeshPro) con `HorrorHUD`, que solo actualiza fillAmount y textos (en Awake pone ZTest Always).
  Se añade con el menú **SGI VR > Añadir HUD a la escena abierta** (`Assets/Editor/HorrorHUDBuilder.cs`, también lo
  llama el generador), que además pone `FlashlightBattery` en la linterna. Nada se crea por código al pulsar Play.
  Pila: solo gasta encendida, no se recarga.
- El payaso, al morir, congela la pose de `death` (el Animator de la demo encadena todas las animaciones),
  espera `reviveTime` (20 s) y se levanta con la vida completa.
- Linterna: se copia en cada generación la ya configurada en `SampleScene` a `Assets/Horror/Prefabs/Linterna_VR.prefab`,
  quitando antes los scripts perdidos (tenía un componente de luz HDRP roto que impedía guardar el prefab y hacía que se
  usara una linterna de repuesto sin AttachPoint → el agarre con G no la llevaba a la mano).
- Las puertas de los modelos no dejaban hueco al detectarlas con rayos, así que el generador construye sus propias aberturas.
- Materiales: la pistola usaba shader Standard → el generador la convierte a URP/Lit
  (menú **SGI VR > Convertir materiales de payaso, linterna y pistola a URP**). Los materiales del zombi se restauraron con git (no se usa).
- Pendiente de verificar en Unity: que se vea bien (orientación de paredes/muebles, dirección del cañón de la pistola,
  que el payaso mire hacia delante al andar). Si el láser sale hacia atrás: marcar "Invertir cañón" en `PistolGun`.

Recordar al usuario que, tras los cambios, debe hacer `git add .`, `git commit` y `git push`, y en el otro ordenador `git pull` y `git lfs pull`.
