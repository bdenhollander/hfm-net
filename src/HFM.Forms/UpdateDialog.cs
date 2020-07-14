﻿
using System;
using System.Globalization;
using System.Windows.Forms;

namespace HFM.Forms
{
    public interface IUpdateView
    {
        void AttachPresenter(UpdatePresenter presenter);

        void SetSelectDownloadLabelTextDefault();

        void SetSelectDownloadLabelText(string value);

        void SetUpdateComboBoxVisible(bool visible);

        void SetDownloadButtonEnabled(bool enabled);

        void SetDownloadProgressVisisble(bool visible);

        void SetDownloadProgressValue(int value);

        void ShowErrorMessage(string message);

        void ShowView();

        void ShowView(IWin32Window owner);

        void CloseView();
    }

    public partial class UpdateDialog : Form, IUpdateView
    {
        private IWin32Window _owner;
        private UpdatePresenter _presenter;

        #region Properties

        protected ApplicationUpdate UpdateData { get; private set; }

        protected string ApplicationName { get; private set; }

        protected string ApplicationVersion { get; private set; }

        #endregion

        public UpdateDialog(ApplicationUpdate update, string applicationName, string applicationVersion)
        {
            InitializeComponent();

            UpdateData = update;
            ApplicationName = applicationName;
            ApplicationVersion = applicationVersion;
        }

        private void UpdateDialog_Load(object sender, EventArgs e)
        {
            // some how leaving the ControlBox enabled in
            // the Designer allows the Form to scale when
            // run under a higher DPI setting, just turn
            // it off once the Form is loaded
            ControlBox = false;
            LayoutUpdateData();
        }

        protected virtual void LayoutUpdateData()
        {
            lblFirstLine.Text = String.Format(CultureInfo.CurrentCulture, lblFirstLine.Text, ApplicationName);

            lblYourVersion.Text = lblYourVersion.Text + ApplicationVersion;
            lblCurrentVersion.Text = lblCurrentVersion.Text + UpdateData.Version;

            cboUpdateFiles.DisplayMember = "Description";
            cboUpdateFiles.ValueMember = "Description";
            cboUpdateFiles.DataSource = UpdateData.UpdateFiles;
        }

        private void btnDownload_Click(object sender, EventArgs e)
        {
            using (var saveFile = DefaultFileDialogPresenter.SaveFile())
            {
                _presenter.DownloadClick(saveFile, cboUpdateFiles.SelectedIndex);
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            _presenter.CancelClick();
        }

        #region IUpdateView Methods

        public void AttachPresenter(UpdatePresenter presenter)
        {
            _presenter = presenter;
        }

        public void ShowView()
        {
            ShowDialog();
        }

        public void ShowView(IWin32Window owner)
        {
            _owner = owner;
            ShowDialog(owner);
        }

        public void CloseView()
        {
            if (InvokeRequired)
            {
                Invoke(new MethodInvoker(CloseView));
                return;
            }

            Close();
        }

        /// <summary>
        /// Modify Download Label Text (Thread Safe)
        /// </summary>
        public void SetSelectDownloadLabelTextDefault()
        {
            SetSelectDownloadLabelText("Please select an update to download.");
        }

        /// <summary>
        /// Modify Download Label Text (Thread Safe)
        /// </summary>
        public void SetSelectDownloadLabelText(string value)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<string>(SetSelectDownloadLabelText), value);
                return;
            }

            lblSelectDownload.Text = value;
        }

        /// <summary>
        /// Set Update ComboBox Visible (Thread Safe)
        /// </summary>
        public void SetUpdateComboBoxVisible(bool visible)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<bool>(SetUpdateComboBoxVisible), visible);
                return;
            }

            cboUpdateFiles.Visible = visible;
        }

        /// <summary>
        /// Set Download Button Enabled (Thread Safe)
        /// </summary>
        public void SetDownloadButtonEnabled(bool enabled)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<bool>(SetDownloadButtonEnabled), enabled);
                return;
            }

            btnDownload.Enabled = enabled;
        }

        /// <summary>
        /// Set Download Progress Bar Visible (Thread Safe)
        /// </summary>
        public void SetDownloadProgressVisisble(bool visible)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<bool>(SetDownloadProgressVisisble), visible);
                return;
            }

            progressDownload.Visible = visible;
        }

        /// <summary>
        /// Set Download Progress Bar Value (Thread Safe)
        /// </summary>
        public void SetDownloadProgressValue(int value)
        {
            if (value < 0) return;

            if (InvokeRequired)
            {
                Invoke(new Action<int>(SetDownloadProgressValue), value);
                return;
            }

            progressDownload.Value = value;
        }

        public void ShowErrorMessage(string message)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<string>(ShowErrorMessage), message);
                return;
            }

            if (_owner != null)
            {
                MessageBox.Show(_owner, message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            else
            {
                MessageBox.Show(message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #endregion
    }
}
