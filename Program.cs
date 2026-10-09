using System.IO;

namespace TrayApp
{
    internal static class Program
    {
        internal const string SingleInstanceMutexName = @"Local\TrayApp.SingleInstance";
        internal const string ActivateInstanceEventName = @"Local\TrayApp.ActivateInstance";
        private static System.Threading.Mutex? _singleInstanceMutex;
        private static FileStream? _singleInstanceLockStream;

        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            if (ShouldRunNativeHost(args))
            {
                var pipeClient = new Services.NativePipeMessageClient(
                    message =>
                    {
                        Services.DiagnosticLog.Write(message);
                        Console.Error.WriteLine(message);
                    });

                var nativeHost = new Services.NativeMessagingHostRunner(
                    pipeClient,
                    message =>
                    {
                        Services.DiagnosticLog.Write(message);
                        Console.Error.WriteLine(message);
                    });

                Services.DiagnosticLog.Write($"[NATIVE-HOST] Starting native host mode. args='{string.Join(" ", args)}'");

                nativeHost.RunAsync().GetAwaiter().GetResult();
                return;
            }

            if (!TryAcquireSingleInstanceFileLock())
            {
                TryActivateRunningInstance();
                return;
            }

            _singleInstanceMutex = new System.Threading.Mutex(
                initiallyOwned: true,
                name: SingleInstanceMutexName,
                createdNew: out bool isFirstInstance);

            if (!isFirstInstance)
            {
                _singleInstanceMutex.Dispose();
                _singleInstanceMutex = null;
                ReleaseSingleInstanceFileLock();
                TryActivateRunningInstance();
                return;
            }

            try
            {
                // To customize application configuration such as set high DPI settings or default font,
                // see https://aka.ms/applicationconfiguration.
                ApplicationConfiguration.Initialize();
                Application.Run(new Form1());
            }
            finally
            {
                _singleInstanceMutex?.ReleaseMutex();
                _singleInstanceMutex?.Dispose();
                _singleInstanceMutex = null;
                ReleaseSingleInstanceFileLock();
            }
        }

        private static bool TryAcquireSingleInstanceFileLock()
        {
            try
            {
                string lockDirectoryPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "TrayApp");

                Directory.CreateDirectory(lockDirectoryPath);

                string lockFilePath = Path.Combine(lockDirectoryPath, "trayapp.lock");
                _singleInstanceLockStream = new FileStream(
                    lockFilePath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None);

                return true;
            }
            catch (IOException)
            {
                return false;
            }
        }

        private static void ReleaseSingleInstanceFileLock()
        {
            _singleInstanceLockStream?.Dispose();
            _singleInstanceLockStream = null;
        }

        private static void TryActivateRunningInstance()
        {
            try
            {
                using var activateEvent = System.Threading.EventWaitHandle.OpenExisting(ActivateInstanceEventName);
                activateEvent.Set();
            }
            catch (System.Threading.WaitHandleCannotBeOpenedException)
            {
            }
        }

        private static bool ShouldRunNativeHost(string[] args)
        {
            bool explicitNativeHost =
                args.Any(arg => string.Equals(arg, "--native-host", StringComparison.OrdinalIgnoreCase));

            bool browserOriginArg = args.Any(arg =>
                arg.StartsWith("chrome-extension://", StringComparison.OrdinalIgnoreCase) ||
                arg.StartsWith("moz-extension://", StringComparison.OrdinalIgnoreCase));

            bool redirectedNativeHost = Console.IsInputRedirected && Console.IsOutputRedirected;

            return explicitNativeHost || (redirectedNativeHost && browserOriginArg);
        }
    }
}