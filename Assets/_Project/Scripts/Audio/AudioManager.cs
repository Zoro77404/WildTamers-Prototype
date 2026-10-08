using UnityEngine;
using WildTamers.Core;

namespace WildTamers.Audio
{
    /// <summary>
    /// Plays the music (two sources that cross-fade, also at the end of a track so it loops smoothly) and the sound effects
    /// (a small pool of sources, so hits can overlap). Music follows the Music slider, every effect the Sound effects slider.
    /// All clips and their volumes come from the <see cref="SoundLibrary"/>. Created on first use and kept across scenes,
    /// so the music carries on from the main menu into the map without a restart.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        private const int SfxVoices = 12;
        private const float LoopFade = 3f;

        private static AudioManager instance;
        private static bool quitting;

        private SoundLibrary library;
        private AudioSource[] music;
        private float[] musicLevel;      // 0–1 fade level of each music source
        private float[] musicTrim;       // loudness trim of the clip each source plays
        private int current = -1;        // music source that is the "current" track
        private Music playing = Music.None;
        private float fadeSeconds = 1.6f;
        private AudioSource[] voices;
        private int nextVoice;
        private int lastNormalAttack = -1;

        public static AudioManager Instance
        {
            get
            {
                if (instance == null && !quitting)
                {
                    var go = new GameObject("[AudioManager]");
                    instance = go.AddComponent<AudioManager>();
                }
                return instance;
            }
        }

        public static bool Exists => instance != null;

        public Music Playing => playing;
        public SoundLibrary Library => library;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
            quitting = false;
        }

        /// <summary>Slider position (0–1) to loudness: squared, so the lower half of the slider still has a useful range.</summary>
        public static float Loudness(float slider) => slider * slider;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);
            library = Resources.Load<SoundLibrary>(SoundLibrary.ResourcePath);
            if (library == null) Debug.LogWarning("[Wild Tamers] SoundLibrary missing from Resources; the game stays silent.");

            music = new AudioSource[2];
            musicLevel = new float[2];
            musicTrim = new float[] { 1f, 1f };
            for (int i = 0; i < 2; i++)
            {
                music[i] = gameObject.AddComponent<AudioSource>();
                music[i].playOnAwake = false;
                music[i].loop = false;
                music[i].spatialBlend = 0f;
                music[i].volume = 0f;
            }
            voices = new AudioSource[SfxVoices];
            for (int i = 0; i < SfxVoices; i++)
            {
                voices[i] = gameObject.AddComponent<AudioSource>();
                voices[i].playOnAwake = false;
                voices[i].spatialBlend = 0f;
            }
            GameSettings.EnsureLoaded();
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) GameSettings.Flush();
        }

        private void OnApplicationQuit()
        {
            GameSettings.Flush();
            quitting = true;
        }

        // ---------- Music ----------

        /// <summary>Starts a track, cross-fading from the current one. Asking for the track that is already playing does nothing.</summary>
        public void PlayMusic(Music which)
        {
            if (which == playing && (which == Music.None || (current >= 0 && music[current].isPlaying))) return;
            playing = which;
            fadeSeconds = library != null ? Mathf.Max(0.05f, library.musicFade) : 1.6f;
            var entry = library != null ? library.Get(which) : null;
            if (entry == null || entry.clip == null)
            {
                for (int i = 0; i < 2; i++) musicFading[i] = true;
                current = -1;
                return;
            }
            StartOn(1 - Mathf.Max(0, current), entry);
        }

        private readonly bool[] musicFading = new bool[2];

        private void StartOn(int index, SoundLibrary.Sound entry)
        {
            // The other source fades out; this one starts at the beginning of the clip and fades in.
            int old = current;
            if (old >= 0 && old != index) musicFading[old] = true;
            var src = music[index];
            src.clip = entry.clip;
            src.time = 0f;
            src.volume = 0f;
            musicLevel[index] = 0f;
            musicTrim[index] = entry.volume;
            musicFading[index] = false;
            src.Play();
            current = index;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            float master = Loudness(GameSettings.MusicVolume);
            for (int i = 0; i < 2; i++)
            {
                var src = music[i];
                if (!src.isPlaying && !musicFading[i]) continue;
                bool isCurrent = i == current && !musicFading[i];
                musicLevel[i] = Mathf.MoveTowards(musicLevel[i], isCurrent ? 1f : 0f, dt / fadeSeconds);
                // Equal-power curve: the cross-fade doesn't dip in the middle.
                src.volume = Mathf.Sin(musicLevel[i] * Mathf.PI * 0.5f) * musicTrim[i] * master;
                if (!isCurrent && musicLevel[i] <= 0f)
                {
                    src.Stop();
                    musicFading[i] = false;
                }
            }

            // Smooth looping: shortly before the track ends, start it again on the other source.
            if (current >= 0 && music[current].isPlaying && music[current].clip != null && playing != Music.None)
            {
                var src = music[current];
                if (src.clip.length - src.time < LoopFade && src.clip.length > LoopFade * 3f)
                {
                    var entry = library.Get(playing);
                    fadeSeconds = LoopFade;
                    StartOn(1 - current, entry);
                }
            }
        }

        // ---------- Sound effects ----------

        /// <summary>Plays a library sound effect once. <paramref name="volume"/> scales the slot's own volume.</summary>
        public void PlaySfx(Sfx sfx, float volume = 1f)
        {
            if (library == null) return;
            if (sfx == Sfx.NormalAttack)
            {
                PlaySound(PickNormalAttack(), volume);
                return;
            }
            PlaySound(library.Get(sfx), volume);
        }

        /// <summary>Plays a library slot (clip, volume, pitch and random pitch change all come from the slot).</summary>
        public void PlaySound(SoundLibrary.Sound sound, float volume = 1f)
        {
            if (sound == null) return;
            PlayClip(sound.clip, sound.volume * volume, sound.pitch, sound.pitchVariance);
        }

        /// <summary>Plays any clip as a sound effect (Sound effects slider applies).</summary>
        public void PlayClip(AudioClip clip, float volume, float pitch = 1f, float pitchVariance = 0.04f)
        {
            if (clip == null) return;
            float level = volume * Loudness(GameSettings.SfxVolume);
            if (level <= 0.0001f) return;
            var voice = voices[nextVoice];
            nextVoice = (nextVoice + 1) % voices.Length;
            voice.pitch = Mathf.Max(0.1f, pitch * (1f + Random.Range(-pitchVariance, pitchVariance)));
            voice.volume = Mathf.Clamp01(level);
            voice.clip = clip;
            voice.Play();
        }

        private SoundLibrary.Sound PickNormalAttack()
        {
            var list = library.normalAttacks;
            if (list == null || list.Length == 0) return null;
            int pick = Random.Range(0, list.Length);
            if (list.Length > 1 && pick == lastNormalAttack) pick = (pick + 1 + Random.Range(0, list.Length - 1)) % list.Length;
            lastNormalAttack = pick;
            return list[pick];
        }

        /// <summary>Convenience: plays a sound effect if the audio system can start.</summary>
        public static void Play(Sfx sfx, float volume = 1f)
        {
            var manager = Instance;
            if (manager != null) manager.PlaySfx(sfx, volume);
        }
    }
}
