using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.IO;
using System.Reflection;
using System.Windows.Input;
using System.Windows.Controls.Primitives;
using System.Collections.ObjectModel;

namespace Unverum.UI
{
    /// <summary>
    /// Interaction logic for ConfigurePaksWindow.xaml
    /// </summary>
    public partial class ConfigurePaksWindow : Window
    {
        public Mod _mod;
        public ConfigurePaksWindow(Mod mod)
        {
            InitializeComponent();
            if (mod != null)
            {
                _mod = mod;
                PakList.ItemsSource = new ObservableCollection<KeyValuePair<string, bool>>(_mod.paks);
                Title = $"Configure Paks for {_mod.name}";
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
        private void ToggleButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleButton button = sender as ToggleButton;
            var item = button.DataContext as KeyValuePair<string, bool>?;
            _mod.paks[item.Value.Key] = (bool)button.IsChecked;
        }
    }
}
