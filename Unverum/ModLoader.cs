using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace Unverum
{
    public static class ModLoader
    {
        // Restore all backups created from previous build
        public static bool Restart(string path, string movies, string splash, string sound)
        {
            try
            {
                // Delete everything in mods folder
                Directory.Delete(path, true);
                Directory.CreateDirectory(path);
                // Reset movies and splash folder
                if (!String.IsNullOrEmpty(movies) && Directory.Exists(movies))
                    RestoreDirectory(movies);
                if (!String.IsNullOrEmpty(splash) && Directory.Exists(splash))
                    RestoreDirectory(splash);
                if (!String.IsNullOrEmpty(sound) && Directory.Exists(sound))
                    RestoreDirectory(sound);
                Global.logger.WriteLine("Restored folders", LoggerType.Info);
            }
            catch (Exception e)
            {
                Global.logger.WriteLine(e.Message, LoggerType.Error);
                return false;
            }
            return true;
        }
        private static void RestoreDirectory(string path)
        {
            foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                if (File.Exists($"{file}.bak"))
                    File.Move($"{file}.bak", file, true);
        }
        private static int CopyFolder(string sourcePath, string targetPath, string defaultSig)
        {
            var counter = 0;
            //Copy all the files & Replaces any files with the same name
            foreach (string path in Directory.GetFiles(sourcePath, "*.*", SearchOption.AllDirectories))
            {
                if (Path.GetExtension(path).Equals(".pak", StringComparison.InvariantCultureIgnoreCase))
                {
                    var newPath = path.Replace(sourcePath, targetPath);
                    Directory.CreateDirectory(Path.GetDirectoryName(newPath));
                    File.Copy(path, newPath, true);
                    if (defaultSig != null)
                    {
                        var sig = Path.ChangeExtension(path, ".sig");
                        var newPathSig = Path.ChangeExtension(newPath, ".sig");
                        // Check if mod folder has corresponding .sig
                        if (File.Exists(sig))
                            File.Copy(sig, newPathSig, true);
                        // Otherwise copy over original game's .sig
                        else if (File.Exists(defaultSig))
                            File.Copy(defaultSig, newPathSig, true);
                        else
                        {
                            Global.logger.WriteLine($"Couldn't find .sig file to go with {newPath}", LoggerType.Warning);
                            continue;
                        }
                    }
                    counter++;
                }
            }
            return counter;
        }

        private static void ReplaceAsset(string file, string path)
        {
            var filesFound = 0;
            foreach (var oldFile in Directory.GetFiles(path, "*", SearchOption.AllDirectories)
                            .Where(a => Path.GetFileName(a).Equals(Path.GetFileName(file),
                            StringComparison.InvariantCultureIgnoreCase)))
            {
                if (!File.Exists($"{oldFile}.bak"))
                    File.Copy(oldFile, $"{oldFile}.bak", true);
                File.Copy(file, oldFile, true);
                Global.logger.WriteLine($"Replaced {oldFile} with {file}", LoggerType.Info);
                filesFound++;
            }
            if (filesFound == 0)
                Global.logger.WriteLine($"Couldn't find {file} within {path}", LoggerType.Warning);
        }

        // Copy over mod files in order of ModList
        public static void Build(string path, List<string> mods, bool? patched, string movies, string splash, string sound)
        {
            var missing = false;
            var game = String.Empty;
            switch (Global.config.CurrentGame)
            {
                case "Dragon Ball FighterZ":
                    game = "DBFZ";
                    break;
                case "Guilty Gear -Strive-":
                    game = "GGS";
                    break;
                case "Granblue Fantasy Versus":
                    game = "GBVS";
                    break;
            }
            Dictionary<string, Entry> entries = null;
            string sig = null;
            var sigs = Directory.GetFiles(Path.GetDirectoryName(path), "*.sig", SearchOption.TopDirectoryOnly);
            if (sigs.Length > 0)
                sig = sigs[0];
            var folderLetter = 'a';
            var tildes = 0;
            foreach (var mod in mods)
            {
                var priorityName = String.Empty;
                foreach (var tilde in Enumerable.Range(0, tildes))
                    priorityName += "~";
                priorityName += folderLetter;
                var folder = $"{path}{Global.s}{priorityName}{Global.s}{Path.GetFileName(mod)}";
                // Copy over .paks and .sigs to ~mods folder in order
                if (CopyFolder(mod, folder, sig) > 0)
                {
                    Global.logger.WriteLine($"Copied paks and sigs from {mod} over to {folder}", LoggerType.Info);
                    folderLetter++;
                    if (folderLetter == '{')
                    {
                        folderLetter = 'a';
                        tildes++;
                    }
                }
                // Copy over mp4s and bmps to the appropriate folders while storing backups
                if (!String.IsNullOrEmpty(movies) || !String.IsNullOrEmpty(splash) || !String.IsNullOrEmpty(sound))
                foreach (var file in Directory.GetFiles(mod, "*", SearchOption.AllDirectories))
                {
                    var ext = Path.GetExtension(file).ToLowerInvariant();
                    switch (ext)
                    {
                        case ".usm":
                        case ".uasset":
                        case ".mp4":
                            if (!String.IsNullOrEmpty(movies) && Directory.Exists(movies))
                                ReplaceAsset(file, movies);
                            break;
                        case ".bmp":
                            if (!String.IsNullOrEmpty(splash) && Directory.Exists(splash))
                                ReplaceAsset(file, splash);
                            break;
                        case ".awb":
                            if (!String.IsNullOrEmpty(sound) && Directory.Exists(sound))
                                ReplaceAsset(file, sound);
                            break;
                        case ".json":
                            if (Path.GetFileName(file).Equals("text.json", StringComparison.InvariantCultureIgnoreCase) &&
                                    (Global.config.CurrentGame.Equals("Dragon Ball FighterZ", StringComparison.InvariantCultureIgnoreCase)
                                    || Global.config.CurrentGame.Equals("Guilty Gear -Strive-", StringComparison.InvariantCultureIgnoreCase)
                                    || Global.config.CurrentGame.Equals("Granblue Fantasy Versus", StringComparison.InvariantCultureIgnoreCase)))
                            {
                                    if (missing)
                                        continue;
                                    if (entries == null)
                                        entries = TextPatcher.GetEntries(game);
                                    // Check if entries are still null
                                    if (entries == null)
                                    {
                                        missing = true;
                                        continue;
                                    }
                                    
                                    var text = File.ReadAllText(file);
                                    TextEntries replacements;
                                    try
                                    {
                                        replacements = JsonSerializer.Deserialize<TextEntries>(text);
                                    }
                                    catch (Exception e)
                                    {
                                        Global.logger.WriteLine(e.Message, LoggerType.Error);
                                        continue;
                                    }
                                    foreach (var replacement in replacements.Entries)
                                    {
                                        entries = TextPatcher.ReplaceEntry(replacement, entries);
                                    }
                                }
                            break;
                    }
                }
            }
            // Create pak if text was patched
            if (entries != null)
            {
                // Write uasset/uexp
                TextPatcher.WriteToFile(entries, game);
                var priorityName = String.Empty;
                foreach (var tilde in Enumerable.Range(0, tildes))
                    priorityName += "~";
                priorityName += folderLetter;
                var folder = $"{path}{Global.s}{priorityName}";
                Directory.CreateDirectory(folder);
                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.CreateNoWindow = true;
                startInfo.UseShellExecute = false;
                startInfo.FileName = $"{Global.assemblyLocation}{Global.s}Dependencies{Global.s}u4pak{Global.s}u4pak.exe";
                startInfo.WindowStyle = ProcessWindowStyle.Hidden;
                startInfo.WorkingDirectory = $"{Global.assemblyLocation}{Global.s}Dependencies{Global.s}u4pak";
                startInfo.Arguments = $"pack \"{folder}{Global.s}Text.pak\" RED";
                using (Process process = new Process())
                {
                    process.StartInfo = startInfo;
                    process.Start();
                    process.WaitForExit();
                }
                var textPak = $"{folder}{Global.s}Text.pak";
                if (File.Exists(textPak))
                {
                    if (sig != null)
                    {
                        var newSig = Path.ChangeExtension(textPak, ".sig");
                        // Copy over original game's .sig
                        if (File.Exists(sig))
                            File.Copy(sig, newSig, true);
                        else
                            Global.logger.WriteLine($"Couldn't find .sig file to go with {textPak}", LoggerType.Warning);
                    }
                    // Delete loose files
                    Directory.Delete($"{Global.assemblyLocation}{Global.s}Dependencies{Global.s}u4pak{Global.s}RED", true);
                }
                else
                    Global.logger.WriteLine($"Failed to create pak for text files!", LoggerType.Error);
            }
            // Costume Patched placeholder files as lowest priority
            if (patched != null && (bool)patched)
            {
                var files = new string[] { "--PlaceholderCostumes--.pak", "--PlaceholderCostumes--.sig" };
                foreach (var file in files)
                {
                    using (var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream($"Unverum.Resources.Base.{file}"))
                    {
                        var baseFolder = $"{path}{Global.s}--Base--";
                        Directory.CreateDirectory(baseFolder);
                        using (var stream = new FileStream($"{baseFolder}{Global.s}{file}", FileMode.Create, FileAccess.Write))
                        {
                            resource.CopyTo(stream);
                        }
                    }
                }
                Global.logger.WriteLine($"Copied over base costume patch files", LoggerType.Info);
            }
            Global.logger.WriteLine("Finished building!", LoggerType.Info);
        }
    }
}
