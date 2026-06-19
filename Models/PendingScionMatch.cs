namespace FaeLightCards
{
    public class PendingScionMatch
    {
        public Player Player { get; set; } = null!;
        public Card Card { get; set; } = null!;
        public int SlotIndex { get; set; }

        public PendingScionMatch(Player player, Card card, int slotIndex)
        {
            Player = player;
            Card = card;
            SlotIndex = slotIndex;
        }
    }
}
