using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

namespace KayipParca.EditorTools
{
    /// <summary>Kayip Parca gorselleri icin otomatik import ayarlari (pixel art).</summary>
    public class KayipParcaTextureImporter : AssetPostprocessor
    {
        static readonly string[] BlobCommon = { "lost_sad", "lost_happy", "yamali", "dragon_awake", "dragon_sleep", "chest_closed", "chest_glow" };
        void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').Contains("/KayipParca/Art/")) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            // Blob karakterler ve hikaye ekranlari 2x cozunurlukte (Tools/art_blob.py): 64 PPU, yumusak filtre.
            // Harita parcalari 32 px piksel sanati olarak kalir.
            string p = assetPath.Replace('\\', '/');
            bool blob = p.Contains("/Art/Player/") || p.Contains("/Art/Enemies/") || p.Contains("/Art/Story/")
                     || System.Array.Exists(BlobCommon, n => p.EndsWith("/Art/Common/" + n + ".png"));
            ti.spritePixelsPerUnit = blob ? 64 : 32;
            ti.filterMode = blob ? FilterMode.Bilinear : FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.wrapMode = TextureWrapMode.Clamp;
        }
    }

    /// <summary>Paket ilk kez eklendiginde 2D renderer'i ve sahneyi kendiliginden kurar.</summary>
    [InitializeOnLoad]
    static class KayipParcaAutoSetup
    {
        static KayipParcaAutoSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (Application.isPlaying || File.Exists("Assets/Scenes/KayipParca.unity")) return;
                if (SessionState.GetBool("KayipParcaAutoSetupDone", false)) return;
                SessionState.SetBool("KayipParcaAutoSetupDone", true);
                AssetDatabase.Refresh();
                KayipParcaSetup.Ensure2DRenderer();
                KayipParcaSetup.BuildScene();
            };
        }
    }

    public static class KayipParcaSetup
    {
        const string ArtPath = "Assets/Resources/KayipParca/Art";
        const string ScenePath = "Assets/Scenes/KayipParca.unity";

        [MenuItem("Kayıp Parça/1) Görsel Ayarlarını Uygula")]
        public static void ApplyImportSettings()
        {
            AssetDatabase.ImportAsset(ArtPath, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
            Debug.Log("[KayipParca] Görseller 32 PPU / Point filtre ile yeniden içe aktarıldı.");
        }


        const string Renderer2DPath = "Assets/Settings/KayipParca_Renderer2D.asset";

        [MenuItem("Kayıp Parça/3) 2D Işık Renderer'ını Kur")]
        public static void Ensure2DRenderer()
        {
            var data = AssetDatabase.LoadAssetAtPath<Renderer2DData>(Renderer2DPath);
            if (!data)
            {
                Directory.CreateDirectory("Assets/Settings");
                data = ScriptableObject.CreateInstance<Renderer2DData>();
                AssetDatabase.CreateAsset(data, Renderer2DPath);
                var dso = new SerializedObject(data);
                // post-process verisi (varsa)
                var getPP = typeof(PostProcessData).GetMethod("GetDefaultPostProcessData", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                var ppProp = dso.FindProperty("m_PostProcessData");
                if (getPP != null && ppProp != null) ppProp.objectReferenceValue = getPP.Invoke(null, null) as Object;
                // isik karisim stilleri bos geldiyse varsayilanlari doldur
                var styles = dso.FindProperty("m_LightBlendStyles");
                if (styles != null && styles.isArray && styles.arraySize == 0)
                {
                    string[] names = { "Multiply", "Additive", "Multiply with Mask", "Additive with Mask" };
                    int[] modes = { 1, 0, 1, 0 };
                    int[] chans = { 0, 0, 1, 1 };
                    styles.arraySize = 4;
                    for (int i = 0; i < 4; i++)
                    {
                        var e = styles.GetArrayElementAtIndex(i);
                        var n = e.FindPropertyRelative("name"); if (n != null) n.stringValue = names[i];
                        var b = e.FindPropertyRelative("blendMode"); if (b != null) b.enumValueIndex = modes[i];
                        var c = e.FindPropertyRelative("maskTextureChannel"); if (c != null) c.enumValueIndex = chans[i];
                    }
                }
                dso.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(data);
                AssetDatabase.SaveAssets();
            }

            var assets = new HashSet<UniversalRenderPipelineAsset>();
            if (GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset def) assets.Add(def);
            for (int i = 0; i < QualitySettings.names.Length; i++)
                if (QualitySettings.GetRenderPipelineAssetAt(i) is UniversalRenderPipelineAsset q) assets.Add(q);
            if (assets.Count == 0) { Debug.LogWarning("[KayipParca] URP asset bulunamadı; 2D ışıklar çalışmayacak."); return; }

            foreach (var a in assets)
            {
                var so = new SerializedObject(a);
                var list = so.FindProperty("m_RendererDataList");
                var defIdx = so.FindProperty("m_DefaultRendererIndex");
                if (list == null || defIdx == null) continue;
                int idx = -1;
                for (int i = 0; i < list.arraySize; i++)
                    if (list.GetArrayElementAtIndex(i).objectReferenceValue == data) idx = i;
                if (idx < 0)
                {
                    idx = list.arraySize;
                    list.arraySize++;
                    list.GetArrayElementAtIndex(idx).objectReferenceValue = data;
                }
                defIdx.intValue = idx;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(a);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[KayipParca] 2D Renderer {assets.Count} URP ayarına bağlandı (2D ışıklar aktif).");
        }

        static void AllSortingLayers(Light2D l)
        {
            var so = new SerializedObject(l);
            var p = so.FindProperty("m_ApplyToSortingLayers");
            if (p == null || !p.isArray) return;
            var layers = SortingLayer.layers;
            p.arraySize = layers.Length;
            for (int i = 0; i < layers.Length; i++) p.GetArrayElementAtIndex(i).intValue = layers[i].id;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------ parca prefabi
        const string PrefabDir = "Assets/Prefabs/KayipParca";
        const string PiecePrefabPath = PrefabDir + "/Parca.prefab";

        [MenuItem("Kayıp Parça/4) Parça Prefabını Kur")]
        public static void SetupPiecePrefab()
        {
            var prefab = EnsurePiecePrefab();
            var lm = Object.FindAnyObjectByType<LevelManager>();
            if (!lm)
            {
                Debug.Log("[KayipParca] Parça prefabı hazır: " + PiecePrefabPath + " (açık sahnede LevelManager yok, elle bağlayın).");
                return;
            }
            if (lm.piecePrefab != prefab)
            {
                Undo.RecordObject(lm, "Parça prefabını bağla");
                lm.piecePrefab = prefab;
                EditorUtility.SetDirty(lm);
                EditorSceneManager.MarkSceneDirty(lm.gameObject.scene);
                EditorSceneManager.SaveScene(lm.gameObject.scene);
            }
            Debug.Log("[KayipParca] Parça prefabı hazır ve LevelManager'a bağlı: " + PiecePrefabPath);
        }

        /// <summary>
        /// Govde + yuz + sapka katmanli parca prefabini (yoksa) olusturur ve PieceView'in
        /// [SerializeField] alanlarini baglar. Sapka spriti prefabda durur; kod yalnizca govde/yuz spritini degistirir.
        /// </summary>
        public static PieceView EnsurePiecePrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<PieceView>(PiecePrefabPath);
            if (existing) return existing;

            if (!AssetDatabase.IsValidFolder("Assets/Prefabs")) AssetDatabase.CreateFolder("Assets", "Prefabs");
            if (!AssetDatabase.IsValidFolder(PrefabDir)) AssetDatabase.CreateFolder("Assets/Prefabs", "KayipParca");

            var root = new GameObject("Parca");
            var view = root.AddComponent<PieceView>();
            SpriteRenderer Layer(string n, int order, string sprite)
            {
                var go = new GameObject(n);
                go.transform.SetParent(root.transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sortingOrder = order;
                sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtPath}/{sprite}.png");
                if (!sr.sprite) Debug.LogWarning($"[KayipParca] Prefab için sprite bulunamadı: {sprite}. Önce 1) Görsel Ayarlarını Uygula.");
                return sr;
            }
            var body = Layer("Govde", 6, "Player/body_1000");
            var face = Layer("Yuz", 7, "Player/face_idle_0");
            var hat = Layer("Sapka", 8, "Player/hat");

            var so = new SerializedObject(view);
            so.FindProperty("body").objectReferenceValue = body;
            so.FindProperty("face").objectReferenceValue = face;
            so.FindProperty("hat").objectReferenceValue = hat;
            so.ApplyModifiedPropertiesWithoutUndo();

            var asset = PrefabUtility.SaveAsPrefabAsset(root, PiecePrefabPath);
            Object.DestroyImmediate(root);
            Debug.Log("[KayipParca] Parça prefabı oluşturuldu: " + PiecePrefabPath);
            return asset.GetComponent<PieceView>();
        }

        [MenuItem("Kayıp Parça/2) Oyun Sahnesini Kur")]
        public static void BuildScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            ApplyImportSettings();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Kamera
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 6;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            camGo.transform.position = new Vector3(0, 0, -10);
            camGo.AddComponent<AudioListener>();

            // Isiklar (URP 2D Renderer gerekir)
            var global = new GameObject("Global Light 2D").AddComponent<Light2D>();
            global.lightType = Light2D.LightType.Global;
            global.intensity = 1f;
            var pLight = new GameObject("Oyuncu Isigi").AddComponent<Light2D>();
            pLight.lightType = Light2D.LightType.Point;
            pLight.pointLightOuterRadius = 3f;
            pLight.intensity = .4f;
            AllSortingLayers(global); AllSortingLayers(pLight);

            // Izgara + tilemap katmanlari
            var grid = new GameObject("Grid").AddComponent<Grid>();
            Tilemap MakeMap(string n, int order)
            {
                var go = new GameObject(n);
                go.transform.SetParent(grid.transform, false);
                var tm = go.AddComponent<Tilemap>();
                go.AddComponent<TilemapRenderer>().sortingOrder = order;
                return tm;
            }
            var floor = MakeMap("Zemin", 0);
            var walls = MakeMap("Duvarlar", 1);
            var decor = MakeMap("Engeller", 1);

            // Parallax arka planlar
            SpriteRenderer MakeBg(string n, int order)
            {
                var sr = new GameObject(n).AddComponent<SpriteRenderer>();
                sr.sortingOrder = order;
                return sr;
            }
            var bgFar = MakeBg("Arkaplan Uzak", -20);
            var bgNear = MakeBg("Arkaplan Yakin", -10);

            var objects = new GameObject("Nesneler").transform;

            // Ses
            var audioGo = new GameObject("Ses");
            AudioSource Src(string n, bool loop)
            {
                var go = new GameObject(n); go.transform.SetParent(audioGo.transform, false);
                var a = go.AddComponent<AudioSource>(); a.playOnAwake = false; a.loop = loop; return a;
            }
            var music = Src("Muzik", true);
            var amb = Src("Ambiyans", true);
            var sfx = Src("Efektler", false);

            // UI: yerlesim LevelManager.LayoutHudTexts'te (oyun basinda da yeniden uygulanir)
            var canvasGo = UIKit.MakeCanvas("UI").gameObject;
            var font = UIKit.DefaultFont;
            Text MakeText(string n) => UIKit.MakeText(canvasGo.transform, n, font, 32, TextAnchor.MiddleCenter);
            var title = MakeText("Baslik");
            var moves = MakeText("Hamle");
            var msg = MakeText("Mesaj");
            var hint = MakeText("Ipucu");
            LevelManager.LayoutHudTexts(title, moves, msg, hint);

            // Yoneticiler
            var game = new GameObject("Oyun");
            var themes = game.AddComponent<ThemeManager>();
            themes.globalLight = global; themes.playerLight = pLight;
            themes.musicSource = music; themes.ambienceSource = amb; themes.cam = cam;
            var lm = game.AddComponent<LevelManager>();
            lm.themes = themes;
            lm.floorMap = floor; lm.wallMap = walls; lm.decorMap = decor;
            lm.objectsRoot = objects;
            lm.bgFar = bgFar; lm.bgNear = bgNear;
            lm.titleText = title; lm.movesText = moves; lm.messageText = msg; lm.hintText = hint;
            lm.sfx = sfx;
            lm.piecePrefab = EnsurePiecePrefab();

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!list.Exists(s => s.path == ScenePath)) list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
            Selection.activeGameObject = game;
            Debug.Log("[KayipParca] Sahne kuruldu: " + ScenePath + "  -> Play'e basin. Sesleri 'Oyun' objesindeki ThemeManager / LevelManager'a sürükleyin.");
        }
    }
}
