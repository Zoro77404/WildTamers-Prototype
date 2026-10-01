using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WildTamers.Animals;

namespace WildTamers.UI
{
    /// <summary>One animal in the team list: portrait, name, level and HP.</summary>
    public class TeamRow : MonoBehaviour
    {
        [SerializeField] private AnimalPreviewImage portrait;
        [SerializeField] private Image portraitBackdrop;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private StatBar hpBar;
        [SerializeField] private TMP_Text hpText;
        [SerializeField] private GameObject activeBadge;
        [SerializeField] private Image highlight;

        public void Setup(AnimalInstance animal, bool isActive)
        {
            var data = animal.Data;
            nameText.text = animal.Name;
            levelText.text = $"Lv. {animal.Level}";
            hpBar.SetValue(animal.HPFraction, null, instant: true);
            hpText.text = $"HP {animal.CurrentHP}/{animal.MaxHP}";
            if (activeBadge != null) activeBadge.SetActive(isActive);
            if (highlight != null) highlight.enabled = isActive;
            if (data != null)
            {
                if (portraitBackdrop != null) portraitBackdrop.color = Color.Lerp(data.themeColor, Color.white, 0.65f);
                portrait.SetAnimal(data);
            }
        }
    }
}
