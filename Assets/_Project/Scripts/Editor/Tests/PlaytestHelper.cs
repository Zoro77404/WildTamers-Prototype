using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using WildTamers.Core;
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

        /// <summary>Starts a fight against a wild animal of <paramref name="speciesId"/> with the default team.</summary>
        public static string StartBattle(string speciesId, int level)
        {
            var session = GameSession.Instance;
            var species = session.Database.Get(speciesId);
            var record = session.AddWildSpawn(species, level, default(GeoCoordinate), 0f, 100.0);
            return "started " + session.StartBattle(record, session.ChooseDefaultParty());
        }

        private static int botPicks;

        /// <summary>A bot that presses Attack (and now and then Skill) whenever the action buttons are ready, and Continue when the result sheet is up.</summary>
        public static void BotOn()
        {
            EditorApplication.update -= Bot;
            EditorApplication.update += Bot;
        }

        public static void BotOff() => EditorApplication.update -= Bot;

        private static void Bot()
        {
            if (!Application.isPlaying) { EditorApplication.update -= Bot; return; }
            var buttons = Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var skill = buttons.FirstOrDefault(b => b.name == "SkillButton" && b.IsInteractable());
            var attack = buttons.FirstOrDefault(b => b.name == "AttackButton" && b.IsInteractable());
            if (skill != null && botPicks++ % 3 == 0) { skill.onClick.Invoke(); return; }
            if (attack != null) { attack.onClick.Invoke(); return; }
        }

        /// <summary>Every visible text that still contains English letters (the Arabic check; "English", WASD and Q/E are fine).</summary>
        public static string LatinTexts()
        {
            var found = new System.Collections.Generic.List<string>();
            foreach (var t in Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!t.gameObject.activeInHierarchy || t.color.a < 0.01f) continue;
                var group = t.GetComponentInParent<CanvasGroup>();
                if (group != null && group.alpha < 0.01f) continue;
                string plain = System.Text.RegularExpressions.Regex.Replace(t.text ?? "", "<[^>]+>", "");
                if (System.Text.RegularExpressions.Regex.IsMatch(plain, "[A-Za-z]")) found.Add(GetPath(t.transform) + " = " + plain.Replace('\n', '|'));
            }
            return found.Count == 0 ? "none" : string.Join(" ;; ", found);
        }

        private static string GetPath(Transform t)
        {
            string path = t.name;
            for (int i = 0; i < 3 && t.parent != null; i++) { t = t.parent; path = t.name + "/" + path; }
            return path;
        }

        /// <summary>Names of every active button on screen.</summary>
        public static string Buttons() =>
            string.Join(", ", Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(b => b.IsInteractable() && b.gameObject.activeInHierarchy).Select(b => b.name).Distinct());
    }
}
