﻿/*
 * View and DataGridView save state code based on code by Ron Dunant.
 * http://www.codeproject.com/KB/grid/PersistentDataGridView.aspx
 */

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using HFM.Core;
using HFM.Core.Client;
using HFM.Core.Logging;
using HFM.Core.Services;
using HFM.Core.WorkUnits;
using HFM.Forms.Internal;
using HFM.Forms.Models;
using HFM.Forms.Presenters;
using HFM.Forms.Views;
using HFM.Log;
using HFM.Preferences;
using HFM.Proteins;

using Microsoft.Extensions.DependencyInjection;

namespace HFM.Forms
{
    public sealed class MainPresenter
    {
        /// <summary>
        /// Holds the state of the window before it is hidden (minimize to tray behaviour)
        /// </summary>
        public FormWindowState OriginalWindowState { get; private set; }

        public MainGridModel GridModel { get; }
        public ILogger Logger { get; }
        public IServiceScopeFactory ServiceScopeFactory { get; }
        public MessageBoxPresenter MessageBox { get; }

        private readonly IMainView _view;
        private readonly UserStatsDataModel _userStatsDataModel;
        private readonly ClientConfiguration _clientConfiguration;
        private readonly IPreferences _preferences;
        private readonly ClientSettingsManager _settingsManager;

        public MainPresenter(MainGridModel gridModel, IMainView view, ILogger logger, IServiceScopeFactory serviceScopeFactory, MessageBoxPresenter messageBox,
                             UserStatsDataModel userStatsDataModel)
        {
            GridModel = gridModel;
            GridModel.AfterResetBindings += (sender, e) =>
            {
                // run asynchronously so binding operation can finish
                _view.BeginInvoke(new Action(() =>
                {
                    DisplaySelectedSlotData();
                    _view.RefreshControlsWithTotalsData(GridModel.SlotTotals);
                }), null);
            };
            GridModel.SelectedSlotChanged += (sender, e) =>
            {
                if (e.Index >= 0 && e.Index < _view.DataGridView.Rows.Count)
                {
                    // run asynchronously so binding operation can finish
                    _view.BeginInvoke(new Action(() =>
                    {
                        _view.DataGridView.Rows[e.Index].Selected = true;
                        DisplaySelectedSlotData();
                    }), null);
                }
            };

            _view = view;
            Logger = logger ?? NullLogger.Instance;
            ServiceScopeFactory = serviceScopeFactory;
            MessageBox = messageBox;
            _userStatsDataModel = userStatsDataModel;

            _clientConfiguration = GridModel.ClientConfiguration;
            _preferences = GridModel.Preferences;
            _settingsManager = new ClientSettingsManager();

            _clientConfiguration.ClientConfigurationChanged += (s, e) => AutoSaveConfig();
        }

        #region Initialize

        private string _openFile;

        public void Initialize(string openFile)
        {
            _openFile = openFile;

            // Restore View Preferences (must be done AFTER DataGridView columns are setup)
            RestoreViewPreferences();
            //
            _view.SetGridDataSource(GridModel.BindingSource);
            //
            _preferences.PreferenceChanged += (s, e) =>
            {
                switch (e.Preference)
                {
                    case Preference.MinimizeTo:
                        SetViewShowStyle();
                        break;
                    case Preference.ColorLogFile:
                        ApplyColorLogFileSetting();
                        break;
                    case Preference.EocUserId:
                        _userStatsDataModel.Refresh();
                        break;
                }
            };
        }

        private void RestoreViewPreferences()
        {
            var restoreLocation = _preferences.Get<Point>(Preference.FormLocation);
            var restoreSize = _preferences.Get<Size>(Preference.FormSize);
            var (location, size) = WindowPosition.Normalize(_view, restoreLocation, restoreSize);
            _view.Location = location;

            // Look for view size
            if (size.Width != 0 && size.Height != 0)
            {
                if (!_preferences.Get<bool>(Preference.FormLogWindowVisible))
                {
                    size = new Size(size.Width, size.Height + _preferences.Get<int>(Preference.FormLogWindowHeight));
                }
                _view.Size = size;
                // make sure split location from the prefs is at least the minimum panel size - Issue 234
                var formSplitLocation = _preferences.Get<int>(Preference.FormSplitterLocation);
                if (formSplitLocation < _view.SplitContainer.Panel2MinSize) formSplitLocation = _view.SplitContainer.Panel2MinSize;
                _view.SplitContainer.SplitterDistance = formSplitLocation;
            }

            if (!_preferences.Get<bool>(Preference.FormLogWindowVisible))
            {
                ShowHideLogWindow(false);
            }
            if (!_preferences.Get<bool>(Preference.QueueWindowVisible))
            {
                ShowHideQueue(false);
            }
            _view.FollowLogFileChecked = _preferences.Get<bool>(Preference.FollowLog);

            GridModel.SortColumnName = _preferences.Get<string>(Preference.FormSortColumn);
            GridModel.SortColumnOrder = _preferences.Get<ListSortDirection>(Preference.FormSortOrder);

            try
            {
                // Restore the columns' state
                var columns = _preferences.Get<ICollection<string>>(Preference.FormColumns);
                if (columns != null)
                {
                    var colsList = columns.ToList();
                    colsList.Sort();

                    for (int i = 0; i < colsList.Count && i < MainForm.NumberOfDisplayFields; i++)
                    {
                        string[] tokens = colsList[i].Split(',');
                        int index = Int32.Parse(tokens[3]);
                        _view.DataGridView.Columns[index].DisplayIndex = Int32.Parse(tokens[0]);
                        if (_view.DataGridView.Columns[index].AutoSizeMode.Equals(DataGridViewAutoSizeColumnMode.Fill) == false)
                        {
                            _view.DataGridView.Columns[index].Width = Int32.Parse(tokens[1]);
                        }
                        _view.DataGridView.Columns[index].Visible = Boolean.Parse(tokens[2]);
                    }
                }
            }
            catch (NullReferenceException)
            {
                // This happens when the FormColumns setting is empty
            }
        }

        #endregion

        #region View Handling Methods

        public void ViewShown()
        {
            // Add the Index Changed Handler here after everything is shown
            _view.DataGridView.ColumnDisplayIndexChanged += delegate { DataGridViewColumnDisplayIndexChanged(); };
            // Then run it once to ensure the last column is set to Fill
            DataGridViewColumnDisplayIndexChanged();
            // Add the Splitter Moved Handler here after everything is shown - Issue 8
            // When the log file window (Panel2) is visible, this event will fire.
            // Update the split location directly from the split panel control. - Issue 8
            _view.SplitContainer.SplitterMoved += delegate
                                                  {
                                                      _preferences.Set(Preference.FormSplitterLocation, _view.SplitContainer.SplitterDistance);
                                                      _preferences.Save();
                                                  };

            if (_preferences.Get<bool>(Preference.RunMinimized))
            {
                _view.WindowState = FormWindowState.Minimized;
            }

            if (!String.IsNullOrEmpty(_openFile))
            {
                LoadConfigFile(_openFile);
            }
            else if (_preferences.Get<bool>(Preference.UseDefaultConfigFile))
            {
                var fileName = _preferences.Get<string>(Preference.DefaultConfigFile);
                if (!String.IsNullOrEmpty(fileName))
                {
                    LoadConfigFile(fileName);
                }
            }

            SetViewShowStyle();
        }

        public void CheckForUpdateOnStartup(IApplicationUpdateService service)
        {
            if (_preferences.Get<bool>(Preference.StartupCheckForUpdate))
            {
                CheckForUpdateInternal(service);
            }
        }

        private void CheckForUpdate(IApplicationUpdateService service)
        {
            var result = CheckForUpdateInternal(service);
            if (result.HasValue && !result.Value)
            {
                string text = $"{Core.Application.NameAndVersion} is already up-to-date.";
                MessageBox.ShowInformation(_view, text, Core.Application.NameAndVersion);
            }
        }

        private readonly object _checkForUpdateLock = new object();
        private ApplicationUpdateModel _applicationUpdateModel;

        private bool? CheckForUpdateInternal(IApplicationUpdateService service)
        {
            if (!Monitor.TryEnter(_checkForUpdateLock))
            {
                return null;
            }
            try
            {
                string url = Properties.Settings.Default.UpdateUrl;
                var update = service.GetApplicationUpdate(url);

                if (update is null) return false;
                if (!update.VersionIsGreaterThan(Core.Application.VersionNumber)) return false;

                _applicationUpdateModel = new ApplicationUpdateModel(update);
                using (var presenter = new ApplicationUpdatePresenter(_applicationUpdateModel, Logger, _preferences, MessageBox))
                {
                    if (presenter.ShowDialog(_view) == DialogResult.OK)
                    {
                        if (_applicationUpdateModel.SelectedUpdateFileIsReadyToBeExecuted)
                        {
                            string text = String.Format(CultureInfo.CurrentCulture,
                                "{0} will install the new version when you exit the application.", Core.Application.Name);
                            MessageBox.ShowInformation(_view, text, Core.Application.NameAndVersion);
                        }
                    }
                }
                return true;
            }
            finally
            {
                Monitor.Exit(_checkForUpdateLock);
            }
        }

        public void ViewResize()
        {
            if (_view.WindowState != FormWindowState.Minimized)
            {
                OriginalWindowState = _view.WindowState;
                // ReApply Sort when restoring from the sys tray - Issue 32
                if (_view.ShowInTaskbar == false)
                {
                    GridModel.Sort();
                }
            }

            SetViewShowStyle();

            // When the log file window (panel) is collapsed, get the split location
            // changes based on the height of Panel1 - Issue 8
            if (_view.Visible && _view.SplitContainer.Panel2Collapsed)
            {
                _preferences.Set(Preference.FormSplitterLocation, _view.SplitContainer.Panel1.Height);
            }
        }

        public bool ViewClosing()
        {
            if (!CheckForConfigurationChanges())
            {
                return true;
            }

            SaveColumnSettings();

            // Save location and size data
            // RestoreBounds remembers normal position if minimized or maximized
            if (_view.WindowState == FormWindowState.Normal)
            {
                _preferences.Set(Preference.FormLocation, _view.Location);
                _preferences.Set(Preference.FormSize, _view.Size);
            }
            else
            {
                _preferences.Set(Preference.FormLocation, _view.RestoreBounds.Location);
                _preferences.Set(Preference.FormSize, _view.RestoreBounds.Size);
            }

            _preferences.Set(Preference.FormLogWindowVisible, _view.LogFileViewer.Visible);
            _preferences.Set(Preference.QueueWindowVisible, _view.QueueControlVisible);

            CheckForAndFireUpdateProcess(_applicationUpdateModel);

            return false;
        }

        public void SetViewShowStyle()
        {
            switch (_preferences.Get<MinimizeToOption>(Preference.MinimizeTo))
            {
                case MinimizeToOption.SystemTray:
                    _view.SetNotifyIconVisible(true);
                    _view.ShowInTaskbar = (_view.WindowState != FormWindowState.Minimized);
                    break;
                case MinimizeToOption.TaskBar:
                    _view.SetNotifyIconVisible(false);
                    _view.ShowInTaskbar = true;
                    break;
                case MinimizeToOption.Both:
                    _view.SetNotifyIconVisible(true);
                    _view.ShowInTaskbar = true;
                    break;
            }
        }

        private void CheckForAndFireUpdateProcess(ApplicationUpdateModel update)
        {
            if (update != null && update.SelectedUpdateFileIsReadyToBeExecuted)
            {
                string path = update.SelectedUpdateFileLocalFilePath;
                Logger.Info($"Firing update file '{path}'...");
                try
                {
                    Process.Start(path);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex.Message, ex);
                    string message = String.Format(CultureInfo.CurrentCulture,
                        "Update process failed to start with the following error:{0}{0}{1}", Environment.NewLine, ex.Message);
                    MessageBox.ShowError(_view, message, _view.Text);
                }
            }
        }

        #endregion

        #region Data Grid View Handling Methods

        private void DisplaySelectedSlotData()
        {
            if (GridModel.SelectedSlot != null)
            {
                // TODO: Surface client arguments?
                //_view.StatusLabelLeftText = $"{_gridModel.SelectedSlot.SlotIdentifier.ClientIdentifier.ToServerPortString()} {_gridModel.SelectedSlot.Arguments}";
                _view.StatusLabelLeftText = GridModel.SelectedSlot.SlotIdentifier.ClientIdentifier.ToServerPortString();

                _view.SetWorkUnitInfos(GridModel.SelectedSlot.WorkUnitInfos,
                                       GridModel.SelectedSlot.SlotType);

                // if we've got a good queue read, let queueControl_QueueIndexChanged()
                // handle populating the log lines.
                if (GridModel.SelectedSlot.WorkUnitInfos != null) return;

                // otherwise, load up the CurrentLogLines
                SetLogLines(GridModel.SelectedSlot, GridModel.SelectedSlot.CurrentLogLines);
            }
            else
            {
                ClearLogAndQueueViewer();
            }
        }

        public void QueueIndexChanged(int index)
        {
            if (index == -1)
            {
                _view.LogFileViewer.SetNoLogLines();
                return;
            }

            if (GridModel.SelectedSlot != null)
            {
                // Check the UnitLogLines array against the requested Queue Index - Issue 171
                try
                {
                    var logLines = GridModel.SelectedSlot.GetLogLinesForQueueIndex(index);
                    // show the current log even if not the current unit index - 2/17/12
                    if (logLines == null) // && index == _gridModel.SelectedSlot.Queue.CurrentWorkUnitKey)
                    {
                        logLines = GridModel.SelectedSlot.CurrentLogLines;
                    }

                    SetLogLines(GridModel.SelectedSlot, logLines);
                }
                catch (ArgumentOutOfRangeException ex)
                {
                    Logger.Error(ex.Message, ex);
                    _view.LogFileViewer.SetNoLogLines();
                }
            }
            else
            {
                ClearLogAndQueueViewer();
            }
        }

        private void ClearLogAndQueueViewer()
        {
            // clear the log text
            _view.LogFileViewer.SetNoLogLines();
            // clear the queue control
            _view.SetWorkUnitInfos(null, SlotType.Unknown);
        }

        private void SetLogLines(SlotModel instance, IList<LogLine> logLines)
        {
            /*** Checked LogLine Count ***/
            if (logLines != null && logLines.Count > 0)
            {
                // Different Client... Load LogLines
                if (_view.LogFileViewer.LogOwnedByInstanceName.Equals(instance.Name) == false)
                {
                    _view.LogFileViewer.SetLogLines(logLines, instance.Name, _preferences.Get<bool>(Preference.ColorLogFile));
                }
                // Textbox has text lines
                else if (_view.LogFileViewer.Lines.Length > 0)
                {
                    string lastLogLine = String.Empty;

                    try // to get the last LogLine from the instance
                    {
                        lastLogLine = logLines[logLines.Count - 1].ToString();
                    }
                    catch (ArgumentOutOfRangeException ex)
                    {
                        // even though i've checked the count above, it could have changed in between then
                        // and now... and if the count is 0 it will yield this exception.  Log It!!!
                        Logger.Warn(String.Format(Core.Logging.Logger.NameFormat, instance.Name, ex.Message), ex);
                    }

                    // If the last text line in the textbox DOES NOT equal the last LogLine Text... Load LogLines.
                    // Otherwise, the log has not changed, don't update and perform the log "flicker".
                    if (_view.LogFileViewer.Lines[_view.LogFileViewer.Lines.Length - 1].Equals(lastLogLine) == false)
                    {
                        _view.LogFileViewer.SetLogLines(logLines, instance.Name, _preferences.Get<bool>(Preference.ColorLogFile));
                    }
                }
                // Nothing in the Textbox... Load LogLines
                else
                {
                    _view.LogFileViewer.SetLogLines(logLines, instance.Name, _preferences.Get<bool>(Preference.ColorLogFile));
                }
            }
            else
            {
                _view.LogFileViewer.SetNoLogLines();
            }

            if (_preferences.Get<bool>(Preference.FollowLog))
            {
                _view.LogFileViewer.ScrollToBottom();
            }
        }

        private void DataGridViewColumnDisplayIndexChanged()
        {
            if (_view.DataGridView.Columns.Count == MainForm.NumberOfDisplayFields)
            {
                foreach (DataGridViewColumn column in _view.DataGridView.Columns)
                {
                    if (column.DisplayIndex < _view.DataGridView.Columns.Count - 1)
                    {
                        column.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                    }
                    else
                    {
                        column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                    }
                }

                SaveColumnSettings(); // Save Column Settings - Issue 73
                _preferences.Save();
            }
        }

        private void SaveColumnSettings()
        {
            // Save column state data
            // including order, column width and whether or not the column is visible
            var columns = new List<string>();
            int i = 0;

            foreach (DataGridViewColumn column in _view.DataGridView.Columns)
            {
                columns.Add(String.Format(CultureInfo.InvariantCulture,
                                        "{0},{1},{2},{3}",
                                        column.DisplayIndex.ToString("D2"),
                                        column.Width,
                                        column.Visible,
                                        i++));
            }

            _preferences.Set(Preference.FormColumns, columns);
        }

        public void DataGridViewSorted()
        {
            GridModel.ResetSelectedSlot();
        }

        public void DataGridViewMouseDown(int coordX, int coordY, MouseButtons button, int clicks)
        {
            DataGridView.HitTestInfo hti = _view.DataGridView.HitTest(coordX, coordY);
            if (button == MouseButtons.Right)
            {
                if (hti.Type == DataGridViewHitTestType.Cell)
                {
                    if (_view.DataGridView.Rows[hti.RowIndex].Cells[hti.ColumnIndex].Selected == false)
                    {
                        _view.DataGridView.Rows[hti.RowIndex].Cells[hti.ColumnIndex].Selected = true;
                    }

                    // Check for SelectedSlot, and get out if not found
                    if (GridModel.SelectedSlot == null) return;

                    _view.ShowGridContextMenuStrip(_view.DataGridView.PointToScreen(new Point(coordX, coordY)));
                }
            }
            if (button == MouseButtons.Left && clicks == 2)
            {
                if (hti.Type == DataGridViewHitTestType.Cell)
                {
                    // Check for SelectedSlot, and get out if not found
                    if (GridModel.SelectedSlot == null) return;

                    // TODO: What to do on double left click on v7 client?
                }
            }
        }

        #endregion

        #region File Handling Methods

        public void FileNewClick()
        {
            if (CheckForConfigurationChanges())
            {
                ClearConfiguration();
            }
        }

        public void FileOpenClick(FileDialogPresenter openFile)
        {
            if (CheckForConfigurationChanges())
            {
                openFile.DefaultExt = _settingsManager.FileExtension;
                openFile.Filter = _settingsManager.FileTypeFilters;
                openFile.FileName = _settingsManager.FileName;
                openFile.RestoreDirectory = true;
                if (openFile.ShowDialog() == DialogResult.OK)
                {
                    ClearConfiguration();
                    LoadConfigFile(openFile.FileName, openFile.FilterIndex);
                }
            }
        }

        private void ClearConfiguration()
        {
            // clear the clients and UI
            _settingsManager.ClearFileName();
            _clientConfiguration.Clear();
        }

        private void LoadConfigFile(string filePath, int filterIndex = 1)
        {
            Debug.Assert(filePath != null);

            try
            {
                // Read the config file
                _clientConfiguration.Load(_settingsManager.Read(filePath, filterIndex));

                if (_clientConfiguration.Count == 0)
                {
                    MessageBox.ShowError(_view, "No client configurations were loaded from the given config file.", _view.Text);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message, ex);
                MessageBox.ShowError(_view, String.Format(CultureInfo.CurrentCulture,
                   "No client configurations were loaded from the given config file.{0}{0}{1}", Environment.NewLine, ex.Message), _view.Text);
            }
        }

        private void AutoSaveConfig()
        {
            if (_preferences.Get<bool>(Preference.AutoSaveConfig) &&
                _clientConfiguration.IsDirty)
            {
                FileSaveClick();
            }
        }

        public void FileSaveClick()
        {
            if (_clientConfiguration.Count == 0)
            {
                return;
            }

            if (String.IsNullOrEmpty(_settingsManager.FileName))
            {
                // TODO: Fix dependency on DefaultFileDialogPresenter
                using (var saveFile = DefaultFileDialogPresenter.SaveFile())
                {
                    FileSaveAsClick(saveFile);
                }
            }
            else
            {
                WriteClientConfiguration(_settingsManager.FileName, _settingsManager.FilterIndex);
            }
        }

        public void FileSaveAsClick(FileDialogPresenter saveFile)
        {
            if (_clientConfiguration.Count == 0)
            {
                return;
            }

            saveFile.DefaultExt = _settingsManager.FileExtension;
            saveFile.Filter = _settingsManager.FileTypeFilters;
            if (saveFile.ShowDialog() == DialogResult.OK)
            {
                WriteClientConfiguration(saveFile.FileName, saveFile.FilterIndex);
            }
        }

        private void WriteClientConfiguration(string fileName, int filterIndex)
        {
            try
            {
                var clients = _clientConfiguration.GetClients();
                _settingsManager.Write(clients.Select(x => x.Settings), fileName, filterIndex);
                _clientConfiguration.IsDirty = false;

                ApplyClientIdentifierToBenchmarks(clients);
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message, ex);
                MessageBox.ShowError(_view, String.Format(CultureInfo.CurrentCulture,
                    "The client configuration has not been saved.{0}{0}{1}", Environment.NewLine, ex.Message), _view.Text);
            }
        }

        private static void ApplyClientIdentifierToBenchmarks(ICollection<IClient> clients)
        {
            var benchmarkService = clients.FirstOrDefault()?.BenchmarkService;
            if (benchmarkService is null)
            {
                return;
            }

            foreach (var benchmarkClientIdentifier in benchmarkService.GetClientIdentifiers())
            {
                var clientIdentifier = clients.Select(x => (ClientIdentifier?)x.Settings.ClientIdentifier)
                    .FirstOrDefault(x => x.Value.Equals(benchmarkClientIdentifier) ||
                                         ClientIdentifier.ProteinBenchmarkEqualityComparer.Equals(x.Value, benchmarkClientIdentifier));

                if (clientIdentifier.HasValue)
                {
                    benchmarkService.UpdateClientIdentifier(clientIdentifier.Value);
                }
            }
        }

        private bool CheckForConfigurationChanges()
        {
            if (_clientConfiguration.Count != 0 && _clientConfiguration.IsDirty)
            {
                DialogResult result = MessageBox.AskYesNoCancelQuestion(_view,
                   String.Format("There are changes to the configuration that have not been saved.  Would you like to save these changes?{0}{0}Yes - Continue and save the changes{0}No - Continue and do not save the changes{0}Cancel - Do not continue", Environment.NewLine),
                   _view.Text);

                switch (result)
                {
                    case DialogResult.Yes:
                        FileSaveClick();
                        return !_clientConfiguration.IsDirty;
                    case DialogResult.No:
                        return true;
                    case DialogResult.Cancel:
                        return false;
                }
                return false;
            }

            return true;
        }

        #endregion

        #region Edit Menu Handling Methods

        public void EditPreferencesClick()
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                using (var presenter = scope.ServiceProvider.GetRequiredService<PreferencesPresenter>())
                {
                    presenter.ShowDialog(_view);
                    // TODO: Invalidate View by mutating the Model
                    _view.DataGridView.Invalidate();
                }
            }
        }

        #endregion

        #region Help Menu Handling Methods

        public void ShowHfmLogFile(LocalProcessService localProcess)
        {
            string path = Path.Combine(_preferences.Get<string>(Preference.ApplicationDataFolderPath), Core.Logging.Logger.LogFileName);
            string errorMessage = String.Format(CultureInfo.CurrentCulture,
                "An error occurred while attempting to open the HFM log file.{0}{0}Please check the log file viewer defined in the preferences.",
                Environment.NewLine);

            string fileName = _preferences.Get<string>(Preference.LogFileViewer);
            string arguments = WrapString.InQuotes(path);
            localProcess.StartAndNotifyError(fileName, arguments, errorMessage, Logger, MessageBox);
        }

        public void ShowHfmDataFiles(LocalProcessService localProcess)
        {
            string path = _preferences.Get<string>(Preference.ApplicationDataFolderPath);
            string errorMessage = String.Format(CultureInfo.CurrentCulture,
                "An error occurred while attempting to open '{0}'.{1}{1}Please check the current file explorer defined in the preferences.",
                path, Environment.NewLine);

            string fileName = _preferences.Get<string>(Preference.FileExplorer);
            string arguments = WrapString.InQuotes(path);
            localProcess.StartAndNotifyError(fileName, arguments, errorMessage, Logger, MessageBox);
        }

        public void ShowHfmGoogleGroup(LocalProcessService localProcess)
        {
            const string errorMessage = "An error occurred while attempting to open the HFM.NET Google Group.";
            localProcess.StartAndNotifyError(Core.Application.SupportForumUrl, errorMessage, Logger, MessageBox);
        }

        public void CheckForUpdateClick(IApplicationUpdateService service)
        {
            CheckForUpdate(service);
        }

        #endregion

        #region Clients Menu Handling Methods

        internal void ClientsAddClick()
        {
            using (var dialog = new FahClientSettingsPresenter(new FahClientSettingsModel(), Logger, MessageBox))
            {
                while (dialog.ShowDialog(_view) == DialogResult.OK)
                {
                    dialog.Model.Save();
                    var settings = dialog.Model.ClientSettings;
                    //if (_clientDictionary.ContainsKey(settings.Name))
                    //{
                    //   string message = String.Format(CultureInfo.CurrentCulture, "Client name '{0}' already exists.", settings.Name);
                    //   _messageBoxView.ShowError(_view, Core.Application.NameAndVersion, message);
                    //   continue;
                    //}
                    // perform the add
                    try
                    {
                        _clientConfiguration.Add(settings);
                        break;
                    }
                    catch (ArgumentException ex)
                    {
                        Logger.Error(ex.Message, ex);
                        MessageBox.ShowError(_view, ex.Message, Core.Application.NameAndVersion);
                    }
                }
            }
        }

        public void ClientsEditClick()
        {
            // Check for SelectedSlot, and get out if not found
            if (GridModel.SelectedSlot == null) return;

            EditFahClient();
        }

        private void EditFahClient()
        {
            Debug.Assert(GridModel.SelectedSlot != null);
            IClient client = _clientConfiguration.Get(GridModel.SelectedSlot.Settings.Name);
            ClientSettings originalSettings = client.Settings;
            Debug.Assert(originalSettings.ClientType == ClientType.FahClient);

            var model = new FahClientSettingsModel(originalSettings);
            model.Load();
            using (var dialog = new FahClientSettingsPresenter(model, Logger, MessageBox))
            {
                while (dialog.ShowDialog(_view) == DialogResult.OK)
                {
                    dialog.Model.Save();
                    var newSettings = dialog.Model.ClientSettings;
                    // perform the edit
                    try
                    {
                        _clientConfiguration.Edit(originalSettings.Name, newSettings);
                        break;
                    }
                    catch (ArgumentException ex)
                    {
                        Logger.Error(ex.Message, ex);
                        MessageBox.ShowError(_view, ex.Message, Core.Application.NameAndVersion);
                    }
                }
            }
        }

        public void ClientsDeleteClick()
        {
            // Check for SelectedSlot, and get out if not found
            if (GridModel.SelectedSlot == null) return;

            _clientConfiguration.Remove(GridModel.SelectedSlot.Settings.Name);
        }

        public void ClientsRefreshSelectedClick()
        {
            // Check for SelectedSlot, and get out if not found
            if (GridModel.SelectedSlot == null) return;

            var client = _clientConfiguration.Get(GridModel.SelectedSlot.Settings.Name);
            Task.Run(client.Retrieve);
        }

        public void ClientsRefreshAllClick()
        {
            _clientConfiguration.ScheduledTasks.RetrieveAll();
        }

        public void ClientsViewCachedLogClick(LocalProcessService localProcess)
        {
            // Check for SelectedSlot, and get out if not found
            if (GridModel.SelectedSlot == null) return;

            string path = Path.Combine(_preferences.Get<string>(Preference.CacheDirectory), GridModel.SelectedSlot.Settings.ClientLogFileName);
            if (File.Exists(path))
            {
                string errorMessage = String.Format(CultureInfo.CurrentCulture,
                    "An error occurred while attempting to open the client log file.{0}{0}Please check the current log file viewer defined in the preferences.",
                    Environment.NewLine);

                string fileName = _preferences.Get<string>(Preference.LogFileViewer);
                string arguments = WrapString.InQuotes(path);
                localProcess.StartAndNotifyError(fileName, arguments, errorMessage, Logger, MessageBox);
            }
            else
            {
                string message = String.Format(CultureInfo.CurrentCulture, "The log file for '{0}' does not exist.", GridModel.SelectedSlot.Settings.Name);
                MessageBox.ShowInformation(_view, message, _view.Text);
            }
        }

        #endregion

        #region Grid Context Menu Handling Methods

        internal void ClientsFoldSlotClick()
        {
            if (GridModel.SelectedSlot == null) return;

            if (_clientConfiguration.Get(GridModel.SelectedSlot.Settings.Name) is IFahClient client)
            {
                client.Fold(GridModel.SelectedSlot.SlotID);
            }
        }

        internal void ClientsPauseSlotClick()
        {
            if (GridModel.SelectedSlot == null) return;

            if (_clientConfiguration.Get(GridModel.SelectedSlot.Settings.Name) is IFahClient client)
            {
                client.Pause(GridModel.SelectedSlot.SlotID);
            }
        }

        internal void ClientsFinishSlotClick()
        {
            if (GridModel.SelectedSlot == null) return;

            if (_clientConfiguration.Get(GridModel.SelectedSlot.Settings.Name) is IFahClient client)
            {
                client.Finish(GridModel.SelectedSlot.SlotID);
            }
        }

        public void CopyPRCGToClipboardClicked()
        {
            if (GridModel.SelectedSlot == null) return;

            string projectString = GridModel.SelectedSlot.WorkUnitModel.WorkUnit.ToProjectString();

            // TODO: Replace ClipboardWrapper.SetText() with abstraction
            ClipboardWrapper.SetText(projectString);
        }

        #endregion

        #region View Menu Handling Methods

        private MessagesPresenter _messagesPresenter;

        public void ViewMessagesClick()
        {
            try
            {
                if (_messagesPresenter is null)
                {
                    var scope = ServiceScopeFactory.CreateScope();
                    _messagesPresenter = scope.ServiceProvider.GetRequiredService<MessagesPresenter>();
                    _messagesPresenter.Closed += (sender, args) =>
                    {
                        scope.Dispose();
                        _messagesPresenter = null;
                    };
                }

                _messagesPresenter?.Show();
            }
            catch (Exception)
            {
                _messagesPresenter?.Dispose();
                _messagesPresenter = null;
                throw;
            }
        }

        public void ShowHideLogWindow()
        {
            ShowHideLogWindow(!_view.LogFileViewer.Visible);
        }

        private void ShowHideLogWindow(bool show)
        {
            if (!show)
            {
                _view.LogFileViewer.Visible = false;
                _view.SplitContainer.Panel2Collapsed = true;
                _preferences.Set(Preference.FormLogWindowHeight, (_view.SplitContainer.Height - _view.SplitContainer.SplitterDistance));
                _view.Size = new Size(_view.Size.Width, _view.Size.Height - _preferences.Get<int>(Preference.FormLogWindowHeight));
            }
            else
            {
                _view.LogFileViewer.Visible = true;
                _view.DisableViewResizeEvent();  // disable Form resize event for this operation
                _view.Size = new Size(_view.Size.Width, _view.Size.Height + _preferences.Get<int>(Preference.FormLogWindowHeight));
                _view.EnableViewResizeEvent();   // re-enable
                _view.SplitContainer.Panel2Collapsed = false;
            }
        }

        public void ShowHideQueue()
        {
            ShowHideQueue(!_view.QueueControlVisible);
        }

        private void ShowHideQueue(bool show)
        {
            if (!show)
            {
                _view.QueueControlVisible = false;
                _view.SetQueueButtonText(String.Format(CultureInfo.CurrentCulture, "S{0}h{0}o{0}w{0}{0}Q{0}u{0}e{0}u{0}e", Environment.NewLine));
                _view.SplitContainer2.SplitterDistance = 27;
            }
            else
            {
                _view.QueueControlVisible = true;
                _view.SetQueueButtonText(String.Format(CultureInfo.CurrentCulture, "H{0}i{0}d{0}e{0}{0}Q{0}u{0}e{0}u{0}e", Environment.NewLine));
                _view.SplitContainer2.SplitterDistance = 289;
            }
        }

        public void ViewToggleDateTimeClick()
        {
            var style = _preferences.Get<TimeFormatting>(Preference.TimeFormatting);
            _preferences.Set(Preference.TimeFormatting, style == TimeFormatting.None
                                    ? TimeFormatting.Format1
                                    : TimeFormatting.None);
            _preferences.Save();
            _view.DataGridView.Invalidate();
        }

        public void ViewToggleCompletedCountStyleClick()
        {
            var style = _preferences.Get<UnitTotalsType>(Preference.UnitTotals);
            _preferences.Set(Preference.UnitTotals, style == UnitTotalsType.All
                                    ? UnitTotalsType.ClientStart
                                    : UnitTotalsType.All);
            _preferences.Save();
            _view.DataGridView.Invalidate();
        }

        public void ViewToggleVersionInformationClick()
        {
            _preferences.Set(Preference.DisplayVersions, !_preferences.Get<bool>(Preference.DisplayVersions));
            _preferences.Save();
            _view.DataGridView.Invalidate();
        }

        public void ViewCycleBonusCalculationClick()
        {
            var calculationType = _preferences.Get<BonusCalculation>(Preference.BonusCalculation);
            int typeIndex = 0;
            // None is always LAST entry
            if (calculationType != BonusCalculation.None)
            {
                typeIndex = (int)calculationType;
                typeIndex++;
            }

            calculationType = (BonusCalculation)typeIndex;
            _preferences.Set(Preference.BonusCalculation, calculationType);
            _preferences.Save();

            string calculationTypeString = (from item in ClientsModel.BonusCalculationList
                                            where ((BonusCalculation)item.ValueMember) == calculationType
                                            select item.DisplayMember).First();
            _view.ShowNotifyToolTip(calculationTypeString);
            _view.DataGridView.Invalidate();
        }

        public void ViewCycleCalculationClick()
        {
            var calculationType = _preferences.Get<PPDCalculation>(Preference.PPDCalculation);
            int typeIndex = 0;
            // EffectiveRate is always LAST entry
            if (calculationType != PPDCalculation.EffectiveRate)
            {
                typeIndex = (int)calculationType;
                typeIndex++;
            }

            calculationType = (PPDCalculation)typeIndex;
            _preferences.Set(Preference.PPDCalculation, calculationType);
            _preferences.Save();

            string calculationTypeString = (from item in ClientsModel.PpdCalculationList
                                            where ((PPDCalculation)item.ValueMember) == calculationType
                                            select item.DisplayMember).First();
            _view.ShowNotifyToolTip(calculationTypeString);
            _view.DataGridView.Invalidate();
        }

        internal void ViewToggleFollowLogFile()
        {
            _preferences.Set(Preference.FollowLog, !_preferences.Get<bool>(Preference.FollowLog));
        }

        #endregion

        #region Tools Menu Handling Methods

        public void ToolsDownloadProjectsClick(IProteinService proteinService)
        {
            try
            {
                IEnumerable<ProteinChange> result = null;
                using (var dialog = new ProgressDialog((progress, token) => result = proteinService.Refresh(progress), false))
                {
                    dialog.Text = Core.Application.NameAndVersion;
                    dialog.ShowDialog(_view);
                    if (dialog.Exception != null)
                    {
                        Logger.Error(dialog.Exception.Message, dialog.Exception);
                        MessageBox.ShowError(dialog.Exception.Message, Core.Application.NameAndVersion);
                    }
                }

                if (result != null)
                {
                    var proteinChanges = result.Where(x => x.Action != ProteinChangeAction.None).ToList();
                    if (proteinChanges.Count > 0)
                    {
                        if (_clientConfiguration.Count > 0)
                        {
                            _clientConfiguration.ScheduledTasks.RetrieveAll();
                        }
                        using (var dialog = new ProteinChangesDialog(proteinChanges))
                        {
                            dialog.ShowDialog(_view);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.ShowError(ex.Message, Core.Application.NameAndVersion);
            }
        }

        public void ToolsBenchmarksClick()
        {
            int projectID = 0;

            // Check for SelectedSlot, and if found... load its ProjectID.
            if (GridModel.SelectedSlot != null)
            {
                projectID = GridModel.SelectedSlot.WorkUnitModel.WorkUnit.ProjectID;
            }

            var scope = ServiceScopeFactory.CreateScope();
            var presenter = scope.ServiceProvider.GetRequiredService<BenchmarksPresenter>();
            presenter.Closed += (s, e) => scope.Dispose();
            presenter.Model.DefaultProjectID = projectID;
            presenter.Show();
        }

        private IFormPresenter _historyPresenter;

        public void ToolsHistoryClick()
        {
            try
            {
                if (_historyPresenter is null)
                {
                    var scope = ServiceScopeFactory.CreateScope();
                    _historyPresenter = scope.ServiceProvider.GetRequiredService<WorkUnitHistoryPresenter>();
                    _historyPresenter.Closed += (sender, args) =>
                    {
                        scope.Dispose();
                        _historyPresenter = null;
                    };
                }

                _historyPresenter?.Show();
            }
            catch (Exception)
            {
                _historyPresenter?.Dispose();
                _historyPresenter = null;
                throw;
            }
        }

        internal void ToolsPointsCalculatorClick()
        {
            var scope = ServiceScopeFactory.CreateScope();
            IWin32Form calculatorForm = scope.ServiceProvider.GetRequiredService<ProteinCalculatorForm>();
            calculatorForm.Closed += (s, e) => scope.Dispose();
            calculatorForm.Show();
        }

        #endregion

        #region Web Menu Handling Methods

        public void ShowEocUserPage(LocalProcessService localProcess)
        {
            string fileName = new Uri(String.Concat(EocStatsService.UserBaseUrl, _preferences.Get<int>(Preference.EocUserId))).AbsoluteUri;
            const string errorMessage = "An error occurred while attempting to open the EOC user stats page.";
            localProcess.StartAndNotifyError(fileName, errorMessage, Logger, MessageBox);
        }

        public void ShowStanfordUserPage(LocalProcessService localProcess)
        {
            string fileName = new Uri(String.Concat(FahUrl.UserBaseUrl, _preferences.Get<string>(Preference.StanfordId))).AbsoluteUri;
            const string errorMessage = "An error occurred while attempting to open the FAH user stats page.";
            localProcess.StartAndNotifyError(fileName, errorMessage, Logger, MessageBox);
        }

        public void ShowEocTeamPage(LocalProcessService localProcess)
        {
            string fileName = new Uri(String.Concat(EocStatsService.TeamBaseUrl, _preferences.Get<int>(Preference.TeamId))).AbsoluteUri;
            const string errorMessage = "An error occurred while attempting to open the EOC team stats page.";
            localProcess.StartAndNotifyError(fileName, errorMessage, Logger, MessageBox);
        }

        public void RefreshUserStatsData()
        {
            _userStatsDataModel.Refresh();
        }

        public void ShowHfmGitHub(LocalProcessService localProcess)
        {
            const string errorMessage = "An error occurred while attempting to open the HFM.NET GitHub project.";
            localProcess.StartAndNotifyError(Core.Application.ProjectSiteUrl, errorMessage, Logger, MessageBox);
        }

        #endregion

        #region System Tray Icon Handling Methods

        public void NotifyIconDoubleClick()
        {
            if (_view.WindowState == FormWindowState.Minimized)
            {
                _view.WindowState = OriginalWindowState;
            }
            else
            {
                OriginalWindowState = _view.WindowState;
                _view.WindowState = FormWindowState.Minimized;
            }
        }

        public void NotifyIconRestoreClick()
        {
            if (_view.WindowState == FormWindowState.Minimized)
            {
                _view.WindowState = OriginalWindowState;
            }
            else if (_view.WindowState == FormWindowState.Maximized)
            {
                _view.WindowState = FormWindowState.Normal;
            }
        }

        public void NotifyIconMinimizeClick()
        {
            if (_view.WindowState != FormWindowState.Minimized)
            {
                OriginalWindowState = _view.WindowState;
                _view.WindowState = FormWindowState.Minimized;
            }
        }

        public void NotifyIconMaximizeClick()
        {
            if (_view.WindowState != FormWindowState.Maximized)
            {
                _view.WindowState = FormWindowState.Maximized;
                OriginalWindowState = _view.WindowState;
            }
        }

        #endregion

        public void AboutClicked()
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                using (var dialog = scope.ServiceProvider.GetRequiredService<AboutDialog>())
                {
                    dialog.ShowDialog(_view);
                }
            }
        }

        #region Other Handling Methods

        private void ApplyColorLogFileSetting()
        {
            _view.LogFileViewer.HighlightLines(_preferences.Get<bool>(Preference.ColorLogFile));
        }

        private void HandleProcessStartResult(string message)
        {
            if (message != null)
            {
                MessageBox.ShowError(_view, message, _view.Text);
            }
        }

        public void SetUserStatsDataViewStyle(bool showTeamStats)
        {
            _userStatsDataModel.SetViewStyle(showTeamStats);
        }

        #endregion
    }
}
