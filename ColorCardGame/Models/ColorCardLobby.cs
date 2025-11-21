using System.Collections.Generic;

namespace ColorCardGame.Models
{
    public class ColorCardLobby
    {
        public string LobbyCode { get; set; }
        public List<ColorCardPlayer> Players { get; set; } = new();
        public ColorCardGameState GameState { get; set; } = new();
        public string CreatorConnectionId { get; set; }
        public bool IsGameStarted { get; set; } = false;
    }
}