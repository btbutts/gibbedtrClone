using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Avalonia;
using NDesk.Options;

namespace Gibbed.TombRaider.DRMEdit
{
    internal static class Program
    {
        private static string GetExecutableName()
        {
            return Path.GetFileName(System.Reflection.Assembly.GetExecutingAssembly().Location);
        }

        private static bool LooksLikeOption(string arg)
        {
            return string.IsNullOrEmpty(arg) == false &&
                (arg[0] == '-' || arg[0] == '/');
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

            List<string> extras = new List<string>();
            string? parseError = null;

            try
            {
                extras = options.Parse(args);
            }
            catch (OptionException e)
            {
                parseError = e.Message;
                showHelp = true;
            }

            if (showHelp == false)
            {
                var badOption = extras.FirstOrDefault(a => LooksLikeOption(a));
                if (badOption != null)
                {
                    parseError = string.Format("unrecognized option `{0}'.", badOption);
                    showHelp = true;
                }
            }

            string? helpText = null;
            if (showHelp == true)
            {
                var sb = new StringBuilder();
                if (parseError != null)
                {
                    sb.AppendFormat("{0}: {1}", GetExecutableName(), parseError);
                    sb.AppendLine();
                    sb.AppendLine();
                }
                sb.AppendFormat("Usage: {0} [OPTIONS]+ [file ...]", GetExecutableName());
                sb.AppendLine();
                sb.AppendLine("Opens the DRM file browser/editor. Any extra arguments are opened as");
                sb.AppendLine("individual DRM/resource files on startup.");
                sb.AppendLine();
                sb.AppendLine("Options:");
                using (var writer = new StringWriter(sb))
                {
                    options.WriteOptionDescriptions(writer);
                }
                helpText = sb.ToString();
            }

            App.StartupFiles = extras;
            App.HelpText = helpText;
            App.HelpIsError = parseError != null;

            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }

        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();
    }
}
