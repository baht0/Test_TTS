using System;
using System.Collections.Generic;
using System.Linq;
using Test_TTS.Interfaces;
using Test_TTS.Models;

namespace Test_TTS.Services
{
    public class AggregationTransferStrategy : ITransferStrategy
    {
        private readonly Action<string> _logAction;

        public AggregationTransferStrategy(Action<string> logAction) => _logAction = logAction;

        public List<RecipeStructure> ProcessStructures(
            List<RecipeStructure> sourceStructures,
            IDatabaseService sourceDb,
            IDatabaseService targetDb,
            Dictionary<int, int> componentMapping,
            Dictionary<int, int> componentTypeMapping)
        {
            _logAction($"Начало агрегации структур. Всего структур: {sourceStructures.Count}");

            var aggregated = new Dictionary<int, RecipeStructure>();

            foreach (var sourceStructure in sourceStructures)
            {
                if (componentMapping.TryGetValue(sourceStructure.ComponentId, out var targetComponentId))
                {
                    _logAction($"Найдено прямое соответствие компонента: {sourceStructure.ComponentId} -> {targetComponentId}");
                    AddOrAggregateStructure(aggregated, targetComponentId, sourceStructure);
                }
                else
                {
                    _logAction($"Прямое соответствие для компонента {sourceStructure.ComponentId} не найдено, поиск по типу");
                    var sourceComponent = sourceDb.GetComponentById(sourceStructure.ComponentId);

                    if (componentTypeMapping.TryGetValue(sourceComponent.TypeId, out var targetTypeId))
                    {
                        _logAction($"Найдено соответствие типов: {sourceComponent.TypeId} -> {targetTypeId}");
                        var targetComponents = targetDb.GetComponents()
                            .Where(c => c.TypeId == targetTypeId)
                            .ToList();

                        if (targetComponents.Count == 1)
                        {
                            _logAction($"Найден один целевой компонент {targetComponents[0].Id} для типа {targetTypeId}");
                            AddOrAggregateStructure(aggregated, targetComponents[0].Id, sourceStructure);
                        }
                        else if (targetComponents.Count > 1)
                        {
                            _logAction($"Предупреждение: Найдено несколько компонентов типа {targetTypeId}, агрегация пропущена");
                        }
                        else
                        {
                            _logAction($"Предупреждение: Не найдено компонентов типа {targetTypeId}");
                        }
                    }
                    else
                    {
                        _logAction($"Предупреждение: Не найдено соответствия для типа {sourceComponent.TypeId}");
                    }
                }
            }

            _logAction($"Агрегация завершена. Получено структур: {aggregated.Count}");
            return aggregated.Values.ToList();
        }

        private void AddOrAggregateStructure(Dictionary<int, RecipeStructure> aggregated, int componentId, RecipeStructure source)
        {
            if (aggregated.TryGetValue(componentId, out var existing))
            {
                _logAction($"Объединение данных для компонента {componentId}. Добавлено количество: {source.Amount}, корректное значение: {source.CorrectValue}");
                existing.Amount += source.Amount;
                existing.CorrectValue += source.CorrectValue;
            }
            else
            {
                _logAction($"Создание новой структуры для компонента {componentId}. Количество: {source.Amount}, корректное значение: {source.CorrectValue}");
                aggregated[componentId] = new RecipeStructure
                {
                    ComponentId = componentId,
                    Amount = source.Amount,
                    CorrectValue = source.CorrectValue
                };
            }
        }
    }
}