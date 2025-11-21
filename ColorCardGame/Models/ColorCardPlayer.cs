using System.Collections.Generic;

namespace ColorCardGame.Models
{
    public class ColorCardPlayer
    {
        public string ConnectionId { get; set; }
        public string Name { get; set; }
        public string PlayerId { get; set; }
        public List<ColorCard> Hand { get; set; } = new();
        public int SeatPosition { get; set; }
        public bool IsActive { get; set; } = true;
        public bool HasCalledUno { get; set; } = false;
    }
}