using System.Windows;
using System.IO;
using System.Text;

namespace Ch34xProgrammer;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, args) =>
        {
            WriteCrashLog(args.Exception);
            MessageBox.Show(args.Exception.Message, "Multi Flash", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
    }

    private static void WriteCrashLog(Exception ex)
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Multi Flash.crash.log");
            var log = new StringBuilder()
                .AppendLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
                .AppendLine(ex.ToString())
                .AppendLine()
                .ToString();
            File.AppendAllText(path, log);
        }
        catch
        {
        }
    }
}
