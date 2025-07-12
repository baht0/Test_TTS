namespace Test_TTS.Models
{
    public class RecipeStructure
    {
        public int RecipeId { get; set; }

        public int ComponentId { get; set; }

        public float CorrectValue { get; set; } = 0.0f;

        public float Amount { get; set; }
    }
}
