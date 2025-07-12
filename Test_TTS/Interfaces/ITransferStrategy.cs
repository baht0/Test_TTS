using System.Collections.Generic;
using Test_TTS.Models;

namespace Test_TTS.Interfaces
{
    public interface ITransferStrategy
    {
        List<RecipeStructure> ProcessStructures(
          List<RecipeStructure> sourceStructures,
          IDatabaseService sourceDb,
          IDatabaseService targetDb,
          Dictionary<int, int> componentMapping,
          Dictionary<int, int> componentTypeMapping);
    }
}
