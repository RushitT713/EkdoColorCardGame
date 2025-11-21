namespace ColorCardGame.Models
{
    public class PlayerAction
    {
        public string Action { get; set; }
        public int? CardIndex { get; set; }
        public CardColor? ChosenColor { get; set; }
    }
}