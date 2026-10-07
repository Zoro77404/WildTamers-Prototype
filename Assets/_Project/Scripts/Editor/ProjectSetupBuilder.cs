using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WildTamers.Audio;

namespace WildTamers.EditorTools
{
    /// <summary>
    /// Project plumbing that is not a scene: the audio library (import settings for the music and sounds, volume trims),
    /// the scenes in the build (main menu first) and "play from the main menu" in the editor.
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectSetupBuilder
    {
        private const string AudioFolder = "Assets/ThirdParty/OpenGameArt/Audio";
        private const string LibraryPath = "Assets/_Project/Resources/AudioLibrary.asset";
        private const string PlayFromMenuKey = "WildTamers.PlayFromMenu";
        private const string PlayFromMenuMenu = "Wild Tamers/Play From Main Menu";

        static ProjectSetupBuilder()
        {
            EditorApplication.delayCall += ApplyPlayModeStartScene;
        }

        // ---------- Audio ----------

        [MenuItem("Wild Tamers/Build/Audio Library")]
        public static void BuildAudio()
        {
            string[] music = { "Music_Menu_DarbukaDelight", "Music_Battle_DarbukaChase" };
            string[] sfx = { "Sfx_Click", "Sfx_Hit", "Sfx_Crit", "Sfx_Guard", "Sfx_Win", "Sfx_Lose", "Sfx_Pop" };
            foreach (var n in music) Import(n, streaming: true);
            foreach (var n in sfx) Import(n, streaming: false);

            Directory.CreateDirectory(Path.GetDirectoryName(LibraryPath));
            var library = AssetDatabase.LoadAssetAtPath<AudioLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<AudioLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }
            Set(library.menuMusic, "Music_Menu_DarbukaDelight", 0.6f);
            Set(library.battleMusic, "Music_Battle_DarbukaChase", 1f);
            Set(library.click, "Sfx_Click", 0.8f);
            Set(library.hit, "Sfx_Hit", 0.75f);
            Set(library.crit, "Sfx_Crit", 1f);
            Set(library.guard, "Sfx_Guard", 0.8f);
            Set(library.win, "Sfx_Win", 0.8f);
            Set(library.lose, "Sfx_Lose", 0.7f);
            Set(library.pop, "Sfx_Pop", 0.7f);
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            Debug.Log("[Wild Tamers] Audio library built.");
        }

        private static void Import(string name, bool streaming)
        {
            var path = $"{AudioFolder}/{name}.ogg";
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null)
            {
                Debug.LogError("[Wild Tamers] Missing audio file " + path);
                return;
            }
            var settings = importer.defaultSampleSettings;
            settings.loadType = streaming ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = streaming ? 0.55f : 0.7f;
            settings.preloadAudioData = !streaming;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = !streaming;
            importer.loadInBackground = streaming;
            importer.SaveAndReimport();
        }

        private static void Set(AudioLibrary.Entry entry, string name, float volume)
        {
            entry.clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioFolder}/{name}.ogg");
            entry.volume = volume;
        }

        // ---------- Scenes in the build ----------

        [MenuItem("Wild Tamers/Build/Build Settings Scenes")]
        public static void BuildSceneList()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MainMenuSceneBuilder.ScenePath, true),
                new EditorBuildSettingsScene(MapSceneBuilder.ScenePath, true),
                new EditorBuildSettingsScene(BattleSceneBuilder.ScenePath, true),
            };
            Debug.Log("[Wild Tamers] Build scenes: MainMenuScene, MapScene, BattleScene.");
        }

        // ---------- Play from the main menu ----------

        private static bool PlayFromMenu
        {
            get => EditorPrefs.GetBool(PlayFromMenuKey, true);
            set => EditorPrefs.SetBool(PlayFromMenuKey, value);
        }

        [MenuItem(PlayFromMenuMenu)]
        private static void TogglePlayFromMenu()
        {
            PlayFromMenu = !PlayFromMenu;
            ApplyPlayModeStartScene();
        }

        [MenuItem(PlayFromMenuMenu, true)]
        private static bool TogglePlayFromMenuValidate()
        {
            UnityEditor.Menu.SetChecked(PlayFromMenuMenu, PlayFromMenu);
            return true;
        }

        /// <summary>Pressing Play in the editor starts on the main menu, just like a build does (switch it off in the Wild Tamers menu).</summary>
        private static void ApplyPlayModeStartScene()
        {
            var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MainMenuSceneBuilder.ScenePath);
            EditorSceneManager.playModeStartScene = PlayFromMenu ? scene : null;
        }
    }
}
