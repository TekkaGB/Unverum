using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Reflection;
using System.Diagnostics;

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
        public string textToReplace { get; set; }
        public int offset { get; set; }
    }
    public static class TextPatcher
    {
        public static bool ExtractBaseFiles()
        {
            var outputFolder = $"{Global.assemblyLocation}{Global.s}Dependencies{Global.s}u4pak";
            var quickbms = $"{Global.assemblyLocation}{Global.s}Dependencies{Global.s}quickbms{Global.s}quickbms_4gb_files.exe";
            var ue4bms = $"{Global.assemblyLocation}{Global.s}Dependencies{Global.s}quickbms{Global.s}unreal_tournament_4.bms";
            if (!Directory.Exists(outputFolder) || !File.Exists(quickbms) || !File.Exists(ue4bms))
            {
                Global.logger.WriteLine($"Missing dependencies, text patching will not work (Try redownloading)", LoggerType.Error);
                return false;
            }
            // Delete previous text files if they exist
            if (Directory.Exists($"{outputFolder}{Global.s}RED"))
                Directory.Delete($"{outputFolder}{Global.s}RED", true);
            var pak = $"{Path.GetDirectoryName(Global.config.Configs[Global.config.CurrentGame].ModsFolder)}{Global.s}pakchunk0-WindowsNoEditor.pak";
            if (!File.Exists(pak))
            {
                Global.logger.WriteLine($"Couldn't find {pak} to extract text files from, text patching will not work", LoggerType.Error);
                return false;
            }
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
            }
            startInfo.Arguments = $@"-Y {args} -f ""*INT/REDGame.*"" unreal_tournament_4.bms ""{pak}"" ""{outputFolder}""";
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardInput = true;
            Global.logger.WriteLine($"Extracting base files for text patching...", LoggerType.Info);
            using (Process process = new Process())
            {
                process.StartInfo = startInfo;
                process.Start();
                process.WaitForExit();
            }
            if (Directory.Exists($"{outputFolder}{Global.s}RED"))
            {
                Global.logger.WriteLine($"Successfully extracted base files for text patching", LoggerType.Info);
                return true;
            }
            else
            {
                Global.logger.WriteLine($"Failed to extract base files, text patching will not work", LoggerType.Error);
                return false;
            }
        }
        public static Dictionary<string, Entry> GetEntries()
        {
            var file = $"{Global.assemblyLocation}{Global.s}Dependencies{Global.s}u4pak{Global.s}RED{Global.s}Content{Global.s}Localization{Global.s}INT{Global.s}REDGame.uexp";
            if (!File.Exists(file) || !File.Exists(Path.ChangeExtension(file, ".uasset")))
                return null;
            var bytes = File.ReadAllBytes(file);
            // Convert entire file to unicode string
            var allText = Encoding.Unicode.GetString(bytes[54..(bytes.Length - 4)]);
            // Split by the delimiter 0x0D0A
            var entries = allText.Split("\r\n");
            var counter = 0;
            var offset = 54;
            var dict = new Dictionary<string, Entry>();
            // Convert array of strings to dictionary
            while (counter < entries.Length - 1)
            {
                offset += (entries[counter].Length + 2) * 2;
                if (!dict.ContainsKey(entries[counter]))
                    dict.Add(entries[counter], new Entry { text = entries[counter + 1], offset = offset});
                offset += (entries[counter + 1].Length + 2) * 2;
                counter += 2;
            }
            return dict;
        }
        // Get header of entry from text.json and replace in dictionary
        public static Dictionary<string, Entry> ReplaceEntry(Entry entry, Dictionary<string, Entry> dict)
        {
            if (dict.ContainsKey(entry.header))
            {
                var entryToReplace = dict[entry.header];
                var offset = entryToReplace.offset;
                var diff = (entry.text.Length - entryToReplace.text.Length) * 2;
                entryToReplace.textToReplace = entry.text;
                dict[entry.header] = entryToReplace;
                Global.logger.WriteLine($"Replacing {entryToReplace.text} with {entryToReplace.textToReplace} at {entry.header}", LoggerType.Info);
                // Update offsets
                foreach (var key in dict.Keys)
                    if (dict[key].offset > offset)
                        dict[key].offset += diff;
            }
            else
            {
                Global.logger.WriteLine($"Couldn't find header {entry.header}", LoggerType.Warning);
            }
            return dict;
        }
        public static void WriteToFile(Dictionary<string, Entry> dict)
        {
            var uexp = $"{Global.assemblyLocation}{Global.s}Dependencies{Global.s}u4pak{Global.s}RED{Global.s}Content{Global.s}Localization{Global.s}INT{Global.s}REDGame.uexp";
            var uasset = $"{Global.assemblyLocation}{Global.s}Dependencies{Global.s}u4pak{Global.s}RED{Global.s}Content{Global.s}Localization{Global.s}INT{Global.s}REDGame.uasset";
            var bytes = File.ReadAllBytes(uexp).ToList();
            foreach (var key in dict.Keys.Where(x => !String.IsNullOrEmpty(dict[x].textToReplace)))
            {
                var text = Encoding.Unicode.GetBytes(dict[key].textToReplace);
                // Remove original text
                bytes.RemoveRange(dict[key].offset, dict[key].text.Length * 2);
                // Insert new text
                bytes.InsertRange(dict[key].offset, text);
            }

            // Update header offsets
            var size = BitConverter.GetBytes(bytes.Count - 56);
            bytes.RemoveRange(36, 8);
            bytes.InsertRange(36, size.Concat(size));
            File.WriteAllBytes(uexp, bytes.ToArray());
            // Update offset in uasset
            var asset = File.ReadAllBytes(uasset).ToList();
            asset.RemoveRange(521, 4);
            asset.InsertRange(521, BitConverter.GetBytes(bytes.Count - 4));
            File.WriteAllBytes(uasset, asset.ToArray());
        }
    }
}
