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
    /// Bolum 2 (Yamali'nin Intikami) sahnesini kurar: Assets/Scenes/Yamali.unity + Build Settings.
    /// Acik sahneye dokunmaz. Test menuleri acik sahneyi kaydetmeden Play'e girer ve Yamali sahnesini oyun icinde yukler.
    /// </summary>
    [InitializeOnLoad]
    static class YamaliAutoSetup
    {
        static YamaliAutoSetup()
        {
            EditorApplication.playModeStateChanged += st =>
            {
                if (st != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(YamaliSetup.LoadFlag, false)) return;
                SessionState.SetBool(YamaliSetup.LoadFlag, false);
                SceneManager.LoadScene("Yamali");
            };
            EditorApplication.delayCall += () =>
            {
                if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (File.Exists(YamaliSetup.ScenePath)) { YamaliSetup.EnsureBuildSettings(); return; }
                if (SessionState.GetBool("YamaliAutoSetupDone", false)) return;
                SessionState.SetBool("YamaliAutoSetupDone", true);
                YamaliSetup.BuildScene();
            };
        }
    }

    public static class YamaliSetup
    {
        public const string ScenePath = "Assets/Scenes/Yamali.unity";
        const string MainScenePath = "Assets/Scenes/KayipParca.unity";
        public const string TestKey = "YamaliTestStartAt", LoadFlag = "YamaliLoadOnPlay";

        [MenuItem("Kayıp Parça/9) Yamalı Bölümünü Kur")]
        public static void BuildScene()
        {
            var active = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true; cam.orthographicSize = 7.5f;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black;
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

            var game = new GameObject("Yamali");
            game.AddComponent<YamaliGame>();

            foreach (var go in new[] { camGo, light.gameObject, game }) SceneManager.MoveGameObjectToScene(go, scene);
            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            if (active.IsValid() && active != scene) EditorSceneManager.CloseScene(scene, true);
            EnsureBuildSettings();
            Debug.Log("[KayipParca] Yamalı sahnesi hazır: " + ScenePath);
        }

        [MenuItem("Kayıp Parça/Yamalı Test/Giriş hikâyesinden")] static void T0() => StartTest(0);
        [MenuItem("Kayıp Parça/Yamalı Test/Seviye 1")] static void T1() => StartTest(1);
        [MenuItem("Kayıp Parça/Yamalı Test/Seviye 2")] static void T2() => StartTest(2);
        [MenuItem("Kayıp Parça/Yamalı Test/Seviye 3")] static void T3() => StartTest(3);
        [MenuItem("Kayıp Parça/Yamalı Test/Seviye 4")] static void T4() => StartTest(4);
        [MenuItem("Kayıp Parça/Yamalı Test/Seviye 5")] static void T5() => StartTest(5);
        [MenuItem("Kayıp Parça/Yamalı Test/Seviye 6")] static void T6() => StartTest(6);
        [MenuItem("Kayıp Parça/Yamalı Test/Seviye 7")] static void T7() => StartTest(7);
        [MenuItem("Kayıp Parça/Yamalı Test/Seviye 8")] static void T8() => StartTest(8);

        public static void StartTest(int startAt)
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
