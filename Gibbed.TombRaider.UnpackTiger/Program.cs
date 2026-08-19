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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Xml;
using Gibbed.CrystalDynamics.FileFormats;
using Gibbed.IO;
using NDesk.Options;
using Big = Gibbed.CrystalDynamics.FileFormats.Big;

namespace Gibbed.TombRaider.UnpackTiger
{
    internal class Program
    {
        // Environment.ProcessPath is the real running apphost (Gibbed.TombRaider.UnpackTiger.exe
        // on Windows, extension-less on macOS/Linux), matching what a user actually typed.
        // Assembly.GetExecutingAssembly().Location, used here previously, returns the managed
        // .dll instead under the modern .NET apphost model, so help/error text showed
        // "Gibbed.TombRaider.UnpackTiger.dll" rather than the real executable name. Falls back
        // to the old behavior only if ProcessPath is ever unavailable (hosting edge case).
        private static string GetExecutableName()
        {
            return Path.GetFileName(Environment.ProcessPath)
                ?? Path.GetFileName(System.Reflection.Assembly.GetExecutingAssembly().Location);
        }

        private static bool LooksLikeOption(string arg)
        {
            if (string.IsNullOrEmpty(arg) == true)
            {
                return false;
            }

            if (File.Exists(arg) == true)
            {
                return false;
            }

            if (arg[0] == '-')
            {
                return true;
            }

            // '/' is only an option prefix on Windows (e.g. "/?"), on Unix-like systems
            // it's the root of every absolute path, so treating it as a flag there rejects
            // every startup file passed with a full path, such as opening a DRM from the
            // command line on macOS or Linux.
            return arg[0] == '/' && RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        }

        private static bool Is000(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException("path");
            }

            var extension = Path.GetExtension(path);

            if (extension != null)
            {
                extension = extension.ToLowerInvariant();

                if (extension == ".tiger")
                {
                    path = Path.ChangeExtension(path, null);
                }
            }

            return Path.GetExtension(path) == ".000";
        }

        private static string GetBasePath(string path, out string suffix)
        {
            if (path == null)
            {
                throw new ArgumentNullException("path");
            }

            suffix = "";

            var extension = Path.GetExtension(path);

            if (extension != null)
            {
                extension = extension.ToLowerInvariant();

                if (extension == ".tiger")
                {
                    suffix = extension;
                    path = Path.ChangeExtension(path, null);
                }
            }

            return Path.ChangeExtension(path, null);
        }

        public static void Main(string[] args)
        {
            bool showHelp = false;
            bool? extractUnknowns = null;
            bool overwriteFiles = false;
            bool verbose = false;
            bool useDateTag = false;
            string currentProject = null;
            bool littleEndian = true;

            var options = new OptionSet()
            {
                {
                    "o|overwrite",
                    "overwrite existing files in the output directory",
                    v => overwriteFiles = v != null
                },
                {
                    "nu|no-unknowns",
                    "don't extract files that couldn't be matched to a name (skips the __UNKNOWN folder)",
                    v => extractUnknowns = v != null ? false : extractUnknowns
                },
                {
                    "ou|only-unknowns",
                    "only extract files that couldn't be matched to a name (only the __UNKNOWN folder)",
                    v => extractUnknowns = v != null ? true : extractUnknowns
                },
                {
                    "l|little-endian",
                    "read the archive as little-endian (default)",
                    v => littleEndian = v != null ? true : littleEndian
                },
                {
                    "b|big-endian",
                    "read the archive as big-endian",
                    v => littleEndian = v != null ? false : littleEndian
                },
                {
                    "v|verbose",
                    "list every extracted file as it's written; without this, a percentage-complete counter is shown instead",
                    v => verbose = v != null
                },
                {
                    "d|date",
                    "tag the output directory name with the current date/time, e.g. '_13AUG2026_0225'",
                    v => useDateTag = v != null
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

            if (extras.Count < 1 ||
                extras.Count > 2 ||
                showHelp == true ||
                Is000(extras[0]) == false)
            {
                Console.WriteLine("Usage: {0} [OPTIONS]+ input_file.tiger [output_dir]", GetExecutableName());
                Console.WriteLine();
                Console.WriteLine("Unpacks a multi-part .tiger bigfile archive (input_file.tiger.000, ...)");
                Console.WriteLine("into a new folder named '<input_file>_unpack', created under output_dir");
                Console.WriteLine("(or the current directory if output_dir is omitted). Pass -d/--date to tag");
                Console.WriteLine("that folder name with the current date/time instead of reusing it, e.g.");
                Console.WriteLine("'<input_file>_unpack_13AUG2026_0225'.");
                Console.WriteLine();
                Console.WriteLine("Options:");
                options.WriteOptionDescriptions(Console.Out);
                return;
            }

            string inputPath = extras[0];

            string bigPathSuffix;
            var bigPathBase = GetBasePath(inputPath, out bigPathSuffix);

            string outputBaseDir = extras.Count > 1 ? extras[1] : ".";
            string unpackDirName = Path.GetFileName(bigPathBase) + "_unpack";
            if (useDateTag == true)
            {
                unpackDirName += "_" + DateTime.Now.ToString("ddMMMyyyy_HHmm", CultureInfo.InvariantCulture).ToUpperInvariant();
            }
            string outputPath = Path.Combine(outputBaseDir, unpackDirName);

            var manager = ProjectData.Manager.Load(currentProject);
            if (manager.ActiveProject == null)
            {
                Console.WriteLine("Warning: no active project loaded.");
            }

            var big = new BigFileV3();
			big.Endianness = littleEndian ? Endian.Little : Endian.Big;
            big.FileAlignment = manager.GetSetting<uint>("bigfile_alignment", 0x7FF00000);

            using (var input = File.OpenRead(inputPath))
            {
                big.Deserialize(input);
            }

            var hashes = manager.LoadLists(
                "*.filelist",
                s => s.HashFileName(),
                s => s.ToLowerInvariant());

            Directory.CreateDirectory(outputPath);

            var settings = new XmlWriterSettings();
            settings.Indent = true;

            using (var xml = XmlWriter.Create(
                Path.Combine(outputPath, "bigfile.xml"), settings))
            {
                xml.WriteStartDocument();
                xml.WriteStartElement("files");
                xml.WriteAttributeString("endian", big.Endianness == Endian.Little ? "little" : "big");
                xml.WriteAttributeString("alignment", big.FileAlignment.ToString("X8"));

                Stream data = null;
                uint? currentBigFile = null;
                uint? lastLocale = null;
                {
                    long current = 0;
                    long total = big.Entries.Count;
                    int lastPercent = -1;

                    foreach (var entry in big.Entries.OrderBy(e => e.File).ThenBy(e => e.Offset))
                    {
                        current++;

                        if (verbose == false)
                        {
                            int percent = total > 0 ? (int)((current * 100) / total) : 100;
                            if (percent != lastPercent)
                            {
                                Console.Write("\rUnpacking... {0,3}% ({1}/{2})", percent, current, total);
                                lastPercent = percent;
                            }
                        }

                        var entryBigFile = entry.File;
                        var entryOffset = entry.Offset;

                        if (currentBigFile.HasValue == false ||
                            currentBigFile.Value != entryBigFile)
                        {
                            if (data != null)
                            {
                                data.Close();
                                data = null;
                            }

                            currentBigFile = entryBigFile;

                            var bigPath = string.Format("{0}.{1}{2}",
                                bigPathBase,
                                currentBigFile.Value.ToString().PadLeft(3, '0'),
                                bigPathSuffix);

                            if (verbose == true)
                            {
                                Console.WriteLine(bigPath);
                            }

                            if (File.Exists(bigPath) == false)
                            {
                                if (verbose == false)
                                {
                                    Console.WriteLine();
                                }
                                Console.WriteLine(
                                    "At least a portion of the multi-file, enumerated{1}bigfile series is missing: '{0}'",
                                    Path.GetFileName(bigPath),
                                    Environment.NewLine);
                                Console.WriteLine(
                                    "Please place '{0}' in the same folder as{3}'{1}' and re-run {2}",
                                    Path.GetFileName(bigPath),
                                    Path.GetFileName(inputPath),
                                    GetExecutableName(),
                                    Environment.NewLine);
                                return;
                            }

                            data = File.OpenRead(bigPath);
                        }

                        string name = hashes[entry.NameHash];
                        if (name == null)
                        {
                            if (extractUnknowns.HasValue == true &&
                                extractUnknowns.Value == false)
                            {
                                continue;
                            }

                            string extension;
                            // detect type
                            {
                                var guess = new byte[64];
                                int read = 0;

                                if (entry.Size > 0)
                                {
                                    data.Seek(entryOffset, SeekOrigin.Begin);
                                    read = data.Read(guess, 0, (int)Math.Min(
                                        entry.Size, guess.Length));
                                }

                                extension = FileExtensions.Detect(
                                    guess, Math.Min(guess.Length, read));
                            }

                            name = entry.NameHash.ToString("X8");
                            name = Path.ChangeExtension(name, "." + extension);
                            name = Path.Combine(extension, name);
                            name = Path.Combine("__UNKNOWN", name);
                        }
                        else
                        {
                            if (extractUnknowns.HasValue == true &&
                                extractUnknowns.Value == true)
                            {
                                continue;
                            }

                            name = name.Replace("/", "\\");
                            if (name.StartsWith("\\") == true)
                            {
                                name = name.Substring(1);
                            }
                        }

                        if (entry.Locale == 0xFFFFFFFF)
                        {
                            name = Path.Combine("default", name);
                        }
                        else
                        {
                            name = Path.Combine(entry.Locale.ToString("X8"), name);
                        }

                        var entryPath = Path.Combine(outputPath, name);
                        Directory.CreateDirectory(Path.GetDirectoryName(entryPath));

                        if (lastLocale.HasValue == false ||
                            lastLocale.Value != entry.Locale)
                        {
                            xml.WriteComment(string.Format(" {0} = {1} ",
                                entry.Locale.ToString("X8"),
                                ((Big.Locale)entry.Locale)));
                            lastLocale = entry.Locale;
                        }

                        xml.WriteStartElement("entry");
                        xml.WriteAttributeString("hash", entry.NameHash.ToString("X8"));
                        xml.WriteAttributeString("locale", entry.Locale.ToString("X8"));
                        xml.WriteValue(name);
                        xml.WriteEndElement();

                        if (overwriteFiles == false &&
                            File.Exists(entryPath) == true)
                        {
                            continue;
                        }

                        if (verbose == true)
                        {
                            Console.WriteLine("[{0}/{1}] {2}",
                                current, total, name);
                        }

                        using (var output = File.Create(entryPath))
                        {
                            if (entry.Size > 0)
                            {
                                data.Seek(entryOffset, SeekOrigin.Begin);
                                output.WriteFromStream(data, entry.Size);
                            }
                        }
                    }
                }

                if (verbose == false)
                {
                    Console.WriteLine();
                }

                if (data != null)
                {
                    data.Close();
                }

                xml.WriteEndElement();
                xml.WriteEndDocument();
                xml.Flush();
            }
        }
    }
}
