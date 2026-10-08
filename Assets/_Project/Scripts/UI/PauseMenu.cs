using UnityEngine;
using UnityEngine.UI;
using WildTamers.Core;

namespace WildTamers.UI
{
    /// <summary>
    /// Pause menu for the map and the battle: Resume / Settings / Main Menu. The game stands still (time scale 0) while it is open.
    /// Leaving a battle first asks "Leave the battle?"; the map saves by itself, so it leaves right away.
    /// </summary>
    public class PauseMenu : UIPanel
    {
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button mainMenuButton;
        [SerializeField] private Button backdropButton;
        [SerializeField] private SettingsPanel settings;
        [SerializeField] private ConfirmPopup confirm;
        [Tooltip("True in the battle scene: leaving asks for confirmation because the fight is given up.")]
        [SerializeField] private bool inBattle;

        protected override void Awake()
        {
            base.Awake();
            resumeButton.onClick.AddListener(Resume);
            settingsButton.onClick.AddListener(() => { if (settings != null) settings.Show(); });
            mainMenuButton.onClick.AddListener(MainMenuPressed);
            ClickSound.Hook(backdropButton);
            if (backdropButton != null) backdropButton.onClick.AddListener(Resume);
        }

        public void Open()
        {
            if (IsVisible || GameSession.Instance.Fader.IsBusy) return;
            Show();
        }

        private bool holdingTime;

        protected override void OnShown()
        {
            holdingTime = true;
            Time.timeScale = 0f;
        }

        protected override void OnHidden() => ReleaseTime();

        protected override void OnDisable()
        {
            base.OnDisable();
            ReleaseTime();
        }

        private void ReleaseTime()
        {
            if (!holdingTime) return;
            holdingTime = false;
            Time.timeScale = 1f;
        }

        private void Resume()
        {
            if ((settings != null && settings.IsVisible) || (confirm != null && confirm.IsVisible)) return;
            Hide();
        }

        private void MainMenuPressed()
        {
            if (inBattle && confirm != null)
                confirm.Open("confirm.leave.title", "confirm.leave.body", "confirm.leave.yes", LeaveToMenu);
            else LeaveToMenu();
        }

        private void LeaveToMenu()
        {
            Time.timeScale = 1f;
            GameSession.Instance.ReturnToMainMenu();
        }
    }
}
