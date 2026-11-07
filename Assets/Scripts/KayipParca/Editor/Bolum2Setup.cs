using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace KayipParca.EditorTools
{
    /// <summary>
    /// Bolum 2 sahnesini (Assets/Scenes/Bolum2.unity) olusturur ve Build Settings'e ekler.
    /// Acik sahneye dokunmaz: yeni sahne arka planda (additive) kurulur, kaydedilir ve kapatilir.
    /// </summary>
    [InitializeOnLoad]
    static class Bolum2AutoSetup
    {
        static Bolum2AutoSetup()
        {
            // Test menusu: Play'e girildikten sonra (kod yeniden yuklendiginde de calisir) Bolum 2'yi oyun icinde yukle.
            EditorApplication.playModeStateChanged += st =>
            {
                if (st != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Bolum2Setup.LoadFlag, false)) return;
                SessionState.SetBool(Bolum2Setup.LoadFlag, false);
                SceneManager.LoadScene("Bolum2");
            };
            EditorApplication.delayCall += () =>
            {
                if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (File.Exists(Bolum2Setup.ScenePath)) { Bolum2Setup.EnsureBuildSettings(); return; }
                if (SessionState.GetBool("Bolum2AutoSetupDone", false)) return;
                SessionState.SetBool("Bolum2AutoSetupDone", true);
                Bolum2Setup.BuildScene();
            };
        }
    }

    public static class Bolum2Setup
    {
        public const string ScenePath = "Assets/Scenes/Bolum2.unity";
        const string MainScenePath = "Assets/Scenes/KayipParca.unity";

        [MenuItem("Kayıp Parça/5) Bölüm 2 Sahnesini Kur")]
        public static void BuildScene()
        {
            var active = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true; cam.orthographicSize = 5.625f;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.56f, .83f, .96f);
            camGo.transform.position = new Vector3(0, 0, -10);
            camGo.AddComponent<AudioListener>();

            var light = new GameObject("Global Light 2D").AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global; light.intensity = 1f;
            var so = new SerializedObject(light);
            var layers = so.FindProperty("m_ApplyToSortingLayers");
            if (layers != null && layers.isArray)
            {
                layers.arraySize = SortingLayer.layers.Length;
                for (int i = 0; i < SortingLayer.layers.Length; i++) layers.GetArrayElementAtIndex(i).intValue = SortingLayer.layers[i].id;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            var game = new GameObject("Bolum2");
            game.AddComponent<Bolum2Game>();

            foreach (var go in new[] { camGo, light.gameObject, game }) SceneManager.MoveGameObjectToScene(go, scene);
            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            if (active.IsValid() && active != scene) EditorSceneManager.CloseScene(scene, true);
            EnsureBuildSettings();
            Debug.Log("[KayipParca] Bölüm 2 sahnesi hazır: " + ScenePath + ". Test için sahneyi açıp Play'e basın.");
        }

        // Test: acik sahneyi kaydetmeden/kapatmadan Play'e girer ve Bolum 2'yi oyun icinde yukler.
        // Play'den cikinca editor acik sahneyi degisiklikleriyle birlikte aynen geri getirir.
        public const string TestKey = "Bolum2TestStartAt", LoadFlag = "Bolum2LoadOnPlay";

        [MenuItem("Kayıp Parça/6) Bölüm 2'yi Test Et (Dalga 1)")]
        public static void TestFromWave1() => StartTest(1);

        [MenuItem("Kayıp Parça/7) Bölüm 2'yi Test Et (Boss)")]
        public static void TestBoss() => StartTest(4);

        [MenuItem("Kayıp Parça/8) Bölüm 2'yi Test Et (Giriş hikâyesinden)")]
        public static void TestFromIntro() => StartTest(0);

        static void StartTest(int startAt)
        {
            if (!File.Exists(ScenePath)) BuildScene();
            EnsureBuildSettings();
            SessionState.SetInt(TestKey, startAt);
            SessionState.SetBool(LoadFlag, SceneManager.GetActiveScene().path != ScenePath);
            EditorApplication.isPlaying = true;
        }

        public static void EnsureBuildSettings()
        {
            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            bool changed = false;
            if (!list.Exists(s => s.path == MainScenePath) && File.Exists(MainScenePath)) { list.Insert(0, new EditorBuildSettingsScene(MainScenePath, true)); changed = true; }
            if (!list.Exists(s => s.path == ScenePath)) { list.Add(new EditorBuildSettingsScene(ScenePath, true)); changed = true; }
            if (changed) EditorBuildSettings.scenes = list.ToArray();
        }
    }
}
