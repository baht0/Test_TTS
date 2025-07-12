using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Test_TTS.Services;

namespace Test_TTS.ViewModels
{
    public class MainViewModel : ObservableObject
    {
        private readonly string _databasesFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\Databases";

        [ObservableProperty]
        public bool IsBusy { get; set; } = true;

        [ObservableProperty]
        public ObservableCollection<SqlDbService> SourceDatabases { get; set; } = new ObservableCollection<SqlDbService>();
        [ObservableProperty]
        public SqlDbService SelectedSourceDb { get; set; }

        [ObservableProperty]
        public ObservableCollection<SqlDbService> TargetDatabases { get; set; } = new ObservableCollection<SqlDbService>();
        [ObservableProperty]
        public SqlDbService SelectedTargetDb { get; set; }

        [ObservableProperty]
        public ObservableCollection<string> LogLines { get; set; } = new ObservableCollection<string>();
        private Logger _logger { get; set; }

        [ObservableProperty]
        public ObservableCollection<string> Methods { get; set; } = new ObservableCollection<string>()
        {
            "Поиск аналогов",
            "Агрегация",
            "Разделение"
        };
        [ObservableProperty]
        public int MethodId { get; set; }

        public ICommand MigrationCommand { get; }
        public ICommand RefreshDatabasesCommand { get; }

        public MainViewModel()
        {
            _logger = new Logger(LogLines);
            _logger.Add("Программа запущена.");
            MigrationCommand = new AsyncRelayCommand(StartMigration, IsCanStart);
            RefreshDatabasesCommand = new RelayCommand(RefreshDatabases);
            RefreshDatabases();
            IsBusy = false;
        }

        private bool IsCanStart() => !IsBusy;

        private async Task StartMigration()
        {
            if (SelectedSourceDb == SelectedTargetDb)
            {
                MessageBox.Show("Выберите отличающийся БД.", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Hand);
            }
            else
            {
                IsBusy = true;
                _logger.Add("Запуск переноса данных...");
                try
                {
                    RecipeTransferService transferService = new RecipeTransferService(
                        SelectedSourceDb,
                        SelectedTargetDb,
                        msg => Application.Current.Dispatcher.Invoke(() => _logger.Add(msg)),
                        MethodId);
                    await Task.Run(() => transferService.TransferAllRecipes());
                    _logger.Add("Перенос данных завершен успешно");
                }
                catch (Exception ex)
                {
                    _logger.Add("Ошибка: " + ex.Message);
                    MessageBox.Show("Произошла ошибка при переносе данных: " + ex.Message, "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Hand);
                }
                finally
                {
                    IsBusy = false;
                }
            }
        }

        private void RefreshDatabases()
        {
            SourceDatabases.Clear();
            TargetDatabases.Clear();
            if (!Directory.Exists(_databasesFolder))
                return;
            foreach (string file in Directory.GetFiles(_databasesFolder, "*.db"))
            {
                var sqlDbService = new SqlDbService(file);
                SourceDatabases.Add(sqlDbService);
                TargetDatabases.Add(sqlDbService);
            }
            SelectedSourceDb = SourceDatabases.FirstOrDefault();
            SelectedTargetDb = TargetDatabases.FirstOrDefault();
            _logger.Add($"Обновлен список доступных БД. Найдено: {SourceDatabases.Count}");
        }
    }
}

