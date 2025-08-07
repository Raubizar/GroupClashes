using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WIN = System.Windows;
using System.Windows.Controls;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Markup;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Threading;
using System.Windows.Threading;

using Autodesk.Navisworks.Api.Clash;
using Autodesk.Navisworks.Api;

namespace GroupClashes
{
    /// <summary>
    /// Interaction logic for GroupClashesInterface.xaml
    /// </summary>
    public partial class GroupClashesInterface : UserControl
    {
        public ObservableCollection<CustomClashTest> ClashTests { get; set; }
        public ObservableCollection<GroupingMode> GroupByList { get; set; }
        public ObservableCollection<GroupingMode> GroupThenList { get; set; }
        public ClashTest SelectedClashTest { get; set; }

        private bool _isProcessing = false;
        private readonly object _processLock = new object();

        public GroupClashesInterface()
        {
            Logger.Initialize();
            Logger.LogInfo("GroupClashesInterface constructor called");
            
            using (Logger.MeasurePerformance("Interface Initialization"))
            {
                InitializeComponent();

                ClashTests = new ObservableCollection<CustomClashTest>();
                GroupByList = new ObservableCollection<GroupingMode>();
                GroupThenList = new ObservableCollection<GroupingMode>();

                RegisterChanges();

                this.DataContext = this;
                
                Logger.LogInfo("Interface initialization completed successfully");
            }
        }

        private async void Group_Button_Click(object sender, WIN.RoutedEventArgs e)
        {
            Logger.LogUserAction("Group Button Clicked", $"Selected items: {ClashTestListBox.SelectedItems.Count}");
            Logger.LogUIThread("Group_Button_Click", Dispatcher.CheckAccess());

            // Prevent multiple simultaneous operations
            lock (_processLock)
            {
                if (_isProcessing)
                {
                    Logger.LogWarning("Group operation already in progress, ignoring click");
                    return;
                }
                _isProcessing = true;
            }

            try
            {
                if (ClashTestListBox.SelectedItems.Count == 0)
                {
                    Logger.LogWarning("No clash tests selected for grouping");
                    return;
                }

                // Disable UI during processing
                SetUIEnabled(false);
                Logger.LogInfo("UI disabled for processing");

                // Unsubscribe temporarily to avoid event conflicts
                UnRegisterChanges();
                Logger.LogInfo("Event handlers unregistered");

                await Task.Run(() =>
                {
                    Logger.LogInfo("Starting grouping operation on background thread");
                    
                    using (Logger.MeasurePerformance("Total Grouping Operation"))
                    {
                        ProcessGroupingOperation();
                    }
                });
            }
            catch (Exception ex)
            {
                Logger.LogError("Error in Group_Button_Click", ex);
                WIN.MessageBox.Show($"An error occurred during grouping: {ex.Message}", "GroupClashes Error", 
                    WIN.MessageBoxButton.OK, WIN.MessageBoxImage.Error);
            }
            finally
            {
                // Re-enable UI and re-register events
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    SetUIEnabled(true);
                    RegisterChanges();
                    Logger.LogInfo("UI re-enabled and events re-registered");
                    
                    lock (_processLock)
                    {
                        _isProcessing = false;
                    }
                }));
            }
        }

        private void ProcessGroupingOperation()
        {
            var selectedItems = new List<CustomClashTest>();
            
            // Capture selected items on UI thread
            Dispatcher.Invoke(() =>
            {
                foreach (object selectedItem in ClashTestListBox.SelectedItems)
                {
                    selectedItems.Add((CustomClashTest)selectedItem);
                }
            });

            Logger.LogInfo($"Processing {selectedItems.Count} clash tests");

            foreach (var selectedClashTest in selectedItems)
            {
                try
                {
                    using (Logger.MeasurePerformance($"Grouping {selectedClashTest.ClashTest.DisplayName}"))
                    {
                        ClashTest clashTest = selectedClashTest.ClashTest;

                        if (clashTest.Children.Count == 0)
                        {
                            Logger.LogWarning($"Clash test '{clashTest.DisplayName}' has no children to group");
                            continue;
                        }

                        GroupingMode groupBy = GroupingMode.None;
                        GroupingMode thenBy = GroupingMode.None;
                        bool keepExisting = false;

                        // Get UI values on UI thread
                        Dispatcher.Invoke(() =>
                        {
                            groupBy = (GroupingMode)(comboBoxGroupBy.SelectedItem ?? GroupingMode.None);
                            thenBy = (GroupingMode)(comboBoxThenBy.SelectedItem ?? GroupingMode.None);
                            keepExisting = (bool)(keepExistingGroupsCheckBox.IsChecked ?? false);
                        });

                        Logger.LogUserAction("Grouping Configuration", 
                            $"GroupBy: {groupBy}, ThenBy: {thenBy}, KeepExisting: {keepExisting}");

                        if (thenBy != GroupingMode.None || groupBy != GroupingMode.None)
                        {
                            if (thenBy == GroupingMode.None && groupBy != GroupingMode.None)
                            {
                                Logger.LogInfo($"Single grouping mode: {groupBy}");
                                GroupingFunctions.GroupClashes(clashTest, groupBy, GroupingMode.None, keepExisting);
                            }
                            else if (groupBy == GroupingMode.None && thenBy != GroupingMode.None)
                            {
                                Logger.LogInfo($"Single grouping mode (then by): {thenBy}");
                                GroupingFunctions.GroupClashes(clashTest, thenBy, GroupingMode.None, keepExisting);
                            }
                            else
                            {
                                Logger.LogInfo($"Hierarchical grouping: {groupBy} then {thenBy}");
                                GroupingFunctions.GroupClashes(clashTest, groupBy, thenBy, keepExisting);
                            }
                            
                            Logger.LogInfo($"Successfully grouped clash test: {clashTest.DisplayName}");
                        }
                        else
                        {
                            Logger.LogWarning("No grouping mode selected");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Error processing clash test '{selectedClashTest.ClashTest.DisplayName}'", ex);
                }
            }
        }

        private void SetUIEnabled(bool enabled)
        {
            Group_Button.IsEnabled = enabled;
            Ungroup_Button.IsEnabled = enabled;
            comboBoxGroupBy.IsEnabled = enabled;
            comboBoxThenBy.IsEnabled = enabled;
            ClashTestListBox.IsEnabled = enabled;
            keepExistingGroupsCheckBox.IsEnabled = enabled;
            
            Logger.LogInfo($"UI controls {(enabled ? "enabled" : "disabled")}");
        }

        private async void Ungroup_Button_Click(object sender, WIN.RoutedEventArgs e)
        {
            Logger.LogUserAction("Ungroup Button Clicked", $"Selected items: {ClashTestListBox.SelectedItems.Count}");
            
            // Prevent multiple simultaneous operations
            lock (_processLock)
            {
                if (_isProcessing)
                {
                    Logger.LogWarning("Ungroup operation already in progress, ignoring click");
                    return;
                }
                _isProcessing = true;
            }

            try
            {
                if (ClashTestListBox.SelectedItems.Count == 0)
                {
                    Logger.LogWarning("No clash tests selected for ungrouping");
                    return;
                }

                SetUIEnabled(false);
                UnRegisterChanges();

                await Task.Run(() =>
                {
                    Logger.LogInfo("Starting ungrouping operation on background thread");
                    
                    using (Logger.MeasurePerformance("Total Ungrouping Operation"))
                    {
                        ProcessUngroupingOperation();
                    }
                });
            }
            catch (Exception ex)
            {
                Logger.LogError("Error in Ungroup_Button_Click", ex);
                WIN.MessageBox.Show($"An error occurred during ungrouping: {ex.Message}", "GroupClashes Error", 
                    WIN.MessageBoxButton.OK, WIN.MessageBoxImage.Error);
            }
            finally
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    SetUIEnabled(true);
                    RegisterChanges();
                    
                    lock (_processLock)
                    {
                        _isProcessing = false;
                    }
                }));
            }
        }

        private void ProcessUngroupingOperation()
        {
            var selectedItems = new List<CustomClashTest>();
            
            Dispatcher.Invoke(() =>
            {
                foreach (object selectedItem in ClashTestListBox.SelectedItems)
                {
                    selectedItems.Add((CustomClashTest)selectedItem);
                }
            });

            foreach (var selectedClashTest in selectedItems)
            {
                try
                {
                    using (Logger.MeasurePerformance($"Ungrouping {selectedClashTest.ClashTest.DisplayName}"))
                    {
                        ClashTest clashTest = selectedClashTest.ClashTest;

                        if (clashTest.Children.Count == 0)
                        {
                            Logger.LogWarning($"Clash test '{clashTest.DisplayName}' has no children to ungroup");
                            continue;
                        }

                        GroupingFunctions.UnGroupClashes(clashTest);
                        Logger.LogInfo($"Successfully ungrouped clash test: {clashTest.DisplayName}");
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Error ungrouping clash test '{selectedClashTest.ClashTest.DisplayName}'", ex);
                }
            }
        }

        private void RegisterChanges()
        {
            try
            {
                Logger.LogInfo("Registering change event handlers");
                
                //When the document change
                Application.MainDocument.Database.Changed += DocumentClashTests_Changed;

                //When a clash test change
                DocumentClashTests dct = Application.MainDocument.GetClash().TestsData;
                //Register
                dct.Changed += DocumentClashTests_Changed;

                //Get all clash tests and check up to date
                GetClashTests();
                CheckPlugin();
                LoadComboBox();
                
                Logger.LogInfo("Event handlers registered successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError("Error registering change handlers", ex);
            }
        }

        private void UnRegisterChanges()
        {
            try
            {
                Logger.LogInfo("Unregistering change event handlers");
                
                //When the document change
                Application.MainDocument.Database.Changed -= DocumentClashTests_Changed;

                //When a clash test change
                DocumentClashTests dct = Application.MainDocument.GetClash().TestsData;
                //Register
                dct.Changed -= DocumentClashTests_Changed;
                
                Logger.LogInfo("Event handlers unregistered successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError("Error unregistering change handlers", ex);
            }
        }

        void DocumentClashTests_Changed(object sender, EventArgs e)
        {
            try
            {
                Logger.LogInfo("Document clash tests changed event received");
                Logger.LogUIThread("DocumentClashTests_Changed", Dispatcher.CheckAccess());
                
                GetClashTests();
                CheckPlugin();
                LoadComboBox();
            }
            catch (Exception ex)
            {
                Logger.LogError("Error handling document change event", ex);
            }
        }

        private void GetClashTests()
        {
            try
            {
                using (Logger.MeasurePerformance("GetClashTests"))
                {
                    DocumentClashTests dct = Application.MainDocument.GetClash().TestsData;
                    ClashTests.Clear();

                    int testCount = 0;
                    foreach (SavedItem savedItem in dct.Tests)
                    {
                        if (savedItem.GetType() == typeof(ClashTest))
                        {
                            ClashTests.Add(new CustomClashTest(savedItem as ClashTest));
                            testCount++;
                        }
                    }
                    
                    Logger.LogInfo($"Loaded {testCount} clash tests");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error getting clash tests", ex);
            }
        }

        private void CheckPlugin()
        {
            try
            {
                //Inactive if there is no document open or there are no clash tests
                bool shouldEnable = !(Application.MainDocument == null
                    || Application.MainDocument.IsClear
                    || Application.MainDocument.GetClash() == null
                    || Application.MainDocument.GetClash().TestsData.Tests.Count == 0);

                Group_Button.IsEnabled = shouldEnable;
                comboBoxGroupBy.IsEnabled = shouldEnable;
                comboBoxThenBy.IsEnabled = shouldEnable;
                Ungroup_Button.IsEnabled = shouldEnable;
                
                Logger.LogInfo($"Plugin UI state: {(shouldEnable ? "enabled" : "disabled")}");
            }
            catch (Exception ex)
            {
                Logger.LogError("Error checking plugin state", ex);
            }
        }

        private void LoadComboBox()
        {
            GroupByList.Clear();
            GroupThenList.Clear();

            foreach (GroupingMode mode in Enum.GetValues(typeof(GroupingMode)).Cast<GroupingMode>())
            {
                GroupThenList.Add(mode);
                GroupByList.Add(mode);
            }

            if (Application.MainDocument.Grids.ActiveSystem == null)
            {
                GroupByList.Remove(GroupingMode.GridIntersection);
                GroupByList.Remove(GroupingMode.Level);
                GroupThenList.Remove(GroupingMode.GridIntersection);
                GroupThenList.Remove(GroupingMode.Level);
            }

            comboBoxGroupBy.SelectedIndex = 0;
            comboBoxThenBy.SelectedIndex = 0;
        }
    }

    public class CustomClashTest
    {
        public CustomClashTest(ClashTest test)
        {
            _clashTest = test;
        }

        public string DisplayName { get { return _clashTest.DisplayName; } }

        private ClashTest _clashTest;
        public ClashTest ClashTest { get { return _clashTest; } }

        public string SelectionAName
        {
            get { return GetSelectedItem(_clashTest.SelectionA); }
        }

        public string SelectionBName
        {
            get { return GetSelectedItem(_clashTest.SelectionB); }
        }

        private string GetSelectedItem(ClashSelection selection)
        {
            string result = "";
            if (selection.Selection.HasSelectionSources)
            {
                result = selection.Selection.SelectionSources.FirstOrDefault().ToString();
                if (result.Contains("lcop_selection_set_tree\\"))
                {
                    result = result.Replace("lcop_selection_set_tree\\", "");
                }

                if (selection.Selection.SelectionSources.Count > 1)
                {
                    result = result + " (and other selection sets)";
                }

            }
            else if (selection.Selection.GetSelectedItems().Count == 0)
            {
                result = "No item have been selected.";
            }
            else if (selection.Selection.GetSelectedItems().Count == 1)
            {
                result = selection.Selection.GetSelectedItems().FirstOrDefault().DisplayName;
            }
            else
            {
                result = selection.Selection.GetSelectedItems().FirstOrDefault().DisplayName;
                foreach (ModelItem item in selection.Selection.GetSelectedItems().Skip(1))
                {
                    result = result + "; " + item.DisplayName;
                }
            }

            return result;
        }

    }
}
