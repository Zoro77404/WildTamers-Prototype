using UnityEngine.UI;
using WildTamers.Audio;

namespace WildTamers.UI
{
    /// <summary>The button click sound (Sound Library → Button Click). Hooked to a button's onClick, so it plays exactly when the press counts.</summary>
    public static class ClickSound
    {
        public static void Hook(Button button)
        {
            if (button != null) button.onClick.AddListener(Play);
        }

        public static void Play() => AudioManager.Play(Sfx.Click);
    }
}
