using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WildTamers.Animals;

namespace WildTamers.UI
{
    /// <summary>One choice on the starter screen: live preview, name, style, stats and a Choose button.</summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class StarterCard : MonoBehaviour
    {
        [SerializeField] private AnimalPreviewImage preview;
        [SerializeField] private Image previewBackdrop;
        [SerializeField] private Image accentStripe;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text styleText;
        [SerializeField] private TMP_Text moveText;
        [SerializeField] private AnimalStatsView stats;
        [SerializeField] private Button chooseButton;
        [SerializeField] private Image chooseButtonImage;
        [SerializeField] private Image chooseButtonLip;

        private CanvasGroup group;
        private AnimalData data;
        private Action<StarterCard> onChosen;

        public AnimalData Data => data;

        private void Awake()
        {
            group = GetComponent<CanvasGroup>();
            chooseButton.onClick.AddListener(() => onChosen?.Invoke(this));
        }

        public void Setup(AnimalData animal, int level, Action<StarterCard> chosen)
        {
            if (group == null) Awake();
            data = animal;
            onChosen = chosen;
            group.alpha = 1f;
            group.interactable = true;
            transform.localScale = Vector3.one;

            nameText.text = animal.displayName;
            styleText.text = animal.styleLabel;
            if (moveText != null) moveText.text = $"{animal.normalAttackName}  •  <color=#{ColorUtility.ToHtmlStringRGB(Darken(animal.themeColor))}>{animal.skill.skillName}</color>";
            var tint = animal.themeColor;
            if (accentStripe != null) accentStripe.color = tint;
            if (previewBackdrop != null) previewBackdrop.color = Color.Lerp(tint, Color.white, 0.72f);
            if (chooseButtonImage != null) chooseButtonImage.color = tint;
            if (chooseButtonLip != null) chooseButtonLip.color = Darken(tint);
            preview.SetAnimal(animal);
            stats.Show(animal, level, instant: false);
        }

        public void SetInteractable(bool value) => group.interactable = value;

        /// <summary>Selected: bounce up. Not selected: fade back.</summary>
        public IEnumerator PlayResult(bool selected)
        {
            group.interactable = false;
            for (float t = 0f; t < 0.45f; t += Time.unscaledDeltaTime)
            {
                float p = t / 0.45f;
                if (selected)
                {
                    float s = 1f + Mathf.Sin(p * Mathf.PI) * 0.06f;
                    transform.localScale = new Vector3(s, s, 1f);
                }
                else
                {
                    group.alpha = Mathf.Lerp(1f, 0.35f, p);
                    transform.localScale = Vector3.one * Mathf.Lerp(1f, 0.96f, p);
                }
                yield return null;
            }
        }

        private static Color Darken(Color c) => Color.Lerp(c, Color.black, 0.25f);
    }
}
