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
        public static bool CheckCostumePatch(string exe)
        {
            var checksum = GetMD5Checksum(exe);
            // Unpatched 1.27 exe
            if (checksum.Equals("13a83d1fd8f4c71baaa580c69e6a46cf", StringComparison.InvariantCultureIgnoreCase))
            {
                var source = File.ReadAllBytes(exe);
                var assembly = Assembly.GetExecutingAssembly();
                using (var stream = assembly.GetManifestResourceStream("Unverum.Resources.CostumePatch.xdelta"))
                {
                    byte[] buffer = new byte[stream.Length];
                    stream.Read(buffer, 0, buffer.Length);
                    var decoded = Xdelta3Lib.Decode(source, buffer);
                    File.WriteAllBytes(exe, decoded.ToArray());
                }
                Global.logger.WriteLine($"Applied Costume Patch to {exe}.", LoggerType.Info);
                return true;
            }
            // Patched 1.27 exe
            else if (checksum.Equals("2f9c8178fc8a5cdb3ac1ff8fee888f89", StringComparison.InvariantCultureIgnoreCase))
            {
                Global.logger.WriteLine($"Costume Patch already applied to {exe}.", LoggerType.Info);
                return true;
            }
            else
            {
                Global.logger.WriteLine($"{exe} wasn't patched since it's not v1.27", LoggerType.Warning);
                return false;
            }
        }
        public static bool Generic(string exe)
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
            {
                var parent = Path.GetDirectoryName(dialog.FileName);
                var ModsFolder = $"{parent}{Global.s}RED{Global.s}Content{Global.s}Paks{Global.s}~mods";
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
        public static bool MHOJ2()
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
            {
                var parent = dialog.FileName.Replace($"{Global.s}HeroGame{Global.s}Binaries{Global.s}Win64{Global.s}MHOJ2.exe", String.Empty);
                var paks = $"{parent}{Global.s}HeroGame{Global.s}Content{Global.s}Paks";
                // Rename paks for modding to work
                foreach (var file in Directory.GetFiles(paks, "*", SearchOption.TopDirectoryOnly))
                    if (Path.GetExtension(file).Equals(".pak", StringComparison.InvariantCultureIgnoreCase)
                        || Path.GetExtension(file).Equals(".sig", StringComparison.InvariantCultureIgnoreCase))
                        File.Move(file, file.Replace("-WindowsNoEditor_0_P", String.Empty, StringComparison.InvariantCultureIgnoreCase), true);
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
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.DefaultExt = ".exe";
            dialog.Filter = "Executable Files (JUMP_FORCE.exe)|JUMP_FORCE.exe";
            dialog.Title = "Select JUMP_FORCE.exe from your Steam Install folder";
            dialog.Multiselect = false;
            dialog.InitialDirectory = Global.assemblyLocation;
            dialog.ShowDialog();
            if (!String.IsNullOrEmpty(dialog.FileName)
                && Path.GetFileName(dialog.FileName).Equals("JUMP_FORCE.exe", StringComparison.InvariantCultureIgnoreCase))
            {
                var parent = Path.GetDirectoryName(dialog.FileName);
                var ModsFolder = $"{parent}{Global.s}JUMP_FORCE{Global.s}Content{Global.s}Paks{Global.s}~mods";

                CheckJFPatch(dialog.FileName);

                Directory.CreateDirectory(ModsFolder);
                Global.config.Configs[Global.config.CurrentGame].ModsFolder = ModsFolder;
                Global.config.Configs[Global.config.CurrentGame].Launcher = dialog.FileName;
                Global.UpdateConfig();
                Global.logger.WriteLine($"Setup completed!", LoggerType.Info);
                return true;
            }
            else if (!String.IsNullOrEmpty(dialog.FileName))
                Global.logger.WriteLine($"Invalid .exe chosen", LoggerType.Error);
            return false;
        }
        public static bool DBFZ()
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
            {
                var parent = Path.GetDirectoryName(dialog.FileName);
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
            else if (!String.IsNullOrEmpty(dialog.FileName))
                Global.logger.WriteLine($"Invalid .exe chosen", LoggerType.Error);
            return false;
        }
    }
}
