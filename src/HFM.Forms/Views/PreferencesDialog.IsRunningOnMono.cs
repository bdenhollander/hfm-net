﻿
using System.ComponentModel;

using HFM.Forms.Models;

namespace HFM.Forms
{
    public partial class PreferencesDialog
    {
        private void ScheduledTasksPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (Core.Application.IsRunningOnMono && Enabled)
            {
                HandleScheduledTasksPropertyEnabledForMono(e.PropertyName);
                HandleScheduledTasksPropertyChangedForMono(e.PropertyName);
            }
        }

        private void HandleScheduledTasksPropertyEnabledForMono(string propertyName)
        {
            switch (propertyName)
            {
                case "GenerateWeb":
                    radioSchedule.Enabled = _presenter.Model.ScheduledTasksModel.GenerateWeb;
                    lbl2MinutesToGen.Enabled = _presenter.Model.ScheduledTasksModel.GenerateWeb;
                    radioFullRefresh.Enabled = _presenter.Model.ScheduledTasksModel.GenerateWeb;
                    WebSiteTargetPathTextBox.Enabled = _presenter.Model.ScheduledTasksModel.GenerateWeb;
                    chkHtml.Enabled = _presenter.Model.ScheduledTasksModel.GenerateWeb;
                    chkXml.Enabled = _presenter.Model.ScheduledTasksModel.GenerateWeb;
                    chkFAHlog.Enabled = _presenter.Model.ScheduledTasksModel.GenerateWeb;
                    TestConnectionButton.Enabled = _presenter.Model.ScheduledTasksModel.GenerateWeb;
                    WebGenTypePanel.Enabled = _presenter.Model.ScheduledTasksModel.GenerateWeb;
                    break;
                case "GenerateIntervalEnabled":
                    txtWebGenMinutes.Enabled = _presenter.Model.ScheduledTasksModel.GenerateIntervalEnabled;
                    break;
                case "FtpModeEnabled":
                    WebSiteServerTextBox.Enabled = _presenter.Model.ScheduledTasksModel.FtpModeEnabled;
                    WebSiteServerLabel.Enabled = _presenter.Model.ScheduledTasksModel.FtpModeEnabled;
                    WebSitePortTextBox.Enabled = _presenter.Model.ScheduledTasksModel.FtpModeEnabled;
                    WebSitePortLabel.Enabled = _presenter.Model.ScheduledTasksModel.FtpModeEnabled;
                    WebSiteUsernameTextBox.Enabled = _presenter.Model.ScheduledTasksModel.FtpModeEnabled;
                    WebSiteUsernameLabel.Enabled = _presenter.Model.ScheduledTasksModel.FtpModeEnabled;
                    WebSitePasswordTextBox.Enabled = _presenter.Model.ScheduledTasksModel.FtpModeEnabled;
                    WebSitePasswordLabel.Enabled = _presenter.Model.ScheduledTasksModel.FtpModeEnabled;
                    FtpModePanel.Enabled = _presenter.Model.ScheduledTasksModel.FtpModeEnabled;
                    break;
                case "BrowseLocalPathEnabled":
                    BrowseWebFolderButton.Enabled = _presenter.Model.ScheduledTasksModel.BrowseLocalPathEnabled;
                    break;
                case "LimitLogSizeEnabled":
                    chkLimitSize.Enabled = _presenter.Model.ScheduledTasksModel.LimitLogSizeEnabled;
                    break;
                case "LimitLogSizeLengthEnabled":
                    udLimitSize.Enabled = _presenter.Model.ScheduledTasksModel.LimitLogSizeLengthEnabled;
                    break;
            }
        }

        private void HandleScheduledTasksPropertyChangedForMono(string propertyName)
        {
            switch (propertyName)
            {
                case "WebRoot":
                    WebSiteTargetPathTextBox.Text = _presenter.Model.ScheduledTasksModel.WebRoot;
                    break;
            }
        }

        private void OptionsPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (Core.Application.IsRunningOnMono && Enabled)
            {
                HandleOptionsPropertyEnabledForMono(e.PropertyName);
                HandleOptionsPropertyChangedForMono(e.PropertyName);
            }
        }

        private void HandleOptionsPropertyEnabledForMono(string propertyName)
        {

        }

        private void HandleOptionsPropertyChangedForMono(string propertyName)
        {
            switch (propertyName)
            {
                case "LogFileViewer":
                    LogFileViewerTextBox.Text = _presenter.Model.OptionsModel.LogFileViewer;
                    break;
                case "FileExplorer":
                    FileExplorerTextBox.Text = _presenter.Model.OptionsModel.FileExplorer;
                    break;
            }
        }

        private void ClientsPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (Core.Application.IsRunningOnMono && Enabled)
            {
                HandleClientsPropertyEnabledForMono(e.PropertyName);
                HandleClientsPropertyChangedForMono(e.PropertyName);
            }
        }

        private void HandleClientsPropertyEnabledForMono(string propertyName)
        {
            switch (propertyName)
            {
                case "UseDefaultConfigFile":
                    txtDefaultConfigFile.Enabled = _presenter.Model.ClientsModel.UseDefaultConfigFile;
                    btnBrowseConfigFile.Enabled = _presenter.Model.ClientsModel.UseDefaultConfigFile;
                    break;
                case "SyncOnSchedule":
                    ClientRefreshIntervalTextBox.Enabled = _presenter.Model.ClientsModel.SyncOnSchedule;
                    break;
            }
        }

        private void HandleClientsPropertyChangedForMono(string propertyName)
        {
            switch (propertyName)
            {
                case "DefaultConfigFile":
                    txtDefaultConfigFile.Text = _presenter.Model.ClientsModel.DefaultConfigFile;
                    break;
            }
        }

        private void ReportingPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (Core.Application.IsRunningOnMono && Enabled)
            {
                HandleReportingPropertyEnabledForMono(e.PropertyName);
            }
        }

        private void HandleReportingPropertyEnabledForMono(string propertyName)
        {
            switch (propertyName)
            {
                case "ReportingEnabled":
                    chkEmailSecure.Enabled = _presenter.Model.ReportingModel.ReportingEnabled;
                    btnTestEmail.Enabled = _presenter.Model.ReportingModel.ReportingEnabled;
                    txtToEmailAddress.Enabled = _presenter.Model.ReportingModel.ReportingEnabled;
                    txtFromEmailAddress.Enabled = _presenter.Model.ReportingModel.ReportingEnabled;
                    txtSmtpServer.Enabled = _presenter.Model.ReportingModel.ReportingEnabled;
                    txtSmtpServerPort.Enabled = _presenter.Model.ReportingModel.ReportingEnabled;
                    txtSmtpUsername.Enabled = _presenter.Model.ReportingModel.ReportingEnabled;
                    txtSmtpPassword.Enabled = _presenter.Model.ReportingModel.ReportingEnabled;
                    grpReportSelections.Enabled = _presenter.Model.ReportingModel.ReportingEnabled;
                    break;
            }
        }

        private void WebSettingsChanged(object sender, PropertyChangedEventArgs e)
        {
            if (Core.Application.IsRunningOnMono && Enabled)
            {
                HandleWebSettingsPropertyEnabledForMono(e.PropertyName);
            }
        }

        private void HandleWebSettingsPropertyEnabledForMono(string propertyName)
        {
            switch (propertyName)
            {
                case "UseProxy":
                    txtProxyServer.Enabled = _presenter.Model.WebSettingsModel.UseProxy;
                    txtProxyPort.Enabled = _presenter.Model.WebSettingsModel.UseProxy;
                    chkUseProxyAuth.Enabled = _presenter.Model.WebSettingsModel.UseProxy;
                    break;
                case "ProxyAuthEnabled":
                    txtProxyUser.Enabled = _presenter.Model.WebSettingsModel.ProxyAuthEnabled;
                    txtProxyPass.Enabled = _presenter.Model.WebSettingsModel.ProxyAuthEnabled;
                    break;
            }
        }

        private void WebVisualStylesPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (Core.Application.IsRunningOnMono && Enabled)
            {
                HandleWebVisualStylesPropertyChangedForMono(e.PropertyName);
            }
        }

        private void HandleWebVisualStylesPropertyChangedForMono(string propertyName)
        {
            switch (propertyName)
            {
                case nameof(WebVisualStylesModel.WebOverview):
                    txtOverview.Text = _presenter.Model.WebVisualStylesModel.WebOverview;
                    break;
                case nameof(WebVisualStylesModel.WebSummary):
                    txtSummary.Text = _presenter.Model.WebVisualStylesModel.WebSummary;
                    break;
                case nameof(WebVisualStylesModel.WebSlot):
                    txtInstance.Text = _presenter.Model.WebVisualStylesModel.WebSlot;
                    break;
            }
        }
    }
}
