using UnityEngine;
using UnityEngine.UI;
using WildTamers.Animals;
using WildTamers.Core;

namespace WildTamers.UI
{
    /// <summary>Shows a 3D animal in a RawImage: a live swaying preview or a cached still portrait.</summary>
    [RequireComponent(typeof(RawImage))]
    public class AnimalPreviewImage : MonoBehaviour
    {
        [SerializeField] private bool live = true;
        [SerializeField] private int resolution = 512;
        [Tooltip("Color behind the animal; match the card so anti-aliased edges blend in.")]
        [SerializeField] private Color background = Color.white;

        private RawImage image;
        private PreviewStudio.Handle handle;
        private AnimalData data;

        public AnimalData Animal => data;

        private void Awake() => image = GetComponent<RawImage>();

        public void SetAnimal(AnimalData animal)
        {
            data = animal;
            if (isActiveAndEnabled) Refresh();
        }

        private void OnEnable() => Refresh();

        private void OnDisable() => ReleaseHandle();

        private void Refresh()
        {
            if (image == null) image = GetComponent<RawImage>();
            if (data == null || !Application.isPlaying)
            {
                image.enabled = false;
                return;
            }
            var studio = GameSession.Instance.Previews;
            if (live)
            {
                if (handle == null || !handle.IsValid) handle = studio.AcquireLive(data, resolution, background);
                else studio.SetAnimal(handle, data);
                image.texture = handle.Texture;
            }
            else
            {
                image.texture = studio.GetPortrait(data, background);
            }
            image.enabled = image.texture != null;
        }

        private void ReleaseHandle()
        {
            if (handle == null) return;
            if (GameSession.Exists) GameSession.Instance.Previews.Release(handle);
            handle = null;
        }
    }
}
