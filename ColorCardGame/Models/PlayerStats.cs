namespace ColorCardGame.Models
{
    public class PlayerStats
    {
        public int Id { get; set; }
        public int PlayerId { get; set; }
        public virtual Player Player { get; set; }

        public int TotalGames { get; set; } = 0;
        public int Wins { get; set; } = 0;
        public int Losses { get; set; } = 0;

        public decimal WinRate => TotalGames > 0 ? (decimal)Wins / TotalGames * 100 : 0;
    }
}