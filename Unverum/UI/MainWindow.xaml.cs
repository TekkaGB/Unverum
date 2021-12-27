﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Text.Json;
using System.Diagnostics;
using System.Reflection;
using System.Windows.Documents;
using System.Text.RegularExpressions;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using System.Net.Http;
using System.Windows.Media;
using Unverum.UI;
using System.Windows.Controls.Primitives;
using System.Security.Cryptography;
using Microsoft.Win32;
using xdelta3.net;
using System.Windows.Input;

namespace Unverum
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public string version;
        // Separated from Global.config so that order is updated when datagrid is modified
        public List<string> exes;
        private FileSystemWatcher ModsWatcher;
        private FlowDocument defaultFlow = new FlowDocument();
        private string defaultText = "Unverum Mod Manager is here to help out with all your UE4 Mods!\n\n" +
            "(Right Click Row > Fetch Metadata and confirm the GameBanana URL of the mod to fetch metadata to show here.)";
        private ObservableCollection<String> LauncherOptions = new ObservableCollection<String>(new string[] { " Executable", " Steam" });
        public MainWindow()
        {
            InitializeComponent();
            Global.logger = new Logger(ConsoleWindow);
            Global.config = new();

            // Get Version Number
            var UnverumVersion = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FileVersion;
            version = UnverumVersion.Substring(0, UnverumVersion.LastIndexOf('.'));

            Global.logger.WriteLine($"Launched Unverum Mod Manager v{version}!", LoggerType.Info);
            // Get Global.config if it exists
            if (File.Exists($@"{Global.assemblyLocation}{Global.s}Config.json"))
            {
                try
                {
                    var configString = File.ReadAllText($@"{Global.assemblyLocation}{Global.s}Config.json");
                    Global.config = JsonSerializer.Deserialize<Config>(configString);
                }
                catch (Exception e)
                {
                    Global.logger.WriteLine(e.Message, LoggerType.Error);
                }
            }

            // Last saved windows settings
            if (Global.config.Height != null && Global.config.Height >= MinHeight)
                Height = (double)Global.config.Height;
            if (Global.config.Width != null && Global.config.Width >= MinWidth)
                Width = (double)Global.config.Width;
            if (Global.config.Maximized)
                WindowState = WindowState.Maximized;
            if (Global.config.TopGridHeight != null)
                MainGrid.RowDefinitions[1].Height = new GridLength((double)Global.config.TopGridHeight, GridUnitType.Star);
            if (Global.config.BottomGridHeight != null)
                MainGrid.RowDefinitions[3].Height = new GridLength((double)Global.config.BottomGridHeight, GridUnitType.Star);
            if (Global.config.LeftGridWidth != null)
                MiddleGrid.ColumnDefinitions[0].Width = new GridLength((double)Global.config.LeftGridWidth, GridUnitType.Star);
            if (Global.config.RightGridWidth != null)
                MiddleGrid.ColumnDefinitions[2].Width = new GridLength((double)Global.config.RightGridWidth, GridUnitType.Star);

            Global.games = new List<string>();
            foreach (var item in GameBox.Items)
            {
                var game = (((item as ComboBoxItem).Content as StackPanel).Children[1] as TextBlock).Text.Trim().Replace(":", String.Empty);
                Global.games.Add(game);
            }

            if (Global.config.Configs == null)
            {
                Global.config.CurrentGame = (((GameBox.SelectedValue as ComboBoxItem).Content as StackPanel).Children[1] as TextBlock).Text.Trim().Replace(":", String.Empty);
                Global.config.Configs = new();
                Global.config.Configs.Add(Global.config.CurrentGame, new());
            }
            else
                GameBox.SelectedIndex = Global.games.IndexOf(Global.config.CurrentGame);

            if (GameBox.SelectedIndex == 5)
                DiscordButton.Visibility = Visibility.Collapsed;

            if (Global.config.Configs[Global.config.CurrentGame].ModList == null)
                Global.config.Configs[Global.config.CurrentGame].ModList = new();

            Global.ModList = Global.config.Configs[Global.config.CurrentGame].ModList;

            if ((String.IsNullOrEmpty(Global.config.Configs[Global.config.CurrentGame].ModsFolder)
                && Global.config.CurrentGame.Equals("Shin Megami Tensei V", StringComparison.InvariantCultureIgnoreCase)
                && Global.config.Configs[Global.config.CurrentGame].LauncherOption) ||
                (!Global.config.CurrentGame.Equals("Shin Megami Tensei V", StringComparison.InvariantCultureIgnoreCase)
                && !Global.config.Configs[Global.config.CurrentGame].LauncherOption &&
                (String.IsNullOrEmpty(Global.config.Configs[Global.config.CurrentGame].ModsFolder)
                || String.IsNullOrEmpty(Global.config.Configs[Global.config.CurrentGame].Launcher) 
                || !File.Exists(Global.config.Configs[Global.config.CurrentGame].Launcher))))
            {
                LaunchButton.IsEnabled = false;
                Global.logger.WriteLine("Please click Setup before starting!", LoggerType.Warning);
            }

            if (Global.config.CurrentGame.Equals("Shin Megami Tensei V", StringComparison.InvariantCultureIgnoreCase))
            {
                LauncherOptions[0] = " Emulator";
                LauncherOptions[1] = " Hardware";
            }
            else if (Global.config.CurrentGame.Equals("Kingdom Hearts III", StringComparison.InvariantCultureIgnoreCase))
                LauncherOptions[1] = " Epic Games";

            Directory.CreateDirectory($@"{Global.assemblyLocation}{Global.s}Mods{Global.s}{Global.config.CurrentGame}");

            // Watch mods folder to detect
            ModsWatcher = new FileSystemWatcher($@"{Global.assemblyLocation}{Global.s}Mods{Global.s}{Global.config.CurrentGame}");
            ModsWatcher.Created += OnModified;
            ModsWatcher.Deleted += OnModified;
            ModsWatcher.Renamed += OnModified;

            Refresh();

            ModsWatcher.EnableRaisingEvents = true;

            defaultFlow.Blocks.Add(ConvertToFlowParagraph(defaultText));
            DescriptionWindow.Document = defaultFlow;
            var bitmap = new BitmapImage(new Uri("pack://application:,,,/Unverum;component/Assets/unverumpreview.png"));
            Preview.Source = bitmap;
            PreviewBG.Source = null;

            Global.logger.WriteLine("Checking for updates...", LoggerType.Info);
            GameBox.IsEnabled = false;
            ModGrid.IsEnabled = false;
            ConfigButton.IsEnabled = false;
            LaunchButton.IsEnabled = false;
            OpenModsButton.IsEnabled = false;
            UpdateButton.IsEnabled = false;
            LauncherOptionsBox.IsEnabled = false;
            App.Current.Dispatcher.Invoke(() =>
            {
                ModUpdater.CheckForUpdates($"{Global.assemblyLocation}{Global.s}Mods{Global.s}{Global.config.CurrentGame}", this);
            });
        }
        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            OnFirstOpen();

            if (Global.config.CurrentGame.Equals("Dragon Ball FighterZ", StringComparison.InvariantCultureIgnoreCase))
                LauncherOptionsBox.IsEnabled = false;
            else
                LauncherOptionsBox.IsEnabled = true;

            LauncherOptionsBox.ItemsSource = LauncherOptions;
            LauncherOptionsBox.SelectedIndex = Convert.ToInt32(Global.config.Configs[Global.config.CurrentGame].LauncherOption);
        }
        private void OnModified(object sender, FileSystemEventArgs e)
        {
            Refresh();
            Global.UpdateConfig();
            // Bring window to front after download is done
            App.Current.Dispatcher.Invoke((Action)delegate
            {
                Activate();
            });
        }

        private async void Refresh()
        {
            var currentModDirectory = $@"{Global.assemblyLocation}{Global.s}Mods{Global.s}{Global.config.CurrentGame}";
            // Add new folders found in Mods to the ModList
            foreach (var mod in Directory.GetDirectories(currentModDirectory))
            {
                if (Global.ModList.ToList().Where(x => x.name == Path.GetFileName(mod)).Count() == 0)
                {
                    Mod m = new Mod();
                    m.name = Path.GetFileName(mod);
                    m.enabled = true;
                    App.Current.Dispatcher.Invoke((Action)delegate
                    {
                        Global.ModList.Add(m);
                    });
                    Global.logger.WriteLine($"Added {Path.GetFileName(mod)}", LoggerType.Info);
                }
            }
            // Remove deleted folders that are still in the ModList
            foreach (var mod in Global.ModList.ToList())
            {
                if (!Directory.GetDirectories(currentModDirectory).ToList().Select(x => Path.GetFileName(x)).Contains(mod.name))
                {
                    App.Current.Dispatcher.Invoke((Action)delegate
                    {
                        Global.ModList.Remove(mod);
                    });
                    Global.logger.WriteLine($"Deleted {mod.name}", LoggerType.Info);
                }
            }

            await Task.Run(() =>
            {
                App.Current.Dispatcher.Invoke((Action)delegate
                {
                    ModGrid.ItemsSource = Global.ModList;
                    Stats.Text = $"{Global.ModList.Count} mods • {Directory.GetFiles($@"{Global.assemblyLocation}{Global.s}Mods{Global.s}{Global.config.CurrentGame}", "*", SearchOption.AllDirectories).Length.ToString("N0")} files • " +
                    $"{StringConverters.FormatSize(new DirectoryInfo($@"{Global.assemblyLocation}{Global.s}Mods{Global.s}{Global.config.CurrentGame}").GetDirectorySize())} • v{version}";
                });
            });
            Global.config.Configs[Global.config.CurrentGame].ModList = Global.ModList;
            Global.logger.WriteLine("Refreshed!", LoggerType.Info);
        }

        // Events for Enabled checkboxes
        private void OnChecked(object sender, RoutedEventArgs e)
        {
            var checkBox = e.OriginalSource as CheckBox;

            Mod mod = checkBox?.DataContext as Mod;

            if (mod != null)
            {
                mod.enabled = true;
                List<Mod> temp = Global.config.Configs[Global.config.CurrentGame].ModList.ToList();
                foreach (var m in temp)
                {
                    if (m.name == mod.name)
                        m.enabled = true;
                }
                Global.config.Configs[Global.config.CurrentGame].ModList = new ObservableCollection<Mod>(temp);
                Global.UpdateConfig();
            }
        }
        private void OnUnchecked(object sender, RoutedEventArgs e)
        {
            var checkBox = e.OriginalSource as CheckBox;

            Mod mod = checkBox?.DataContext as Mod;

            if (mod != null)
            {
                mod.enabled = false;
                List<Mod> temp = Global.config.Configs[Global.config.CurrentGame].ModList.ToList();
                foreach (var m in temp)
                {
                    if (m.name == mod.name)
                        m.enabled = false;
                }
                Global.config.Configs[Global.config.CurrentGame].ModList = new ObservableCollection<Mod>(temp);
                Global.UpdateConfig();
            }
        }
        // Triggered when priority is switched on drag and dropped
        private void ModGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            Global.UpdateConfig();
        }

        private bool SetupGame()
        {
            var index = 0;
            bool emu = true;
            Application.Current.Dispatcher.Invoke(() =>
            {
                index = GameBox.SelectedIndex;
                emu = LauncherOptionsBox.SelectedIndex == 0;
            });
            var game = (GameFilter)index;
            switch (game)
            {
                case GameFilter.DBFZ:
                    return Setup.DBFZ();
                case GameFilter.MHOJ2:
                    return Setup.MHOJ2();
                case GameFilter.GBVS:
                    return Setup.Generic("GBVS.exe", "RED", @"C:\Program Files (x86)\Steam\steamapps\common\Granblue Fantasy Versus\GBVS.exe");
                case GameFilter.GGS:
                    return Setup.Generic("GGST.exe", "RED", @"C:\Program Files (x86)\Steam\steamapps\common\GUILTY GEAR -STRIVE-\GGST.exe");
                case GameFilter.JF:
                    return Setup.JF();
                case GameFilter.KHIII:
                    return Setup.KHIII();
                case GameFilter.SN:
                    return Setup.Generic("ScarletNexus.exe", "ScarletNexus", @"C:\Program Files (x86)\Steam\steamapps\common\ScarletNexus\ScarletNexus.exe");
                case GameFilter.ToA:
                    return Setup.ToA();
                case GameFilter.DS:
                    return Setup.Generic("APK.exe", "APK", @"C:\Program Files (x86)\Steam\steamapps\common\Demon Slayer\APK.exe");
                case GameFilter.IM:
                    return Setup.Generic("StarlitSeason.exe", "StarlitSeason", @"C:\Program Files (x86)\Steam\steamapps\common\StarlitSeason\StarlitSeason.exe");
                case GameFilter.SMTV:
                    return Setup.SMTV(emu);
                case GameFilter.FNAFSB:
                    return Setup.Generic("fnaf9.exe", "fnaf9", @"C:\Program Files (x86)\Steam\steamapps\common\FNaF - Security Breach\fnaf0.exe");
            }
            return false;
        }

        private async void Setup_Click(object sender, RoutedEventArgs e)
        {
            GameBox.IsEnabled = false;
            await Task.Run(() =>
            {
                var index = 0;
                Dispatcher.Invoke(() =>
                {
                    index = GameBox.SelectedIndex;
                });
                if (!String.IsNullOrEmpty(Global.config.Configs[Global.config.CurrentGame].ModsFolder)
                    || !String.IsNullOrEmpty(Global.config.Configs[Global.config.CurrentGame].Launcher) && File.Exists(Global.config.Configs[Global.config.CurrentGame].Launcher))
                {
                    var dialogResult = MessageBox.Show($@"Setup again?", $@"Notification", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (dialogResult == MessageBoxResult.No)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            GameBox.IsEnabled = true;
                        });
                        return;
                    }
                }
                if (SetupGame())
                {
                    Dispatcher.Invoke(() =>
                    {
                        LaunchButton.IsEnabled = true;
                    });
                }
            });
            GameBox.IsEnabled = true;
        }
        private async void Launch_Click(object sender, RoutedEventArgs e)
        {
            if (Global.config.Configs[Global.config.CurrentGame].ModsFolder != null)
            {
                GameBox.IsEnabled = false;
                ModGrid.IsEnabled = false;
                ConfigButton.IsEnabled = false;
                LaunchButton.IsEnabled = false;
                OpenModsButton.IsEnabled = false;
                UpdateButton.IsEnabled = false;
                LauncherOptionsBox.IsEnabled = false;
                Refresh();
                Directory.CreateDirectory(Global.config.Configs[Global.config.CurrentGame].ModsFolder);
                Global.logger.WriteLine($"Building loadout for {Global.config.CurrentGame}", LoggerType.Info);
                if (!await Build(Global.config.Configs[Global.config.CurrentGame].ModsFolder))
                {
                    Global.logger.WriteLine($"Failed to build loadout, not building and launching", LoggerType.Error);
                    ModGrid.IsEnabled = true;
                    ConfigButton.IsEnabled = true;
                    LaunchButton.IsEnabled = true;
                    OpenModsButton.IsEnabled = true;
                    UpdateButton.IsEnabled = true;
                    GameBox.IsEnabled = true;
                    LauncherOptionsBox.IsEnabled = true;
                    return;
                }
                ModGrid.IsEnabled = true;
                ConfigButton.IsEnabled = true;
                LaunchButton.IsEnabled = true;
                OpenModsButton.IsEnabled = true;
                UpdateButton.IsEnabled = true;
                GameBox.IsEnabled = true;
                LauncherOptionsBox.IsEnabled = true;
            }
            else
            {
                Global.logger.WriteLine("Please click Setup before starting!", LoggerType.Warning);
                return;
            }
            if (Global.config.CurrentGame.Equals("Shin Megami Tensei V", StringComparison.InvariantCultureIgnoreCase))
            {
                if (LauncherOptionsBox.SelectedIndex == 1)
                    return;
                else if (Global.config.Configs[Global.config.CurrentGame].Launcher == null || !File.Exists(Global.config.Configs[Global.config.CurrentGame].Launcher)
                    && Global.config.Configs[Global.config.CurrentGame].GamePath == null || !File.Exists(Global.config.Configs[Global.config.CurrentGame].GamePath))
                {
                    Global.logger.WriteLine($"Please click Setup to configure launching from emulator!", LoggerType.Warning);
                    return;
                }
                else
                {
                    try
                    {
                        Global.logger.WriteLine($"Launching {Global.config.Configs[Global.config.CurrentGame].GamePath} with {Global.config.Configs[Global.config.CurrentGame].Launcher}", LoggerType.Info);
                        var ps = new ProcessStartInfo(Global.config.Configs[Global.config.CurrentGame].Launcher)
                        {
                            WorkingDirectory = Path.GetDirectoryName(Global.config.Configs[Global.config.CurrentGame].Launcher),
                            UseShellExecute = true,
                            Verb = "open",
                            Arguments = $"\"{Global.config.Configs[Global.config.CurrentGame].GamePath}\""
                        };
                        Process.Start(ps);
                    }
                    catch (Exception ex)
                    {
                        Global.logger.WriteLine($"Couldn't launch {Global.config.Configs[Global.config.CurrentGame].GamePath} with {Global.config.Configs[Global.config.CurrentGame].Launcher} ({ex.Message})", LoggerType.Error);
                    }

                }
            }
            else if (Global.config.Configs[Global.config.CurrentGame].Launcher != null && File.Exists(Global.config.Configs[Global.config.CurrentGame].Launcher))
            {
                var path = Global.config.Configs[Global.config.CurrentGame].Launcher;
                try
                {
                    Global.config.Configs[Global.config.CurrentGame].LauncherOption = Convert.ToBoolean(LauncherOptionsBox.SelectedIndex);
                    Global.UpdateConfig();
                    if (Global.config.Configs[Global.config.CurrentGame].LauncherOption)
                    {
                        var id = "";
                        var epic = false;
                        switch ((GameFilter)GameBox.SelectedIndex)
                        {
                            case GameFilter.DBFZ:
                                Global.logger.WriteLine($"Mods will not work since DBFZ is being launched through Steam", LoggerType.Warning);
                                id = "678950";
                                break;
                            case GameFilter.MHOJ2:
                                id = "1058450";
                                break;
                            case GameFilter.GBVS:
                                id = "1090630";
                                break;
                            case GameFilter.GGS:
                                id = "1384160";
                                break;
                            case GameFilter.JF:
                                id = "816020";
                                break;
                            case GameFilter.KHIII:
                                id = "fd711544a06543e0ab1b0808de334120";
                                epic = true;
                                break;
                            case GameFilter.SN:
                                id = "775500";
                                break;
                            case GameFilter.ToA:
                                id = "740130";
                                break;
                            case GameFilter.DS:
                                id = "1490890";
                                break;
                            case GameFilter.IM:
                                id = "1046480";
                                break;
                            case GameFilter.FNAFSB:
                                id = "747660";
                                break;
                        }
                        path = epic ? $"com.epicgames.launcher://apps/{id}?action=launch&silent=true" : $"steam://rungameid/{id}";
                    }
                    Global.logger.WriteLine($"Launching {path}", LoggerType.Info);
                    var ps = new ProcessStartInfo(path)
                    {
                        WorkingDirectory = Path.GetDirectoryName(Global.config.Configs[Global.config.CurrentGame].Launcher),
                        UseShellExecute = true,
                        Verb = "open"
                    };
                    Process.Start(ps);
                }
                catch (Exception ex)
                {
                    Global.logger.WriteLine($"Couldn't launch {path} ({ex.Message})", LoggerType.Error);
                }
            }
            else
                Global.logger.WriteLine($"Please click Setup before starting!", LoggerType.Warning);
        }
        private void GameBanana_Click(object sender, RoutedEventArgs e)
        {
            var id = "";
            switch ((GameFilter)GameFilterBox.SelectedIndex)
            {
                case GameFilter.DBFZ:
                    id = "6246";
                    break;
                case GameFilter.MHOJ2:
                    id = "11605";
                    break;
                case GameFilter.GBVS:
                    id = "8897";
                    break;
                case GameFilter.GGS:
                    id = "11534";
                    break;
                case GameFilter.JF:
                    id = "7019";
                    break;
                case GameFilter.KHIII:
                    id = "9219";
                    break;
                case GameFilter.SN:
                    id = "12028";
                    break;
                case GameFilter.ToA:
                    id = "13821";
                    break;
                case GameFilter.DS:
                    id = "14246";
                    break;
                case GameFilter.IM:
                    id = "14247";
                    break;
                case GameFilter.SMTV:
                    id = "14768";
                    break;
                case GameFilter.FNAFSB:
                    id = "15359";
                    break;
            }
            try
            {
                var ps = new ProcessStartInfo($"https://gamebanana.com/games/{id}")
                {
                    UseShellExecute = true,
                    Verb = "open"
                };
                Process.Start(ps);
            }
            catch (Exception ex)
            {
                Global.logger.WriteLine($"Couldn't open up GameBanana ({ex.Message})", LoggerType.Error);
            }
        }
        private void Discord_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var discordLink = (managerSelected && GameBox.SelectedIndex == 7) || (!managerSelected && GameFilterBox.SelectedIndex == 7) 
                    ? "https://discord.gg/Se2XTnA" : "https://discord.gg/tgFrebr";
                var ps = new ProcessStartInfo(discordLink)
                {
                    UseShellExecute = true,
                    Verb = "open"
                };
                Process.Start(ps);
            }
            catch (Exception ex)
            {
                Global.logger.WriteLine(ex.Message, LoggerType.Error);
            }
        }
        private void ScrollToBottom(object sender, TextChangedEventArgs args)
        {
            ConsoleWindow.ScrollToEnd();
        }

        private void ModGrid_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            FrameworkElement element = sender as FrameworkElement;
            if (element == null)
            {
                return;
            }

            if (ModGrid.SelectedItem == null)
                element.ContextMenu.Visibility = Visibility.Collapsed;
            else
                element.ContextMenu.Visibility = Visibility.Visible;
        }

        private void DeleteItem_Click(object sender, RoutedEventArgs e)
        {
            var selectedMods = ModGrid.SelectedItems;
            var temp = new Mod[selectedMods.Count];
            selectedMods.CopyTo(temp, 0);
            foreach (var row in temp)
                if (row != null)
                {
                    var dialogResult = MessageBox.Show($@"Are you sure you want to delete {row.name}?" + Environment.NewLine + "This cannot be undone.", $@"Deleting {row.name}: Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (dialogResult == MessageBoxResult.Yes)
                    {
                        try
                        {
                            Directory.Delete($@"{Global.assemblyLocation}{Global.s}Mods{Global.s}{Global.config.CurrentGame}{Global.s}{row.name}", true);
                            Global.logger.WriteLine($@"Deleting {row.name}.", LoggerType.Info);
                            ShowMetadata(null);
                        }
                        catch (Exception ex)
                        {
                            Global.logger.WriteLine($@"Couldn't delete {row.name} ({ex.Message})", LoggerType.Error);
                        }
                    }
                }
        }

        private async Task<bool> Build(string path)
        {
            return await Task.Run(() =>
            {
                // Get other folders using the mods folder
                string SplashFolder = null;
                string MoviesFolder = null;
                string SoundsFolder = null;
                var ContentFolder = new DirectoryInfo(Global.config.Configs[Global.config.CurrentGame].ModsFolder).Parent.Parent.FullName;
                if (Directory.Exists($"{ContentFolder}{Global.s}Splash"))
                    SplashFolder = $"{ContentFolder}{Global.s}Splash";
                if (Directory.Exists($"{ContentFolder}{Global.s}Movies"))
                    MoviesFolder = $"{ContentFolder}{Global.s}Movies";
                else if (Directory.Exists($"{ContentFolder}{Global.s}Binaries{Global.s}Movie"))
                    MoviesFolder = $"{ContentFolder}{Global.s}Binaries{Global.s}Movie";
                else if (Directory.Exists($"{ContentFolder}{Global.s}Movies"))
                    MoviesFolder = $"{ContentFolder}{Global.s}Movie";
                if (Directory.Exists($"{ContentFolder}{Global.s}Sound") && !Global.config.CurrentGame.Equals("Shin Megami Tensei V", StringComparison.InvariantCultureIgnoreCase))
                    SoundsFolder = $"{ContentFolder}{Global.s}Sound";
                else if (Directory.Exists($"{ContentFolder}{Global.s}CriWareData"))
                    SoundsFolder = $"{ContentFolder}{Global.s}CriWareData";
                // DBFZ specific
                bool? CostumePatched = null;
                if (Global.config.CurrentGame == "Dragon Ball FighterZ")
                    CostumePatched = Setup.CheckCostumePatch(Global.config.Configs[Global.config.CurrentGame].Launcher);
                if (!ModLoader.Restart(path, MoviesFolder, SplashFolder, SoundsFolder))
                    return false;
                List<string> mods = Global.config.Configs[Global.config.CurrentGame].ModList.Where(x => x.enabled).Select(y => $@"{Global.assemblyLocation}{Global.s}Mods{Global.s}{Global.config.CurrentGame}{Global.s}{y.name}").ToList();
                mods.Reverse();

                // Rename HeroGame back since its no longer needed to be renamed
                var index = 0;
                Application.Current.Dispatcher.Invoke(() =>
                {
                    index = GameBox.SelectedIndex;
                });
                if ((GameFilter)index == GameFilter.MHOJ2)
                    foreach (var file in Directory.GetFiles(Path.GetDirectoryName(Global.config.Configs[Global.config.CurrentGame].ModsFolder), "*", SearchOption.TopDirectoryOnly))
                    if (Path.GetExtension(file).Equals(".pak", StringComparison.InvariantCultureIgnoreCase)
                        || Path.GetExtension(file).Equals(".sig", StringComparison.InvariantCultureIgnoreCase))
                        File.Move(file, file.Replace("HeroGame.", "HeroGame-WindowsNoEditor_0_P.", StringComparison.InvariantCultureIgnoreCase), true);

                ModLoader.Build(path, mods, CostumePatched, MoviesFolder, SplashFolder, SoundsFolder);
                return true;
            });
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                Global.config.Height = RestoreBounds.Height;
                Global.config.Width = RestoreBounds.Width;
                Global.config.Maximized = true;
            }
            else
            {
                Global.config.Height = Height;
                Global.config.Width = Width;
                Global.config.Maximized = false;
            }
            Global.config.TopGridHeight = MainGrid.RowDefinitions[1].Height.Value;
            Global.config.BottomGridHeight = MainGrid.RowDefinitions[3].Height.Value;
            Global.config.LeftGridWidth = MiddleGrid.ColumnDefinitions[0].Width.Value;
            Global.config.RightGridWidth = MiddleGrid.ColumnDefinitions[2].Width.Value;
            Global.UpdateConfig();
            Application.Current.Shutdown();
        }

        private void OpenItem_Click(object sender, RoutedEventArgs e)
        {
            var selectedMods = ModGrid.SelectedItems;
            var temp = new Mod[selectedMods.Count];
            selectedMods.CopyTo(temp, 0);
            foreach (var row in temp)
                if (row != null)
                {
                    var folderName = $@"{Global.assemblyLocation}{Global.s}Mods{Global.s}{Global.config.CurrentGame}{Global.s}{row.name}";
                    if (Directory.Exists(folderName))
                    {
                        try
                        {
                            Process process = Process.Start("explorer.exe", folderName);
                            Global.logger.WriteLine($@"Opened {folderName}.", LoggerType.Info);
                        }
                        catch (Exception ex)
                        {
                            Global.logger.WriteLine($@"Couldn't open {folderName}. ({ex.Message})", LoggerType.Error);
                        }
                    }
                }
        }
        private void EditItem_Click(object sender, RoutedEventArgs e)
        {
            var selectedMods = ModGrid.SelectedItems;
            var temp = new Mod[selectedMods.Count];
            selectedMods.CopyTo(temp, 0);
            foreach (var row in temp)
                if (row != null)
                {
                    EditWindow ew = new EditWindow(row);
                    ew.ShowDialog();
                }
        }
        private void FetchItem_Click(object sender, RoutedEventArgs e)
        {
            var selectedMods = ModGrid.SelectedItems;
            var temp = new Mod[selectedMods.Count];
            selectedMods.CopyTo(temp, 0);
            foreach (var row in temp)
                if (row != null)
                {
                    FetchWindow fw = new FetchWindow(row);
                    fw.ShowDialog();
                    if (fw.success)
                        ShowMetadata(row.name);
                }
        }
        private void Add_Enter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Handled = true;
                e.Effects = DragDropEffects.Move;
                DropBox.Visibility = Visibility.Visible;
            }
        }
        private void Add_Leave(object sender, DragEventArgs e)
        {
            e.Handled = true;
            DropBox.Visibility = Visibility.Collapsed;
        }
        private void Add_Drop(object sender, DragEventArgs e)
        {
            e.Handled = true;
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] fileList = (string[])e.Data.GetData(DataFormats.FileDrop, false);
                CreateMod(fileList);
            }
            DropBox.Visibility = Visibility.Collapsed;
        }
        private void CreateMod(string[] files)
        {
            var nameWindow = new EditWindow(null);
            nameWindow.ShowDialog();
            if (nameWindow.directory != null)
            {
                Directory.CreateDirectory(nameWindow.directory);
                string defaultSig = null;
                if (Directory.Exists(Global.config.Configs[Global.config.CurrentGame].ModsFolder))
                {
                    var sigs = Directory.GetFiles(Path.GetDirectoryName(Global.config.Configs[Global.config.CurrentGame].ModsFolder), "*.sig", SearchOption.TopDirectoryOnly);
                    if (sigs.Length > 0)
                        defaultSig = sigs[0];
                }
                foreach (var file in files)
                {
                    // Get the file attributes for file or directory
                    FileAttributes attr = File.GetAttributes(file);

                    if (attr.HasFlag(FileAttributes.Directory))
                    {
                        foreach (string path in Directory.GetFiles(file, "*.*", SearchOption.AllDirectories))
                        {
                            var newPath = path.Replace(file, $"{nameWindow.directory}{Global.s}{Path.GetFileName(file)}");
                            Directory.CreateDirectory(Path.GetDirectoryName(newPath));
                            File.Copy(path, newPath, true);
                            if (Path.GetExtension(path).Equals(".pak", StringComparison.InvariantCultureIgnoreCase) && defaultSig != null)
                            {
                                var sig = Path.ChangeExtension(path, ".sig");
                                var newPathSig = Path.ChangeExtension(newPath, ".sig");
                                // Check if mod folder has corresponding .sig
                                if (File.Exists(sig))
                                    File.Copy(sig, newPathSig, true);
                                // Otherwise copy over original game's .sig
                                else if (File.Exists(defaultSig))
                                    File.Copy(defaultSig, newPathSig, true);
                            }
                        }
                    }
                    else
                    {
                        var newPath = $"{nameWindow.directory}{Global.s}{Path.GetFileName(file)}";
                        File.Copy(file, newPath, true);
                        if (Path.GetExtension(file).Equals(".pak", StringComparison.InvariantCultureIgnoreCase) && defaultSig != null)
                        {
                            var sig = Path.ChangeExtension(file, ".sig");
                            var newPathSig = Path.ChangeExtension(newPath, ".sig");
                            // Check if mod folder has corresponding .sig
                            if (File.Exists(sig))
                                File.Copy(sig, newPathSig, true);
                            // Otherwise copy over original game's .sig
                            else if (File.Exists(defaultSig))
                                File.Copy(defaultSig, newPathSig, true);
                        }
                    }
                }
            }
        }
        private void ModsFolder_Click(object sender, RoutedEventArgs e)
        {
            var choice = new ChoiceWindow("Create New Mod", "Name a mod and choose the .pak file for it to use",
                "Open Mods Folder", "Drag or extract mod folders into this directory");
            choice.ShowDialog();
            if (choice.choice != null && (bool)choice.choice)
            {
                var nameWindow = new EditWindow(null);
                nameWindow.ShowDialog();
                if (nameWindow.directory != null)
                {
                    OpenFileDialog dialog = new OpenFileDialog();
                    dialog.DefaultExt = ".pak";
                    dialog.Filter = "UE4 Package Files (*.pak)|*.pak";
                    dialog.Title = $"Select .pak to add in {Path.GetFileName(nameWindow.directory)}";
                    dialog.Multiselect = false;
                    dialog.ShowDialog();
                    if (!String.IsNullOrEmpty(dialog.FileName))
                    {
                        Directory.CreateDirectory(nameWindow.directory);
                        File.Copy(dialog.FileName, $"{nameWindow.directory}{Global.s}{Path.GetFileName(dialog.FileName)}", true);
                        if (Directory.Exists(Global.config.Configs[Global.config.CurrentGame].ModsFolder))
                        {
                            // Copy over sig if it exists
                            var sigs = Directory.GetFiles(Path.GetDirectoryName(Global.config.Configs[Global.config.CurrentGame].ModsFolder), "*.sig", SearchOption.TopDirectoryOnly);
                            if (sigs.Length > 0)
                                File.Copy(sigs[0], Path.ChangeExtension($"{nameWindow.directory}{Global.s}{Path.GetFileName(dialog.FileName)}", ".sig"), true);
                        }
                    }
                }
            }
            else if (choice.choice != null && !(bool)choice.choice) 
            { 
                var folderName = $"{Global.assemblyLocation}{Global.s}Mods{Global.s}{Global.config.CurrentGame}";
                if (Directory.Exists(folderName))
                {
                    try
                    {
                        Process process = Process.Start("explorer.exe", folderName);
                        Global.logger.WriteLine($@"Opened {folderName}.", LoggerType.Info);
                    }
                    catch (Exception ex)
                    {
                        Global.logger.WriteLine($@"Couldn't open {folderName}. ({ex.Message})", LoggerType.Error);
                    }
                }
            }
        }
        private void Update_Click(object sender, RoutedEventArgs e)
        {
            Global.logger.WriteLine("Checking for updates...", LoggerType.Info);
            GameBox.IsEnabled = false;
            ModGrid.IsEnabled = false;
            ConfigButton.IsEnabled = false;
            LaunchButton.IsEnabled = false;
            OpenModsButton.IsEnabled = false;
            UpdateButton.IsEnabled = false;
            LauncherOptionsBox.IsEnabled = false;
            App.Current.Dispatcher.Invoke(() =>
            {
                ModUpdater.CheckForUpdates($"{Global.assemblyLocation}{Global.s}Mods{Global.s}{Global.config.CurrentGame}", this);
            });
        }
        private Paragraph ConvertToFlowParagraph(string text)
        {
            var flowDocument = new FlowDocument();

            var regex = new Regex(@"(https?:\/\/[^\s]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            var matches = regex.Matches(text).Cast<Match>().Select(m => m.Value).ToList();

            var paragraph = new Paragraph();
            flowDocument.Blocks.Add(paragraph);


            foreach (var segment in regex.Split(text))
            {
                if (matches.Contains(segment))
                {
                    var hyperlink = new Hyperlink(new Run(segment))
                    {
                        NavigateUri = new Uri(segment),
                    };

                    hyperlink.RequestNavigate += (sender, args) =>
                    {
                        var ps = new ProcessStartInfo(segment)
                        {
                            UseShellExecute = true,
                            Verb = "open"
                        };
                        Process.Start(ps);
                    };

                    paragraph.Inlines.Add(hyperlink);
                }
                else
                {
                    paragraph.Inlines.Add(new Run(segment));
                }
            }

            return paragraph;
        }

        private void ShowMetadata(string mod)
        {
            if (mod == null || !File.Exists($"{Global.assemblyLocation}{Global.s}Mods{Global.s}{Global.config.CurrentGame}{Global.s}{mod}{Global.s}mod.json"))
            {
                DescriptionWindow.Document = defaultFlow;
                var bitmap = new BitmapImage(new Uri("pack://application:,,,/Unverum;component/Assets/unverumpreview.png"));
                Preview.Source = bitmap;
                PreviewBG.Source = null;
            }
            else
            {
                FlowDocument descFlow = new FlowDocument();
                var metadataString = File.ReadAllText($"{Global.assemblyLocation}{Global.s}Mods{Global.s}{Global.config.CurrentGame}{Global.s}{mod}{Global.s}mod.json");
                Metadata metadata = JsonSerializer.Deserialize<Metadata>(metadataString);

                var para = new Paragraph();
                if (metadata.submitter != null)
                {
                    para.Inlines.Add($"Submitter: ");
                    if (metadata.avi != null && metadata.avi.ToString().Length > 0)
                    {
                        BitmapImage bm = new BitmapImage(metadata.avi);
                        Image image = new Image();
                        image.Source = bm;
                        image.Height = 35;
                        para.Inlines.Add(image);
                        para.Inlines.Add(" ");
                    }
                    if (metadata.upic != null && metadata.upic.ToString().Length > 0)
                    {
                        BitmapImage bm = new BitmapImage(metadata.upic);
                        Image image = new Image();
                        image.Source = bm;
                        image.Height= 25;
                        para.Inlines.Add(image);
                    }
                    else
                        para.Inlines.Add(metadata.submitter);
                    descFlow.Blocks.Add(para);
                }
                if (metadata.preview != null)
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = metadata.preview;
                    bitmap.EndInit();
                    Preview.Source = bitmap;
                    PreviewBG.Source = bitmap;
                }
                else
                {
                    var bitmap = new BitmapImage(new Uri("pack://application:,,,/Unverum;component/Assets/unverumpreview.png"));
                    Preview.Source = bitmap;
                    PreviewBG.Source = null;
                }
                    para = new Paragraph();
                    para.Inlines.Add("Category: ");
                if (metadata.caticon != null && metadata.caticon.ToString().Length > 0)
                {
                    BitmapImage bm = new BitmapImage(metadata.caticon);
                    Image image = new Image();
                    image.Source = bm;
                    image.Width = 20;
                    para.Inlines.Add(image);
                }
                para.Inlines.Add($" {metadata.cat} {metadata.section}");
                descFlow.Blocks.Add(para);
                var text = "";
                if (metadata.description != null && metadata.description.Length > 0)
                    text += $"Description: {metadata.description}\n\n";
                if (metadata.homepage != null && metadata.homepage.ToString().Length > 0)
                    text += $"Home Page: {metadata.homepage}";
                var init = ConvertToFlowParagraph(text);
                descFlow.Blocks.Add(init);
                DescriptionWindow.Document = descFlow;
                var descriptionText = new TextRange(DescriptionWindow.Document.ContentStart, DescriptionWindow.Document.ContentEnd);
                descriptionText.ApplyPropertyValue(Inline.BaselineAlignmentProperty, BaselineAlignment.Center);
            }
        }
        private void ModGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Mod row = (Mod)ModGrid.SelectedItem;
            if (row != null)
                ShowMetadata(row.name);
        }

        private void Download_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            var item = button.DataContext as GameBananaRecord;
            new ModDownloader().BrowserDownload(Global.games[GameFilterBox.SelectedIndex], item);
        }
        private void AltDownload_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            var item = button.DataContext as GameBananaRecord;
            new AltLinkWindow(item.AlternateFileSources, item.Title,
                (((GameFilterBox.SelectedValue as ComboBoxItem).Content as StackPanel).Children[1] as TextBlock).Text.Trim().Replace(":", String.Empty),
                item.Link.AbsoluteUri).ShowDialog();
        }
        private void Homepage_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            var item = button.DataContext as GameBananaRecord;
            try
            {
                var ps = new ProcessStartInfo(item.Link.ToString())
                {
                    UseShellExecute = true,
                    Verb = "open"
                };
                Process.Start(ps);
            }
            catch (Exception ex)
            {
                Global.logger.WriteLine($"Couldn't open up {item.Link} ({ex.Message})", LoggerType.Error);
            }
        }
        private int imageCounter;
        private int imageCount;
        private FlowDocument ConvertToFlowDocument(string text)
        {
            var flowDocument = new FlowDocument();

            var regex = new Regex(@"(https?:\/\/[^\s]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            var matches = regex.Matches(text).Cast<Match>().Select(m => m.Value).ToList();

            var paragraph = new Paragraph();
            flowDocument.Blocks.Add(paragraph);


            foreach (var segment in regex.Split(text))
            {
                if (matches.Contains(segment))
                {
                    var hyperlink = new Hyperlink(new Run(segment))
                    {
                        NavigateUri = new Uri(segment),
                    };

                    hyperlink.RequestNavigate += (sender, args) => Process.Start(segment);

                    paragraph.Inlines.Add(hyperlink);
                }
                else
                {
                    paragraph.Inlines.Add(new Run(segment));
                }
            }

            return flowDocument;
        }
        private void MoreInfo_Click(object sender, RoutedEventArgs e)
        {
            HomepageButton.Content = $"{(TypeBox.SelectedValue as ComboBoxItem).Content.ToString().Trim().TrimEnd('s')} Page";
            Button button = sender as Button;
            var item = button.DataContext as GameBananaRecord;
            if (item.Compatible)
                DownloadButton.Visibility = Visibility.Visible;
            else
                DownloadButton.Visibility = Visibility.Collapsed;
            if (item.HasAltLinks)
                AltButton.Visibility = Visibility.Visible;
            else
                AltButton.Visibility = Visibility.Collapsed;
            DescPanel.DataContext = button.DataContext;
            MediaPanel.DataContext = button.DataContext;
            DescText.ScrollToHome();
            var text = "";
            text += item.ConvertedText;
            DescText.Document = ConvertToFlowDocument(text);
            ImageLeft.IsEnabled = true;
            ImageRight.IsEnabled = true;
            BigImageLeft.IsEnabled = true;
            BigImageRight.IsEnabled = true;
            imageCount = item.Media.Where(x => x.Type == "image").ToList().Count;
            imageCounter = 0;
            if (imageCount > 0)
            {
                Grid.SetColumnSpan(DescText, 1);
                ImagePanel.Visibility = Visibility.Visible;
                var image = new BitmapImage(new Uri($"{item.Media[imageCounter].Base}/{item.Media[imageCounter].File}"));
                Screenshot.Source = image;
                BigScreenshot.Source = image;
                CaptionText.Text = item.Media[imageCounter].Caption;
                BigCaptionText.Text = item.Media[imageCounter].Caption;
                if (!String.IsNullOrEmpty(CaptionText.Text))
                {
                    BigCaptionText.Visibility = Visibility.Visible;
                    CaptionText.Visibility = Visibility.Visible;
                }
                else
                {
                    BigCaptionText.Visibility = Visibility.Collapsed;
                    CaptionText.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                Grid.SetColumnSpan(DescText, 2);
                ImagePanel.Visibility = Visibility.Collapsed;
            }
            if (imageCount == 1)
            {
                ImageLeft.IsEnabled = false;
                ImageRight.IsEnabled = false;
                BigImageLeft.IsEnabled = false;
                BigImageRight.IsEnabled = false;
            }

            DescPanel.Visibility = Visibility.Visible;
        }
        private void CloseDesc_Click(object sender, RoutedEventArgs e)
        {
            DescPanel.Visibility = Visibility.Collapsed;
        }
        private void CloseMedia_Click(object sender, RoutedEventArgs e)
        {
            MediaPanel.Visibility = Visibility.Collapsed;
        }

        private void Image_Click(object sender, RoutedEventArgs e)
        {
            MediaPanel.Visibility = Visibility.Visible;
        }

        private void ImageLeft_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            var item = button.DataContext as GameBananaRecord;
            if (--imageCounter == -1)
                imageCounter = imageCount - 1;
            var image = new BitmapImage(new Uri($"{item.Media[imageCounter].Base}/{item.Media[imageCounter].File}"));
            Screenshot.Source = image;
            CaptionText.Text = item.Media[imageCounter].Caption;
            BigScreenshot.Source = image;
            BigCaptionText.Text = item.Media[imageCounter].Caption;
            if (!String.IsNullOrEmpty(CaptionText.Text))
            {
                BigCaptionText.Visibility = Visibility.Visible;
                CaptionText.Visibility = Visibility.Visible;
            }
            else
            {
                BigCaptionText.Visibility = Visibility.Collapsed;
                CaptionText.Visibility = Visibility.Collapsed;
            }
        }

        private void ImageRight_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            var item = button.DataContext as GameBananaRecord;
            if (++imageCounter == imageCount)
                imageCounter = 0;
            var image = new BitmapImage(new Uri($"{item.Media[imageCounter].Base}/{item.Media[imageCounter].File}"));
            Screenshot.Source = image;
            CaptionText.Text = item.Media[imageCounter].Caption;
            BigScreenshot.Source = image;
            BigCaptionText.Text = item.Media[imageCounter].Caption;
            if (!String.IsNullOrEmpty(CaptionText.Text))
            {
                BigCaptionText.Visibility = Visibility.Visible;
                CaptionText.Visibility = Visibility.Visible;
            }
            else
            {
                BigCaptionText.Visibility = Visibility.Collapsed;
                CaptionText.Visibility = Visibility.Collapsed;
            }
        }
        private static bool selected = false;

        private static Dictionary<GameFilter, Dictionary<TypeFilter, List<GameBananaCategory>>> cats = new();

        private static readonly List<GameBananaCategory> All = new GameBananaCategory[]
        {
            new GameBananaCategory()
            {
                Name = "All",
                ID = null
            }
        }.ToList();
        private static readonly List<GameBananaCategory> None = new GameBananaCategory[]
        {
            new GameBananaCategory()
            {
                Name = "- - -",
                ID = null
            }
        }.ToList();
        private async void InitializeBrowser()
        {
            using (var httpClient = new HttpClient())
            {
                ErrorPanel.Visibility = Visibility.Collapsed;
                // Initialize categories and games
                var gameIDS = new string[] { "6246", "11605", "8897", "11534", "7019", "9219", "12028", "13821", "14246", "14247", "14768", "15359" };
                var types = new string[] { "Mod", "Wip", "Sound" };
                var gameCounter = 0;
                foreach (var gameID in gameIDS)
                {
                    var counter = 0;
                    double totalPages = 0;
                    foreach (var type in types)
                    {
                        var requestUrl = $"https://gamebanana.com/apiv4/{type}Category/ByGame?_aGameRowIds[]={gameID}&_sRecordSchema=Custom" +
                            "&_csvProperties=_idRow,_sName,_sProfileUrl,_sIconUrl,_idParentCategoryRow&_nPerpage=50";
                        string responseString = "";
                        try
                        {
                            var responseMessage = await httpClient.GetAsync(requestUrl);
                            responseString = await responseMessage.Content.ReadAsStringAsync();
                            responseString = Regex.Replace(responseString, @"""(\d+)""", @"$1");
                            var numRecords = responseMessage.GetHeader("X-GbApi-Metadata_nRecordCount");
                            if (numRecords != -1)
                            {
                                totalPages = Math.Ceiling(numRecords / 50);
                            }
                        }
                        catch (HttpRequestException ex)
                        {
                            LoadingBar.Visibility = Visibility.Collapsed;
                            ErrorPanel.Visibility = Visibility.Visible;
                            BrowserRefreshButton.Visibility = Visibility.Visible;
                            switch (Regex.Match(ex.Message, @"\d+").Value)
                            {
                                case "443":
                                    BrowserMessage.Text = "Your internet connection is down.";
                                    break;
                                case "500":
                                case "503":
                                case "504":
                                    BrowserMessage.Text = "GameBanana's servers are down.";
                                    break;
                                default:
                                    BrowserMessage.Text = ex.Message;
                                    break;
                            }
                            return;
                        }
                        catch (Exception ex)
                        {
                            LoadingBar.Visibility = Visibility.Collapsed;
                            ErrorPanel.Visibility = Visibility.Visible;
                            BrowserRefreshButton.Visibility = Visibility.Visible;
                            BrowserMessage.Text = ex.Message;
                            return;
                        }
                        List<GameBananaCategory> response = new();
                        try
                        {
                            response = JsonSerializer.Deserialize<List<GameBananaCategory>>(responseString);
                        }
                        catch (Exception)
                        {
                            LoadingBar.Visibility = Visibility.Collapsed;
                            ErrorPanel.Visibility = Visibility.Visible;
                            BrowserRefreshButton.Visibility = Visibility.Visible;
                            BrowserMessage.Text = "Uh oh! Something went wrong while deserializing the categories...";
                            return;
                        }
                        if (!cats.ContainsKey((GameFilter)gameCounter))
                            cats.Add((GameFilter)gameCounter, new Dictionary<TypeFilter, List<GameBananaCategory>>());
                        if (!cats[(GameFilter)gameCounter].ContainsKey((TypeFilter)counter))
                            cats[(GameFilter)gameCounter].Add((TypeFilter)counter, response);

                        // Make more requests if needed
                        if (totalPages > 1)
                        {
                            for (double i = 2; i <= totalPages; i++)
                            {
                                var requestUrlPage = $"{requestUrl}&_nPage={i}";
                                try
                                {
                                    responseString = await httpClient.GetStringAsync(requestUrlPage);
                                    responseString = Regex.Replace(responseString, @"""(\d+)""", @"$1");
                                }
                                catch (HttpRequestException ex)
                                {
                                    LoadingBar.Visibility = Visibility.Collapsed;
                                    ErrorPanel.Visibility = Visibility.Visible;
                                    BrowserRefreshButton.Visibility = Visibility.Visible;
                                    switch (Regex.Match(ex.Message, @"\d+").Value)
                                    {
                                        case "443":
                                            BrowserMessage.Text = "Your internet connection is down.";
                                            break;
                                        case "500":
                                        case "503":
                                        case "504":
                                            BrowserMessage.Text = "GameBanana's servers are down.";
                                            break;
                                        default:
                                            BrowserMessage.Text = ex.Message;
                                            break;
                                    }
                                    return;
                                }
                                catch (Exception ex)
                                {
                                    LoadingBar.Visibility = Visibility.Collapsed;
                                    ErrorPanel.Visibility = Visibility.Visible;
                                    BrowserRefreshButton.Visibility = Visibility.Visible;
                                    BrowserMessage.Text = ex.Message;
                                    return;
                                }
                                try
                                {
                                    response = JsonSerializer.Deserialize<List<GameBananaCategory>>(responseString);
                                }
                                catch (Exception ex)
                                {
                                    LoadingBar.Visibility = Visibility.Collapsed;
                                    ErrorPanel.Visibility = Visibility.Visible;
                                    BrowserRefreshButton.Visibility = Visibility.Visible;
                                    BrowserMessage.Text = "Uh oh! Something went wrong while deserializing the categories...";
                                    return;
                                }
                                cats[(GameFilter)gameCounter][(TypeFilter)counter] = cats[(GameFilter)gameCounter][(TypeFilter)counter].Concat(response).ToList();
                            }
                        }
                        counter++;
                    }
                    gameCounter++;
                }
            }
            filterSelect = true;
            GameFilterBox.SelectedIndex = GameBox.SelectedIndex;
            FilterBox.ItemsSource = FilterBoxList;
            CatBox.ItemsSource = All.Concat(cats[(GameFilter)GameFilterBox.SelectedIndex][(TypeFilter)TypeBox.SelectedIndex].Where(x => x.RootID == 0).OrderBy(y => y.ID));
            SubCatBox.ItemsSource = None;
            CatBox.SelectedIndex = 0;
            SubCatBox.SelectedIndex = 0;
            FilterBox.SelectedIndex = 1;
            filterSelect = false;
            RefreshFilter();
            selected = true;
        }
        private void OnBrowserTabSelected(object sender, RoutedEventArgs e)
        {
            managerSelected = false;
            if (!selected)
            {
                InitializeBrowser();
            }
            if (GameFilterBox.SelectedIndex != 5)
                DiscordButton.Visibility = Visibility.Visible;
            else
                DiscordButton.Visibility = Visibility.Collapsed;
        }
        bool managerSelected = true;
        private void OnManagerTabSelected(object sender, RoutedEventArgs e)
        {
            managerSelected = true;
            if (GameBox.SelectedIndex != 5)
                DiscordButton.Visibility = Visibility.Visible;
            else
                DiscordButton.Visibility = Visibility.Collapsed;
        }

        private static int page = 1;
        private void DecrementPage(object sender, RoutedEventArgs e)
        {
            --page;
            RefreshFilter();
        }
        private void IncrementPage(object sender, RoutedEventArgs e)
        {
            ++page;
            RefreshFilter();
        }
        private void BrowserRefresh(object sender, RoutedEventArgs e)
        {
            if (!selected)
                InitializeBrowser();
            else
                RefreshFilter();
        }
        private static bool filterSelect;
        private static bool searched = false;
        private async void RefreshFilter()
        {
            NSFWCheckbox.IsEnabled = false;
            SearchBar.IsEnabled = false;
            SearchButton.IsEnabled = false;
            GameFilterBox.IsEnabled = false;
            FilterBox.IsEnabled = false;
            TypeBox.IsEnabled = false;
            CatBox.IsEnabled = false;
            SubCatBox.IsEnabled = false;
            PageLeft.IsEnabled = false;
            PageRight.IsEnabled = false;
            PageBox.IsEnabled = false;
            PerPageBox.IsEnabled = false;
            ErrorPanel.Visibility = Visibility.Collapsed;
            filterSelect = true;
            PageBox.SelectedValue = page;
            filterSelect = false;
            Page.Text = $"Page {page}";
            LoadingBar.Visibility = Visibility.Visible;
            FeedBox.Visibility = Visibility.Collapsed;
            PageLeft.IsEnabled = false;
            PageRight.IsEnabled = false;
            var search = searched ? SearchBar.Text : null;
            await FeedGenerator.GetFeed(page, (GameFilter)GameFilterBox.SelectedIndex, (TypeFilter)TypeBox.SelectedIndex, (FeedFilter)FilterBox.SelectedIndex, (GameBananaCategory)CatBox.SelectedItem,
                (GameBananaCategory)SubCatBox.SelectedItem, (PerPageBox.SelectedIndex + 1) * 10, (bool)NSFWCheckbox.IsChecked, search);
            FeedBox.ItemsSource = FeedGenerator.CurrentFeed.Records;
            if (FeedGenerator.error)
            {
                LoadingBar.Visibility = Visibility.Collapsed;
                ErrorPanel.Visibility = Visibility.Visible;
                BrowserRefreshButton.Visibility = Visibility.Visible;
                if (FeedGenerator.exception.Message.Contains("JSON tokens"))
                {
                    BrowserMessage.Text = "Uh oh! Unverum failed to deserialize the GameBanana feed.";
                    return;
                }
                switch (Regex.Match(FeedGenerator.exception.Message, @"\d+").Value)
                {
                    case "443":
                        BrowserMessage.Text = "Your internet connection is down.";
                        break;
                    case "500":
                    case "503":
                    case "504":
                        BrowserMessage.Text = "GameBanana's servers are down.";
                        break;
                    default:
                        BrowserMessage.Text = FeedGenerator.exception.Message;
                        break;
                }
                return;
            }
            if (page < FeedGenerator.CurrentFeed.TotalPages)
                PageRight.IsEnabled = true;
            if (page != 1)
                PageLeft.IsEnabled = true;
            if (FeedBox.Items.Count > 0)
            {
                FeedBox.ScrollIntoView(FeedBox.Items[0]);
                FeedBox.Visibility = Visibility.Visible;
            }
            else
            {
                ErrorPanel.Visibility = Visibility.Visible;
                BrowserRefreshButton.Visibility = Visibility.Collapsed;
                BrowserMessage.Visibility = Visibility.Visible;
                BrowserMessage.Text = "Unverum couldn't find any mods.";
            }
            PageBox.ItemsSource = Enumerable.Range(1, (int)(FeedGenerator.CurrentFeed.TotalPages));

            LoadingBar.Visibility = Visibility.Collapsed;
            CatBox.IsEnabled = true;
            SubCatBox.IsEnabled = true;
            TypeBox.IsEnabled = true;
            FilterBox.IsEnabled = true;
            PageBox.IsEnabled = true;
            PerPageBox.IsEnabled = true;
            GameFilterBox.IsEnabled = true;
            SearchBar.IsEnabled = true;
            SearchButton.IsEnabled = true;
            NSFWCheckbox.IsEnabled = true;
        }

        private void FilterSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded && !filterSelect)
            {
                if (!searched || FilterBox.SelectedIndex != 3)
                {
                    filterSelect = true;
                    var temp = FilterBox.SelectedIndex;
                    FilterBox.ItemsSource = FilterBoxList;
                    FilterBox.SelectedIndex = temp;
                    filterSelect = false;
                }
                SearchBar.Clear();
                searched = false;
                page = 1;
                RefreshFilter();
            }
        }
        private void PerPageSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded && !filterSelect)
            {
                page = 1;
                RefreshFilter();
            }
        }
        private void GameFilterSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded && !filterSelect)
            {
                SearchBar.Clear();
                searched = false;
                if (GameFilterBox.SelectedIndex != 5)
                    DiscordButton.Visibility = Visibility.Visible;
                else
                    DiscordButton.Visibility = Visibility.Collapsed;
                filterSelect = true;
                if (!searched)
                {
                    FilterBox.ItemsSource = FilterBoxList;
                    FilterBox.SelectedIndex = 1;
                }
                // Set categories
                if (cats[(GameFilter)GameFilterBox.SelectedIndex][(TypeFilter)TypeBox.SelectedIndex].Any(x => x.RootID == 0))
                    CatBox.ItemsSource = All.Concat(cats[(GameFilter)GameFilterBox.SelectedIndex][(TypeFilter)TypeBox.SelectedIndex].Where(x => x.RootID == 0).OrderBy(y => y.ID));
                else
                    CatBox.ItemsSource = None;
                CatBox.SelectedIndex = 0;
                var cat = (GameBananaCategory)CatBox.SelectedValue;
                if (cats[(GameFilter)GameFilterBox.SelectedIndex][(TypeFilter)TypeBox.SelectedIndex].Any(x => x.RootID == cat.ID))
                    SubCatBox.ItemsSource = All.Concat(cats[(GameFilter)GameFilterBox.SelectedIndex][(TypeFilter)TypeBox.SelectedIndex].Where(x => x.RootID == cat.ID).OrderBy(y => y.ID));
                else
                    SubCatBox.ItemsSource = None;
                SubCatBox.SelectedIndex = 0;
                filterSelect = false;
                page = 1;
                RefreshFilter();
            }
        }
        private void TypeFilterSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded && !filterSelect)
            {
                SearchBar.Clear();
                searched = false;
                filterSelect = true;
                if (!searched)
                {
                    FilterBox.ItemsSource = FilterBoxList;
                    FilterBox.SelectedIndex = 1;
                }
                // Set categories
                if (cats[(GameFilter)GameFilterBox.SelectedIndex][(TypeFilter)TypeBox.SelectedIndex].Any(x => x.RootID == 0))
                    CatBox.ItemsSource = All.Concat(cats[(GameFilter)GameFilterBox.SelectedIndex][(TypeFilter)TypeBox.SelectedIndex].Where(x => x.RootID == 0).OrderBy(y => y.ID));
                else
                    CatBox.ItemsSource = None;
                CatBox.SelectedIndex = 0;
                var cat = (GameBananaCategory)CatBox.SelectedValue;
                if (cats[(GameFilter)GameFilterBox.SelectedIndex][(TypeFilter)TypeBox.SelectedIndex].Any(x => x.RootID == cat.ID))
                    SubCatBox.ItemsSource = All.Concat(cats[(GameFilter)GameFilterBox.SelectedIndex][(TypeFilter)TypeBox.SelectedIndex].Where(x => x.RootID == cat.ID).OrderBy(y => y.ID));
                else
                    SubCatBox.ItemsSource = None;
                SubCatBox.SelectedIndex = 0;
                filterSelect = false;
                page = 1;
                RefreshFilter();
            }
        }
        private void MainFilterSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded && !filterSelect)
            {
                SearchBar.Clear();
                searched = false;
                filterSelect = true;
                if (!searched)
                {
                    FilterBox.ItemsSource = FilterBoxList;
                    FilterBox.SelectedIndex = 1;
                }
                // Set Categories
                var cat = (GameBananaCategory)CatBox.SelectedValue;
                if (cats[(GameFilter)GameFilterBox.SelectedIndex][(TypeFilter)TypeBox.SelectedIndex].Any(x => x.RootID == cat.ID))
                    SubCatBox.ItemsSource = All.Concat(cats[(GameFilter)GameFilterBox.SelectedIndex][(TypeFilter)TypeBox.SelectedIndex].Where(x => x.RootID == cat.ID).OrderBy(y => y.ID));
                else
                    SubCatBox.ItemsSource = None;
                SubCatBox.SelectedIndex = 0;
                filterSelect = false;
                page = 1;
                RefreshFilter();
            }
        }
        private void SubFilterSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!filterSelect && IsLoaded)
            {
                SearchBar.Clear();
                searched = false;
                page = 1;
                RefreshFilter();
            }
        }
        private void UniformGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            var grid = sender as UniformGrid;
            if (grid.ActualWidth > 2000)
                grid.Columns = 6;
            else if (grid.ActualWidth > 1600) 
                grid.Columns = 5;
            else if (grid.ActualWidth > 1200) 
                grid.Columns = 4;
            else 
                grid.Columns = 3;
        }
        private void OnResize(object sender, RoutedEventArgs e)
        {
            BigScreenshot.MaxHeight = ActualHeight - 240;
        }

        private void PageBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!filterSelect && IsLoaded)
            {
                page = (int)PageBox.SelectedValue;
                RefreshFilter();
            }
        }
        private void NSFWCheckbox_Checked(object sender, RoutedEventArgs e)
        {
            if (!filterSelect && IsLoaded)
            {
                if (searched)
                {
                    filterSelect = true;
                    FilterBox.ItemsSource = FilterBoxList;
                    FilterBox.SelectedIndex = 1;
                    filterSelect = false;
                }
                SearchBar.Clear();
                searched = false;
                page = 1;
                RefreshFilter();
            }
        }

        private void OnFirstOpen()
        {
            if (!Global.config.CurrentGame.Equals("Dragon Ball FighterZ", StringComparison.InvariantCultureIgnoreCase) && !Global.config.Configs[Global.config.CurrentGame].FirstOpen)
            {
                ChoiceWindow choice;
                var store = Global.config.CurrentGame.Equals("Kingdom Hearts III", StringComparison.InvariantCultureIgnoreCase) ? "Epic Games" : "Steam";
                if (Global.config.CurrentGame.Equals("Shin Megami Tensei V", StringComparison.InvariantCultureIgnoreCase))
                    choice = new ChoiceWindow("Launch through Emulator", "Launches the game through Yuzu or Ryujinx emulator",
                    "Build for Hardware", "Builds mod output without launching the game", $"Launcher Options for {Global.config.CurrentGame}");
                else
                    choice = new ChoiceWindow("Launch through Executable", "Launches the executable directly",
                        $"Launch through {store}", $"Uses the {store} shortcut to launch", $"Launcher Options for {Global.config.CurrentGame}");
                choice.ShowDialog();
                if (choice.choice != null && (bool)choice.choice)
                {
                    Global.config.Configs[Global.config.CurrentGame].LauncherOption = false;
                    LauncherOptionsBox.SelectedIndex = 0;
                }
                else if (choice.choice != null && !(bool)choice.choice)
                {
                    Global.config.Configs[Global.config.CurrentGame].LauncherOption = true;
                    LauncherOptionsBox.SelectedIndex = 1;
                }
                else if (!Global.config.CurrentGame.Equals("Shin Megami Tensei V", StringComparison.InvariantCultureIgnoreCase))
                {
                    Global.logger.WriteLine($"No launch option chosen, defaulting to {store} shortcut", LoggerType.Warning);
                    Global.config.Configs[Global.config.CurrentGame].LauncherOption = true;
                    LauncherOptionsBox.SelectedIndex = 1;
                }
                else
                {
                    Global.logger.WriteLine($"No launch option chosen, defaulting to emulator setup", LoggerType.Warning);
                    Global.config.Configs[Global.config.CurrentGame].LauncherOption = false;
                    LauncherOptionsBox.SelectedIndex = 0;
                }
                Global.config.Configs[Global.config.CurrentGame].FirstOpen = true;
                Global.UpdateConfig();
                Global.logger.WriteLine($"If you want to switch the Launch Method, use the dropdown box to the right of the Launch Button", LoggerType.Info);
            }
        }
        private bool handle;
        private void GameBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded || Global.config == null)
                return;
            handle = true;
            
        }
        private void LauncherOptionsBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LauncherOptionsBox.SelectedIndex == 1
                && Global.config.CurrentGame.Equals("Shin Megami Tensei V", StringComparison.InvariantCultureIgnoreCase))
                LaunchButton.Content = "Build";
            else
                LaunchButton.Content = "Launch";
            if (!handle)
            {
                Global.config.Configs[Global.config.CurrentGame].LauncherOption = LauncherOptionsBox.SelectedIndex == 1;
                Global.UpdateConfig();
            }
        }
        private void GameBox_DropDownClosed(object sender, EventArgs e)
        {
            if (handle)
            {
                if (GameBox.SelectedIndex == 5)
                    DiscordButton.Visibility = Visibility.Collapsed;
                else
                    DiscordButton.Visibility = Visibility.Visible;
                Global.config.CurrentGame = (((GameBox.SelectedValue as ComboBoxItem).Content as StackPanel).Children[1] as TextBlock).Text.Trim().Replace(":", String.Empty);
                if (!Global.config.Configs.ContainsKey(Global.config.CurrentGame))
                {
                    Global.ModList = new();
                    Global.config.Configs.Add(Global.config.CurrentGame, new());
                }
                else
                    Global.ModList = Global.config.Configs[Global.config.CurrentGame].ModList;
                var currentModDirectory = $@"{Global.assemblyLocation}{Global.s}Mods{Global.s}{Global.config.CurrentGame}";
                Directory.CreateDirectory(currentModDirectory);
                ModsWatcher.Path = currentModDirectory;
                Global.logger.WriteLine($"Game switched to {Global.config.CurrentGame}", LoggerType.Info);
                Refresh();
                Global.UpdateConfig();
                if (String.IsNullOrEmpty(Global.config.Configs[Global.config.CurrentGame].ModsFolder)
                    || String.IsNullOrEmpty(Global.config.Configs[Global.config.CurrentGame].Launcher) || !File.Exists(Global.config.Configs[Global.config.CurrentGame].Launcher))
                {
                    LaunchButton.IsEnabled = false;
                    Global.logger.WriteLine("Please click Setup before starting!", LoggerType.Warning);
                }
                else
                {
                    LaunchButton.IsEnabled = true;
                }
                if (Global.config.CurrentGame.Equals("Shin Megami Tensei V", StringComparison.InvariantCultureIgnoreCase))
                {
                    LauncherOptions[0] = " Emulator";
                    LauncherOptions[1] = " Hardware";
                }
                else if (Global.config.CurrentGame.Equals("Kingdom Hearts III", StringComparison.InvariantCultureIgnoreCase))
                {
                    LauncherOptions[0] = " Executable";
                    LauncherOptions[1] = " Epic Games";
                }
                else
                {
                    LauncherOptions[0] = " Executable";
                    LauncherOptions[1] = " Steam";
                }

                OnFirstOpen();

                if (Global.config.CurrentGame.Equals("Dragon Ball FighterZ", StringComparison.InvariantCultureIgnoreCase))
                    LauncherOptionsBox.IsEnabled = false;
                else
                    LauncherOptionsBox.IsEnabled = true;
                LauncherOptionsBox.ItemsSource = LauncherOptions;
                LauncherOptionsBox.SelectedIndex = Convert.ToInt32(Global.config.Configs[Global.config.CurrentGame].LauncherOption);

                DescriptionWindow.Document = defaultFlow;
                var bitmap = new BitmapImage(new Uri("pack://application:,,,/Unverum;component/Assets/unverumpreview.png"));
                Preview.Source = bitmap;
                PreviewBG.Source = null; 
                
                Global.logger.WriteLine("Checking for updates...", LoggerType.Info);
                GameBox.IsEnabled = false;
                ModGrid.IsEnabled = false;
                ConfigButton.IsEnabled = false;
                LaunchButton.IsEnabled = false;
                OpenModsButton.IsEnabled = false;
                UpdateButton.IsEnabled = false;
                LauncherOptionsBox.IsEnabled = false;
                App.Current.Dispatcher.Invoke(() =>
                {
                    ModUpdater.CheckForUpdates($"{Global.assemblyLocation}{Global.s}Mods{Global.s}{Global.config.CurrentGame}", this);
                });
                handle = false;
            }
        }

        private async void SortAlphabeticallyAndGroupEnabled_Click(object sender, RoutedEventArgs e)
        {
            DataGridColumnHeader colHeader = sender as DataGridColumnHeader;
            if (colHeader != null)
            {
                if (colHeader.Column.Header.Equals("Name"))
                {
                    // Sort alphabetically
                    Global.ModList = new ObservableCollection<Mod>(Global.ModList.ToList().OrderBy(x => x.name, new NaturalSort()).ToList());
                    Global.logger.WriteLine("Sorted alphanumerically!", LoggerType.Info);
                }
                else if (colHeader.Column.Header.Equals("Enabled"))
                {
                    // Move all enabled mods to top
                    Global.ModList = new ObservableCollection<Mod>(Global.ModList.ToList().OrderByDescending(x => x.enabled).ToList());
                    Global.logger.WriteLine("Moved all enabled mods to the top!", LoggerType.Info);
                }
                await Task.Run(() =>
                {
                    App.Current.Dispatcher.Invoke((Action)delegate
                    {
                        ModGrid.ItemsSource = Global.ModList;
                    });
                });
                Global.config.Configs[Global.config.CurrentGame].ModList = Global.ModList;
            }
            e.Handled = true;
        }
        private void Search()
        {
            if (!filterSelect && IsLoaded && !String.IsNullOrWhiteSpace(SearchBar.Text))
            {
                filterSelect = true;
                FilterBox.ItemsSource = FilterBoxListWhenSearched;
                FilterBox.SelectedIndex = 3;
                NSFWCheckbox.IsChecked = true;
                // Set categories
                if (cats[(GameFilter)GameFilterBox.SelectedIndex][(TypeFilter)TypeBox.SelectedIndex].Any(x => x.RootID == 0))
                    CatBox.ItemsSource = All.Concat(cats[(GameFilter)GameFilterBox.SelectedIndex][(TypeFilter)TypeBox.SelectedIndex].Where(x => x.RootID == 0).OrderBy(y => y.ID));
                else
                    CatBox.ItemsSource = None;
                CatBox.SelectedIndex = 0;
                var cat = (GameBananaCategory)CatBox.SelectedValue;
                if (cats[(GameFilter)GameFilterBox.SelectedIndex][(TypeFilter)TypeBox.SelectedIndex].Any(x => x.RootID == cat.ID))
                    SubCatBox.ItemsSource = All.Concat(cats[(GameFilter)GameFilterBox.SelectedIndex][(TypeFilter)TypeBox.SelectedIndex].Where(x => x.RootID == cat.ID).OrderBy(y => y.ID));
                else
                    SubCatBox.ItemsSource = None;
                SubCatBox.SelectedIndex = 0;
                filterSelect = false;
                searched = true;
                page = 1;
                RefreshFilter();
            }
        }
        private void SearchBar_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                Search();
        }
        private static readonly List<string> FilterBoxList = new string[] { " Featured", " Recent", " Popular" }.ToList();
        private static readonly List<string> FilterBoxListWhenSearched = new string[] { " Featured", " Recent", " Popular", " - - -" }.ToList();

        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            Search();
        }

        private void ModGrid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space && ModGrid.CurrentColumn.Header.ToString() != "Enabled")
                foreach (var item in ModGrid.SelectedItems)
                {
                    var checkbox = ModGrid.Columns[0].GetCellContent(item) as CheckBox;
                    if (checkbox != null)
                        checkbox.IsChecked = !checkbox.IsChecked;
                }
        }
    }
}
