using System.Collections.Generic;

namespace WildTamers.Core
{
    /// <summary>
    /// Lets modal UI (starter screen, popups, team list) pause map input such as walking and tapping.
    /// Each blocker registers itself; input is blocked while any blocker is registered.
    /// </summary>
    public static class MapInputGate
    {
        private static readonly HashSet<object> Blockers = new HashSet<object>();

        public static bool IsBlocked
        {
            get
            {
                // Destroyed Unity objects compare equal to null; drop them so a missed Unblock can't lock input forever.
                Blockers.RemoveWhere(b => b == null || (b is UnityEngine.Object o && o == null));
                return Blockers.Count > 0;
            }
        }

        public static void Block(object owner) => Blockers.Add(owner);
        public static void Unblock(object owner) => Blockers.Remove(owner);
        public static void Clear() => Blockers.Clear();
    }
}
