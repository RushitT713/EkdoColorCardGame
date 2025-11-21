using System.Collections.Generic;

namespace ColorCardGame.Models
{
    public class GameResult
    {
        public string WinnerName { get; set; }
        public string WinnerId { get; set; }
        public List<PlayerScore> FinalScores { get; set; } = new();
    }

    public class PlayerScore
    {
        public string Name { get; set; }
        public int CardsLeft { get; set; }
        public int Points { get; set; }
    }
}