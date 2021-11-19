using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using xdelta3.net;
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
        public static void PatchExe(string exe, string patch)
        {
            var source = File.ReadAllBytes(exe);
            var assembly = Assembly.GetExecutingAssembly();
            using (var stream = assembly.GetManifestResourceStream($"Unverum.Resources.{patch}.xdelta"))
            {
                byte[] buffer = new byte[stream.Length];
                stream.Read(buffer, 0, buffer.Length);
                var decoded = Xdelta3Lib.Decode(source, buffer);
                File.WriteAllBytes(exe, decoded.ToArray());
            }
            Global.logger.WriteLine($"Applied Costume Patch to {exe}.", LoggerType.Info);
        }
        public static bool CheckCostumePatch(string exe)
        {
            var checksum = GetMD5Checksum(exe).ToLowerInvariant();
            var originalExe = exe.Replace("-eac-nop-loaded", String.Empty);
            var originalChecksum = GetMD5Checksum(originalExe).ToLowerInvariant();
            // Check unpatched exe (without eac tag)
            switch (originalChecksum)
            {
                // Unpatched 1.27 exe
                case "13a83d1fd8f4c71baaa580c69e6a46cf":
                    // Patched 1.27 exe
                    if (!checksum.Equals("2f9c8178fc8a5cdb3ac1ff8fee888f89", StringComparison.InvariantCultureIgnoreCase))
                    {
                        File.Copy(originalExe, exe, true);
                        PatchExe(exe, "CostumePatch");
                    }
                    else
                        Global.logger.WriteLine($"Costume patch already applied to {exe}.", LoggerType.Info);
                    return true;
                // Unpatched 1.28 exe
                case "1f716cdf0846d1db7bdf2b907fead008":
                    // Patched 1.28 exe
                    if (!checksum.Equals("ae356567060af7ffa7d0d9e293fafbd9", StringComparison.InvariantCultureIgnoreCase))
                    {
                        File.Copy(originalExe, exe, true);
                        PatchExe(exe, "v1.28_Patch");
                    }
                    else
                        Global.logger.WriteLine($"Costume patch already applied to {exe}.", LoggerType.Info);
                    return true;
                // Unpatched 1.29 exe
                case "2ddb93cb518b6036b7725552477117bd":
                    // Patched 1.29 exe
                    if (!checksum.Equals("8c442c2913b2be5e4a21730c7c15b39f", StringComparison.InvariantCultureIgnoreCase))
                    {
                        File.Copy(originalExe, exe, true);
                        PatchExe(exe, "v1.29_Patch");
                    }
                    else
                        Global.logger.WriteLine($"Costume patch already applied to {exe}.", LoggerType.Info);
                    return true;
                default:
                    Global.logger.WriteLine($"{exe} wasn't patched since it's not v1.27/v1.28/v1.29", LoggerType.Warning);
                    return false;
            }
        }
        public static bool Generic(string exe, string projectName, string defaultPath)
        {
            if (!File.Exists(defaultPath))
            {
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
            var parent = Path.GetDirectoryName(defaultPath);
            var ModsFolder = $"{parent}{Global.s}{projectName}{Global.s}Content{Global.s}Paks{Global.s}~mods";
            Directory.CreateDirectory(ModsFolder);
            Global.config.Configs[Global.config.CurrentGame].ModsFolder = ModsFolder;
            Global.config.Configs[Global.config.CurrentGame].Launcher = defaultPath;
            Global.UpdateConfig();
            Global.logger.WriteLine($"Setup completed for {Global.config.CurrentGame}!", LoggerType.Info);
            return true;
        }
        public static bool MHOJ2()
        {
            var defaultPath = @"C:\Program Files (x86)\Steam\steamapps\common\My Hero Ones Justice 2\HeroGame\Binaries\Win64\MHOJ2.exe";
            if (!File.Exists(defaultPath))
            {
                OpenFileDialog dialog = new OpenFileDialog();
                dialog.DefaultExt = ".exe";
                dialog.Filter = $"Executable Files (MHOJ2.exe)|MHOJ2.exe";
                dialog.Title = $"Select MHOJ2.exe from your Steam Install folder";
                dialog.Multiselect = false;
                dialog.InitialDirectory = Global.assemblyLocation;
                dialog.ShowDialog();
                if (!String.IsNullOrEmpty(dialog.FileName)
                    && Path.GetFileName(dialog.FileName).Equals("MHOJ2.exe", StringComparison.InvariantCultureIgnoreCase))
                    defaultPath = dialog.FileName;
                else if (!String.IsNullOrEmpty(dialog.FileName))
                {
                    Global.logger.WriteLine($"Invalid .exe chosen", LoggerType.Error);
                    return false;
                }
                else
                    return false;
            }
            var parent = defaultPath.Replace($"{Global.s}HeroGame{Global.s}Binaries{Global.s}Win64{Global.s}MHOJ2.exe", String.Empty);
            var paks = $"{parent}{Global.s}HeroGame{Global.s}Content{Global.s}Paks";
            var ModsFolder = $"{paks}{Global.s}~mods";
            Directory.CreateDirectory(ModsFolder);
            Global.config.Configs[Global.config.CurrentGame].ModsFolder = ModsFolder;
            Global.config.Configs[Global.config.CurrentGame].Launcher = defaultPath;
            Global.UpdateConfig();
            Global.logger.WriteLine($"Setup completed for {Global.config.CurrentGame}!", LoggerType.Info);
            return true;
        }
        public static bool ToA()
        {
            var defaultPath = @"C:\Program Files (x86)\Steam\steamapps\common\Tales of Arise\Arise\Binaries\Win64\Tales of Arise.exe";
            if (!File.Exists(defaultPath))
            {
                OpenFileDialog dialog = new OpenFileDialog();
                dialog.DefaultExt = ".exe";
                dialog.Filter = $"Executable Files (Tales of Arise.exe)|Tales of Arise.exe";
                dialog.Title = $"Select Tales of Arise.exe from your Steam Install folder";
                dialog.Multiselect = false;
                dialog.InitialDirectory = Global.assemblyLocation;
                dialog.ShowDialog();
                if (!String.IsNullOrEmpty(dialog.FileName)
                    && Path.GetFileName(dialog.FileName).Equals("Tales of Arise.exe", StringComparison.InvariantCultureIgnoreCase))
                    defaultPath = dialog.FileName;
                else if (!String.IsNullOrEmpty(dialog.FileName))
                {
                    Global.logger.WriteLine($"Invalid .exe chosen", LoggerType.Error);
                    return false;
                }
                else
                    return false;
            }
            var parent = defaultPath.Replace($"{Global.s}Arise{Global.s}Binaries{Global.s}Win64{Global.s}Tales of Arise.exe", String.Empty);
            var paks = $"{parent}{Global.s}Arise{Global.s}Content{Global.s}Paks";
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
            if (!File.Exists(defaultPath))
            {
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
            if (!File.Exists(defaultPath))
            {
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

            CheckCostumePatch(renamedLauncher);

            Directory.CreateDirectory(ModsFolder);
            Global.config.Configs[Global.config.CurrentGame].ModsFolder = ModsFolder;
            Global.config.Configs[Global.config.CurrentGame].Launcher = renamedLauncher;
            Global.UpdateConfig();
            Global.logger.WriteLine($"Setup completed!", LoggerType.Info);
            return true;

        }

        // TODO: disable Launch/Build if current launcher option setup isnt complete
        public static bool SMTV(bool emu)
        {
            if (emu)
            {
                // Select emulator path
                OpenFileDialog dialog = new OpenFileDialog();
                dialog.DefaultExt = ".exe";
                dialog.Filter = "Emulator Exe|yuzu.exe; Ryujinx.exe";
                dialog.Title = "Select Exectuable for Emulator (yuzu.exe or Ryujinx.exe)";
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
            openFolder.Title = "Select Mod Folder (NA: 010063b012dc6000, EU: 0100B870126CE000, JP: 01006BD0095F4000, HK/TW: 010038D0133C2000, KR: 0100FB70133C0000)";
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
