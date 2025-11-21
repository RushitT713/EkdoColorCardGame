namespace ColorCardGame.Models
{
    public class ColorCard
    {
        public CardColor Color { get; set; }
        public CardValue Value { get; set; }

        public override string ToString() => $"{Color}_{Value}";

        public bool CanPlayOn(ColorCard topCard, CardColor? currentColor = null)
        {
            if (Color == CardColor.Wild) return true;

            if (currentColor.HasValue && Color == currentColor.Value) return true;
            if (Color == topCard.Color) return true;
            if (Value == topCard.Value) return true;

            return false;
        }

        public int GetPoints()
        {
            return Value switch
            {
                CardValue.Skip or CardValue.Reverse or CardValue.DrawTwo => 20,
                CardValue.Wild or CardValue.WildDrawFour => 50,
                _ => (int)Value
            };
        }
    }
}