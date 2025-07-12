using System;

namespace Test_TTS.Models
{
    public class Recipe
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public DateTime DateModified { get; set; }

        public int? MixerSetId { get; set; }

        public int? TimeSetId { get; set; }

        public int? MixTime { get; set; }

        public float? MixerHumidity { get; set; }

        public float? WaterCorrect { get; set; }

        public int? ConsistencyId { get; set; }
    }
}
