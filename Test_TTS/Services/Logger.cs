using System;
using System.Collections.ObjectModel;

namespace Test_TTS.Services
{
    public class Logger
    {
        private readonly ObservableCollection<string> _log;

        public Logger(ObservableCollection<string> log) => _log = log;

        public void Add(string message) => _log.Insert(0, $"{DateTime.Now:HH:mm:ss}: {message}");
    }
}
