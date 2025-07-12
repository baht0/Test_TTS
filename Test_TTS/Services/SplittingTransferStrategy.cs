using System;
using System.Collections.Generic;
using System.Linq;
using Test_TTS.Interfaces;
using Test_TTS.Models;

namespace Test_TTS.Services
{
    public class SplittingTransferStrategy : ITransferStrategy
    {
        private readonly Action<string> _logAction;
        private Dictionary<int, List<int>> _componentToBunkersMap;

        public SplittingTransferStrategy(Action<string> logAction) => _logAction = logAction;

        public List<RecipeStructure> ProcessStructures(
            List<RecipeStructure> sourceStructures,
            IDatabaseService sourceDb,
            IDatabaseService targetDb,
            Dictionary<int, int> componentMapping,
            Dictionary<int, int> componentTypeMapping)
        {
            // Автоматически строим карту распределения при первом вызове
            if (_componentToBunkersMap == null)
            {
                BuildComponentDistributionMap(sourceDb, targetDb);
            }

            var result = new List<RecipeStructure>();

            foreach (var sourceStructure in sourceStructures)
            {
                ProcessComponent(sourceStructure, sourceDb, result);
            }

            return result;
        }

        private void BuildComponentDistributionMap(IDatabaseService sourceDb, IDatabaseService targetDb)
        {
            _componentToBunkersMap = new Dictionary<int, List<int>>();
            var allTargetComponents = targetDb.GetComponents();

            // Группируем бункеры по типам компонентов
            var componentsByType = allTargetComponents
                .GroupBy(c => c.TypeId)
                .ToDictionary(g => g.Key, g => g.Select(c => c.Id).ToList());

            foreach (var sourceComponent in sourceDb.GetComponents())
            {
                if (componentsByType.TryGetValue(sourceComponent.TypeId, out var targetComponents))
                {
                    // Автоматически распределяем исходный компонент по всем бункерам его типа
                    _componentToBunkersMap[sourceComponent.Id] = targetComponents;
                    _logAction($"Автоматическое сопоставление: компонент {sourceComponent.Name} будет разделен между {targetComponents.Count} бункерами");
                }
            }
        }

        private void ProcessComponent(
            RecipeStructure sourceStructure,
            IDatabaseService sourceDb,
            List<RecipeStructure> result)
        {
            if (_componentToBunkersMap.TryGetValue(sourceStructure.ComponentId, out var targetBunkers))
            {
                SplitComponent(sourceStructure, targetBunkers, result);
            }
            else
            {
                var component = sourceDb.GetComponentById(sourceStructure.ComponentId);
                _logAction($"Не найдено бункеров для компонента {component.Name} (ID: {component.Id})");
            }
        }

        private void SplitComponent(RecipeStructure source, List<int> targetBunkers, List<RecipeStructure> result)
        {
            if (targetBunkers.Count == 0) return;

            float amountPerBunker = source.Amount / targetBunkers.Count;
            float correctionPerBunker = source.CorrectValue / targetBunkers.Count;

            foreach (var bunkerId in targetBunkers)
            {
                result.Add(new RecipeStructure
                {
                    ComponentId = bunkerId,
                    Amount = amountPerBunker,
                    CorrectValue = correctionPerBunker
                });
            }

            _logAction($"Компонент ID {source.ComponentId} разделен на {targetBunkers.Count} бункеров");
        }
    }
}