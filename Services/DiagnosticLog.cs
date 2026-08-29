using System.Text;

namespace TrayApp.Services
{
    internal static class DiagnosticLog
    {
        private static readonly object SyncRoot = new();
        private static readonly string LogDirectoryPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TrayApp",
            "logs");
        private static readonly string LogFilePath = Path.Combine(LogDirectoryPath, "native-messaging.log");

        public static void Write(string message)
        {
            try
            {
                Directory.CreateDirectory(LogDirectoryPath);

                string line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} {message}{Environment.NewLine}";

                lock (SyncRoot)
                {
                    File.AppendAllText(LogFilePath, line, Encoding.UTF8);
                }
            }
            catch
            {
            }
        }
    }
}