using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WildTamers.Audio;
using WildTamers.Core;
using WildTamers.Lang;

namespace WildTamers.UI
{
    /// <summary>
    /// Settings screen (from the main menu and the pause menu): language (English / العربية, switches the whole game at once),
    /// music and sound-effect volume, and "Reset save" with an "Are you sure?" popup. Everything is saved through <see cref="GameSettings"/>.
    /// </summary>
    public class SettingsPanel : UIPanel
    {
        private const float SaveDelay = 0.5f;

        [Header("Language")]
        [SerializeField] private Button englishButton;
        [SerializeField] private Button arabicButton;
        [SerializeField] private Image englishFace;
        [SerializeField] private Image arabicFace;
        [SerializeField] private Color selectedColor = new Color32(0x2E, 0xC4, 0xB6, 0xFF);
        [SerializeField] private Color idleColor = new Color32(0xB9, 0xC6, 0xD8, 0xFF);

        [Header("Volume")]
        [SerializeField] private Slider musicSlider;
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private TMP_Text musicValue;
        [SerializeField] private TMP_Text sfxValue;

        [Header("Other")]
        [SerializeField] private Button resetButton;
        [SerializeField] private Button doneButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button backdropButton;
        [SerializeField] private ConfirmPopup confirm;
        [SerializeField] private ToastMessage toast;
        [Tooltip("On the main menu a reset stays on the menu; in a game scene it restarts at the map.")]
        [SerializeField] private bool stayInSceneOnReset;

        private float lastChange = -1f;
        private bool sfxChanged;

        protected override void Awake()
        {
            base.Awake();
            englishButton.onClick.AddListener(() => Loc.SetLanguage(GameLanguage.English));
            arabicButton.onClick.AddListener(() => Loc.SetLanguage(GameLanguage.Arabic));
            musicSlider.onValueChanged.AddListener(v => { GameSettings.SetMusicVolume(v); Touched(false); });
            sfxSlider.onValueChanged.AddListener(v => { GameSettings.SetSfxVolume(v); Touched(true); });
            resetButton.onClick.AddListener(AskReset);
            if (doneButton != null) doneButton.onClick.AddListener(Close);
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            ClickSound.Hook(backdropButton);
            if (backdropButton != null) backdropButton.onClick.AddListener(Close);
        }

        protected override void OnShown()
        {
            Loc.LanguageChanged -= Refresh;
            Loc.LanguageChanged += Refresh;
            Refresh();
            transform.SetAsLastSibling();
            if (confirm != null) confirm.transform.SetAsLastSibling();
        }

        protected override void OnHidden()
        {
            Loc.LanguageChanged -= Refresh;
            GameSettings.Flush();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            Loc.LanguageChanged -= Refresh;
        }

        /// <summary>Shows the current language and volumes (also runs when the language is switched).</summary>
        private void Refresh()
        {
            englishFace.color = Loc.Language == GameLanguage.English ? selectedColor : idleColor;
            arabicFace.color = Loc.Language == GameLanguage.Arabic ? selectedColor : idleColor;
            // The sliders fill from the right in Arabic.
            var direction = Loc.IsRTL ? Slider.Direction.RightToLeft : Slider.Direction.LeftToRight;
            musicSlider.direction = direction;
            sfxSlider.direction = direction;
            musicSlider.SetValueWithoutNotify(GameSettings.MusicVolume);
            sfxSlider.SetValueWithoutNotify(GameSettings.SfxVolume);
            ShowPercent();
        }

        private void ShowPercent()
        {
            musicValue.text = Loc.T("common.percent", Mathf.RoundToInt(GameSettings.MusicVolume * 100f));
            sfxValue.text = Loc.T("common.percent", Mathf.RoundToInt(GameSettings.SfxVolume * 100f));
        }

        private void Touched(bool sfx)
        {
            lastChange = Time.unscaledTime;
            sfxChanged |= sfx;
            ShowPercent();
        }

        private void Update()
        {
            if (!IsVisible || lastChange < 0f || Time.unscaledTime - lastChange < SaveDelay) return;
            // The slider was let go a moment ago: save, and let the player hear the new effect volume.
            lastChange = -1f;
            GameSettings.Flush();
            if (sfxChanged) AudioManager.Play(Sfx.NormalAttack);
            sfxChanged = false;
        }

        private void Close()
        {
            if (confirm != null && confirm.IsVisible) return;
            Hide();
        }

        private void AskReset()
        {
            if (confirm == null) return;
            confirm.Open("confirm.reset.title", "confirm.reset.body", "confirm.reset.yes", DoReset);
        }

        private void DoReset()
        {
            Time.timeScale = 1f;
            if (stayInSceneOnReset)
            {
                GameSession.Instance.ResetSave(null);
                if (toast != null) toast.Show(Loc.T("settings.resetDone"));
                return;
            }
            Hide();
            GameSession.Instance.ResetSave(SceneNames.Map);
        }
    }
}
