using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAPICodePack.Dialogs;
using System.Windows;

namespace Unverum
{
    public static class Setup
    {
        public static string GetMD5Checksum(string filename)
        {
            using (var md5 = MD5.Create())
            {
                using (var stream = File.OpenRead(filename))
                {
                    var hash = md5.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", "");
                }
            }
        }
        public static bool CheckPatch(string exe)
        {
            // Remove old costume patch for DBFZ
            if (Global.config.CurrentGame == "Dragon Ball FighterZ")
            {
                var checksum = GetMD5Checksum(exe).ToLowerInvariant();
                var originalExe = exe.Replace("-eac-nop-loaded", String.Empty);
                // Check if old patch exe is still there
                switch (checksum)
                {
                    // Old patched exes
                    case "2f9c8178fc8a5cdb3ac1ff8fee888f89":
                    case "ae356567060af7ffa7d0d9e293fafbd9":
                    case "8c442c2913b2be5e4a21730c7c15b39f":
                    case "d21dbaa08aba836a799254b4125967a9":
                    case "ca33c47b6df1f561a068c627786226df":
                        File.Copy(originalExe, exe, true);
                        break;
                    default:
                        if (!File.Exists(exe))
                            File.Copy(originalExe, exe, true);
                        break;
                }
            }
            var PatchPath = $"{Path.GetDirectoryName(Global.config.Configs[Global.config.CurrentGame].Launcher)}" +
                $"{Global.s}RED{Global.s}Binaries{Global.s}Win64";
            if (Global.config.CurrentGame == "Dragon Ball FighterZ")
                PatchPath = $"{Path.GetDirectoryName(Global.config.Configs[Global.config.CurrentGame].Launcher)}";
            else if (Global.config.CurrentGame == "Scarlet Nexus")
                PatchPath = $"{Path.GetDirectoryName(Global.config.Configs[Global.config.CurrentGame].Launcher)}" +
                $"{Global.s}ScarletNexus{Global.s}Binaries{Global.s}Win64";
            else if (Global.config.CurrentGame == "Dragon Ball Sparking! ZERO")
                PatchPath = $"{Path.GetDirectoryName(Global.config.Configs[Global.config.CurrentGame].Launcher)}" +
                $"{Global.s}SparkingZERO{Global.s}Binaries{Global.s}Win64";
            // Check if files exist
            switch (Global.config.CurrentGame)
            {
                case "Dragon Ball FighterZ":
                    if (!File.Exists($"{PatchPath}{Global.s}plugins{Global.s}DBFZExtraCostumesPatch.asi")
                        || GetMD5Checksum($"{PatchPath}{Global.s}plugins{Global.s}DBFZExtraCostumesPatch.asi").Equals("e99c11be64f7fb81e8b2eebdff72b164", StringComparison.InvariantCultureIgnoreCase)
                        || GetMD5Checksum($"{PatchPath}{Global.s}plugins{Global.s}DBFZExtraCostumesPatch.asi").Equals("960d0f042522485a56665e156c0d9820", StringComparison.InvariantCultureIgnoreCase))
                        GetPatchFiles(PatchPath);
                    break;
                case "Scarlet Nexus":
                    if (!File.Exists($"{PatchPath}{Global.s}plugins{Global.s}ScarletNexusUTOCSigBypass.asi"))
                        GetPatchFiles(PatchPath);
                    break;
                case "Dragon Ball Sparking! ZERO":
                    if (!File.Exists($"{PatchPath}{Global.s}plugins{Global.s}DBSparkingZeroUTOCBypass.asi"))
                        GetPatchFiles(PatchPath);
                    break;
            }
            return true;
        }
        public static void GetPatchFiles(string PatchPath)
        {
            foreach (var file in Assembly.GetExecutingAssembly().GetManifestResourceNames()
                    .Where(x => x.Contains($"{Global.config.CurrentGame.Replace(" ", "_").Replace("-", "_").Replace("!", "_")}.Patch")))
                using (var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream(file))
                {
                    var split = file.Split('.');
                    var index = split.ToList().FindIndex(x => x == "Patch");
                    var path = $"{PatchPath}{Global.s}{string.Join(Global.s, split[(index + 1)..(split.Length - 1)])}.{split[split.Length - 1]}";
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write))
                    {
                        resource.CopyTo(stream);
                    }
                }
        }
        public static bool Generic(string exe, string projectName, string defaultPath, string otherExe = null, string steamId = null, bool epic = false)
        {
            // Get install path from registry
            if (steamId != null && !epic)
            {
                try
                {
                    var key = Registry.LocalMachine.OpenSubKey($@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App {steamId}");
                    if (!String.IsNullOrEmpty(key.GetValue("InstallLocation") as string))
                        defaultPath = $"{key.GetValue("InstallLocation") as string}{Global.s}{exe}";
                }
                catch (Exception e)
                {
                }
            }
            if (!File.Exists(defaultPath))
            {
                if (!epic)
                    Global.logger.WriteLine($"Couldn't find install path in registry, select path to exe instead", LoggerType.Warning);
                OpenFileDialog dialog = new OpenFileDialog();
                dialog.DefaultExt = ".exe";
                dialog.Filter = otherExe == null ? $"Executable Files ({exe})|{exe}" 
                    : $"Executable Files ({exe};{otherExe})|{exe};{otherExe}";
                dialog.Title = otherExe == null ? $"Select {exe} from your Steam Install folder"
                    : $"Select {exe} from your Steam Install folder or {otherExe} from your Epic Games install folder";
                dialog.Multiselect = false;
                dialog.InitialDirectory = Global.assemblyLocation;
                dialog.ShowDialog();
                if (!String.IsNullOrEmpty(dialog.FileName)
                    && (Path.GetFileName(dialog.FileName).Equals(exe, StringComparison.InvariantCultureIgnoreCase)
                    || (otherExe != null && Path.GetFileName(dialog.FileName).Equals(otherExe, StringComparison.InvariantCultureIgnoreCase))))
                    defaultPath = dialog.FileName;
                else if (!String.IsNullOrEmpty(dialog.FileName))
                {
                    Global.logger.WriteLine($"Invalid .exe chosen", LoggerType.Error);
                    return false;
                }
                else
                    return false;
            }
            var parent = Path.GetDirectoryName(defaultPath);
            var ModsFolder = $"{parent}{Global.s}{projectName}{Global.s}Content{Global.s}Paks{Global.s}~mods";
            Directory.CreateDirectory(ModsFolder);
            Global.config.Configs[Global.config.CurrentGame].ModsFolder = ModsFolder;
            Global.config.Configs[Global.config.CurrentGame].Launcher = defaultPath;

            if (Global.config.CurrentGame == "Dragon Ball Sparking! ZERO"
                || Global.config.CurrentGame == "Scarlet Nexus")
                CheckPatch(Global.config.Configs[Global.config.CurrentGame].Launcher);

            Global.UpdateConfig();
            Global.logger.WriteLine($"Setup completed for {Global.config.CurrentGame}!", LoggerType.Info);
            return true;
        }
        public static bool GBVSR()
        {
            var defaultPath = @"C:\Program Files (x86)\Steam\steamapps\common\Granblue Fantasy Versus Rising\GBVSR.exe";
            try
            {
                var key = Registry.LocalMachine.OpenSubKey($@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 2157560");
                if (!String.IsNullOrEmpty(key.GetValue("InstallLocation") as string))
                    defaultPath = $"{key.GetValue("InstallLocation") as string}{Global.s}GBVSR.exe";
            }
            catch (Exception e)
            {
                try
                {
                    var key = Registry.LocalMachine.OpenSubKey($@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 2667960");
                    if (!String.IsNullOrEmpty(key.GetValue("InstallLocation") as string))
                        defaultPath = $"{key.GetValue("InstallLocation") as string}{Global.s}GBVSR.exe";
                }
                catch (Exception ex)
                {

                }
            }
            if (!File.Exists(defaultPath))
            {
                Global.logger.WriteLine($"Couldn't find install path in registry, select path to exe instead", LoggerType.Warning);
                OpenFileDialog dialog = new OpenFileDialog();
                dialog.DefaultExt = ".exe";
                dialog.Filter = $"Executable Files (GBVSR.exe)|GBVSR.exe";
                dialog.Title = $"Select GBVSR.exe from your Steam Install folder";
                dialog.Multiselect = false;
                dialog.InitialDirectory = Global.assemblyLocation;
                dialog.ShowDialog();
                if (!String.IsNullOrEmpty(dialog.FileName)
                    && Path.GetFileName(dialog.FileName).Equals("GBVSR.exe", StringComparison.InvariantCultureIgnoreCase))
                    defaultPath = dialog.FileName;
                else if (!String.IsNullOrEmpty(dialog.FileName))
                {
                    Global.logger.WriteLine($"Invalid .exe chosen", LoggerType.Error);
                    return false;
                }
                else
                    return false;
            }
            var parent = Path.GetDirectoryName(defaultPath);
            var ModsFolder = $"{parent}{Global.s}RED{Global.s}Content{Global.s}Paks{Global.s}~mods";
            Directory.CreateDirectory(ModsFolder);
            Global.config.Configs[Global.config.CurrentGame].ModsFolder = ModsFolder;
            Global.config.Configs[Global.config.CurrentGame].Launcher = defaultPath;
            Global.UpdateConfig();
            Global.logger.WriteLine($"Setup completed for {Global.config.CurrentGame}!", LoggerType.Info);
            return true;
        }

        public static bool Win64FolderSetup(string exe, string projectName, string gameName, string steamId)
        {
            var defaultPath = $@"C:\Program Files (x86)\Steam\steamapps\common\{gameName}\{projectName}\Binaries\Win64\{exe}";
            try
            {
                var key = Registry.LocalMachine.OpenSubKey($@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App {steamId}");
                if (!String.IsNullOrEmpty(key.GetValue("InstallLocation") as string))
                    defaultPath = $"{key.GetValue("InstallLocation") as string}{Global.s}{projectName}{Global.s}Binaries{Global.s}Win64{Global.s}{exe}";
            }
            catch (Exception e)
            {
            }
            if (!File.Exists(defaultPath))
            {
                Global.logger.WriteLine($"Couldn't find install path in registry, select path to exe instead", LoggerType.Warning);
                OpenFileDialog dialog = new OpenFileDialog();
                dialog.DefaultExt = ".exe";
                dialog.Filter = $"Executable Files ({exe})|{exe}";
                dialog.Title = $"Select {exe} from your Steam Install folder";
                dialog.Multiselect = false;
                dialog.InitialDirectory = Global.assemblyLocation;
                dialog.ShowDialog();
                if (!String.IsNullOrEmpty(dialog.FileName)
                    && Path.GetFileName(dialog.FileName).Equals(exe, StringComparison.InvariantCultureIgnoreCase))
                    defaultPath = dialog.FileName;
                else if (!String.IsNullOrEmpty(dialog.FileName))
                {
                    Global.logger.WriteLine($"Invalid .exe chosen", LoggerType.Error);
                    return false;
                }
                else
                    return false;
            }
            var parent = defaultPath.Replace($"{Global.s}{projectName}{Global.s}Binaries{Global.s}Win64{Global.s}{exe}", String.Empty);
            var paks = $"{parent}{Global.s}{projectName}{Global.s}Content{Global.s}Paks";
            var ModsFolder = $"{paks}{Global.s}~mods";
            Directory.CreateDirectory(ModsFolder);
            Global.config.Configs[Global.config.CurrentGame].ModsFolder = ModsFolder;
            Global.config.Configs[Global.config.CurrentGame].Launcher = defaultPath;
            Global.UpdateConfig();
            Global.logger.WriteLine($"Setup completed for {Global.config.CurrentGame}!", LoggerType.Info);
            return true;
        }
        public static bool KHIII()
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.DefaultExt = ".exe";
            dialog.Filter = $"Executable Files (KINGDOM HEARTS III.exe)|KINGDOM HEARTS III.exe";
            dialog.Title = $"Select KINGDOM HEARTS III.exe from your Epic Games Install folder";
            dialog.Multiselect = false;
            dialog.InitialDirectory = Global.assemblyLocation;
            dialog.ShowDialog();
            if (!String.IsNullOrEmpty(dialog.FileName)
                && Path.GetFileName(dialog.FileName).Equals("KINGDOM HEARTS III.exe", StringComparison.InvariantCultureIgnoreCase))
            {
                var parent = dialog.FileName.Replace($"{Global.s}Binaries{Global.s}Win64{Global.s}KINGDOM HEARTS III.exe", String.Empty, StringComparison.InvariantCultureIgnoreCase);
                var paks = $"{parent}{Global.s}Content{Global.s}Paks";
                var ModsFolder = $"{paks}{Global.s}~mods";
                Directory.CreateDirectory(ModsFolder);
                Global.config.Configs[Global.config.CurrentGame].ModsFolder = ModsFolder;
                Global.config.Configs[Global.config.CurrentGame].Launcher = dialog.FileName;
                Global.UpdateConfig();
                Global.logger.WriteLine($"Setup completed for {Global.config.CurrentGame}!", LoggerType.Info);
                return true;
            }
            else if (!String.IsNullOrEmpty(dialog.FileName))
                Global.logger.WriteLine($"Invalid .exe chosen", LoggerType.Error);
            return false;
        }
        public static void CheckJFPatch(string exe)
        {
            var assembly = Assembly.GetExecutingAssembly();
            using (var stream = assembly.GetManifestResourceStream("Unverum.Resources.JUMP_FORCE.exe"))
            {
                if (stream.Length != new FileInfo(exe).Length)
                {
                    byte[] buffer = new byte[stream.Length];
                    stream.Read(buffer, 0, buffer.Length);
                    File.WriteAllBytes(exe, buffer);
                    Global.logger.WriteLine($"{exe} patched to ignore EasyAntiCheat.", LoggerType.Info);
                }
                else
                    Global.logger.WriteLine($"{exe} already patched to ignore EasyAntiCheat.", LoggerType.Info);
            }
        }
        public static bool JF()
        {
            var defaultPath = @"C:\Program Files (x86)\Steam\steamapps\common\JUMP FORCE\JUMP_FORCE.exe";
            try
            {
                var key = Registry.LocalMachine.OpenSubKey($@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 816020");
                if (!String.IsNullOrEmpty(key.GetValue("InstallLocation") as string))
                    defaultPath = $"{key.GetValue("InstallLocation") as string}{Global.s}JUMP_FORCE.exe";
            }
            catch (Exception e)
            {
            }
            if (!File.Exists(defaultPath))
            {
                Global.logger.WriteLine($"Couldn't find install path in registry, select path to exe instead", LoggerType.Warning);
                OpenFileDialog dialog = new OpenFileDialog();
                dialog.DefaultExt = ".exe";
                dialog.Filter = "Executable Files (JUMP_FORCE.exe)|JUMP_FORCE.exe";
                dialog.Title = "Select JUMP_FORCE.exe from your Steam Install folder";
                dialog.Multiselect = false;
                dialog.InitialDirectory = Global.assemblyLocation;
                dialog.ShowDialog();
                if (!String.IsNullOrEmpty(dialog.FileName)
                    && Path.GetFileName(dialog.FileName).Equals("JUMP_FORCE.exe", StringComparison.InvariantCultureIgnoreCase))
                    defaultPath = dialog.FileName;
                else if (!String.IsNullOrEmpty(dialog.FileName))
                {
                    Global.logger.WriteLine($"Invalid .exe chosen", LoggerType.Error);
                    return false;
                }
                else
                    return false;
            }
            var parent = Path.GetDirectoryName(defaultPath);
            var ModsFolder = $"{parent}{Global.s}JUMP_FORCE{Global.s}Content{Global.s}Paks{Global.s}~mods";

            CheckJFPatch(defaultPath);

            Directory.CreateDirectory(ModsFolder);
            Global.config.Configs[Global.config.CurrentGame].ModsFolder = ModsFolder;
            Global.config.Configs[Global.config.CurrentGame].Launcher = defaultPath;
            Global.UpdateConfig();
            Global.logger.WriteLine($"Setup completed!", LoggerType.Info);
            return true;
        }
        public static bool DBFZ()
        {
            var defaultPath = @"C:\Program Files (x86)\Steam\steamapps\common\DRAGON BALL FighterZ\DBFighterZ.exe";
            try
            {
                var key = Registry.LocalMachine.OpenSubKey($@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 678950");
                if (!String.IsNullOrEmpty(key.GetValue("InstallLocation") as string))
                    defaultPath = $"{key.GetValue("InstallLocation") as string}{Global.s}DBFighterZ.exe";
            }
            catch (Exception e)
            {
            }
            if (!File.Exists(defaultPath))
            {
                Global.logger.WriteLine($"Couldn't find install path in registry, select path to exe instead", LoggerType.Warning);
                OpenFileDialog dialog = new OpenFileDialog();
                dialog.DefaultExt = ".exe";
                dialog.Filter = "Executable Files (DBFighterZ.exe)|DBFighterZ.exe";
                dialog.Title = "Select DBFighterZ.exe from your Steam Install folder";
                dialog.Multiselect = false;
                dialog.InitialDirectory = Global.assemblyLocation;
                dialog.ShowDialog();
                if (!String.IsNullOrEmpty(dialog.FileName)
                    && Path.GetFileName(dialog.FileName).Equals("DBFighterZ.exe", StringComparison.InvariantCultureIgnoreCase))
                    defaultPath = dialog.FileName;
                else if (!String.IsNullOrEmpty(dialog.FileName))
                {
                    Global.logger.WriteLine($"Invalid .exe chosen", LoggerType.Error);
                    return false;
                }
                else
                    return false;
            }
            var parent = Path.GetDirectoryName(defaultPath);
            var launcher = $"{parent}{Global.s}RED{Global.s}Binaries{Global.s}Win64{Global.s}RED-Win64-Shipping.exe";
            var renamedLauncher = $"{parent}{Global.s}RED{Global.s}Binaries{Global.s}Win64{Global.s}RED-Win64-Shipping-eac-nop-loaded.exe";
            var ModsFolder = $"{parent}{Global.s}RED{Global.s}Content{Global.s}Paks{Global.s}~mods";
            if (File.Exists(launcher))
                File.Copy(launcher, renamedLauncher, true);
            if (!File.Exists(renamedLauncher))
            {
                Global.logger.WriteLine($"Couldn't find {renamedLauncher}, select the correct exe", LoggerType.Error);
                return false;
            }


            Directory.CreateDirectory(ModsFolder);
            Global.config.Configs[Global.config.CurrentGame].ModsFolder = ModsFolder;
            Global.config.Configs[Global.config.CurrentGame].Launcher = renamedLauncher;

            CheckPatch(renamedLauncher);

            Global.UpdateConfig();
            Global.logger.WriteLine($"Setup completed!", LoggerType.Info);
            return true;

        }

        // TODO: disable Launch/Build if current launcher option setup isn't complete
        public static bool SMTV(bool emu)
        {
            if (emu)
            {
                // Select emulator path
                OpenFileDialog dialog = new OpenFileDialog();
                dialog.DefaultExt = ".exe";
                dialog.Filter = "Emulator Exe|yuzu.exe; Ryujinx.exe";
                dialog.Title = "Select Executable for Emulator (yuzu.exe or Ryujinx.exe)";
                dialog.Multiselect = false;
                dialog.InitialDirectory = Global.assemblyLocation;
                dialog.ShowDialog();
                if (!String.IsNullOrEmpty(dialog.FileName)
                        && (Path.GetFileName(dialog.FileName).Equals("yuzu.exe", StringComparison.InvariantCultureIgnoreCase)
                        || Path.GetFileName(dialog.FileName).Equals("Ryujinx.exe", StringComparison.InvariantCultureIgnoreCase)))
                    Global.config.Configs[Global.config.CurrentGame].Launcher = dialog.FileName;
                else if (!String.IsNullOrEmpty(dialog.FileName))
                {
                    Global.logger.WriteLine($"Invalid .exe chosen", LoggerType.Error);
                    return false;
                }
                else
                    return false;

                // Select game path
                dialog.FileName = String.Empty;
                dialog.DefaultExt = ".xci;.nsp";
                dialog.Filter = "Switch Game|*.xci;*.nsp";
                dialog.Title = "Select Switch Game for Emulator to Launch";
                dialog.Multiselect = false;
                dialog.InitialDirectory = Global.assemblyLocation;
                dialog.ShowDialog();
                if (String.IsNullOrEmpty(dialog.FileName))
                    return false;
                Global.config.Configs[Global.config.CurrentGame].GamePath = dialog.FileName;
            }

            var openFolder = new CommonOpenFileDialog();
            openFolder.AllowNonFileSystemItems = true;
            openFolder.IsFolderPicker = true;
            openFolder.EnsurePathExists = true;
            openFolder.EnsureValidNames = true;
            openFolder.Multiselect = false;
            openFolder.Title = "Select Mod Folder (NA: 010063B012DC6000, EU: 0100B870126CE000, JP: 01006BD0095F4000, HK/TW: 010038D0133C2000, KR: 0100FB70133C0000)";
            var selected = true;
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (openFolder.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    var hash = Path.GetFileName(openFolder.FileName).ToLowerInvariant();
                    switch (hash)
                    {
                        case "010063b012dc6000":
                        case "0100b870126ce000":
                        case "01006bd0095f4000":
                        case "010038d0133c2000":
                        case "0100fb70133c0000":
                            Global.config.Configs[Global.config.CurrentGame].ModsFolder = $"{openFolder.FileName}{Global.s}Unverum Mods{Global.s}romfs{Global.s}Project{Global.s}Content{Global.s}Paks{Global.s}~mods";
                            Global.config.Configs[Global.config.CurrentGame].PatchesFolder = $"{openFolder.FileName}{Global.s}Unverum Mods{Global.s}exefs";
                            break;
                        default:
                            Global.logger.WriteLine($"Invalid output path chosen", LoggerType.Error);
                            selected = false;
                            break;
                    }
                }
                else
                    selected = false;
            });
            Global.UpdateConfig();
            if (selected)
                Global.logger.WriteLine($"Setup completed!", LoggerType.Info);
            return selected;
        }
    }
}
