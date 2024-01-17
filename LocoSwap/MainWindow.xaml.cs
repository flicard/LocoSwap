using Ionic.Zip;
using LocoSwap.Properties;
using Serilog;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace LocoSwap
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public ObservableCollection<Route> Routes { get; } = new ObservableCollection<Route>();
        public ObservableCollection<Scenario> Scenarios { get; } = new ObservableCollection<Scenario>();
        public string WindowTitle { get; set; } = "LocoSwap";
        public MainWindow()
        {
            InitializeComponent();
            UpdateColumnVisibility();

            DataContext = this;
            WindowTitle = "LocoSwap " + Assembly.GetEntryAssembly().GetName().Version.ToString();

            var routes = Route.ListAllRoutes();

            foreach (Route route in routes)
            {
                try
                {
                    route.PropertyChanged += Route_PropertyChanged;
                    Routes.Add(route);
                }
                catch (Exception)
                {
                }
            }

            // Asynchronously read the scenario DB to populate the Scenario completion status
            ReadScenarioDb();

            FileSystemWatcher watcher = new FileSystemWatcher(Path.Combine(Properties.Settings.Default.TsPath, "Content"));

            watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.LastWrite;

            watcher.Changed += OnSDBCacheUpdate;
            watcher.Created += OnSDBCacheUpdate;

            watcher.Filter = "SDBCache.bin";
            watcher.IncludeSubdirectories = false;
            watcher.EnableRaisingEvents = true;

            Loaded += On_MainWindow_Loaded;
        }

        private void On_MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Add filter to RouteList
            CollectionView RouteListview = (CollectionView)CollectionViewSource.GetDefaultView(RouteList.ItemsSource);
            RouteListview.Filter = RouteFilter;

            // Add filter to ScenarioList
            CollectionView ScenarioListview = (CollectionView)CollectionViewSource.GetDefaultView(ScenarioList.ItemsSource);
            ScenarioListview.Filter = ScenarioFilter;
        }

        private void Route_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "IsFavorite")
            {
                Route route = sender as Route;
                StringCollection favorite = Properties.Settings.Default.FavoriteRoutes ?? new StringCollection();
                if (Properties.Settings.Default.FavoriteRoutes == null) Properties.Settings.Default.FavoriteRoutes = favorite;
                if (route.IsFavorite)
                {
                    Log.Debug("Adding {0} to favorite..", route.Name);
                    favorite.Add(route.Id);
                }
                else
                {
                    Log.Debug("Removing {0} from favorite..", route.Name);
                    favorite.Remove(route.Id);
                }
                Properties.Settings.Default.Save();
            }
        }

        private void RouteList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (RouteList.SelectedItem != null)
            {
                Refresh_Scenario_List();
            }
            else
            {
                Scenarios.Clear();
            }
        }
        private void Backups_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //Tuple<DateTime, DateTime> selectedBackup = sender as Tuple<DateTime, DateTime>;

        }
        private void Refresh_Scenario_List()
        {
            Scenarios.Clear();
            Route route = (Route)RouteList.SelectedItem;

            string routeDirectory = Route.GetRouteDirectory(route.Id);
            string scenariosDirectory = Scenario.GetScenariosDirectory(route.Id);

            if (Directory.Exists(scenariosDirectory))
            {
                string[] scenarioDirectories = Directory.GetDirectories(scenariosDirectory);
                foreach (string directory in scenarioDirectories)
                {
                    string id = new DirectoryInfo(directory).Name;
                    string xmlPath = Path.Combine(directory, "ScenarioProperties.xml");
                    string xmlPathIfArchived = Path.Combine(directory, "ScenarioPropertiesLocoSwapOff.xml");
                    string binPath = Path.Combine(directory, "Scenario.bin");
                    if (!File.Exists(binPath) || (!File.Exists(xmlPath) && !File.Exists(xmlPathIfArchived))) continue;
                    Scenarios.Add(new Scenario(route, id, ""));
                }
            }
            string[] allowedExtensions = new[] { ".ap", ".ap.LSoff" };
            string[] apFiles = Directory.GetFiles(routeDirectory, "*", SearchOption.TopDirectoryOnly).Where(file => allowedExtensions.Any(file.EndsWith)).ToArray();
            foreach (string apPath in apFiles)
            {

                try
                {
                    ZipFile zipFile = ZipFile.Read(apPath);
                    try
                    {


                        Regex scenarioPropFileRegex = new Regex(@"^(Scenarios/([a-f\d\-]{36})/)ScenarioProperties\.xml$");
                        foreach (ZipEntry file in zipFile)
                        {
                            Match match = scenarioPropFileRegex.Match(file.FileName);
                            if (match.Success &&
                                zipFile.Select(file2 => file2.FileName == match.Groups[1].Value + "Scenario.bin").Count() > 0 &&
                                !File.Exists(Path.Combine(routeDirectory, "Scenarios", match.Groups[2].Value, "ScenarioProperties.xml")))
                            {
                                Scenarios.Add(new Scenario(route, match.Groups[2].Value, apPath));
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Error("Error while reading " + apPath + " for scenarios, " + e.Message);
                    }
                    finally
                    {
                        zipFile.Dispose();
                    }
                }

                catch(Exception e)
                {
                    Log.Error("Couldn't read " + apPath + " for scenarios, " + e.Message);
                }
            }
        }

        private void EditScenarioButton_Click(object sender, RoutedEventArgs e)
        {
            string routeId = ((Route)RouteList.SelectedItem).Id;
            Scenario scenario = (Scenario)ScenarioList.SelectedItem;
            new ScenarioEditWindow(routeId, scenario).Show();
        }


        private void OpenScenarioDirButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (Scenario scenario in ScenarioList.SelectedItems)
            {
                if (scenario.ApFileName == "")
                {
                    Process.Start(scenario.ScenarioDirectory);
                }
            }
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            bool previousCheckScenarioConsistsValue = Settings.Default.CheckScenarioConsists;

            new SettingsWindow().ShowDialog();

            if (previousCheckScenarioConsistsValue != Settings.Default.CheckScenarioConsists)
            {
                UpdateColumnVisibility();
            }
        }

        private void ScenarioList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var dataContext = ((FrameworkElement)e.OriginalSource).DataContext;
            if (dataContext is Scenario)
            {
                if (RouteList.SelectedItem == null) return;
                string routeId = ((Route)RouteList.SelectedItem).Id;
                new ScenarioEditWindow(routeId, (Scenario)dataContext).Show();
            }
        }

        private void Delete_Scenarios_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult msgResult = MessageBox.Show(LocoSwap.Language.Resources.scenario_delete_prompt_message, LocoSwap.Language.Resources.scenario_delete_prompt_title, MessageBoxButton.YesNoCancel);
            if (msgResult == MessageBoxResult.Yes)
            {
                foreach (Scenario scenario in ScenarioList.SelectedItems)
                {
                    scenario.Delete();
                }
                if (RouteList.SelectedItem != null)
                {
                    Refresh_Scenario_List();
                }
            }
        }

        private void ToggleArchiveRoutes_Click(object sender, RoutedEventArgs e)
        {
            if (ScenarioDb.dbState != ScenarioDb.DBState.Loaded)
            {
                MessageBoxResult msgResult = MessageBox.Show(LocoSwap.Language.Resources.archive_without_db_loaded_prompt_message, LocoSwap.Language.Resources.archive_without_db_loaded_prompt_title, MessageBoxButton.YesNoCancel);
                if (msgResult != MessageBoxResult.Yes)
                {
                    return;
                }
            }
            foreach (Route route in RouteList.SelectedItems)
            {
                route.ToggleArchive();
            }
        }

        private void ArchiveAllButSelectedRoutes_Click(object sender, RoutedEventArgs e)
        {
            foreach (Route route in Routes)
            {
                if (!route.IsArchived && !RouteList.SelectedItems.Contains(route) && !(Properties.Settings.Default.DoNotAutoArchiveWorkshopRoutes && route.IsWorkshop)
                    ||
                    route.IsArchived && RouteList.SelectedItems.Contains(route))
                {
                    route.ToggleArchive();
                }
            }
        }
        
        private bool RouteFilter(object item)
        {
            if (string.IsNullOrEmpty(RouteFilterTextbox.Text))
                return true;

            Route candidateRoute = item as Route;

            string[] filteredProperties = {
                candidateRoute.Id,
                candidateRoute.Name
            };

            return RouteFilterTextbox.Text.Split(' ').All(
                filterToken => filteredProperties.Where(
                    prop => prop?.IndexOf(filterToken, StringComparison.OrdinalIgnoreCase) >= 0).ToArray().Length > 0
                );
        }

        private bool ScenarioFilter(object item)
        {
            Scenario candidateScenario = item as Scenario;

            // Hide played scenarios ?
            if (HidePlayedScenariosCheckbox.IsChecked == true && (
                candidateScenario.Completion == ScenarioDb.ScenarioCompletion.CompletedSuccessfully ||
                candidateScenario.Completion == ScenarioDb.ScenarioCompletion.CompletedFailed)
                )
            {
                return false;
            }

            // Textual filter
            if (string.IsNullOrEmpty(ScenarioFilterTextbox.Text))
                return true;

            string[] filteredProperties = {
                candidateScenario.Id,
                candidateScenario.Name,
                candidateScenario.Description,
                candidateScenario.PlayerTrainName,
                candidateScenario.Author
            };

            return ScenarioFilterTextbox.Text.Split(' ').All(
                filterToken => filteredProperties.Where(
                    prop => prop?.IndexOf(filterToken, StringComparison.OrdinalIgnoreCase) >= 0).ToArray().Length > 0
                );
        }

        public async void ReadScenarioDb()
        {
            Task readDbTask = Task.Run(() =>
            {
                ScenarioDb.ParseScenarioDb();
            });
            
            Log.Debug("ReadScenarioDb is about to invoke parallel read");
            
            await Task.WhenAll(readDbTask);

            Log.Debug("SDB is read, refreshing scenarios list");

            // Refresh scenario list with completion
            // Use Dispatcher to update UI on the main (UI) thread
            Application.Current.Dispatcher.Invoke(() =>
            {
                CollectionViewSource.GetDefaultView(ScenarioList.ItemsSource).Refresh();
            });
        }

        private void OnSDBCacheUpdate(object sender, FileSystemEventArgs e)
        {
            FileInfo sdbCacheFileInfo = new FileInfo(Path.Combine(Properties.Settings.Default.TsPath, "Content", "SDBCache.bin"));
            Log.Information("SDBCache.bin updated Event=Changed ! Size = " + sdbCacheFileInfo.Length);

            // When TS rewrites the SDBCache, a first event will be triggered while the file is at 0 byte: ignore
            if (sdbCacheFileInfo.Length > 0)
            {
                ReadScenarioDb();
            }
        }

        public void RouteFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            CollectionViewSource.GetDefaultView(RouteList.ItemsSource).Refresh();
        }

        private void EmptyRouteFilter_Click(object sender, RoutedEventArgs e)
        {
            RouteFilterTextbox.Text = "";
        }

        public void ScenarioFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            CollectionViewSource.GetDefaultView(ScenarioList.ItemsSource).Refresh();
        }
        private void EmptyScenarioFilter_Click(object sender, RoutedEventArgs e)
        {
            ScenarioFilterTextbox.Text = "";
        }

        public void HidePlayedScenario_CheckboxChanged(object sender, RoutedEventArgs e)
        {
            CollectionViewSource.GetDefaultView(ScenarioList.ItemsSource).Refresh();
        }

        public void OpenManual_Click(object sender, RoutedEventArgs e)
        {
            Utilities.OpenManual();
        }

        private void UpdateColumnVisibility()
        {
            if (Settings.Default.CheckScenarioConsists)
            {
                AddColumnIfNotExists(CheckScenarioConsists);
            }
            else
            {
                RemoveColumnIfExists(CheckScenarioConsists);
            }
        }

        private void AddColumnIfNotExists(GridViewColumn column)
        {
            GridView gridView = ScenarioList.View as GridView;
            if (gridView != null && !gridView.Columns.Contains(column))
            {
                gridView.Columns.Insert(0, column);
            }
            if (RouteList.SelectedItem != null)
            {
                Refresh_Scenario_List();
            }
        }

        private void RemoveColumnIfExists(GridViewColumn column)
        {
            GridView gridView = ScenarioList.View as GridView;
            if (gridView != null && gridView.Columns.Contains(column))
            {
                gridView.Columns.Remove(column);
            }
            if (RouteList.SelectedItem != null)
            {
                Refresh_Scenario_List();
            }
        }

        private void BackupsItem_Click(object sender, RoutedEventArgs e)
        {
            ScenarioBackup clickedBackup = (ScenarioBackup)((Button)sender).DataContext;

            if (clickedBackup.NamingDate != null)
            {
                clickedBackup.SwitchToBackup();

                Refresh_Scenario_List();
            }
        }
    }
}
