using UnityEngine;
using UnityEngine.UI;

namespace WildTamers.UI
{
    /// <summary>
    /// The small round pause button in the corner. It lives on an object that is always active, so it can open the
    /// <see cref="PauseMenu"/> even though the menu itself starts switched off.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class PauseButton : MonoBehaviour
    {
        [SerializeField] private PauseMenu menu;

        private void Awake() => GetComponent<Button>().onClick.AddListener(() => { if (menu != null) menu.Open(); });
    }
}
