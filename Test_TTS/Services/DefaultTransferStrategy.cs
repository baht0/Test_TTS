using System;
using System.Collections.Generic;
using System.Linq;
using Test_TTS.Interfaces;
using Test_TTS.Models;

namespace Test_TTS.Services
{
    public class DefaultTransferStrategy : ITransferStrategy
    {
        private readonly Action<string> _logAction;

        public DefaultTransferStrategy(Action<string> logAction) => _logAction = logAction;

        public List<RecipeStructure> ProcessStructures(
            List<RecipeStructure> sourceStructures,
            IDatabaseService sourceDb,
            IDatabaseService targetDb,
            Dictionary<int, int> componentMapping,
            Dictionary<int, int> componentTypeMapping)
        {
            _logAction($"Начало переноса структур. Всего структур для обработки: {sourceStructures.Count}");

            var result = new List<RecipeStructure>();

            foreach (var sourceStructure in sourceStructures)
            {
                if (componentMapping.TryGetValue(sourceStructure.ComponentId, out var targetComponentId))
                {
                    _logAction($"Найдено прямое соответствие компонента {sourceStructure.ComponentId} -> {targetComponentId}");
                    result.Add(new RecipeStructure
                    {
                        ComponentId = targetComponentId,
                        Amount = sourceStructure.Amount,
                        CorrectValue = sourceStructure.CorrectValue
                    });
                }
                else
                {
                    ProcessNonMappedComponent(sourceStructure, sourceDb, targetDb, componentTypeMapping, result);
                }
            }

            _logAction($"Перенос завершен. Успешно перенесено структур: {result.Count}");
            return result;
        }

        private void ProcessNonMappedComponent(
            RecipeStructure sourceStructure,
            IDatabaseService sourceDb,
            IDatabaseService targetDb,
            Dictionary<int, int> componentTypeMapping,
            List<RecipeStructure> result)
        {
            var sourceComponent = sourceDb.GetComponentById(sourceStructure.ComponentId);
            var sourceType = sourceDb.GetComponentTypeById(sourceComponent.TypeId);

            _logAction($"Поиск соответствия для компонента {sourceComponent.Name} (ID: {sourceComponent.Id}, тип: {sourceType.Type})");

            // 1. Попытка найти компонент того же типа
            if (componentTypeMapping.TryGetValue(sourceComponent.TypeId, out var targetTypeId))
            {
                var similarComponents = targetDb.GetComponents()
                    .Where(c => c.TypeId == targetTypeId)
                    .ToList();

                if (similarComponents.Any())
                {
                    var targetComponent = similarComponents.First();
                    result.Add(new RecipeStructure
                    {
                        ComponentId = targetComponent.Id,
                        Amount = sourceStructure.Amount,
                        CorrectValue = sourceStructure.CorrectValue
                    });
                    _logAction($"Компонент {sourceComponent.Name} перенесен как {targetComponent.Name} (совпадение по типу)");
                    return;
                }
                else
                {
                    _logAction($"Не найдено компонентов типа {targetTypeId} в целевой БД");
                }
            }
            else
            {
                _logAction($"Не найдено соответствия для типа компонента {sourceType.Type}");
            }

            // 2. Попытка найти компонент с похожим названием
            var similarByName = targetDb.GetComponents()
                .FirstOrDefault(c => c.Name.Contains(sourceComponent.Name) || sourceComponent.Name.Contains(c.Name));

            if (similarByName != null)
            {
                result.Add(new RecipeStructure
                {
                    ComponentId = similarByName.Id,
                    Amount = sourceStructure.Amount,
                    CorrectValue = sourceStructure.CorrectValue
                });
                _logAction($"Компонент {sourceComponent.Name} перенесен как {similarByName.Name} (совпадение по названию)");
                return;
            }

            // 3. Если ничего не найдено
            _logAction($"Внимание: Компонент {sourceComponent.Name} (тип: {sourceType.Type}) не найден в целевой БД и будет пропущен");
        }
    }
}