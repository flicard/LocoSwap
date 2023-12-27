using LocoSwap.Properties;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace LocoSwap
{
    /// <summary>
    /// Interaction logic for SwapPresetWindow.xaml
    /// </summary>
    public partial class SwapPresetWindow : Window
    {
        public event EventHandler ApplyClicked;
        public List<SwapPresetItem> SelectedItems
        {
            get => PresetList.SelectedItems.Cast<SwapPresetItem>().ToList();
        }
        public SwapPresetWindow()
        {
            InitializeComponent();

            // Add filter to PresetList
            CollectionView PresetListview = (CollectionView)CollectionViewSource.GetDefaultView(PresetList.ItemsSource);
            PresetListview.Filter = PresetFilter;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            var selected = PresetList.SelectedItems.Cast<object>().ToArray();
            foreach (SwapPresetItem item in selected)
            {
                Settings.Default.Preset.List.Remove(item);
            }
            Settings.Default.Save();
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            ApplyClicked?.Invoke(this, new EventArgs());
        }

        private void PresetsFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            CollectionViewSource.GetDefaultView(PresetList.ItemsSource).Refresh();
        }

        private bool PresetFilter(object item)
        {
            if (string.IsNullOrEmpty(PresetsFilterTextbox.Text))
                return true;

            SwapPresetItem candidateSwapPresetItem = item as SwapPresetItem;

            string[] filteredProperties = {
                candidateSwapPresetItem.NewName,
                candidateSwapPresetItem.NewXmlPath,
                candidateSwapPresetItem.TargetName,
                candidateSwapPresetItem.TargetXmlPath
            };

            return PresetsFilterTextbox.Text.Split(' ').All(
                filterToken => filteredProperties.Where(
                    prop => prop?.IndexOf(filterToken, StringComparison.OrdinalIgnoreCase) >= 0).ToArray().Length > 0
                );
        }
        private void EmptyPresetFilter_Click(object sender, RoutedEventArgs e)
        {
            PresetsFilterTextbox.Text = "";
        }
    }
}
