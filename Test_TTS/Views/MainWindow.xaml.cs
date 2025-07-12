using System.Windows;
using Test_TTS.ViewModels;

namespace Test_TTS.Views
{
    public partial class MainWindow : Window
    {
        private static readonly MainViewModel ViewModel = new MainViewModel();
        public MainWindow()
        {
            InitializeComponent();
            DataContext = ViewModel;
        }
    }
}
