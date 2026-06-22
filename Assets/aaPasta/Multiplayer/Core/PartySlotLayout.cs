using System.Collections.Generic;

namespace ExoBeasts.Multiplayer.Core
{
    /// <summary>
    /// Canonical slot layout shared by selection, spawn, commander resolution and build UI.
    /// Mirrors the layout already enforced by SelecaoManager.
    /// </summary>
    public static class PartySlotLayout
    {
        private static readonly int[] AllSlots = { 0, 1, 2, 3, 4, 5, 6, 7 };
        private static readonly string[] CharacterDisplayNameValues = { "Coruja", "Samurai" };

        public static IReadOnlyList<string> CharacterDisplayNames => CharacterDisplayNameValues;

        public static string GetCharacterDisplayName(int characterIndex)
        {
            return characterIndex >= 0 && characterIndex < CharacterDisplayNameValues.Length
                ? CharacterDisplayNameValues[characterIndex]
                : characterIndex.ToString();
        }

        public static List<int> GetSlots(int totalPlayers, int playerIndex)
        {
            if (playerIndex < 0)
                return new List<int>(AllSlots);

            if (totalPlayers == 2)
            {
                if (playerIndex == 0) return new List<int> { 0, 1, 4, 5 };
                if (playerIndex == 1) return new List<int> { 2, 3, 6, 7 };
            }
            else if (totalPlayers == 3)
            {
                if (playerIndex == 0) return new List<int> { 0, 1, 4, 5 };
                if (playerIndex == 1) return new List<int> { 2, 3 };
                if (playerIndex == 2) return new List<int> { 6, 7 };
            }
            else if (totalPlayers == 4)
            {
                if (playerIndex == 0) return new List<int> { 0, 1 };
                if (playerIndex == 1) return new List<int> { 2, 3 };
                if (playerIndex == 2) return new List<int> { 4, 5 };
                if (playerIndex == 3) return new List<int> { 6, 7 };
            }

            return new List<int>(AllSlots);
        }

        public static int GetCommanderSlot(int totalPlayers, int playerIndex)
        {
            List<int> slots = GetSlots(totalPlayers, playerIndex);
            return slots.Count > 0 ? slots[0] : 0;
        }

        public static List<int> GetTowerSlots(int totalPlayers, int playerIndex)
        {
            List<int> slots = GetSlots(totalPlayers, playerIndex);
            if (slots.Count > 0)
                slots.RemoveAt(0);
            return slots;
        }
    }
}
