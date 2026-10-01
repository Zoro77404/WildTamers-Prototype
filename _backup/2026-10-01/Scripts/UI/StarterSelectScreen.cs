using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WildTamers.Animals;

namespace WildTamers.UI
{
    /// <summary>First-launch screen: pick 1 of 3 starter animals.</summary>
    public class StarterSelectScreen : UIPanel
    {
        [SerializeField] private StarterCard[] cards = new StarterCard[0];

        private bool choosing;

        public event Action<AnimalData> StarterChosen;

        public void Open(IReadOnlyList<AnimalData> starters, int level)
        {
            choosing = false;
            for (int i = 0; i < cards.Length; i++)
            {
                bool has = i < starters.Count && starters[i] != null;
                cards[i].gameObject.SetActive(has);
                if (has) cards[i].Setup(starters[i], level, OnCardChosen);
            }
            Show();
            StartCoroutine(StaggerIn());
        }

        private IEnumerator StaggerIn()
        {
            foreach (var c in cards) if (c.gameObject.activeSelf) c.transform.localScale = Vector3.zero;
            yield return null;
            for (int i = 0; i < cards.Length; i++)
            {
                if (!cards[i].gameObject.activeSelf) continue;
                StartCoroutine(PopCard(cards[i].transform, 0.12f * i));
            }
        }

        private static IEnumerator PopCard(Transform t, float delay)
        {
            for (float w = 0f; w < delay; w += Time.unscaledDeltaTime) yield return null;
            for (float e = 0f; e < 0.4f; e += Time.unscaledDeltaTime)
            {
                float s = UIEase.OutBack(e / 0.4f);
                t.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            t.localScale = Vector3.one;
        }

        private void OnCardChosen(StarterCard chosen)
        {
            if (choosing) return;
            choosing = true;
            StartCoroutine(ChooseRoutine(chosen));
        }

        private IEnumerator ChooseRoutine(StarterCard chosen)
        {
            foreach (var c in cards)
                if (c.gameObject.activeSelf) StartCoroutine(c.PlayResult(c == chosen));
            for (float t = 0f; t < 0.55f; t += Time.unscaledDeltaTime) yield return null;
            Hide();
            StarterChosen?.Invoke(chosen.Data);
        }
    }
}
