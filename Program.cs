using System;
using System.Threading;
using System.Windows.Forms;

namespace AudioSwitcher
{
    static class Program
    {
        private static Mutex _mutex;

        /// <summary>
        /// Application entry point
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            // Single instance check
            const string mutexName = "GProAudioSwitcher_SingleInstance";
            bool createdNew;

            _mutex = new Mutex(true, mutexName, out createdNew);

            if (!createdNew)
            {
                // Another instance is already running
                MessageBox.Show(
                    L.Get("Dialog_AlreadyRunning_Text"),
                    L.Get("Dialog_AlreadyRunning_Title"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            try
            {
                // Enable visual styles for modern look
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                // Handle unhandled exceptions
                Application.ThreadException += OnThreadException;
                AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

                // Run the tray application
                Application.Run(new TrayApplication());
            }
            finally
            {
                _mutex?.ReleaseMutex();
                _mutex?.Dispose();
            }
        }

        private static void OnThreadException(object sender, ThreadExceptionEventArgs e)
        {
            LogException("Thread Exception", e.Exception);
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            LogException("Unhandled Exception", e.ExceptionObject as Exception);
        }

        private static void LogException(string type, Exception ex)
        {
            string message = $"{type}: {ex?.Message ?? "Unknown error"}\n\n{ex?.StackTrace}";
            System.Diagnostics.Debug.WriteLine(message);

            try
            {
                string logPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "GProAudioSwitcher",
                    "error.log");

                string dir = System.IO.Path.GetDirectoryName(logPath);
                if (!System.IO.Directory.Exists(dir))
                {
                    System.IO.Directory.CreateDirectory(dir);
                }

                System.IO.File.AppendAllText(logPath,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n\n");
            }
            catch { }
        }
    }
}
