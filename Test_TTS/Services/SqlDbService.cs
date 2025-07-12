using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using Test_TTS.Interfaces;
using Test_TTS.Models;

namespace Test_TTS.Services
{
    public class SqlDbService : IDatabaseService
    {
        private readonly string _connectionString;
        private readonly string _dbName;
        private readonly Lazy<bool> _hasMixerSetsTable;
        private readonly Lazy<bool> _hasTimeSetsTable;
        private readonly Lazy<bool> _hasConsistencyField;
        private readonly Lazy<bool> _hasDirectMixTimeField;

        public SqlDbService(string dbPath)
        {
            _connectionString = $"Data Source={dbPath}";
            _dbName = Path.GetFileNameWithoutExtension(dbPath);

            _hasMixerSetsTable = new Lazy<bool>(() => TableExists("recipe_mixer_set"));
            _hasTimeSetsTable = new Lazy<bool>(() => TableExists("recipe_time_set"));
            _hasConsistencyField = new Lazy<bool>(() => ColumnExists("recipe", "consistency_id"));
            _hasDirectMixTimeField = new Lazy<bool>(() => ColumnExists("recipe", "mix_time"));
        }

        public string DatabaseName => _dbName;
        public bool HasMixerSetsTable => _hasMixerSetsTable.Value;
        public bool HasTimeSetsTable => _hasTimeSetsTable.Value;
        public bool HasConsistencyField => _hasConsistencyField.Value;
        public bool HasDirectMixTimeField => _hasDirectMixTimeField.Value;

        public List<Recipe> GetRecipes()
        {
            const string baseQuery = @"
                SELECT id, name, date_modified, 
                       {0} as mixer_set_id, 
                       {1} as time_set_id, 
                       {2} as mix_time, 
                       {3} as mixer_humidity, 
                       {4} as water_correct, 
                       {5} as consistency_id
                FROM recipe";

            var queryParts = HasMixerSetsTable && HasTimeSetsTable
                ? new[] { "mixer_set_id", "time_set_id", "NULL", "NULL", "NULL", HasConsistencyField ? "consistency_id" : "NULL" }
                : HasDirectMixTimeField
                    ? new[] {
                        "NULL", "NULL", "mix_time",
                        ColumnExists("recipe", "mixer_humidity") ? "mixer_humidity" : "NULL",
                        ColumnExists("recipe", "water_correct") ? "water_correct" : "0",
                        "NULL"
                    }
                    : new[] { "NULL", "NULL", "NULL", "NULL", "NULL", "NULL" };

            var query = string.Format(baseQuery, queryParts);

            return ExecuteQuery(query, reader => new Recipe
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                DateModified = reader.GetDateTime(2),
                MixerSetId = reader.IsDBNull(3) ? null : (int?)reader.GetInt32(3),
                TimeSetId = reader.IsDBNull(4) ? null : (int?)reader.GetInt32(4),
                MixTime = reader.IsDBNull(5) ? null : (int?)reader.GetInt32(5),
                MixerHumidity = reader.IsDBNull(6) ? null : (float?)reader.GetFloat(6),
                WaterCorrect = reader.IsDBNull(7) ? null : (float?)reader.GetFloat(7),
                ConsistencyId = reader.IsDBNull(8) ? null : (int?)reader.GetInt32(8)
            });
        }

        public List<ComponentType> GetComponentTypes() =>
            ExecuteQuery("SELECT id, type FROM component_type", reader => new ComponentType
            {
                Id = reader.GetInt32(0),
                Type = reader.GetString(1)
            });

        public List<Component> GetComponents() =>
            ExecuteQuery("SELECT id, name, type_id, humidity FROM component", reader => new Component
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                TypeId = reader.GetInt32(2),
                Humidity = reader.GetFloat(3)
            });

        public List<RecipeStructure> GetRecipeStructures(int recipeId)
        {
            const string query = @"
                SELECT recipe_id, component_id, amount, correct_value 
                FROM recipe_structure 
                WHERE recipe_id = @recipeId";

            return ExecuteQuery(query, reader => new RecipeStructure
            {
                RecipeId = reader.GetInt32(0),
                ComponentId = reader.GetInt32(1),
                Amount = reader.GetFloat(2),
                CorrectValue = reader.GetFloat(3)
            }, new SQLiteParameter("@recipeId", recipeId));
        }

        public List<RecipeMixerSet> GetMixerSets() =>
            HasMixerSetsTable
                ? ExecuteQuery("SELECT id, name, unload_time, unload_mode FROM recipe_mixer_set", reader => new RecipeMixerSet
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    UnloadTime = reader.GetInt32(2),
                    UnloadMode = (MixerUnloadMode)Enum.Parse(typeof(MixerUnloadMode), reader.GetString(3))
                })
                : new List<RecipeMixerSet>();

        public List<RecipeTimeSet> GetTimeSets() =>
            HasTimeSetsTable
                ? ExecuteQuery("SELECT id, name, mix_time FROM recipe_time_set", reader => new RecipeTimeSet
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    MixTime = reader.GetInt32(2)
                })
                : new List<RecipeTimeSet>();

        public int SaveRecipe(Recipe recipe)
        {
            if (HasMixerSetsTable && HasTimeSetsTable)
            {
                var fields = new List<string> { "name", "date_modified", "mixer_set_id", "time_set_id" };
                var values = new List<string> { "@name", "@dateModified", "@mixerSetId", "@timeSetId" };
                var parameters = new List<SQLiteParameter>
                {
                    new SQLiteParameter("@name", recipe.Name),
                    new SQLiteParameter("@dateModified", recipe.DateModified),
                    new SQLiteParameter("@mixerSetId", recipe.MixerSetId ?? (object)DBNull.Value),
                    new SQLiteParameter("@timeSetId", recipe.TimeSetId ?? (object)DBNull.Value)
                };

                if (HasConsistencyField)
                {
                    fields.Add("consistency_id");
                    values.Add("@consistencyId");
                    parameters.Add(new SQLiteParameter("@consistencyId", recipe.ConsistencyId ?? (object)DBNull.Value));
                }

                return ExecuteScalar<int>($"INSERT INTO recipe ({string.Join(", ", fields)}) VALUES ({string.Join(", ", values)}); SELECT last_insert_rowid();", parameters.ToArray());
            }

            if (HasDirectMixTimeField)
            {
                var fields = new List<string> { "name", "date_modified", "mix_time" };
                var values = new List<string> { "@name", "@dateModified", "@mixTime" };
                var parameters = new List<SQLiteParameter>
                {
                    new SQLiteParameter("@name", recipe.Name),
                    new SQLiteParameter("@dateModified", recipe.DateModified),
                    new SQLiteParameter("@mixTime", recipe.MixTime ?? (object)DBNull.Value)
                };

                if (ColumnExists("recipe", "water_correct"))
                {
                    fields.Add("water_correct");
                    values.Add("@waterCorrect");
                    parameters.Add(new SQLiteParameter("@waterCorrect", recipe.WaterCorrect ?? 0f));
                }

                return ExecuteScalar<int>($"INSERT INTO recipe ({string.Join(", ", fields)}) VALUES ({string.Join(", ", values)}); SELECT last_insert_rowid();", parameters.ToArray());
            }

            return ExecuteScalar<int>(
                "INSERT INTO recipe (name, date_modified) VALUES (@name, @dateModified); SELECT last_insert_rowid();",
                new SQLiteParameter("@name", recipe.Name),
                new SQLiteParameter("@dateModified", recipe.DateModified));
        }

        public void SaveRecipeStructure(RecipeStructure structure) =>
            ExecuteNonQuery(
                "INSERT INTO recipe_structure (recipe_id, component_id, amount, correct_value) VALUES (@recipeId, @componentId, @amount, @correctValue)",
                new SQLiteParameter("@recipeId", structure.RecipeId),
                new SQLiteParameter("@componentId", structure.ComponentId),
                new SQLiteParameter("@amount", structure.Amount),
                new SQLiteParameter("@correctValue", structure.CorrectValue));

        public int SaveMixerSet(RecipeMixerSet mixerSet) =>
            ExecuteScalar<int>(
                "INSERT INTO recipe_mixer_set (name, unload_time, unload_mode) VALUES (@name, @unloadTime, @unloadMode); SELECT last_insert_rowid();",
                new SQLiteParameter("@name", mixerSet.Name),
                new SQLiteParameter("@unloadTime", mixerSet.UnloadTime),
                new SQLiteParameter("@unloadMode", mixerSet.UnloadMode.ToString()));

        public int SaveTimeSet(RecipeTimeSet timeSet) =>
            ExecuteScalar<int>(
                "INSERT INTO recipe_time_set (name, mix_time) VALUES (@name, @mixTime); SELECT last_insert_rowid();",
                new SQLiteParameter("@name", timeSet.Name),
                new SQLiteParameter("@mixTime", timeSet.MixTime));

        public ComponentType GetComponentTypeById(int id) =>
            ExecuteSingle("SELECT id, type FROM component_type WHERE id = @id",
                reader => new ComponentType { Id = reader.GetInt32(0), Type = reader.GetString(1) },
                new SQLiteParameter("@id", id));

        public Component GetComponentById(int id) =>
            ExecuteSingle("SELECT id, name, type_id, humidity FROM component WHERE id = @id",
                reader => new Component { Id = reader.GetInt32(0), Name = reader.GetString(1), TypeId = reader.GetInt32(2), Humidity = reader.GetFloat(3) },
                new SQLiteParameter("@id", id));

        #region Helper Methods
        private List<T> ExecuteQuery<T>(string query, Func<SQLiteDataReader, T> mapper, params SQLiteParameter[] parameters)
        {
            var results = new List<T>();
            using (var connection = new SQLiteConnection(_connectionString))
            using (var command = new SQLiteCommand(query, connection))
            {
                command.Parameters.AddRange(parameters);
                connection.Open();
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        results.Add(mapper(reader));
                    }
                }
            }
            return results;
        }

        private T ExecuteSingle<T>(string query, Func<SQLiteDataReader, T> mapper, params SQLiteParameter[] parameters) where T : class
        {
            using (var connection = new SQLiteConnection(_connectionString))
            using (var command = new SQLiteCommand(query, connection))
            {
                command.Parameters.AddRange(parameters);
                connection.Open();
                using (var reader = command.ExecuteReader())
                {
                    return reader.Read() ? mapper(reader) : null;
                }
            }
        }

        private T ExecuteScalar<T>(string query, params SQLiteParameter[] parameters)
        {
            using (var connection = new SQLiteConnection(_connectionString))
            using (var command = new SQLiteCommand(query, connection))
            {
                command.Parameters.AddRange(parameters);
                connection.Open();
                return (T)Convert.ChangeType(command.ExecuteScalar(), typeof(T));
            }
        }

        private int ExecuteNonQuery(string query, params SQLiteParameter[] parameters)
        {
            using (var connection = new SQLiteConnection(_connectionString))
            using (var command = new SQLiteCommand(query, connection))
            {
                command.Parameters.AddRange(parameters);
                connection.Open();
                return command.ExecuteNonQuery();
            }
        }

        private bool TableExists(string tableName) =>
            ExecuteScalar<int>($"SELECT 1 FROM sqlite_master WHERE type='table' AND name='{tableName}'") == 1;

        private bool ColumnExists(string tableName, string columnName)
        {
            using (var connection = new SQLiteConnection(_connectionString))
            using (var command = new SQLiteCommand($"PRAGMA table_info({tableName})", connection))
            {
                connection.Open();
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        if (reader.GetString(1).Equals(columnName, StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                }
            }
            return false;
        }
        #endregion
    }
}