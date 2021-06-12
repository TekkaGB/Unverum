using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Unverum
{
    public static class ModLoader
    {
        // Restore all backups created from previous build
        public static bool Restart(string path, string movies, string splash)
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
                Global.logger.WriteLine("Restored mods, splash, and movies", LoggerType.Info);
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
        private static void ModSplash(string file, string path)
        {
            var splash = $"{path}{Global.s}Splash.bmp";
            if (File.Exists(splash))
            {
                if (!File.Exists($"{splash}.bak"))
                    File.Copy(splash, $"{splash}.bak", true);
                File.Copy(file, splash, true);
                Global.logger.WriteLine($"Copied over {file} to {splash}", LoggerType.Info);
            }
        }
        private static void ModMovies(string file, string path)
        {
            var wwMovie = $"{path}{Global.s}mv_opening_ww_pakchunk1.mp4";
            var newPath = $"{path}{Global.s}{Path.GetFileName(file)}";
            // Rename to worldwide op if the name doesnt match with anything
            if (!File.Exists(newPath))
                newPath = wwMovie;
            if (!File.Exists($"{newPath}.bak"))
                File.Copy(newPath, $"{newPath}.bak", true);
            File.Copy(file, newPath, true);
            Global.logger.WriteLine($"Copied over {file} to {newPath}", LoggerType.Info);
        }

        // Copy over mod files in order of ModList
        public static void Build(string path, List<string> mods, bool? patched, string movies, string splash)
        {
            string sig = null;
            var sigs = Directory.GetFiles(Path.GetDirectoryName(path), "*.sig", SearchOption.TopDirectoryOnly);
            if (sigs.Length > 0)
                sig = sigs[0];
            var folderLetter = 'z';
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
                    Global.logger.WriteLine($"Copied {mod} over to {folder}", LoggerType.Info);
                    folderLetter--;
                    if (folderLetter == '`')
                    {
                        folderLetter = 'z';
                        tildes++;
                    }
                }
                // Copy over mp4s and bmps to the appropriate folders while storing backups
                if (!String.IsNullOrEmpty(movies) || !String.IsNullOrEmpty(splash))
                foreach (var file in Directory.GetFiles(mod, "*", SearchOption.AllDirectories))
                {
                    var ext = Path.GetExtension(file);
                    switch (ext)
                    {
                        case ".mp4":
                            if (!String.IsNullOrEmpty(movies) && Directory.Exists(movies))
                                ModMovies(file, movies);
                            break;
                        case ".bmp":
                            if (!String.IsNullOrEmpty(splash) && Directory.Exists(splash))
                                ModSplash(file, splash);
                            break;
                    }
                }
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
                        using (var stream = new FileStream($"{baseFolder}/{file}", FileMode.Create, FileAccess.Write))
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
