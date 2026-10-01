using UnityEngine;
using WildTamers.Animals;

namespace WildTamers.UI
{
    /// <summary>Four stat bars (HP / ATK / DEF / SPD) for a species at a level.</summary>
    public class AnimalStatsView : MonoBehaviour
    {
        // Bars are scaled against these level-1 maxima so species compare at a glance.
        private const float MaxHP = 60f, MaxStat = 20f;

        [SerializeField] private StatBar hp;
        [SerializeField] private StatBar attack;
        [SerializeField] private StatBar defense;
        [SerializeField] private StatBar speed;

        public void Show(AnimalData data, int level, bool instant = false)
        {
            if (data == null) return;
            Set(hp, data.maxHP / MaxHP, data.GetMaxHP(level), instant);
            Set(attack, data.attack / MaxStat, data.GetAttack(level), instant);
            Set(defense, data.defense / MaxStat, data.GetDefense(level), instant);
            Set(speed, data.speed / MaxStat, data.GetSpeed(level), instant);
        }

        private static void Set(StatBar bar, float normalized, int value, bool instant)
        {
            if (bar != null) bar.SetValue(normalized, value.ToString(), instant);
        }
    }
}
