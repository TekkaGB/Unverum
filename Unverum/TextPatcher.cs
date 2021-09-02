using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Reflection;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Unverum
{
    public class TextEntries
    {
        public List<Entry> Entries { get; set; }
    }
    public class Entry
    {
        public string header { get; set; }
        public string text { get; set; }
    }
    public static class TextPatcher
    {
        public static bool ExtractBaseFiles(string pakName, string filter, string extractedPath)
        {
            var outputFolder = $"{Global.assemblyLocation}{Global.s}Resources{Global.s}{Global.config.CurrentGame}";
            var quickbms = $"{Global.assemblyLocation}{Global.s}Dependencies{Global.s}quickbms{Global.s}quickbms_4gb_files.exe";
            var ue4bms = $"{Global.assemblyLocation}{Global.s}Dependencies{Global.s}quickbms{Global.s}unreal_tournament_4.bms";
            var file = $"{outputFolder}{Global.s}{extractedPath}";
            Directory.CreateDirectory(outputFolder);
            if (!File.Exists(quickbms) || !File.Exists(ue4bms))
            {
                Global.logger.WriteLine($"Missing dependencies, text patching will not work (Try redownloading)", LoggerType.Error);
                return false;
            }
            var pak = $"{Path.GetDirectoryName(Global.config.Configs[Global.config.CurrentGame].ModsFolder)}{Global.s}{pakName}";;
            if (!File.Exists(pak))
            {
                Global.logger.WriteLine($"Couldn't find {pak} to extract text files from, text patching will not work", LoggerType.Error);
                return false;
            }
            Global.logger.WriteLine($"Checking pak file...", LoggerType.Info);
            var pakLength = new FileInfo(pak).Length;
            // First time
            if (Global.config.Configs[Global.config.CurrentGame].PakLength == null)
            {
                Global.config.Configs[Global.config.CurrentGame].PakLength = pakLength;
                Global.UpdateConfig();
            }
            // File size matches (no need to reextract)
            else if (pakLength.Equals(Global.config.Configs[Global.config.CurrentGame].PakLength)
                && File.Exists(file))
            {
                Global.logger.WriteLine($"Base files already extracted, continuing to next step", LoggerType.Info);
                return true;
            }
            // File size mismatch
            else if (!pakLength.Equals(Global.config.Configs[Global.config.CurrentGame].PakLength))
            {
                Global.logger.WriteLine("Game files have been updated, reextracting base files for text patching", LoggerType.Info);
                Global.config.Configs[Global.config.CurrentGame].PakLength = pakLength;
                Global.UpdateConfig();
            }
            // Extract files
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.CreateNoWindow = true;
            startInfo.UseShellExecute = false;
            startInfo.FileName = quickbms;
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            startInfo.WorkingDirectory = Path.GetDirectoryName(quickbms);
            // Set encryption key
            var args = String.Empty;
            switch (Global.config.CurrentGame)
            {
                case "Dragon Ball FighterZ":
                    args = "-a 0";
                    break;
                case "Guilty Gear -Strive-":
                    args = "-a 1";
                    break;
                case "Granblue Fantasy Versus":
                    args = "-a 2";
                    break;
                case "My Hero One's Justice 2":
                    args = "-a 3";
                    break;
            }
            startInfo.Arguments = $@"-Y {args} -f ""{filter}"" unreal_tournament_4.bms ""{pak}"" ""{outputFolder}""";
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardInput = true;
            Global.logger.WriteLine($"Extracting base files for patching...", LoggerType.Info);
            using (Process process = new Process())
            {
                process.StartInfo = startInfo;
                process.Start();
                process.WaitForExit();
            }
            if (File.Exists(file))
            {
                Global.logger.WriteLine($"Successfully extracted base files for patching", LoggerType.Info);
                return true;
            }
            else
            {
                Global.logger.WriteLine($"Failed to extract base files, patching will not work", LoggerType.Error);
                return false;
            }
        }
        public static Dictionary<string, Entry> GetEntries()
        {
            var file = $"{Global.assemblyLocation}{Global.s}Resources{Global.s}{Global.config.CurrentGame}{Global.s}RED{Global.s}Content{Global.s}Localization{Global.s}INT{Global.s}REDGame.uexp";
            if (!File.Exists(file) || !File.Exists(Path.ChangeExtension(file, ".uasset")))
                return null;
            var bytes = File.ReadAllBytes(file);
            // Convert entire file to unicode string
            var allText = Encoding.Unicode.GetString(bytes[54..(bytes.Length - 4)]);
            // Split by the delimiter 0x0D0A
            var entries = allText.Split("\r\n");
            var counter = 0;
            var dict = new Dictionary<string, Entry>();
            // Convert array of strings to dictionary
            while (counter < entries.Length - 1)
            {
                // Append duplicate headers
                var index = 2;
                while (dict.ContainsKey(entries[counter]))
                {
                    entries[counter] = $"{entries[counter]} ({index})";
                    index++;
                }
                dict.Add(entries[counter], new Entry { text = entries[counter + 1] });
                counter += 2;
            }
            return dict;
        }
        // Get header of entry from text.json and replace in dictionary
        public static Dictionary<string, Entry> ReplaceEntry(Entry entry, Dictionary<string, Entry> dict)
        {
            if (dict.ContainsKey(entry.header))
            {
                Global.logger.WriteLine($"Replacing {dict[entry.header].text} with {entry.text} at {entry.header}", LoggerType.Info);
                dict[entry.header].text = entry.text;
            }
            else
            {
                Global.logger.WriteLine($"Appending header: {entry.header} with text: {entry.text}", LoggerType.Info);
                dict.Add(entry.header, new Entry { text = entry.text });
            }
            return dict;
        }
        public static void WriteToFile(Dictionary<string, Entry> dict)
        {
            // Delete previous text files if they exist
            if (Directory.Exists($"{Global.assemblyLocation}{Global.s}Dependencies{Global.s}u4pak{Global.s}RED"))
                Directory.Delete($"{Global.assemblyLocation}{Global.s}Dependencies{Global.s}u4pak{Global.s}RED", true);
            var inputUexp = $"{Global.assemblyLocation}{Global.s}Resources{Global.s}{Global.config.CurrentGame}{Global.s}RED{Global.s}Content{Global.s}Localization{Global.s}INT{Global.s}REDGame.uexp";
            var inputUasset = $"{Global.assemblyLocation}{Global.s}Resources{Global.s}{Global.config.CurrentGame}{Global.s}RED{Global.s}Content{Global.s}Localization{Global.s}INT{Global.s}REDGame.uasset";
            var outputUexp = $"{Global.assemblyLocation}{Global.s}Dependencies{Global.s}u4pak{Global.s}RED{Global.s}Content{Global.s}Localization{Global.s}INT{Global.s}REDGame.uexp";
            var outputUasset = $"{Global.assemblyLocation}{Global.s}Dependencies{Global.s}u4pak{Global.s}RED{Global.s}Content{Global.s}Localization{Global.s}INT{Global.s}REDGame.uasset";
            var bytes = File.ReadAllBytes(inputUexp).ToList();
            bytes.RemoveRange(54, bytes.Count - 54);
            // Append dictionary of entries
            foreach (var key in dict.Keys)
            {
                var header = Encoding.Unicode.GetBytes($"{Regex.Replace(key, @" \(\d+\)", String.Empty)}\r\n");
                var text = Encoding.Unicode.GetBytes($"{dict[key].text}\r\n");
                bytes.AddRange(header);
                bytes.AddRange(text);
            }
            // End with footer
            var footer = new byte[] { 0xC1, 0x83, 0x2A, 0x9E };
            bytes.AddRange(footer);
            // Update header offsets
            var size = BitConverter.GetBytes(bytes.Count - 56);
            bytes.RemoveRange(36, 8);
            bytes.InsertRange(36, size.Concat(size));
            Directory.CreateDirectory(Path.GetDirectoryName(outputUexp));
            File.WriteAllBytes(outputUexp, bytes.ToArray());
            // Update offset in uasset
            var asset = File.ReadAllBytes(inputUasset).ToList();
            asset.RemoveRange(521, 4);
            asset.InsertRange(521, BitConverter.GetBytes(bytes.Count - 4));
            Directory.CreateDirectory(Path.GetDirectoryName(outputUasset));
            File.WriteAllBytes(outputUasset, asset.ToArray());
        }
    }
}
