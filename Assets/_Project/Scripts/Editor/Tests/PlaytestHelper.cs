using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using WildTamers.Lang;

namespace WildTamers.EditorTools
{
    /// <summary>Small helpers for play-testing through the Unity MCP (execute_code): press a button by name, switch language, list what is on screen.</summary>
    public static class PlaytestHelper
    {
        /// <summary>Presses the active, interactable button whose object is called <paramref name="name"/> (first match).</summary>
        public static string Click(string name)
        {
            var button = Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .FirstOrDefault(b => b.name == name && b.IsInteractable());
            if (button == null) return "no active button named " + name;
            button.onClick.Invoke();
            return "clicked " + name;
        }

        public static string Language(bool arabic)
        {
            Loc.SetLanguage(arabic ? GameLanguage.Arabic : GameLanguage.English);
            return Loc.Language.ToString();
        }

        /// <summary>Names of every active button on screen.</summary>
        public static string Buttons() =>
            string.Join(", ", Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(b => b.IsInteractable() && b.gameObject.activeInHierarchy).Select(b => b.name).Distinct());
    }
}
