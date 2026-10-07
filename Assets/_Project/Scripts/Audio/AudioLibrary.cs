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
        Hit,
        Crit,
        Guard,
        Win,
        Lose,
        Pop
    }

    /// <summary>
    /// The game's music and sound effects (all free CC0 files, see RequiredAssets.md) with a loudness trim for each one,
    /// so the two volume sliders in the settings only have to deal with one master level per group. Lives in Resources.
    /// </summary>
    [CreateAssetMenu(menuName = "Wild Tamers/Audio Library", fileName = "AudioLibrary")]
    public class AudioLibrary : ScriptableObject
    {
        public const string ResourcePath = "AudioLibrary";

        [System.Serializable]
        public class Entry
        {
            public AudioClip clip;
            [Range(0f, 1f)] public float volume = 1f;
        }

        [Header("Music")]
        public Entry menuMusic = new Entry();
        public Entry battleMusic = new Entry();

        [Header("Sound effects")]
        public Entry click = new Entry();
        public Entry hit = new Entry();
        public Entry crit = new Entry();
        public Entry guard = new Entry();
        public Entry win = new Entry();
        public Entry lose = new Entry();
        public Entry pop = new Entry();

        public Entry Get(Music music)
        {
            switch (music)
            {
                case Music.Menu: return menuMusic;
                case Music.Battle: return battleMusic;
                default: return null;
            }
        }

        public Entry Get(Sfx sfx)
        {
            switch (sfx)
            {
                case Sfx.Click: return click;
                case Sfx.Hit: return hit;
                case Sfx.Crit: return crit;
                case Sfx.Guard: return guard;
                case Sfx.Win: return win;
                case Sfx.Lose: return lose;
                case Sfx.Pop: return pop;
                default: return null;
            }
        }
    }
}
