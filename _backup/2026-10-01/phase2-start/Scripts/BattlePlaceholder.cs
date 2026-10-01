using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WildTamers.Animals;
using WildTamers.Core;
using WildTamers.UI;

namespace WildTamers.Battle
{
    /// <summary>
    /// Phase 1 stand-in for the battle: shows both animals from the GameSession hand-off
    /// and a Back button to return to the map. The real turn-based battle replaces this in Phase 2.
    /// </summary>
    public class BattlePlaceholder : MonoBehaviour
    {
        [Header("Arena")]
        [SerializeField] private Transform playerSpot;
        [SerializeField] private Transform wildSpot;
        [Tooltip("Display scale of the player's animal (nearer the camera).")]
        [SerializeField] private float playerScale = 1.7f;
        [Tooltip("Display scale of the wild animal (farther away, so a bit larger).")]
        [SerializeField] private float wildScale = 2.05f;
        [Tooltip("Tall animals (antlers, coiled snake) shrink to this height so they stay in frame and clear of the UI cards.")]
        [SerializeField] private float playerMaxHeight = 3.4f;
        [SerializeField] private float wildMaxHeight = 3.8f;

        [Header("UI")]
        [SerializeField] private TMP_Text wildName;
        [SerializeField] private TMP_Text wildLevel;
        [SerializeField] private StatBar wildHp;
        [SerializeField] private TMP_Text playerName;
        [SerializeField] private TMP_Text playerLevel;
        [SerializeField] private StatBar playerHp;
        [SerializeField] private TMP_Text playerHpText;
        [SerializeField] private TMP_Text logText;
        [SerializeField] private Button backButton;

        private void Start()
        {
            var session = GameSession.Instance;
            if (session.CurrentBattle == null) session.CreateDebugBattle();
            var battle = session.CurrentBattle;
            if (battle == null)
            {
                logText.text = "No animals found.";
                backButton.onClick.AddListener(session.ReturnToMap);
                return;
            }

            var wild = battle.Wild;
            var mine = battle.Player;

            wildName.text = wild.Name;
            wildLevel.text = $"Lv. {wild.Level}";
            wildHp.SetValue(wild.HPFraction, null, instant: true);
            playerName.text = mine.Name;
            playerLevel.text = $"Lv. {mine.Level}";
            playerHp.SetValue(mine.HPFraction, null, instant: true);
            if (playerHpText != null) playerHpText.text = $"{mine.CurrentHP}/{mine.MaxHP}";
            logText.text = $"A wild {wild.Name} wants to fight!\n<size=75%><alpha=#AA>Turn-based battles arrive in Phase 2.</size>";

            StartCoroutine(Enter(Spawn(wild.Data, wildSpot, wildScale, wildMaxHeight), 0.15f, fromAbove: true));
            StartCoroutine(Enter(Spawn(mine.Data, playerSpot, playerScale, playerMaxHeight), 0.45f, fromAbove: false));

            backButton.onClick.AddListener(session.ReturnToMap);
        }

        private static Transform Spawn(AnimalData data, Transform spot, float scale, float maxHeight)
        {
            if (data == null || data.prefab == null || spot == null) return null;
            var hopRoot = new GameObject("Hop").transform;
            hopRoot.SetParent(spot, false);
            hopRoot.gameObject.AddComponent<IdleHop>();
            var model = Instantiate(data.prefab, hopRoot);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one * scale;
            var visual = model.GetComponent<AnimalVisual>();
            if (visual != null)
            {
                // Measured before the spot is zeroed for the entrance pop.
                float height = visual.GetBounds().size.y;
                if (height > maxHeight) model.transform.localScale *= maxHeight / height;
                visual.PlayIdle(randomStart: true);
            }
            spot.localScale = Vector3.zero;
            return spot;
        }

        private static IEnumerator Enter(Transform spot, float delay, bool fromAbove)
        {
            if (spot == null) yield break;
            var rest = spot.localPosition;
            for (float t = 0f; t < delay; t += Mathf.Min(Time.deltaTime, 1f / 30f)) yield return null;
            const float duration = 0.55f;
            for (float t = 0f; t < duration; t += Mathf.Min(Time.deltaTime, 1f / 30f))
            {
                float p = t / duration;
                float s = UIEase.OutBack(p);
                spot.localScale = new Vector3(s, s, s);
                float offset = (1f - UIEase.OutCubic(p)) * (fromAbove ? 3f : 0f);
                spot.localPosition = rest + Vector3.up * offset;
                yield return null;
            }
            spot.localScale = Vector3.one;
            spot.localPosition = rest;
        }
    }
}
