using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WildTamers.Animals;
using WildTamers.Core;
using WildTamers.Lang;
using WildTamers.Audio;

namespace WildTamers.UI
{
    /// <summary>
    /// The animal info card: live 3D model, name, description and history.
    /// Opens as "New animal!" (with a little celebration) when an animal joins for the first time,
    /// or as plain "Animal info" when you tap an animal in the Team screen.
    /// </summary>
    public class AnimalCardPanel : UIPanel
    {
        [SerializeField] private AnimalPreviewImage preview;
        [SerializeField] private Image previewBackdrop;
        [SerializeField] private RectTransform header;
        [SerializeField] private Image headerPill;
        [SerializeField] private TMP_Text headerText;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text styleText;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private TMP_Text historyText;
        [SerializeField] private Button closeButton;
        [SerializeField] private TMP_Text closeLabel;
        [SerializeField] private Button backdropButton;
        [Tooltip("Little stars around the picture that twinkle on a new animal.")]
        [SerializeField] private RectTransform[] sparkles = new RectTransform[0];
        [SerializeField] private Color newColor = new Color32(0xFF, 0x8A, 0x3D, 0xFF);
        [SerializeField] private Color infoColor = new Color32(0x2E, 0xC4, 0xB6, 0xFF);

        private Action onClosed;
        private bool isNew;
        private float shownAt;
        private AnimalData shownData;
        private int shownLevel;

        protected override void Awake()
        {
            base.Awake();
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (backdropButton != null) backdropButton.onClick.AddListener(Close);
        }

        /// <summary>"New animal!" card for a species the player gets for the first time. Marks it as seen.</summary>
        public void OpenNew(AnimalData data, Action closed = null)
        {
            if (data == null) { closed?.Invoke(); return; }
            Fill(data, level: 0, isNew: true, closed);
            if (GameSession.Exists) GameSession.Instance.MarkSeen(data.id);
        }

        /// <summary>The same card without the celebration (tap an animal in the Team screen).</summary>
        public void OpenInfo(AnimalData data, int level = 0, Action closed = null)
        {
            if (data == null) { closed?.Invoke(); return; }
            Fill(data, level, isNew: false, closed);
        }

        private void Fill(AnimalData data, int level, bool isNew, Action closed)
        {
            this.isNew = isNew;
            onClosed = closed;
            shownAt = Time.unscaledTime;
            shownData = data;
            shownLevel = level;

            RefreshTexts();
            if (headerPill != null) headerPill.color = isNew ? newColor : infoColor;
            if (levelText != null)
            {
                // The text sits inside a pill; hide the whole pill when no level is shown.
                var pill = levelText.transform.parent;
                (pill != null ? pill.gameObject : levelText.gameObject).SetActive(level > 0);
            }
            if (previewBackdrop != null) previewBackdrop.color = Color.Lerp(data.themeColor, Color.white, 0.68f);
            preview.SetAnimal(data);
            foreach (var s in sparkles) if (s != null) s.gameObject.SetActive(isNew);
            if (isNew) AudioManager.Play(Sfx.Pop);
            Show();
            if (isNew && header != null) StartCoroutine(HeaderPop());
        }

        /// <summary>Every text of the card in the current language (also runs when the language is switched while it is open).</summary>
        private void RefreshTexts()
        {
            if (shownData == null) return;
            headerText.text = Loc.T(isNew ? "card.new" : "card.info");
            nameText.text = shownData.LocalizedName;
            styleText.text = shownData.LocalizedStyle;
            if (levelText != null) levelText.text = Loc.Level(shownLevel);
            descriptionText.text = shownData.LocalizedDescription;
            historyText.text = shownData.LocalizedHistory;
            if (closeLabel != null) closeLabel.text = Loc.T(isNew ? "card.awesome" : "common.close");
        }

        protected override void OnShown()
        {
            Loc.LanguageChanged -= RefreshTexts;
            Loc.LanguageChanged += RefreshTexts;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            Loc.LanguageChanged -= RefreshTexts;
        }

        private void Close()
        {
            if (!IsVisible) return;
            Hide();
        }

        protected override void OnHidden()
        {
            Loc.LanguageChanged -= RefreshTexts;
            var callback = onClosed;
            onClosed = null;
            callback?.Invoke();
        }

        private void Update()
        {
            if (!IsVisible || !isNew) return;
            float t = Time.unscaledTime - shownAt;
            for (int i = 0; i < sparkles.Length; i++)
            {
                if (sparkles[i] == null) continue;
                float phase = t * 3.2f + i * 1.7f;
                float s = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(phase));
                sparkles[i].localScale = new Vector3(s, s, 1f);
                sparkles[i].localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(phase * 0.7f) * 25f);
            }
        }

        private IEnumerator HeaderPop()
        {
            for (float t = 0f; t < 0.55f; t += UIEase.DeltaTime)
            {
                float p = t / 0.55f;
                float s = Mathf.LerpUnclamped(0.4f, 1f, UIEase.OutBack(p, 3f));
                header.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            header.localScale = Vector3.one;
        }
    }
}
