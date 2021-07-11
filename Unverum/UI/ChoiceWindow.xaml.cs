using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Diagnostics;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Win32;
using System.Media;

namespace Unverum.UI
{
    /// <summary>
    /// Interaction logic for UpdateFileBox.xaml
    /// </summary>
    public partial class ChoiceWindow : Window
    {
        public bool? choice = null;
        public ChoiceWindow(string _FirstOptionText, string _FirstOptionSubText, string _SecondOptionText, string _SecondOptionSubText, string title = null)
        {
            InitializeComponent();
            FirstOptionText.Text = _FirstOptionText;
            FirstOptionSubText.Text = _FirstOptionSubText;
            SecondOptionText.Text = _SecondOptionText;
            SecondOptionSubText.Text = _SecondOptionSubText;
            if (title != null)
                Title = title;
        }

        private void CreateMods_Click(object sender, RoutedEventArgs e)
        {
            choice = true;
            Close();
        }
        private void OpenMods_Click(object sender, RoutedEventArgs e)
        {
            choice = false;
            Close();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {

        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
