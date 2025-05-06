namespace SamFirmDownloader.Helper
{
    internal class LogHelper
    {
        public static bool Enable { get; set; } = true;

        public static Action<string>? WriteAction;

        public static Action? SaveAction;

        public static void WriteLog(string str)
        {
            if (!Enable)
                return;

            WriteAction?.Invoke(str);
        }

        public static void SaveLog()
        {
            SaveAction?.Invoke();
        }
    }
}