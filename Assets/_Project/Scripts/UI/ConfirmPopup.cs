using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WildTamers.Lang;

namespace WildTamers.UI
{
    /// <summary>
    /// "Are you sure?" popup with a title, a text, a Cancel button and a confirm button (for example "Yes, reset").
    /// Texts are given as string table keys, so the popup follows the language even while it is open.
    /// </summary>
    public class ConfirmPopup : UIPanel
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private TMP_Text confirmLabel;
        [SerializeField] private TMP_Text cancelLabel;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;
        [Tooltip("Tapping the dimmed background cancels.")]
        [SerializeField] private Button backdropButton;

        private Action onConfirm;
        private Action onCancel;
        private string titleKey, bodyKey, confirmKey;
        private bool resolved;

        protected override void Awake()
        {
            base.Awake();
            confirmButton.onClick.AddListener(Confirm);
            cancelButton.onClick.AddListener(Cancel);
            ClickSound.Hook(backdropButton);
            if (backdropButton != null) backdropButton.onClick.AddListener(Cancel);
        }

        /// <param name="title">Key of the title text in the UI string table.</param>
        /// <param name="body">Key of the explanation.</param>
        /// <param name="confirm">Key of the confirm button's label.</param>
        public void Open(string title, string body, string confirm, Action confirmed, Action cancelled = null)
        {
            titleKey = title;
            bodyKey = body;
            confirmKey = confirm;
            onConfirm = confirmed;
            onCancel = cancelled;
            resolved = false;
            confirmButton.interactable = true;
            cancelButton.interactable = true;
            RefreshTexts();
            Show();
            transform.SetAsLastSibling();
        }

        private void RefreshTexts()
        {
            if (titleKey == null) return;
            titleText.text = Loc.T(titleKey);
            bodyText.text = Loc.T(bodyKey);
            confirmLabel.text = Loc.T(confirmKey);
            cancelLabel.text = Loc.T("confirm.cancel");
        }

        protected override void OnShown()
        {
            Loc.LanguageChanged -= RefreshTexts;
            Loc.LanguageChanged += RefreshTexts;
        }

        protected override void OnHidden() => Loc.LanguageChanged -= RefreshTexts;

        protected override void OnDisable()
        {
            base.OnDisable();
            Loc.LanguageChanged -= RefreshTexts;
        }

        private void Confirm()
        {
            if (resolved) return;
            resolved = true;
            Hide();
            onConfirm?.Invoke();
        }

        private void Cancel()
        {
            if (resolved) return;
            resolved = true;
            Hide();
            onCancel?.Invoke();
        }
    }
}
