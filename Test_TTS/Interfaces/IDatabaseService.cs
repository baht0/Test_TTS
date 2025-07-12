using System.Collections.Generic;
using Test_TTS.Models;

namespace Test_TTS.Interfaces
{
    public interface IDatabaseService
    {
        string DatabaseName { get; }

        // Свойства для проверки структуры БД
        bool HasMixerSetsTable { get; }
        bool HasTimeSetsTable { get; }
        bool HasConsistencyField { get; }
        bool HasDirectMixTimeField { get; }

        // Методы для получения данных
        List<Recipe> GetRecipes();
        List<ComponentType> GetComponentTypes();
        List<Component> GetComponents();
        List<RecipeStructure> GetRecipeStructures(int recipeId);
        List<RecipeMixerSet> GetMixerSets();
        List<RecipeTimeSet> GetTimeSets();

        // Методы для получения отдельных сущностей
        ComponentType GetComponentTypeById(int id);
        Component GetComponentById(int id);

        // Методы для сохранения данных
        int SaveRecipe(Recipe recipe);
        void SaveRecipeStructure(RecipeStructure structure);
        int SaveMixerSet(RecipeMixerSet mixerSet);
        int SaveTimeSet(RecipeTimeSet timeSet);
    }
}