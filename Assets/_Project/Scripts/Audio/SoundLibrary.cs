using System;
using UnityEngine;

namespace WildTamers.Audio
{
    public enum Music
    {
        None,
        Menu,
        Battle
    }

    public enum Sfx
    {
        Click,
        Footstep,
        NormalAttack,
        Swing,
        Charge,
        Heavy,
        Guard,
        Win,
        Lose,
        Pop
    }

    /// <summary>
    /// Every sound in the game in one place (Resources/SoundLibrary): drag a new file into a slot to swap a sound, and set each
    /// slot's own volume. Music follows the Music slider in Settings, everything else the Sound effects slider.
    /// Special-attack sounds of each animal live on its AnimalData (Special).
    /// </summary>
    [CreateAssetMenu(menuName = "Wild Tamers/Sound Library", fileName = "SoundLibrary")]
    public class SoundLibrary : ScriptableObject
    {
        public const string ResourcePath = "SoundLibrary";

        [Serializable]
        public class Sound
        {
            public AudioClip clip;
            [Tooltip("Loudness of this sound before the Settings slider.")]
            [Range(0f, 1f)] public float volume = 1f;
            [Tooltip("Base pitch (1 = as recorded, lower = deeper/heavier). Music ignores this.")]
            [Range(0.5f, 2f)] public float pitch = 1f;
            [Tooltip("Random pitch change each time it plays (0.05 = up to ±5%), so repeats don't sound robotic.")]
            [Range(0f, 0.3f)] public float pitchVariance = 0.04f;

            public Sound() { }

            public Sound(float volume, float pitch = 1f, float variance = 0.04f)
            {
                this.volume = volume;
                this.pitch = pitch;
                pitchVariance = variance;
            }
        }

        [Header("Music (Music slider)")]
        [Tooltip("Main menu and map.")]
        public Sound mainTheme = new Sound(0.6f, 1f, 0f);
        [Tooltip("Battles.")]
        public Sound battleMusic = new Sound(0.6f, 1f, 0f);
        [Tooltip("Seconds to cross-fade between the two tracks.")]
        [Range(0.2f, 5f)] public float musicFade = 1.6f;

        [Header("Interface (Sound effects slider)")]
        [Tooltip("Every button.")]
        public Sound buttonClick = new Sound(0.5f, 1f, 0.03f);
        [Tooltip("'New animal!' card and XP bar ticks.")]
        public Sound pop = new Sound(0.7f);
        public Sound win = new Sound(0.8f, 1f, 0f);
        public Sound lose = new Sound(0.7f, 1f, 0f);

        [Header("Map")]
        [Tooltip("The player walking on the map, once per step.")]
        public Sound footstep = new Sound(0.18f, 1f, 0.1f);

        [Header("Battle")]
        [Tooltip("Normal attack hits: one is picked at random each time (never the same twice in a row).")]
        public Sound[] normalAttacks = Array.Empty<Sound>();
        [Tooltip("Whoosh when an animal lunges at its target.")]
        public Sound attackSwing = new Sound(0.35f, 1f, 0.06f);
        [Tooltip("Special attack charge-up (before the lunge).")]
        public Sound specialCharge = new Sound(0.55f, 1f, 0.03f);
        [Tooltip("Critical hits. Special attack impacts use the animal's own Special sound (AnimalData), which defaults to this file.")]
        public Sound heavyHit = new Sound(0.6f, 0.85f, 0.03f);
        [Tooltip("Extra clink when a hit lands on a guarding animal.")]
        public Sound guard = new Sound(0.6f);

        public Sound Get(Music music)
        {
            switch (music)
            {
                case Music.Menu: return mainTheme;
                case Music.Battle: return battleMusic;
                default: return null;
            }
        }

        public Sound Get(Sfx sfx)
        {
            switch (sfx)
            {
                case Sfx.Click: return buttonClick;
                case Sfx.Footstep: return footstep;
                case Sfx.Swing: return attackSwing;
                case Sfx.Charge: return specialCharge;
                case Sfx.Heavy: return heavyHit;
                case Sfx.Guard: return guard;
                case Sfx.Win: return win;
                case Sfx.Lose: return lose;
                case Sfx.Pop: return pop;
                default: return null;
            }
        }
    }
}
