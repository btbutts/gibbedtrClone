using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Avalonia;
using NDesk.Options;

namespace Gibbed.DeusEx3.DRMEdit
{
    internal static class Program
    {
        private static string GetExecutableName()
        {
            return Path.GetFileName(System.Reflection.Assembly.GetExecutingAssembly().Location);
        }

        [STAThread]
        public static void Main(string[] args)
        {
            bool showHelp = false;

            var options = new OptionSet()
            {
                {
                    "h|help",
                    "show this message and exit",
                    v => showHelp = v != null
                },
            };

            List<string> extras;
            string? errorText = null;

            try
            {
                extras = options.Parse(args);
            }
            catch (OptionException e)
            {
                extras = new List<string>();

                var sb = new StringBuilder();
                sb.AppendFormat("{0}: ", GetExecutableName());
                sb.AppendLine(e.Message);
                sb.AppendFormat("Try `{0} --help' for more information.", GetExecutableName());
                sb.AppendLine();
                errorText = sb.ToString();
            }

            // NOTE: matches the original WinForms behavior exactly — showHelp is parsed but
            // never displayed here either; -h/--help has always been a silent no-op in this
            // tool (the original Program.cs set showHelp and never read it). Preserved as-is
            // rather than "fixed" during the Avalonia port, per this stage's rule of porting
            // Deus Ex 3's actual current behavior, not inventing functionality it never had.
            _ = showHelp;

            App.StartupFiles = extras;
            App.HelpText = errorText;
            App.HelpIsError = true;

            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }

        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();
    }
}
