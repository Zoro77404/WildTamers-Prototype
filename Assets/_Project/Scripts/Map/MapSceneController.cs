using System.Collections;
using UnityEngine;
using WildTamers.Animals;
using WildTamers.Core;
using WildTamers.Player;
using WildTamers.UI;

namespace WildTamers.Map
{
    /// <summary>
    /// Glue for the map scene: starter pick on first launch, tap-to-move, tapping wild animals
    /// (popup when in range, "Get closer" when not, then hand-off to the battle scene) and the battle result message.
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
        [SerializeField] private StarterSelectScreen starterScreen;
        [SerializeField] private EncounterPopup encounterPopup;
        [SerializeField] private ToastMessage toast;

        private GameSession session;
        private WildAnimal engaged;

        private void Start()
        {
            session = GameSession.Instance;
            pointerInput.AnimalTapped += OnAnimalTapped;
            pointerInput.GroundTapped += OnGroundTapped;
            starterScreen.StarterChosen += OnStarterChosen;

            if (!session.HasStarter)
                starterScreen.Open(session.Database.Starters, session.Config.starterLevel);
            else
                starterScreen.HideImmediate();
            encounterPopup.HideImmediate();
            if (session.TryTakeMapMessage(out var message, out bool warning)) StartCoroutine(ShowMessageSoon(message, warning));
        }

        private IEnumerator ShowMessageSoon(string message, bool warning)
        {
            // Wait for the scene fade-in so the message is not missed.
            for (float t = 0f; t < 0.45f; t += UIEase.DeltaTime) yield return null;
            toast.Show(message, warning, 2.4f);
        }

        private void OnDestroy()
        {
            if (pointerInput != null)
            {
                pointerInput.AnimalTapped -= OnAnimalTapped;
                pointerInput.GroundTapped -= OnGroundTapped;
            }
            if (starterScreen != null) starterScreen.StarterChosen -= OnStarterChosen;
        }

        private void OnStarterChosen(AnimalData starter)
        {
            var animal = session.AddToTeam(starter, session.Config.starterLevel);
            toast.Show($"{animal.Name} joined your team!", warning: false, duration: 2.2f);
        }

        private void OnGroundTapped(Vector3 point)
        {
            if (fakeLocation != null && session.HasStarter) fakeLocation.SetDestination(point);
        }

        private void OnAnimalTapped(WildAnimal animal)
        {
            if (!session.HasStarter || encounterPopup.IsVisible || animal == null || animal.IsLeaving) return;

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
            encounterPopup.Open(animal.Data, animal.Level, OnFight, OnLeave);
        }

        private void OnFight()
        {
            var record = engaged != null ? session.FindWildSpawn(engaged.SpawnId) : null;
            if (record == null || !session.StartBattle(record))
            {
                encounterPopup.Hide();
                OnLeave();
                toast.Show("It ran away!", warning: true);
            }
        }

        private void OnLeave()
        {
            if (engaged != null) engaged.IsEngaged = false;
            engaged = null;
        }
    }
}
