﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
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
    public partial class UpdateFileBox : Window
    {
        public string chosenFileUrl;
        public string chosenFileName;
        public string chosenFileDescription;
        public bool selectedDownloadAll;
        public UpdateFileBox(List<GameBananaItemFile> files, string packageName)
        {
            InitializeComponent();
            selectedDownloadAll = false;
            FileList.ItemsSource = files;
            TitleBox.Text = packageName;
        }

        private void SelectButton_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            var item = button.DataContext as GameBananaItemFile;
            chosenFileUrl = item.DownloadUrl;
            chosenFileName = item.FileName;
            chosenFileDescription = item.Description;
            Close();
        }

        private void DownloadAll_Click(object sender, RoutedEventArgs e)
        {
            selectedDownloadAll = true;
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
