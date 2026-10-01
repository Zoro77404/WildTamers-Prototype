using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WildTamers.Battle
{
    /// <summary>One of the four big battle buttons: icon, label, small hint line and an optional counter badge.</summary>
    public class BattleActionButton : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text label;
        [SerializeField] private TMP_Text hint;
        [SerializeField] private Image face;
        [SerializeField] private Image lip;
        [SerializeField] private GameObject badge;
        [SerializeField] private TMP_Text badgeText;

        public event Action Clicked;

        /// <summary>True when a tap would work (also respects a parent CanvasGroup turning input off).</summary>
        public bool Interactable => button != null && button.IsInteractable();

        private void Awake()
        {
            if (button != null) button.onClick.AddListener(() => Clicked?.Invoke());
        }

        public void Set(string labelText, string hintText)
        {
            if (label != null) label.text = labelText;
            if (hint != null) hint.text = hintText;
        }

        public void SetInteractable(bool value)
        {
            if (button != null) button.interactable = value;
        }

        /// <summary>Shows a small number badge (e.g. skill turns left); null hides it.</summary>
        public void SetBadge(string text)
        {
            if (badge == null) return;
            badge.SetActive(!string.IsNullOrEmpty(text));
            if (badgeText != null && text != null) badgeText.text = text;
        }

        /// <summary>Same as a tap (keyboard shortcut).</summary>
        public void Press()
        {
            if (Interactable) button.onClick.Invoke();
        }
    }
}
