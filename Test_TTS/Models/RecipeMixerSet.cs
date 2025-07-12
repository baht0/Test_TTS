namespace Test_TTS.Models
{
    public class RecipeMixerSet
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public int UnloadTime { get; set; } = 1;

        public MixerUnloadMode UnloadMode { get; set; } = MixerUnloadMode.Constant;
    }
    public enum MixerUnloadMode
    {
        Constant,
        Impulse,
    }
}
