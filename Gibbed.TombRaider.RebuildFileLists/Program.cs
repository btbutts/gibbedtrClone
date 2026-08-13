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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gibbed.CrystalDynamics.FileFormats;
using Gibbed.IO;
using NDesk.Options;

namespace Gibbed.TombRaider.RebuildFileLists
{
    internal class Program
    {
        private static string GetExecutableName()
        {
            return Path.GetFileName(System.Reflection.Assembly.GetExecutingAssembly().CodeBase);
        }

        private static bool LooksLikeOption(string arg)
        {
            return string.IsNullOrEmpty(arg) == false &&
                (arg[0] == '-' || arg[0] == '/');
        }

        private static string GetListPath(string installPath, string inputPath)
        {
            installPath = installPath.ToLowerInvariant();
            inputPath = inputPath.ToLowerInvariant();

            if (inputPath.StartsWith(installPath) == false)
            {
                return null;
            }

            var baseName = inputPath.Substring(installPath.Length + 1);

            string outputPath;
            outputPath = Path.Combine("files", baseName);
            outputPath = Path.ChangeExtension(outputPath, ".filelist");
            return outputPath;
        }

        public static void Main(string[] args)
        {
            bool showHelp = false;
            string currentProject = null;
            bool littleEndian = true;

            var options = new OptionSet()
            {
                {
                    "l|little-endian",
                    "read archives as little-endian (default)",
                    v => littleEndian = v != null ? true : littleEndian
                },
                {
                    "b|big-endian",
                    "read archives as big-endian",
                    v => littleEndian = v != null ? false : littleEndian
                },
                {
                    "p|project=",
                    "override the active project (see the project data configuration for valid names)",
                    v => currentProject = v
                },
                {
                    "h|help",
                    "show this message and exit",
                    v => showHelp = v != null
                },
            };

            List<string> extras = new List<string>();

            try
            {
                extras = options.Parse(args);
            }
            catch (OptionException e)
            {
                // Option-parsing error (missing value, unknown option, etc.) -- show the same help as -h/--help.
                Console.Write("{0}: ", GetExecutableName());
                Console.WriteLine(e.Message);
                Console.WriteLine();
                showHelp = true;
            }

            if (showHelp == false)
            {
                var badOption = extras.FirstOrDefault(a => LooksLikeOption(a));
                if (badOption != null)
                {
                    // Not every unrecognized flag throws -- NDesk.Options passes unmatched
                    // long/"/"-style options through as plain positional args instead.
                    Console.Write("{0}: ", GetExecutableName());
                    Console.WriteLine("unrecognized option `{0}'.", badOption);
                    Console.WriteLine();
                    showHelp = true;
                }
            }

            if (extras.Count != 0 || showHelp == true)
            {
                Console.WriteLine("Usage: {0} [OPTIONS]+", GetExecutableName());
                Console.WriteLine();
                Console.WriteLine("Scans the active project's install path for every '*.000' archive and");
                Console.WriteLine("rebuilds the matching '.filelist' name lists under the project's lists path.");
                Console.WriteLine("This tool takes no positional arguments.");
                Console.WriteLine();
                Console.WriteLine("Options:");
                options.WriteOptionDescriptions(Console.Out);
                return;
            }

            Console.WriteLine("Loading project...");

            var manager = ProjectData.Manager.Load(currentProject);
            if (manager.ActiveProject == null)
            {
                Console.WriteLine("Nothing to do: no active project loaded.");
                return;
            }

            var project = manager.ActiveProject;
            var hashes = manager.LoadLists(
                "*.filelist",
                s => s.HashFileName(),
                s => s.ToLowerInvariant());

            var installPath = project.InstallPath;
            var listsPath = project.ListsPath;

            if (installPath == null)
            {
                Console.WriteLine("Could not detect install path.");
                return;
            }
            else if (listsPath == null)
            {
                Console.WriteLine("Could not detect lists path.");
                return;
            }

            Console.WriteLine("Searching for archives...");
            var inputPaths = new List<string>();
            inputPaths.AddRange(Directory.GetFiles(installPath, "*.000", SearchOption.AllDirectories));

            var outputPaths = new List<string>();

            var fileAlignment = manager.GetSetting<uint>("bigfile_alignment", 0x7FF00000);

            Console.WriteLine("Processing...");
            foreach (var inputPath in inputPaths)
            {
                var outputPath = GetListPath(installPath, inputPath);
                if (outputPath == null)
                {
                    throw new InvalidOperationException();
                }

                Console.WriteLine(outputPath);
                outputPath = Path.Combine(listsPath, outputPath);

                if (outputPaths.Contains(outputPath) == true)
                {
                    throw new InvalidOperationException();
                }

                outputPaths.Add(outputPath);

                var big = new BigFileV1();
                big.Endianness = littleEndian ? Endian.Little : Endian.Big;
                big.FileAlignment = fileAlignment;

                if (File.Exists(inputPath + ".bak") == true)
                {
                    using (var input = File.OpenRead(inputPath + ".bak"))
                    {
                        big.Deserialize(input);
                    }
                }
                else
                {
                    using (var input = File.OpenRead(inputPath))
                    {
                        big.Deserialize(input);
                    }
                }

                var localBreakdown = new Breakdown();

                var names = new List<string>();
                foreach (var nameHash in big.Entries.Select(e => e.NameHash).Distinct())
                {
                    var name = hashes[nameHash];
                    if (name != null)
                    {
                        if (names.Contains(name) == false)
                        {
                            names.Add(name);
                            localBreakdown.Known++;
                        }
                    }

                    localBreakdown.Total++;
                }

                names.Sort();

                Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                using (var output = new StreamWriter(outputPath))
                {
                    output.WriteLine("; {0}", localBreakdown);

                    foreach (string name in names)
                    {
                        output.WriteLine(name);
                    }
                }
            }
        }
    }
}
