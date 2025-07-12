using System.Windows;
using Test_TTS.Views;

namespace Test_TTS
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private void ApplicationStart(object sender, StartupEventArgs e)
        {
            Window start = new MainWindow();
            start.Show();
        }
    }
}
