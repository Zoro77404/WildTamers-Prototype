using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace WildTamers.Core
{
    /// <summary>Full-screen fade used for every scene change. Owned by GameSession.</summary>
    public class SceneFader : MonoBehaviour
    {
        [SerializeField] private float fadeDuration = 0.35f;
        [SerializeField] private Color fadeColor = new Color32(0x22, 0x2E, 0x45, 0xFF);

        private CanvasGroup group;

        public bool IsBusy { get; private set; }

        private void Awake()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            group = gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            gameObject.AddComponent<GraphicRaycaster>();

            var img = new GameObject("Fade", typeof(RectTransform), typeof(Image));
            img.transform.SetParent(transform, false);
            var rt = (RectTransform)img.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            img.GetComponent<Image>().color = fadeColor;
        }

        public void LoadScene(string sceneName)
        {
            if (IsBusy) return;
            StartCoroutine(LoadRoutine(sceneName));
        }

        private IEnumerator LoadRoutine(string sceneName)
        {
            IsBusy = true;
            group.blocksRaycasts = true;
            yield return Fade(0f, 1f);

            var op = SceneManager.LoadSceneAsync(sceneName);
            while (op != null && !op.isDone) yield return null;
            yield return null; // let the new scene run Start()

            yield return Fade(1f, 0f);
            group.blocksRaycasts = false;
            IsBusy = false;
        }

        private IEnumerator Fade(float from, float to)
        {
            for (float t = 0f; t < fadeDuration; t += Mathf.Min(Time.unscaledDeltaTime, 1f / 30f)) // a load hitch must not skip the fade
            {
                group.alpha = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / fadeDuration));
                yield return null;
            }
            group.alpha = to;
        }
    }
}
