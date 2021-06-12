using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.IO;
using System.Reflection;

namespace Unverum
{
    /// <summary>
    /// Interaction logic for EditWindow.xaml
    /// </summary>
    public partial class EditWindow : Window
    {
        public Mod _mod;
        private Logger _logger;
        private string assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        public EditWindow(Mod mod, Logger logger)
        {
            InitializeComponent();
            _mod = mod;
            NameBox.Text = _mod.name;
            Title = $"Edit {_mod.name}";
            _logger = logger;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            if (!NameBox.Text.Equals(_mod.name, StringComparison.InvariantCultureIgnoreCase))
            {
                var oldDirectory = $"{assemblyLocation}/Mods/{_mod.name}";
                var newDirectory = $"{assemblyLocation}/Mods/{NameBox.Text}";
                if (!Directory.Exists(newDirectory))
                {
                    try
                    {
                        Directory.Move(oldDirectory, newDirectory);
                    }
                    catch (Exception ex)
                    {
                        _logger.WriteLine($"Couldn't rename {oldDirectory} to {newDirectory} ({ex.Message})", LoggerType.Error);
                    }
                }
                else
                    _logger.WriteLine($"{newDirectory} already exists", LoggerType.Error);
            }
            Close();
        }
    }
}
