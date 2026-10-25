namespace SmartTaskbar
{
    public static class Program
    {
        private const string MutexName = "{959d3545-aa5c-42a8-a327-6e2c079daa94}";
        private const string EventName = "{959d3545-aa5c-42a8-a327-6e2c079daa94}_ShowSettings";

        /// <summary>
        ///     The main entry point for the application.
        /// </summary>
        [STAThread]
        private static void Main(string[] args)
        {
            using var mutex = new Mutex(true, MutexName, out var createNew);
            if (!createNew)
            {
                // Signal first instance to open SettingsForm
                try
                {
                    using var showEvent = EventWaitHandle.OpenExisting(EventName);
                    showEvent.Set();
                }
                catch { }
                return;
            }

            using var eventWaitHandle = new EventWaitHandle(false, EventResetMode.AutoReset, EventName);

            ApplicationConfiguration.Initialize();
            var tray = new SystemTray();

            // Background thread to listen for second instance signals
            var listenerThread = new Thread(() =>
            {
                while (true)
                {
                    try
                    {
                        if (eventWaitHandle.WaitOne())
                        {
                            tray.InvokeShowSettings();
                        }
                    }
                    catch
                    {
                        break;
                    }
                }
            })
            {
                IsBackground = true
            };
            listenerThread.Start();

            if (args != null && args.Length > 0 && (args[0] == "--settings" || args[0] == "-s"))
            {
                tray.ShowSettings();
            }

            Application.Run(tray);
        }
    }
}