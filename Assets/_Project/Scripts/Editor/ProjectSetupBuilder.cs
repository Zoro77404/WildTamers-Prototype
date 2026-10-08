using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WildTamers.Audio;

namespace WildTamers.EditorTools
{
    /// <summary>
    /// Project plumbing that is not a scene: the Sound Library (import settings for the music and sounds, volume trims),
    /// the scenes in the build (main menu first) and "play from the main menu" in the editor.
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectSetupBuilder
    {
        /// <summary>The user's own music and sounds (named by what they are; never moved or renamed).</summary>
        private const string SoundFolder = "Assets/_Project/Sounds";
        private const string AttackFolder = SoundFolder + "/Attacks";
        /// <summary>CC0 interface sounds still used for win / lose / pop / guard.</summary>
        private const string ExtraFolder = "Assets/ThirdParty/OpenGameArt/Audio";
        private const string LibraryPath = "Assets/_Project/Resources/SoundLibrary.asset";
        private const string PlayFromMenuKey = "WildTamers.PlayFromMenu";
        private const string PlayFromMenuMenu = "Wild Tamers/Play From Main Menu";

        static ProjectSetupBuilder()
        {
            EditorApplication.delayCall += ApplyPlayModeStartScene;
        }

        // ---------- Sound Library ----------

        /// <summary>
        /// Sets import settings and fills the Sound Library. Only empty slots are filled, so a sound dragged in by hand is kept.
        /// </summary>
        [MenuItem("Wild Tamers/Build/Sound Library")]
        public static SoundLibrary BuildAudio()
        {
            var mainTheme = $"{SoundFolder}/Main theme.mp3";
            var battle = $"{SoundFolder}/Fighting Song.mp3";
            Import(mainTheme, streaming: true);
            Import(battle, streaming: true);
            foreach (var file in new[] { "ButtonClick", "FootStep" }) Import($"{SoundFolder}/{file}.wav", streaming: false);
            foreach (var file in new[] { "Attack", "Attack_1", "Attack_2", "Attack_3", "Attack_Swing", "Attack_Charged", "Attack_Heavy" })
                Import($"{AttackFolder}/{file}.wav", streaming: false);
            foreach (var file in new[] { "Sfx_Win", "Sfx_Lose", "Sfx_Pop", "Sfx_Guard" }) Import($"{ExtraFolder}/{file}.ogg", streaming: false);

            Directory.CreateDirectory(Path.GetDirectoryName(LibraryPath));
            var library = AssetDatabase.LoadAssetAtPath<SoundLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<SoundLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }
            Fill(library.mainTheme, mainTheme);
            Fill(library.battleMusic, battle);
            Fill(library.buttonClick, $"{SoundFolder}/ButtonClick.wav");
            Fill(library.footstep, $"{SoundFolder}/FootStep.wav");
            Fill(library.attackSwing, $"{AttackFolder}/Attack_Swing.wav");
            Fill(library.specialCharge, $"{AttackFolder}/Attack_Charged.wav");
            Fill(library.heavyHit, $"{AttackFolder}/Attack_Heavy.wav");
            Fill(library.win, $"{ExtraFolder}/Sfx_Win.ogg");
            Fill(library.lose, $"{ExtraFolder}/Sfx_Lose.ogg");
            Fill(library.pop, $"{ExtraFolder}/Sfx_Pop.ogg");
            Fill(library.guard, $"{ExtraFolder}/Sfx_Guard.ogg");
            if (library.normalAttacks == null || library.normalAttacks.Length == 0)
            {
                library.normalAttacks = new[] { "Attack", "Attack_1", "Attack_2", "Attack_3" }
                    .Select(n => new SoundLibrary.Sound(0.45f, 1f, 0.08f) { clip = Load($"{AttackFolder}/{n}.wav") })
                    .ToArray();
            }
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            Debug.Log("[Wild Tamers] Sound Library built.");
            return library;
        }

        private static void Import(string path, bool streaming)
        {
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null)
            {
                Debug.LogError("[Wild Tamers] Missing audio file " + path);
                return;
            }
            var settings = importer.defaultSampleSettings;
            // Music streams from disk (long files); effects are decoded once at load so they start with no delay.
            var load = streaming ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            float quality = streaming ? 0.6f : 0.75f;
            bool mono = !streaming;
            if (settings.loadType == load && settings.compressionFormat == AudioCompressionFormat.Vorbis &&
                Mathf.Approximately(settings.quality, quality) && importer.forceToMono == mono && settings.preloadAudioData == !streaming)
                return;
            settings.loadType = load;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = quality;
            settings.preloadAudioData = !streaming;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = mono;
            importer.loadInBackground = streaming;
            importer.SaveAndReimport();
        }

        private static AudioClip Load(string path) => AssetDatabase.LoadAssetAtPath<AudioClip>(path);

        private static void Fill(SoundLibrary.Sound slot, string path)
        {
            if (slot.clip == null) slot.clip = Load(path);
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
