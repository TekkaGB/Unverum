using System;
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
                var game = (item as ComboBoxItem).Content.ToString().Trim().Replace(":", String.Empty);
                Global.games.Add(game);
            }

            if (Global.config.Configs == null)
            {
                Global.config.CurrentGame = (GameBox.SelectedValue as ComboBoxItem).Content.ToString().Trim().Replace(":", String.Empty);
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

            if (String.IsNullOrEmpty(Global.config.Configs[Global.config.CurrentGame].ModsFolder) || !Directory.Exists(Global.config.Configs[Global.config.CurrentGame].ModsFolder)
                || String.IsNullOrEmpty(Global.config.Configs[Global.config.CurrentGame].Launcher) || !File.Exists(Global.config.Configs[Global.config.CurrentGame].Launcher))
            {
                BuildButton.IsEnabled = false;
                LaunchButton.IsEnabled = false;
                Global.logger.WriteLine("Please click Setup before starting!", LoggerType.Warning);
            }

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

            // Move all enabled mods to top
            Global.ModList = new ObservableCollection<Mod>(Global.ModList.ToList().OrderByDescending(x => x.enabled).ToList());

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
            Application.Current.Dispatcher.Invoke(() =>
            {
                index = GameBox.SelectedIndex;
            });
            var game = (GameFilter)index;
            switch (game)
            {
                case GameFilter.DBFZ:
                    return Setup.DBFZ();
                case GameFilter.MHOJ2:
                    return Setup.MHOJ2();
                case GameFilter.GBVS:
                    return Setup.Generic("GBVS.exe");
                case GameFilter.GGS:
                    return Setup.Generic("GGST.exe");
                case GameFilter.JF:
                    return Setup.JF();
                case GameFilter.KHIII:
                    return Setup.KHIII();
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
                if (!String.IsNullOrEmpty(Global.config.Configs[Global.config.CurrentGame].ModsFolder) && Directory.Exists(Global.config.Configs[Global.config.CurrentGame].ModsFolder)
                    || !String.IsNullOrEmpty(Global.config.Configs[Global.config.CurrentGame].Launcher) && File.Exists(Global.config.Configs[Global.config.CurrentGame].Launcher))
                {
                    if ((GameFilter)index == GameFilter.MHOJ2)
                    {
                        var resetResult = MessageBox.Show($@"{Global.config.CurrentGame} is already setup.{Environment.NewLine}Remove setup?", $@"Notification", MessageBoxButton.YesNo, MessageBoxImage.Question);
                        if (resetResult == MessageBoxResult.Yes)
                        {
                            foreach (var file in Directory.GetFiles(Path.GetDirectoryName(Global.config.Configs[Global.config.CurrentGame].ModsFolder), "*", SearchOption.TopDirectoryOnly))
                                if (Path.GetExtension(file).Equals(".pak", StringComparison.InvariantCultureIgnoreCase)
                                    || Path.GetExtension(file).Equals(".sig", StringComparison.InvariantCultureIgnoreCase))
                                    File.Move(file, file.Replace("HeroGame.", "HeroGame-WindowsNoEditor_0_P.", StringComparison.InvariantCultureIgnoreCase), true);
                            Global.config.Configs[Global.config.CurrentGame].ModsFolder = null;
                            Global.config.Configs[Global.config.CurrentGame].Launcher = null;
                            Global.UpdateConfig();
                            Dispatcher.Invoke(() =>
                            {
                                BuildButton.IsEnabled = false;
                                LaunchButton.IsEnabled = false;
                                GameBox.IsEnabled = true;
                            });
                            Global.logger.WriteLine($"Removed setup for {Global.config.CurrentGame}", LoggerType.Info);
                            return;
                        }
                    }
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
                        BuildButton.IsEnabled = true;
                        LaunchButton.IsEnabled = true;
                    });
                }
            });
            GameBox.IsEnabled = true;
        }
        private void Launch_Click(object sender, RoutedEventArgs e)
        {
            if (Global.config.Configs[Global.config.CurrentGame].Launcher != null && File.Exists(Global.config.Configs[Global.config.CurrentGame].Launcher))
            {
                Global.logger.WriteLine($"Launching {Global.config.Configs[Global.config.CurrentGame].Launcher}", LoggerType.Info);
                try
                {
                    var ps = new ProcessStartInfo(Global.config.Configs[Global.config.CurrentGame].Launcher)
                    {
                        WorkingDirectory = Path.GetDirectoryName(Global.config.Configs[Global.config.CurrentGame].Launcher)
                    };
                    Process.Start(ps);
                }
                catch (Exception ex)
                {
                    Global.logger.WriteLine($"Couldn't launch {Global.config.Configs[Global.config.CurrentGame].Launcher} ({ex.Message})", LoggerType.Error);
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
                var ps = new ProcessStartInfo($"https://discord.gg/tgFrebr")
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
            Mod row = (Mod)ModGrid.SelectedItem;
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

        private async void Build_Click(object sender, RoutedEventArgs e)
        {
            if (Global.config.Configs[Global.config.CurrentGame].ModsFolder != null && Directory.Exists(Global.config.Configs[Global.config.CurrentGame].ModsFolder))
            {
                GameBox.IsEnabled = false;
                ModGrid.IsEnabled = false;
                ConfigButton.IsEnabled = false;
                BuildButton.IsEnabled = false;
                LaunchButton.IsEnabled = false;
                OpenModsButton.IsEnabled = false;
                UpdateButton.IsEnabled = false;
                Refresh();
                await Build(Global.config.Configs[Global.config.CurrentGame].ModsFolder);
                ModGrid.IsEnabled = true;
                ConfigButton.IsEnabled = true;
                BuildButton.IsEnabled = true;
                LaunchButton.IsEnabled = true;
                OpenModsButton.IsEnabled = true;
                UpdateButton.IsEnabled = true;
                GameBox.IsEnabled = true;
                MessageBox.Show($@"Finished building loadout and ready to launch!", "Notification", MessageBoxButton.OK);
            }
            else
                Global.logger.WriteLine("Please click Setup before starting!", LoggerType.Warning);
        }

        private async Task Build(string path)
        {
            await Task.Run(() =>
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
                else if (Directory.Exists($"{ContentFolder}{Global.s}CriWareData"))
                    MoviesFolder = $"{ContentFolder}{Global.s}CriWareData{Global.s}Movie";
                if (Directory.Exists($"{ContentFolder}{Global.s}Sound"))
                    SoundsFolder = $"{ContentFolder}{Global.s}Sound";
                else if (Directory.Exists($"{ContentFolder}{Global.s}CriWareData"))
                    SoundsFolder = $"{ContentFolder}{Global.s}CriWareData";
                // DBFZ specific
                bool? CostumePatched = null;
                if (Global.config.CurrentGame == "Dragon Ball FighterZ")
                    CostumePatched = Setup.CheckCostumePatch(Global.config.Configs[Global.config.CurrentGame].Launcher);
                if (!ModLoader.Restart(path, MoviesFolder, SplashFolder, SoundsFolder))
                    return;
                List<string> mods = Global.config.Configs[Global.config.CurrentGame].ModList.Where(x => x.enabled).Select(y => $@"{Global.assemblyLocation}{Global.s}Mods{Global.s}{Global.config.CurrentGame}{Global.s}{y.name}").ToList();
                mods.Reverse();
                ModLoader.Build(path, mods, CostumePatched, MoviesFolder, SplashFolder, SoundsFolder);
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
            Mod row = (Mod)ModGrid.SelectedItem;
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
            Mod row = (Mod)ModGrid.SelectedItem;
            if (row != null)
            {
                EditWindow ew = new EditWindow(row);
                ew.ShowDialog();
            }
        }
        private void FetchItem_Click(object sender, RoutedEventArgs e)
        {
            Mod row = (Mod)ModGrid.SelectedItem;
            if (row != null)
            {
                FetchWindow fw = new FetchWindow(row);
                fw.ShowDialog();
                if (fw.success)
                    ShowMetadata(row.name);
            }
        }
        private void ModsFolder_Click(object sender, RoutedEventArgs e)
        {
            var choice = new AddChoiceWindow();
            choice.ShowDialog();
            if (choice.create != null && (bool)choice.create)
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
                    dialog.InitialDirectory = Global.assemblyLocation;
                    dialog.ShowDialog();
                    if (!String.IsNullOrEmpty(dialog.FileName))
                    {
                        Directory.CreateDirectory(nameWindow.directory);
                        File.Copy(dialog.FileName, $"{nameWindow.directory}{Global.s}{Path.GetFileName(dialog.FileName)}", true);
                    }
                }
            }
            else if (choice.create != null && !(bool)choice.create) 
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
            ModGrid.IsHitTestVisible = false;
            ConfigButton.IsHitTestVisible = false;
            BuildButton.IsHitTestVisible = false;
            LaunchButton.IsHitTestVisible = false;
            OpenModsButton.IsHitTestVisible = false;
            UpdateButton.IsHitTestVisible = false;
            ModUpdater.CheckForUpdates($"{Global.assemblyLocation}{Global.s}Mods{Global.s}{Global.config.CurrentGame}", this);
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
                (GameFilterBox.SelectedValue as ComboBoxItem).Content.ToString().Trim().Replace(":", String.Empty),
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
                var gameIDS = new string[] { "6246", "11605", "8897", "11534", "7019", "9219" };
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
            GameFilterBox.SelectedIndex = GameBox.SelectedIndex;
            CatBox.ItemsSource = All.Concat(cats[(GameFilter)GameFilterBox.SelectedIndex][(TypeFilter)TypeBox.SelectedIndex].Where(x => x.RootID == 0).OrderBy(y => y.ID));
            SubCatBox.ItemsSource = None;
            filterSelect = true;
            CatBox.SelectedIndex = 0;
            SubCatBox.SelectedIndex = 0;
            filterSelect = false;
            RefreshFilter();
            selected = true;
        }
        private void OnBrowserTabSelected(object sender, RoutedEventArgs e)
        {
            if (!selected)
            {
                InitializeBrowser();
            }
            if (GameFilterBox.SelectedIndex != 5)
                DiscordButton.Visibility = Visibility.Visible;
            else
                DiscordButton.Visibility = Visibility.Collapsed;
        }
        private void OnManagerTabSelected(object sender, RoutedEventArgs e)
        {
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
        private static Random rand = new Random();
        private async void RefreshFilter()
        {
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
            await FeedGenerator.GetFeed(page, (GameFilter)GameFilterBox.SelectedIndex, (TypeFilter)TypeBox.SelectedIndex, (FeedFilter)FilterBox.SelectedIndex, (GameBananaCategory)CatBox.SelectedItem,
                (GameBananaCategory)SubCatBox.SelectedItem, (PerPageBox.SelectedIndex + 1) * 10, (bool)NSFWCheckbox.IsChecked);
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
        }

        private void FilterSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded)
            {
                page = 1;
                RefreshFilter();
            }
        }
        private void GameFilterSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded && !filterSelect)
            {
                if (GameFilterBox.SelectedIndex != 5)
                    DiscordButton.Visibility = Visibility.Visible;
                else
                    DiscordButton.Visibility = Visibility.Collapsed;
                filterSelect = true;
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
                filterSelect = true;
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
                filterSelect = true;
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
            page = 1;
            RefreshFilter();
        }

        private void GameBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded || Global.config == null)
                return;
            if (GameBox.SelectedIndex == 5)
                DiscordButton.Visibility = Visibility.Collapsed;
            else
                DiscordButton.Visibility = Visibility.Visible;
            Global.config.CurrentGame = (GameBox.SelectedValue as ComboBoxItem).Content.ToString().Trim().Replace(":", String.Empty);
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
            if (String.IsNullOrEmpty(Global.config.Configs[Global.config.CurrentGame].ModsFolder) || !Directory.Exists(Global.config.Configs[Global.config.CurrentGame].ModsFolder)
                || String.IsNullOrEmpty(Global.config.Configs[Global.config.CurrentGame].Launcher) || !File.Exists(Global.config.Configs[Global.config.CurrentGame].Launcher))
            {
                BuildButton.IsEnabled = false;
                LaunchButton.IsEnabled = false;
                Global.logger.WriteLine("Please click Setup before starting!", LoggerType.Warning);
            }
            else
            {
                BuildButton.IsEnabled = true;
                LaunchButton.IsEnabled = true;
            }

            DescriptionWindow.Document = defaultFlow;
            var bitmap = new BitmapImage(new Uri("pack://application:,,,/Unverum;component/Assets/unverumpreview.png"));
            Preview.Source = bitmap;
            PreviewBG.Source = null;
        }
    }
}
