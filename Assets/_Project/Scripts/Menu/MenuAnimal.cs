using System.Collections;
using UnityEngine;
using WildTamers.Animals;

namespace WildTamers.Menu
{
    /// <summary>
    /// The animals idling in the main-menu background. Each spot gets its species model and loops its idle animation;
    /// every so often an animal does something small (eats, hops, looks around) so the scene feels alive.
    /// </summary>
    public class MenuAnimal : MonoBehaviour
    {
        [SerializeField] private string animalId;
        [SerializeField] private float scale = 1.6f;
        [Tooltip("Animator states this animal may play now and then (besides the idle loop).")]
        [SerializeField] private string[] extras = { AnimalVisual.Eat };
        [SerializeField] private Vector2 pauseBetween = new Vector2(3f, 8f);

        private AnimalVisual visual;

        public string AnimalId => animalId;

        private IEnumerator Start()
        {
            var database = AnimalDatabase.Instance;
            var data = database != null ? database.Get(animalId) : null;
            if (data == null || data.prefab == null) yield break;

            var model = Instantiate(data.prefab, transform);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one * scale;
            visual = model.GetComponent<AnimalVisual>();
            if (visual == null) yield break;
            visual.PlayIdle(randomStart: true);

            while (true)
            {
                yield return new WaitForSeconds(Random.Range(pauseBetween.x, pauseBetween.y));
                if (extras == null || extras.Length == 0) continue;
                string state = extras[Random.Range(0, extras.Length)];
                if (!visual.HasState(state)) continue;
                visual.Play(state);
                // One-shot states return to idle by themselves in the controllers; looping ones (Eat) need a nudge.
                yield return new WaitForSeconds(Random.Range(2.2f, 4f));
                visual.Play(AnimalVisual.Idle, 0.3f);
            }
        }
    }
}
