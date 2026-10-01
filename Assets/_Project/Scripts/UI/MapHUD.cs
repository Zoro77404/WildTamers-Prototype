using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WildTamers.Core;

namespace WildTamers.UI
{
    /// <summary>Map overlay: Team button, active-animal chip (with HP) and a controls hint.</summary>
    public class MapHUD : MonoBehaviour
    {
        [SerializeField] private Button teamButton;
        [SerializeField] private TeamPanel teamPanel;
        [SerializeField] private CanvasGroup activeChip;
        [SerializeField] private AnimalPreviewImage activePortrait;
        [SerializeField] private Image activePortraitBackdrop;
        [SerializeField] private TMP_Text activeName;
        [SerializeField] private TMP_Text activeLevel;
        [SerializeField] private HealthBar activeHp;
        [SerializeField] private CanvasGroup hint;
        [SerializeField] private float hintDuration = 9f;

        private static bool hintShown; // once per session, not after every battle

        private GameSession session;
        private int shownHP = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => hintShown = false;

        private void Start()
        {
            session = GameSession.Instance;
            teamButton.onClick.AddListener(() => { if (!teamPanel.IsVisible) teamPanel.Show(); });
            session.TeamChanged += Refresh;
            Refresh();
            if (hint != null)
            {
                if (hintShown) hint.gameObject.SetActive(false);
                else StartCoroutine(HintRoutine());
            }
        }

        private void OnDestroy()
        {
            if (session != null) session.TeamChanged -= Refresh;
        }

        private void Refresh()
        {
            var active = session.ActiveAnimal;
            bool has = active != null;
            teamButton.gameObject.SetActive(has);
            if (activeChip != null)
            {
                activeChip.alpha = has ? 1f : 0f;
                activeChip.gameObject.SetActive(has);
            }
            if (!has) return;
            activeName.text = active.Name;
            activeLevel.text = $"Lv. {active.Level}";
            shownHP = -1;
            UpdateHP(instant: true);
            if (activePortraitBackdrop != null && active.Data != null)
                activePortraitBackdrop.color = Color.Lerp(active.Data.themeColor, Color.white, 0.6f);
            activePortrait.SetAnimal(active.Data);
        }

        private void Update()
        {
            if (session != null) UpdateHP(instant: false);
        }

        /// <summary>The active animal heals while walking; keep its bar in step.</summary>
        private void UpdateHP(bool instant)
        {
            var active = session.ActiveAnimal;
            if (activeHp == null || active == null || active.CurrentHP == shownHP) return;
            shownHP = active.CurrentHP;
            activeHp.Set(active.CurrentHP, active.MaxHP, instant);
        }

        private IEnumerator HintRoutine()
        {
            hint.alpha = 0f;
            while (!session.HasStarter) yield return null;
            hintShown = true;
            for (float t = 0f; t < 0.5f; t += UIEase.DeltaTime) { hint.alpha = t / 0.5f; yield return null; }
            hint.alpha = 1f;
            for (float t = 0f; t < hintDuration; t += UIEase.DeltaTime) yield return null;
            for (float t = 0f; t < 0.8f; t += UIEase.DeltaTime) { hint.alpha = 1f - t / 0.8f; yield return null; }
            hint.gameObject.SetActive(false);
        }
    }
}
