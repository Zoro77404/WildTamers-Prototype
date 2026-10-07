using UnityEngine;
using UnityEngine.UI;
using WildTamers.Audio;
using WildTamers.Core;
using WildTamers.UI;

namespace WildTamers.Menu
{
    /// <summary>
    /// The first screen: Play (to the map), Team (the animal list and their cards), Settings and Quit.
    /// Starts the menu music and keeps the popups closed until they are asked for.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [SerializeField] private Button playButton;
        [SerializeField] private Button teamButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private TeamPanel teamPanel;
        [SerializeField] private SettingsPanel settingsPanel;
        [SerializeField] private AnimalCardPanel animalCard;
        [SerializeField] private ConfirmPopup confirm;

        private GameSession session;

        private void Start()
        {
            Time.timeScale = 1f;
            session = GameSession.Instance;
            AudioManager.Instance.PlayMusic(Music.Menu);

            teamPanel.HideImmediate();
            settingsPanel.HideImmediate();
            animalCard.HideImmediate();
            if (confirm != null) confirm.HideImmediate();

            playButton.onClick.AddListener(Play);
            teamButton.onClick.AddListener(() => { if (!teamPanel.IsVisible) teamPanel.Show(); });
            settingsButton.onClick.AddListener(() => { if (!settingsPanel.IsVisible) settingsPanel.Show(); });
            quitButton.onClick.AddListener(Quit);
        }

        private void Play()
        {
            if (session.Fader.IsBusy) return;
            session.Fader.LoadScene(SceneNames.Map);
        }

        private static void Quit()
        {
            GameSettings.Flush();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
