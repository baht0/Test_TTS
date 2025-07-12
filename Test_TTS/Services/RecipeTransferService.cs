using System;
using System.Collections.Generic;
using System.Linq;
using Test_TTS.Interfaces;
using Test_TTS.Models;

namespace Test_TTS.Services
{
    public class RecipeTransferService
    {
        private readonly IDatabaseService _sourceDb;
        private readonly IDatabaseService _targetDb;
        private readonly Action<string> _logAction;
        private readonly ITransferStrategy _transferStrategy;
        private readonly Dictionary<int, int> _componentTypeMapping = new Dictionary<int, int>();
        private readonly Dictionary<int, int> _componentMapping = new Dictionary<int, int>();
        private readonly Dictionary<int, int> _mixerSetMapping = new Dictionary<int, int>();
        private readonly Dictionary<int, int> _timeSetMapping = new Dictionary<int, int>();

        public RecipeTransferService(
            IDatabaseService sourceDb,
            IDatabaseService targetDb,
            Action<string> logAction,
            int methodId = 0)
        {
            _sourceDb = sourceDb;
            _targetDb = targetDb;
            _logAction = logAction;

            if (methodId == 1)
                _transferStrategy = new AggregationTransferStrategy(logAction);
            else if (methodId == 2)
                _transferStrategy = new SplittingTransferStrategy(logAction);
        }

        public void TransferAllRecipes()
        {
            _logAction($"Начало переноса данных из {_sourceDb.DatabaseName} в {_targetDb.DatabaseName}");

            try
            {
                BuildComponentMappings();
                BuildMixerSetMappings();
                BuildTimeSetMappings();

                var recipes = _sourceDb.GetRecipes();
                _logAction($"Найдено {recipes.Count} рецептов для переноса");

                foreach (var sourceRecipe in recipes)
                {
                    TransferRecipe(sourceRecipe);
                }

                _logAction("Перенос данных завершен успешно");
            }
            catch (Exception ex)
            {
                _logAction($"Ошибка при переносе данных: {ex.Message}");
                throw;
            }
        }

        private void BuildComponentMappings()
        {
            var sourceTypes = _sourceDb.GetComponentTypes();
            var targetTypes = _targetDb.GetComponentTypes();

            foreach (var sourceType in sourceTypes)
            {
                var targetType = targetTypes.FirstOrDefault(t =>
                    t.Type.Equals(sourceType.Type, StringComparison.OrdinalIgnoreCase));

                if (targetType != null)
                {
                    _componentTypeMapping[sourceType.Id] = targetType.Id;

                    var sourceComponents = _sourceDb.GetComponents()
                        .Where(c => c.TypeId == sourceType.Id).ToList();
                    var targetComponents = _targetDb.GetComponents()
                        .Where(c => c.TypeId == targetType.Id).ToList();

                    foreach (var sourceComponent in sourceComponents)
                    {
                        var targetComponent = targetComponents.FirstOrDefault(c =>
                            c.Name.Equals(sourceComponent.Name, StringComparison.OrdinalIgnoreCase));

                        if (targetComponent != null)
                        {
                            _componentMapping[sourceComponent.Id] = targetComponent.Id;
                        }
                    }
                }
            }

            _logAction($"Создано {_componentTypeMapping.Count} сопоставлений типов компонентов");
            _logAction($"Создано {_componentMapping.Count} сопоставлений компонентов");
        }
        private void BuildMixerSetMappings()
        {
            if (!_targetDb.HasMixerSetsTable) return;

            var sourceSets = _sourceDb.GetMixerSets();
            var targetSets = _targetDb.GetMixerSets();

            foreach (var sourceSet in sourceSets)
            {
                var targetSet = targetSets.FirstOrDefault(s =>
                    s.Name.Equals(sourceSet.Name, StringComparison.OrdinalIgnoreCase) &&
                    s.UnloadTime == sourceSet.UnloadTime &&
                    s.UnloadMode == sourceSet.UnloadMode);

                if (targetSet == null)
                {
                    var newSet = new RecipeMixerSet
                    {
                        Name = sourceSet.Name,
                        UnloadTime = sourceSet.UnloadTime,
                        UnloadMode = sourceSet.UnloadMode
                    };

                    var newId = _targetDb.SaveMixerSet(newSet);
                    _mixerSetMapping[sourceSet.Id] = newId;
                    _logAction($"Создан новый набор настроек смесителя: {newSet.Name}");
                }
                else
                {
                    _mixerSetMapping[sourceSet.Id] = targetSet.Id;
                }
            }
        }
        private void BuildTimeSetMappings()
        {
            if (!_targetDb.HasMixerSetsTable) return;

            var sourceSets = _sourceDb.GetTimeSets();
            var targetSets = _targetDb.GetTimeSets();

            foreach (var sourceSet in sourceSets)
            {
                var targetSet = targetSets.FirstOrDefault(s =>
                    s.Name.Equals(sourceSet.Name, StringComparison.OrdinalIgnoreCase) &&
                    s.MixTime == sourceSet.MixTime);

                if (targetSet == null)
                {
                    var newSet = new RecipeTimeSet
                    {
                        Name = sourceSet.Name,
                        MixTime = sourceSet.MixTime
                    };

                    var newId = _targetDb.SaveTimeSet(newSet);
                    _timeSetMapping[sourceSet.Id] = newId;
                    _logAction($"Создан новый набор настроек времени: {newSet.Name}");
                }
                else
                {
                    _timeSetMapping[sourceSet.Id] = targetSet.Id;
                }
            }
        }

        private void TransferRecipe(Recipe sourceRecipe)
        {
            _logAction($"Перенос рецепта: {sourceRecipe.Name}");

            var targetRecipe = AdaptRecipeStructure(sourceRecipe);
            var newRecipeId = _targetDb.SaveRecipe(targetRecipe);
            _logAction($"Рецепт сохранен в целевой БД с ID: {newRecipeId}");

            TransferRecipeComponents(sourceRecipe.Id, newRecipeId);
        }
        private Recipe AdaptRecipeStructure(Recipe sourceRecipe)
        {
            return new Recipe
            {
                Name = sourceRecipe.Name,
                DateModified = DateTime.Now,
                MixerSetId = GetMappedMixerSetId(sourceRecipe.MixerSetId),
                TimeSetId = GetMappedTimeSetId(sourceRecipe.TimeSetId),
                MixTime = GetAdaptedMixTime(sourceRecipe),
                WaterCorrect = sourceRecipe.WaterCorrect ?? 0f,
                ConsistencyId = sourceRecipe.ConsistencyId
            };
        }

        private int? GetMappedMixerSetId(int? sourceId)
        {
            if (sourceId.HasValue && _mixerSetMapping.TryGetValue(sourceId.Value, out var targetId))
                return targetId;
            return null;
        }

        private int? GetMappedTimeSetId(int? sourceId)
        {
            if (sourceId.HasValue && _timeSetMapping.TryGetValue(sourceId.Value, out var targetId))
                return targetId;
            return null;
        }

        private int? GetAdaptedMixTime(Recipe sourceRecipe)
        {
            if (sourceRecipe.MixTime.HasValue) return sourceRecipe.MixTime;

            return sourceRecipe.TimeSetId.HasValue &&
                   _timeSetMapping.TryGetValue(sourceRecipe.TimeSetId.Value, out var timeSetId)
                ? _targetDb.GetTimeSets().FirstOrDefault(t => t.Id == timeSetId)?.MixTime
                : null;
        }

        private void TransferRecipeComponents(int sourceRecipeId, int targetRecipeId)
        {
            var sourceStructures = _sourceDb.GetRecipeStructures(sourceRecipeId);
            var targetStructures = _transferStrategy.ProcessStructures(
                sourceStructures,
                _sourceDb,
                _targetDb,
                _componentMapping,
                _componentTypeMapping);

            foreach (var structure in targetStructures)
            {
                structure.RecipeId = targetRecipeId;
                _targetDb.SaveRecipeStructure(structure);
            }

            _logAction($"Перенесено {targetStructures.Count} компонентов из {sourceStructures.Count} исходных");
        }
    }
}