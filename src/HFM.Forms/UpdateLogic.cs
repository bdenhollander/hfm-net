﻿
using System;
using System.Globalization;
using System.Net;

using HFM.Core;
using HFM.Core.Logging;
using HFM.Core.Net;
using HFM.Preferences;

namespace HFM.Forms
{
    public interface IUpdateLogic
    {
        IMainView Owner { get; set; }

        bool CheckInProgress { get; }

        string UpdateFilePath { get; }

        void CheckForUpdate();

        void CheckForUpdate(bool userInvoked);
    }

    /* Cannot write an effective unit test for this class
     * until I remove the concrete dependencies on NetworkOps
     * and UpdateChecker.  I'd also like to enable a synchronous
     * or asynchronous option since it's more difficult to unit
     * test an asynchronous operation *OR* do like the following:
     * http://stackoverflow.com/questions/1174702/is-there-a-way-to-unit-test-an-async-method
     */

    public sealed class UpdateLogic : IUpdateLogic
    {
        #region Properties

        public IMainView Owner { get; set; }

        public bool CheckInProgress { get; private set; }

        public string UpdateFilePath { get; private set; }

        private ILogger _logger;

        public ILogger Logger
        {
            get { return _logger ?? (_logger = NullLogger.Instance); }
            set { _logger = value; }
        }

        #endregion

        #region Fields

        private bool _userInvoked;
        private IWebProxy _proxy;

        private readonly IPreferenceSet _preferences;
        private readonly MessageBoxPresenter _messageBox;

        #endregion

        public UpdateLogic(IPreferenceSet preferences, MessageBoxPresenter messageBox)
        {
            _preferences = preferences;
            _messageBox = messageBox;
        }

        public void CheckForUpdate()
        {
            CheckForUpdate(false);
        }

        public void CheckForUpdate(bool userInvoked)
        {
            if (Owner == null)
            {
                throw new InvalidOperationException("Owner property cannot be null.");
            }

            if (CheckInProgress)
            {
                throw new InvalidOperationException("Update check already in progress.");
            }

            CheckInProgress = true;

            // set globals
            _userInvoked = userInvoked;
            _proxy = WebProxyFactory.Create(_preferences);

            Func<ApplicationUpdate> func = DoCheckForUpdate;
            func.BeginInvoke(CheckForUpdateCallback, func);
        }

        private ApplicationUpdate DoCheckForUpdate()
        {
            var updateChecker = new UpdateChecker();
            return updateChecker.CheckForUpdate(Application.NameAndVersion, _proxy);
        }

        private void CheckForUpdateCallback(IAsyncResult result)
        {
            try
            {
                var func = (Func<ApplicationUpdate>)result.AsyncState;
                ApplicationUpdate update = func.EndInvoke(result);
                if (update != null)
                {
                    if (NewVersionAvailable(update.Version))
                    {
                        ShowUpdate(update);
                    }
                    else if (_userInvoked)
                    {
                        Owner.Invoke(new Action(() => _messageBox.ShowInformation(Owner, String.Format(CultureInfo.CurrentCulture,
                                                         "{0} is already up-to-date.", Application.NameAndVersion), Owner.Text)), null);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message, ex);
                if (_userInvoked)
                {
                    string message = String.Format(CultureInfo.CurrentCulture, "{0} encountered the following error while checking for an update:{1}{1}{2}.",
                                                   Application.NameAndVersion, Environment.NewLine, ex.Message);
                    Owner.Invoke(new Action(() => _messageBox.ShowError(Owner, message, Owner.Text)), null);
                }
            }
            finally
            {
                CheckInProgress = false;
            }
        }

        private bool NewVersionAvailable(string updateVersion)
        {
            if (updateVersion == null) return false;

            try
            {
                return Application.ParseVersionNumber(updateVersion) > Application.VersionNumber;
            }
            catch (FormatException ex)
            {
                Logger.Warn(ex.Message, ex);
                return false;
            }
        }

        private void ShowUpdate(ApplicationUpdate update)
        {
            if (Owner.InvokeRequired)
            {
                Owner.Invoke(new Action(() => ShowUpdate(update)), null);
                return;
            }

            var updatePresenter = new ApplicationUpdatePresenter(ExceptionLogger,
               update, _proxy, Application.Name, Application.FullVersion);
            updatePresenter.Show(Owner);
            HandleUpdatePresenterResults(updatePresenter);
        }

        private void ExceptionLogger(Exception ex)
        {
            Logger.Error(ex.Message, ex);
        }

        private void HandleUpdatePresenterResults(ApplicationUpdatePresenter presenter)
        {
            if (presenter.UpdateReady &&
                presenter.SelectedUpdate.UpdateType == 0 &&
                Application.IsRunningOnMono == false)
            {
                string message = String.Format(CultureInfo.CurrentCulture,
                                               "{0} will install the new version when you exit the application.",
                                               Application.NameAndVersion);
                _messageBox.ShowInformation(Owner, message, Owner.Text);
                UpdateFilePath = presenter.LocalFilePath;
            }
        }
    }
}
