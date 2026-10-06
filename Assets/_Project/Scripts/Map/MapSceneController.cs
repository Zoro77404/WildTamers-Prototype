using System.Collections;
using UnityEngine;
using WildTamers.Animals;
using WildTamers.Core;
using WildTamers.Player;
using WildTamers.UI;

namespace WildTamers.Map
{
    /// <summary>
    /// Glue for the map scene: "New animal!" cards for animals you haven't met yet, tap-to-move, tapping wild animals
    /// (popup when in range, "Get closer" when not), the team select screen and the hand-off to the battle scene,
    /// plus the battle result message.
    /// </summary>
    public class MapSceneController : MonoBehaviour
    {
        [Header("World")]
        [SerializeField] private MapPointerInput pointerInput;
        [Tooltip("Only used for click-to-move; a GPS provider ignores taps on the ground.")]
        [SerializeField] private FakeLocationProvider fakeLocation;
        [SerializeField] private PlayerAvatar player;
        [SerializeField] private RangeIndicator rangeIndicator;
        [SerializeField] private WildAnimalSpawner spawner;

        [Header("UI")]
        [SerializeField] private AnimalCardPanel animalCard;
        [SerializeField] private EncounterPopup encounterPopup;
        [SerializeField] private TeamSelectScreen teamSelect;
        [SerializeField] private ToastMessage toast;

        private GameSession session;
        private WildAnimal engaged;

        /// <summary>True while a "New animal!" card is on screen or waiting to be shown (the map ignores taps then).</summary>
        public bool ShowingCards { get; private set; }

        private void Start()
        {
            session = GameSession.Instance;
            pointerInput.AnimalTapped += OnAnimalTapped;
            pointerInput.GroundTapped += OnGroundTapped;

            encounterPopup.HideImmediate();
            teamSelect.HideImmediate();
            animalCard.HideImmediate();
            StartCoroutine(StartupRoutine());
        }

        private IEnumerator StartupRoutine()
        {
            // Wait for the scene fade-in so nothing is missed.
            ShowingCards = true;
            for (float t = 0f; t < 0.45f; t += UIEase.DeltaTime) yield return null;

            // Every animal you own but haven't met yet gets its card (all three starters on a new game).
            while (true)
            {
                var unseen = session.GetUnseenSpecies();
                if (unseen.Count == 0) break;
                bool closed = false;
                animalCard.OpenNew(unseen[0], () => closed = true);
                while (!closed) yield return null;
                for (float t = 0f; t < 0.2f; t += UIEase.DeltaTime) yield return null;
            }
            ShowingCards = false;

            if (session.TryTakeMapMessage(out var message, out bool warning)) toast.Show(message, warning, 2.4f);
        }

        private void OnDestroy()
        {
            if (pointerInput != null)
            {
                pointerInput.AnimalTapped -= OnAnimalTapped;
                pointerInput.GroundTapped -= OnGroundTapped;
            }
        }

        private void OnGroundTapped(Vector3 point)
        {
            if (fakeLocation != null && session.HasStarter) fakeLocation.SetDestination(point);
        }

        private void OnAnimalTapped(WildAnimal animal)
        {
            if (!session.HasStarter || ShowingCards || encounterPopup.IsVisible || teamSelect.IsVisible || animal == null || animal.IsLeaving) return;

            var a = animal.transform.position; a.y = 0f;
            var p = player.transform.position; p.y = 0f;
            float distance = Vector3.Distance(a, p);
            float range = session.Config.fightRange;

            if (distance > range)
            {
                toast.Show($"Get closer!  ({Mathf.CeilToInt(distance - range)} m to go)", warning: true);
                if (rangeIndicator != null) rangeIndicator.FlashWarning();
                animal.ReactTooFar();
                return;
            }

            engaged = animal;
            animal.IsEngaged = true;
            animal.ReactSelected();
            if (fakeLocation != null) fakeLocation.ClearDestination();
            OpenEncounter();
        }

        private void OpenEncounter() => encounterPopup.Open(engaged.Data, engaged.Level, OnFightPressed, OnLeave);

        /// <summary>"Fight!" on the encounter popup: pick the team first.</summary>
        private void OnFightPressed()
        {
            var record = engaged != null ? session.FindWildSpawn(engaged.SpawnId) : null;
            if (record == null)
            {
                encounterPopup.Hide();
                OnLeave();
                toast.Show("It ran away!", warning: true);
                return;
            }

            encounterPopup.Hide();
            teamSelect.Open(engaged.Data, record.level, party => OnTeamChosen(record, party), OnTeamSelectBack);
        }

        private void OnTeamChosen(WildSpawnRecord record, System.Collections.Generic.IReadOnlyList<AnimalInstance> party)
        {
            if (session.StartBattle(record, party)) return;
            teamSelect.Hide();
            OnLeave();
            toast.Show("It ran away!", warning: true);
        }

        /// <summary>Back on the team select screen returns to the encounter popup.</summary>
        private void OnTeamSelectBack()
        {
            if (engaged != null && !engaged.IsLeaving) OpenEncounter();
            else OnLeave();
        }

        private void OnLeave()
        {
            if (engaged != null) engaged.IsEngaged = false;
            engaged = null;
        }
    }
}
