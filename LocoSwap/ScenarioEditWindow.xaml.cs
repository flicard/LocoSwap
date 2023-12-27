using Ionic.Zip;
using LocoSwap.Properties;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace LocoSwap
{
    /// <summary>
    /// Interaction logic for ScenarioEditWindow.xaml
    /// </summary>
    public partial class ScenarioEditWindow : Window
    {
        public class ScenarioViewModel : ModelBase
        {
            private Route _route;
            public Route Route
            {
                get => _route;
                set => SetProperty(ref _route, value);
            }
            private Scenario _scenario;
            public Scenario Scenario
            {
                get => _scenario;
                set => SetProperty(ref _scenario, value);
            }
            private string _loadingInformation = "";
            public string LoadingInformation
            {
                get => _loadingInformation;
                set => SetProperty(ref _loadingInformation, value);
            }
            private int _loadingProgress = 0;
            public int LoadingProgress
            {
                get => _loadingProgress;
                set
                {
                    SetProperty(ref _loadingProgress, value);
                    OnPropertyChanged(new PropertyChangedEventArgs("LoadingGridVisibility"));
                    OnPropertyChanged(new PropertyChangedEventArgs("SaveButtonEnabled"));
                }
            }
            private bool _vehicleScanInProgress = false;
            public Visibility LoadingGridVisibility
            {
                get => LoadingProgress < 100 ? Visibility.Visible : Visibility.Hidden;
            }
            public bool SaveButtonEnabled
            {
                get => LoadingProgress >= 100;
            }
            public bool VehicleScanInProgress
            {
                get => _vehicleScanInProgress;
                set => SetProperty(ref _vehicleScanInProgress, value);
            }

            public ObservableCollection<Consist> Consists { get; set; } = new ObservableCollection<Consist>();
            public ObservableCollection<ScenarioVehicle> Vehicles { get; set; } = new ObservableCollection<ScenarioVehicle>();
            public ObservableCollection<DirectoryItem> Directories { get; set; } = new ObservableCollection<DirectoryItem>();
            public ObservableCollection<AvailableVehicle> AvailableVehicles { get; set; } = new ObservableCollection<AvailableVehicle>();
        }

        private string RouteId;
        private ScenarioViewModel ViewModel;
        private CancellationTokenSource ScanCancellationTokenSource;
        private SwapPresetWindow PresetWindow;

        public ScenarioEditWindow(string routeId, Scenario scenario)
        {
            InitializeComponent();
            RouteId = routeId;
            ViewModel = new ScenarioViewModel();
            DataContext = ViewModel;
            ViewModel.Route = new Route(RouteId);
            ViewModel.Scenario = scenario;
            VehicleAvailibility.ClearTable();
            ReadScenario();
        }

        private bool AvailableVehicleFilter(object item)
        {
            if (string.IsNullOrEmpty(AvailableVehicleFilterTextbox.Text))
                return true;

            return AvailableVehicleFilterTextbox.Text.Split(' ').All(
                filterToken => (item as AvailableVehicle).DisplayName?.IndexOf(filterToken, StringComparison.OrdinalIgnoreCase) >= 0
                );
        }
        private void EmptyAvailableVehicleFilter_Click(object sender, RoutedEventArgs e)
        {
            AvailableVehicleFilterTextbox.Text = "";
        }

        public async void ReadScenario()
        {
            IProgress<int> progress = new Progress<int>(value => { ViewModel.LoadingProgress = value; });
            ViewModel.LoadingInformation = LocoSwap.Language.Resources.reading_scenario_files;

            List<Task> tasks = new List<Task>();
            var readConsistsTask = Task.Run(() =>
            {
                ViewModel.Scenario.ReadScenario(progress);
                List<Consist> ret = ViewModel.Scenario.GetConsists(progress);

                App.Current.Dispatcher.Invoke((Action)delegate
                {
                    ViewModel.Consists.Clear();
                    foreach (Consist consist in ret)
                    {
                        ViewModel.Consists.Add(consist);
                    }
                });
            });
            var populateDirectoryTask = Task.Run(() =>
            {
                DirectoryItem rootNode = new DirectoryItem
                {
                    Name = "Assets",
                    Path = Path.Combine(Settings.Default.TsPath, "Assets")
                };
                rootNode.PopulateSubDirectories();

                App.Current.Dispatcher.Invoke((Action)delegate
                {
                    foreach (DirectoryItem item in rootNode.SubDirectories)
                    {
                        ViewModel.Directories.Add(item);
                    }
                });
            });

            await Task.WhenAll(readConsistsTask, populateDirectoryTask);
            progress.Report(100);
        }

        private void ConsistListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ViewModel.Vehicles.Clear();
            foreach (ScenarioVehicle vehicle in ((Consist)ConsistListBox.SelectedItem).Vehicles)
            {
                ViewModel.Vehicles.Add(vehicle);
            }
        }

        private void TreeView_Expanded(object sender, RoutedEventArgs e)
        {
            TreeViewItem tvi = (TreeViewItem)e.OriginalSource;

            // Focus to avoid undesired scroll to previously selected node
            tvi.Focus();

            DirectoryItem selected = tvi.Header as DirectoryItem;
            selected.PopulateSubDirectories();
        }

        private void ScanButton_Click(object sender, RoutedEventArgs e)
        {
            DirectoryItem selected = DirectoryTree.SelectedItem as DirectoryItem;
            if (selected == null)
            {
                MessageBox.Show(
                    LocoSwap.Language.Resources.msg_no_directory_selected,
                    LocoSwap.Language.Resources.msg_message,
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            LookupVehicles(selected.Path);
        }

        private async void LookupVehicles(string path)
        {
            ScanCancellationTokenSource = new CancellationTokenSource();
            var token = ScanCancellationTokenSource.Token;
            IProgress<int> progress = new Progress<int>(value => { ViewModel.LoadingProgress = value == 0 ? value : Math.Max(value, ViewModel.LoadingProgress); });
            ViewModel.VehicleScanInProgress = true;
            List<string> files = Directory.GetFiles(path, "*.bin", SearchOption.AllDirectories).ToList();
            List<string> apFiles = Directory.GetFiles(path, "*.ap", SearchOption.AllDirectories).ToList();

            ViewModel.LoadingInformation = LocoSwap.Language.Resources.scanning_bin_files;
            var startDateTime = DateTime.Now;
            var binTask = Task.Run(() =>
            {
                progress.Report(0);
                Parallel.ForEach(files.Select((value, i) => (value, i)), (item) =>
                {
                    var fullBin = item.value;
                    var binPath = fullBin.Replace(Settings.Default.TsPath + "\\Assets\\", "");

                    var index = item.i;
                    try
                    {
                        Log.Debug("Try: {0}", binPath);
                        var vehicle = new AvailableVehicle(binPath);
                        Application.Current.Dispatcher.Invoke(delegate
                        {
                            ViewModel.AvailableVehicles.Add(vehicle);
                        });
                        Log.Debug("Found: {0}", vehicle.Name);
                    }
                    catch (Exception e)
                    {
                        Log.Debug("{0}: {1}", e.Message, binPath);
                    }

                    progress.Report((int)Math.Ceiling((float)index / files.Count() * 100));
                    token.ThrowIfCancellationRequested();
                });
            }, token);
            try
            {
                await binTask;
                var endDateTime = DateTime.Now;
                Log.Debug(".bin scan took {0} seconds", (endDateTime - startDateTime).TotalSeconds);
            }
            catch (Exception)
            {
                Log.Debug("Operation cancelled");
                ViewModel.LoadingProgress = 100;
                ViewModel.VehicleScanInProgress = false;

                return;
            }

            ViewModel.LoadingProgress = 0;
            ViewModel.LoadingInformation = LocoSwap.Language.Resources.scanning_ap_files;
            startDateTime = DateTime.Now;
            var apTask = Task.Run(() =>
            {
                foreach (var item in apFiles.Select((value, i) => (value, i)))
                {
                    Log.Debug("Trying ap file {0}", item.value);
                    var zipFile = ZipFile.Read(item.value);
                    var binEntries = zipFile.Where(entry => { return entry.FileName.ToLower().StartsWith("railvehicles/") && entry.FileName.EndsWith(".bin"); }).ToList();

                    if (!binEntries.Any())
                    {
                        binEntries = zipFile.Where(entry => { return entry.FileName.EndsWith(".bin"); }).ToList();
                    }

                    var baseProgress = (int)Math.Ceiling((float)item.i / apFiles.Count() * 100);
                    var basePath = Path.GetDirectoryName(item.value).Replace(Settings.Default.TsPath + "\\Assets\\", "");
                    var binCount = binEntries.Count();
                    Log.Debug("There are {0} bin entries", binCount);
                    Parallel.ForEach(binEntries.Select((value, i) => (value, i)), (binItem) =>
                    {
                        var binEntry = binItem.value;
                        var binPath = Path.Combine(basePath, binEntry.FileName.Replace('/', '\\'));
                        try
                        {
                            Log.Debug("Try {0}", binPath);
                            var vehicle = new AvailableVehicle(binPath);
                            App.Current.Dispatcher.Invoke((Action)delegate
                            {
                                ViewModel.AvailableVehicles.Add(vehicle);
                            });
                        }
                        catch (Exception)
                        {

                        }

                        var ownProgress = (int)Math.Ceiling((float)binItem.i / binCount * 100 / apFiles.Count());
                        progress.Report(baseProgress + ownProgress);
                        token.ThrowIfCancellationRequested();
                    });
                    token.ThrowIfCancellationRequested();
                }
            }, token);
            try
            {
                await apTask;
                var endDateTime = DateTime.Now;
                Log.Debug(".ap scan took {0} seconds", (endDateTime - startDateTime).TotalSeconds);
            }
            catch (Exception apException)
            {
                Log.Warning("Exception caught during .ap scan: {0} - {1}\n{2}", apException.GetType().ToString(), apException.Message, apException.StackTrace.ToString());
            }

            ViewModel.LoadingProgress = 100;
            ViewModel.VehicleScanInProgress = false;

            // Add filter to AvailableVehicleList
            CollectionView AvailableVehicleListview = (CollectionView)CollectionViewSource.GetDefaultView(AvailableVehicleListBox.ItemsSource);
            AvailableVehicleListview.Filter = AvailableVehicleFilter;
        }

        private void InsertButton_Click(bool after)
        {
            if (AvailableVehicleListBox.SelectedItem == null || VehicleListBox.SelectedItem == null)
            {
                MessageBox.Show(
                    LocoSwap.Language.Resources.msg_no_vehicle_selected,
                    LocoSwap.Language.Resources.msg_message,
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var insertIndex = after ? (VehicleListBox.SelectedIndex + 1) : VehicleListBox.SelectedIndex;

            Consist consist = (Consist)ConsistListBox.SelectedItem;
            AvailableVehicle newVehicle = (AvailableVehicle)AvailableVehicleListBox.SelectedItem;

            ScenarioVehicle vehicle = ViewModel.Scenario.InsertVehicle(consist.Idx, insertIndex, newVehicle);
            consist.Vehicles.Insert(insertIndex, vehicle);
            for (var i = 0; i < consist.Vehicles.Count; i++)
            {
                consist.Vehicles[i].Idx = i;
            }

            ViewModel.Vehicles.Clear();
            foreach (ScenarioVehicle scenarioVehicle in ((Consist)ConsistListBox.SelectedItem).Vehicles)
            {
                ViewModel.Vehicles.Add(scenarioVehicle);
            }

            return;
        }

        private void InsertBeforeButton_Click(object sender, RoutedEventArgs e)
        {
            InsertButton_Click(false);
        }

        private void InsertAfterButton_Click(object sender, RoutedEventArgs e)
        {
            InsertButton_Click(true);
        }

        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            if (VehicleListBox.SelectedItems.Count == 0)
            {
                MessageBox.Show(
                    LocoSwap.Language.Resources.msg_no_vehicle_selected,
                    LocoSwap.Language.Resources.msg_message,
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Consist consist = (Consist)ConsistListBox.SelectedItem;

            if (consist.Vehicles.Count == VehicleListBox.SelectedItems.Count)
            {
                MessageBox.Show(
                    LocoSwap.Language.Resources.msg_consist_empty,
                    LocoSwap.Language.Resources.msg_message,
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            foreach (ScenarioVehicle vehicle in VehicleListBox.SelectedItems)
            {
                ViewModel.Scenario.RemoveVehicle(consist.Idx, vehicle.Idx);
                consist.Vehicles.RemoveAt(vehicle.Idx);
                for (var i = 0; i < consist.Vehicles.Count; i++)
                {
                    consist.Vehicles[i].Idx = i;
                }
            }

            ViewModel.Vehicles.Clear();
            foreach (ScenarioVehicle scenarioVehicle in ((Consist)ConsistListBox.SelectedItem).Vehicles)
            {
                ViewModel.Vehicles.Add(scenarioVehicle);
            }
        }

        private void ReplaceButton_Click(object sender, RoutedEventArgs e)
        {
            if (VehicleListBox.SelectedItems.Count == 0 || AvailableVehicleListBox.SelectedItem == null)
            {
                MessageBox.Show(
                    LocoSwap.Language.Resources.msg_no_vehicle_selected,
                    LocoSwap.Language.Resources.msg_message,
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            Consist consist = (Consist)ConsistListBox.SelectedItem;
            IEnumerable<ScenarioVehicle> oldVehicles = VehicleListBox.SelectedItems.Cast<ScenarioVehicle>();
            AvailableVehicle newVehicle = (AvailableVehicle)AvailableVehicleListBox.SelectedItem;

            foreach (var vehicle in oldVehicles)
            {
                vehicle.CopyFrom(newVehicle);
                ViewModel.Scenario.ReplaceVehicle(consist.Idx, vehicle.Idx, newVehicle);
                ViewModel.Scenario.ChangeVehicleNumber(consist.Idx, vehicle.Idx, vehicle.Number);
                vehicle.PossibleSubstitutionDisplayName = null;
            }
            consist.DetermineCompletenessAfterReplace();
        }

        private async void SaveScenario()
        {
            ViewModel.LoadingInformation = LocoSwap.Language.Resources.saving_scenario;
            ViewModel.LoadingProgress = 20;
            var task = Task.Run(() =>
            {
                try
                {
                    ViewModel.Scenario.Save();
                }
                catch (Exception)
                {
                    return false;
                }
                return true;
            });
            var result = await task;
            ViewModel.LoadingProgress = 100;
            if (result)
            {
                MessageBox.Show(
                    LocoSwap.Language.Resources.msg_scenario_saved,
                    LocoSwap.Language.Resources.msg_message,
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show(
                    LocoSwap.Language.Resources.msg_write_access_denied,
                    LocoSwap.Language.Resources.msg_error,
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            return;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            SaveScenario();

        }

        private void AvailableVehicleNumberListButton_Click(object sender, RoutedEventArgs e)
        {
            AvailableVehicle vehicle = (AvailableVehicle)AvailableVehicleListBox.SelectedItem;
            if (vehicle == null) return;
            VehicleNumberSelectionWindow window = new VehicleNumberSelectionWindow(vehicle.NumberingList, VehicleNumberSelectionWindow.WindowType.List);
            window.ShowDialog();
        }

        private void ChangeNumberButton_Click(object sender, RoutedEventArgs e)
        {
            List<string> list = new List<string>();
            list.Add(LocoSwap.Language.Resources.numbering_list_not_found);
            Consist consist = (Consist)ConsistListBox.SelectedItem;
            ScenarioVehicle vehicle = (ScenarioVehicle)VehicleListBox.SelectedItem;
            try
            {
                AvailableVehicle actualVehicle = new AvailableVehicle(Path.ChangeExtension(vehicle.XmlPath, "bin"));
                list = actualVehicle.NumberingList;
            }
            catch (Exception)
            {

            }

            VehicleNumberSelectionWindow window = new VehicleNumberSelectionWindow(list, VehicleNumberSelectionWindow.WindowType.Selection, string.Copy(vehicle.Number));
            window.ShowDialog();

            if (window.DialogResult == true)
            {
                var number = window.SelectedNumber;
                vehicle.Number = number;
                ViewModel.Scenario.ChangeVehicleNumber(consist.Idx, vehicle.Idx, number);
            }
        }

        private void AvailableVehiclesClearButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.AvailableVehicles.Clear();
        }

        private void ReplaceIdenticalButton_Click(object sender, RoutedEventArgs e)
        {
            if (VehicleListBox.SelectedItems.Count == 0 || AvailableVehicleListBox.SelectedItem == null)
            {
                MessageBox.Show(
                    LocoSwap.Language.Resources.msg_no_vehicle_selected,
                    LocoSwap.Language.Resources.msg_message,
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (AddAsRule.IsChecked == true)
            {
                AddToRules(true);
            }

            Dictionary<string, bool> identicalXmlPathList = new Dictionary<string, bool>();
            foreach (ScenarioVehicle vehicle in VehicleListBox.SelectedItems)
            {
                identicalXmlPathList[vehicle.XmlPath] = true;
            }

            AvailableVehicle newVehicle = (AvailableVehicle)AvailableVehicleListBox.SelectedItem;

            foreach (Consist consist in ViewModel.Consists)
            {
                foreach (ScenarioVehicle vehicle in consist.Vehicles)
                {
                    if (identicalXmlPathList.ContainsKey(vehicle.XmlPath))
                    {
                        vehicle.CopyFrom(newVehicle);
                        ViewModel.Scenario.ReplaceVehicle(consist.Idx, vehicle.Idx, newVehicle);
                        ViewModel.Scenario.ChangeVehicleNumber(consist.Idx, vehicle.Idx, vehicle.Number);
                        vehicle.PossibleSubstitutionDisplayName = null;

                        consist.DetermineCompletenessAfterReplace();
                    }
                }
            }

        }

        private void CancelScanningButton_Click(object sender, RoutedEventArgs e)
        {
            if (ScanCancellationTokenSource != null)
            {
                ScanCancellationTokenSource.Cancel();
            }
        }

        private void AddToRules(bool overwriteWithoutPrompting)
        {
            List<ScenarioVehicle> targetVehicleList = new List<ScenarioVehicle>();
            foreach (ScenarioVehicle vehicle in VehicleListBox.SelectedItems)
            {
                if (targetVehicleList.Contains(vehicle)) continue;
                if (!overwriteWithoutPrompting && Settings.Default.Preset.Contains(vehicle.XmlPath))
                {
                    var result = MessageBox.Show(
                        string.Format(LocoSwap.Language.Resources.msg_vehicle_already_in_rules, vehicle.Name),
                        LocoSwap.Language.Resources.msg_message,
                        MessageBoxButton.YesNoCancel,
                        MessageBoxImage.Question);
                    switch (result)
                    {
                        case MessageBoxResult.Yes:
                            targetVehicleList.Add(vehicle);
                            break;
                        case MessageBoxResult.No:
                            break;
                        default:
                            return;
                    }
                }
                else
                {
                    targetVehicleList.Add(vehicle);
                }
            }

            AvailableVehicle newVehicle = (AvailableVehicle)AvailableVehicleListBox.SelectedItem;
            foreach (ScenarioVehicle vehicle in targetVehicleList)
            {
                var existingItem = Settings.Default.Preset.Find(vehicle.XmlPath);
                if (existingItem != null)
                {
                    Settings.Default.Preset.List.Remove(existingItem);
                }
                Settings.Default.Preset.List.Add(new SwapPresetItem()
                {
                    TargetName = vehicle.Name,
                    TargetXmlPath = vehicle.XmlPath,
                    NewName = newVehicle.DisplayName,
                    NewXmlPath = newVehicle.XmlPath,
                    NewLength = newVehicle.Length
                });
            }
            Settings.Default.Save();
        }

        private void AddToRulesButton_Click(object sender, RoutedEventArgs e)
        {
            if (VehicleListBox.SelectedItems.Count == 0 || AvailableVehicleListBox.SelectedItem == null)
            {
            MessageBox.Show(
                    LocoSwap.Language.Resources.msg_no_vehicle_selected,
                LocoSwap.Language.Resources.msg_message,
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            AddToRules(false);

            BringUpPresetWindow();
        }

        private void PresetButton_Click(object sender, RoutedEventArgs e)
        {
            BringUpPresetWindow();
        }

        private void BringUpPresetWindow()
        {
            if (PresetWindow != null)
            {
                PresetWindow.Activate();
                return;
            }
            PresetWindow = new SwapPresetWindow();
            PresetWindow.ApplyClicked += PresetWindow_ApplyClicked;
            PresetWindow.Closed += PresetWindow_Closed;
            PresetWindow.Show();
        }

        private void PresetWindow_ApplyClicked(object sender, EventArgs e)
        {
            List<SwapPresetItem> selectedPresetRules = PresetWindow.SelectedItems;
            Dictionary<string, AvailableVehicle> availableVehicles = new Dictionary<string, AvailableVehicle>();
            foreach (var item in selectedPresetRules)
            {
                var binPath = Path.ChangeExtension(item.NewXmlPath, "bin");
                try
                {
                    availableVehicles[item.NewXmlPath] = new AvailableVehicle(binPath);
                }
                catch (Exception)
                {
                    MessageBox.Show(
                        string.Format(LocoSwap.Language.Resources.msg_cannot_load_vehicle, item.NewName),
                        LocoSwap.Language.Resources.msg_error,
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            foreach (Consist consist in ViewModel.Consists)
            {
                foreach (ScenarioVehicle vehicle in consist.Vehicles)
                {
                    var rule = selectedPresetRules.Where((item) => item.TargetXmlPath.Equals(vehicle.XmlPath, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                    if (rule == null) continue;
                    vehicle.CopyFrom(availableVehicles[rule.NewXmlPath]);
                    ViewModel.Scenario.ReplaceVehicle(consist.Idx, vehicle.Idx, availableVehicles[rule.NewXmlPath]);
                    ViewModel.Scenario.ChangeVehicleNumber(consist.Idx, vehicle.Idx, vehicle.Number);
                    vehicle.PossibleSubstitutionDisplayName = null;

                    consist.DetermineCompletenessAfterReplace();
                }
            }

            MessageBox.Show(
                LocoSwap.Language.Resources.msg_swap_completed,
                LocoSwap.Language.Resources.msg_message,
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void PresetWindow_Closed(object sender, EventArgs e)
        {
            PresetWindow = null;
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            if (PresetWindow != null) PresetWindow.Close();
        }

        private void FlipButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedVehicles = VehicleListBox.SelectedItems;
            Consist consist = (Consist)ConsistListBox.SelectedItem;
            foreach (ScenarioVehicle vehicle in selectedVehicles)
            {
                vehicle.Flipped = !vehicle.Flipped;
                Log.Debug("consist {0}, vehicle {1}, flipped = {2}", consist.Idx, vehicle.Idx, vehicle.Flipped);
                ViewModel.Scenario.ChangeVehicleFlipped(consist.Idx, vehicle.Idx, vehicle.Flipped);
            }
            MessageBox.Show(LocoSwap.Language.Resources.msg_flip_completed,
                LocoSwap.Language.Resources.msg_message,
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void AllVehiclesListButton_Click(object sender, RoutedEventArgs e)
        {
            Dictionary<string, ScenarioVehicle> dict = new Dictionary<string, ScenarioVehicle>();
            foreach (Consist consist in ViewModel.Consists)
            {
                foreach (ScenarioVehicle vehicle in consist.Vehicles)
                {
                    string identifier;
                    if (vehicle.IsReskin)
                    {
                        identifier = vehicle.ReskinXmlPath;
                    }
                    else
                    {
                        identifier = vehicle.XmlPath;
                    }
                    if (!dict.ContainsKey(identifier))
                    {
                        dict.Add(identifier, vehicle);
                    }
                }
            }
            List<ScenarioVehicle> list = new List<ScenarioVehicle>();
            foreach (KeyValuePair<string, ScenarioVehicle> kvpair in dict)
            {
                list.Add(kvpair.Value);
            }
            AllVehiclesWindow window = new AllVehiclesWindow(list);
            window.ShowDialog();
        }

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key >= Key.A && e.Key <= Key.Z)
            {
                int index = e.Key - Key.A;
                char ch = 'a';
                ch += (char)index;

                foreach (DirectoryItem dir in DirectoryTree.Items)
                {
                    if (dir.Name.ToLower()[0] == ch)
                    {
                        dir.IsSelected = true;
                        TreeViewItem tvi = DirectoryTree.ItemContainerGenerator.ContainerFromItem(dir) as TreeViewItem;
                        tvi.BringIntoView();
                        break;
                    }
                }
            }
        }

        private void ApplyAllRulesOnlyOnMissingStock_Click(object sender, RoutedEventArgs e)
        {
            ApplyAllRules(true);
        }

        private void ApplyAllRulesOnAllStock_Click(object sender, RoutedEventArgs e)
        {
            ApplyAllRules(false);
        }

        private void ApplyAllRules(bool onlyOnMissingStock)
        {
            Dictionary<string, AvailableVehicle> availableVehicles = new Dictionary<string, AvailableVehicle>();

            foreach (Consist consist in ViewModel.Consists)
            {
                if (!onlyOnMissingStock || consist.IsComplete != ConsistVehicleExistance.Found && consist.IsComplete != ConsistVehicleExistance.FullyReplaced)
                {
                    foreach (ScenarioVehicle vehicle in consist.Vehicles)
                    {
                        if (vehicle.Exists != VehicleExistance.Missing && !onlyOnMissingStock || vehicle.Exists == VehicleExistance.MissingWithRule)
                        {
                            var item = Settings.Default.Preset.Find(vehicle.XmlPath);

                            if (item != null)
                            {
                                var binPath = Path.ChangeExtension(item.NewXmlPath, "bin");

                                if (!availableVehicles.ContainsKey(item.NewXmlPath))
                                {
                                    try
                                    {
                                        availableVehicles[item.NewXmlPath] = new AvailableVehicle(binPath);
                                    }
                                    catch (Exception)
                                    {
                                        MessageBox.Show(
                                            string.Format(LocoSwap.Language.Resources.msg_cannot_load_vehicle, item.NewName),
                                            LocoSwap.Language.Resources.msg_error,
                                            MessageBoxButton.OK, MessageBoxImage.Warning);
                                        return;
                                    }
                                }

                                vehicle.CopyFrom(availableVehicles[item.NewXmlPath]);
                                ViewModel.Scenario.ReplaceVehicle(consist.Idx, vehicle.Idx, availableVehicles[item.NewXmlPath]);
                                ViewModel.Scenario.ChangeVehicleNumber(consist.Idx, vehicle.Idx, vehicle.Number);
                                vehicle.PossibleSubstitutionDisplayName = null;
                                consist.DetermineCompletenessAfterReplace();
                            }
                        }
                    }
                }
            }
        }

        public void AvailableVehiclesFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            CollectionViewSource.GetDefaultView(AvailableVehicleListBox.ItemsSource).Refresh();
        }
        public void OpenManual_Click(object sender, RoutedEventArgs e)
        {
            Utilities.OpenManual();
        }
    }
}
