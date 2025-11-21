using System.Collections.Generic;

namespace ColorCardGame.Models
{
    public class ColorCardGameState
    {
        public List<ColorCard> Deck { get; set; } = new();
        public List<ColorCard> DiscardPile { get; set; } = new();
        public ColorCard TopCard => DiscardPile.Count > 0 ? DiscardPile[^1] : null;

        public int CurrentPlayerIndex { get; set; }
        public bool IsClockwise { get; set; } = true;
        public CardColor? CurrentColor { get; set; }

        public GamePhase Phase { get; set; } = GamePhase.Waiting;
        public List<string> GameLog { get; set; } = new();

        public int DrawStackCount { get; set; } = 0;
    }
}