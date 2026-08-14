/* Copyright (c) 2011 Rick (rick 'at' gibbed 'dot' us)
 * 
 * This software is provided 'as-is', without any express or implied
 * warranty. In no event will the authors be held liable for any damages
 * arising from the use of this software.
 * 
 * Permission is granted to anyone to use this software for any purpose,
 * including commercial applications, and to alter it and redistribute it
 * freely, subject to the following restrictions:
 * 
 * 1. The origin of this software must not be misrepresented; you must not
 *    claim that you wrote the original software. If you use this software
 *    in a product, an acknowledgment in the product documentation would
 *    be appreciated but is not required.
 * 
 * 2. Altered source versions must be plainly marked as such, and must not
 *    be misrepresented as being the original software.
 * 
 * 3. This notice may not be removed or altered from any source
 *    distribution.
 */

//-----------------------------------------------------------------------------
// Additional modifications by sephiroth99
//-----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
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
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.SetColorMode(SystemColorMode.System);

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
            string parseError = null;

            try
            {
                extras = options.Parse(args);
            }
            catch (OptionException e)
            {
                // Option-parsing error (missing value, unknown option, etc.) -- show the same help as -h/--help.
                parseError = e.Message;
                showHelp = true;
            }

            if (showHelp == false)
            {
                var badOption = extras.FirstOrDefault(a => LooksLikeOption(a));
                if (badOption != null)
                {
                    // Not every unrecognized flag throws -- NDesk.Options passes unmatched
                    // long/"/"-style options through as plain positional args instead.
                    parseError = string.Format("unrecognized option `{0}'.", badOption);
                    showHelp = true;
                }
            }

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
                MessageBox.Show(
                    sb.ToString(),
                    GetExecutableName(),
                    MessageBoxButtons.OK,
                    parseError != null ? MessageBoxIcon.Error : MessageBoxIcon.Information);
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Explorer(extras));
        }
    }
}
