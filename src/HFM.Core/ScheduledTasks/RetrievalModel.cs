﻿
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Castle.Core.Logging;

using HFM.Core.Client;
using HFM.Core.SlotXml;
using HFM.Preferences;

namespace HFM.Core.ScheduledTasks
{
    public enum ProcessingMode
    {
        Parallel,
        Serial
    }

    public class RetrievalModel
    {
        private ILogger _logger;

        public ILogger Logger
        {
            get { return _logger ?? (_logger = NullLogger.Instance); }
            set { _logger = value; }
        }

        private const string ClientTaskKey = "Client Retrieval";
        private const string WebTaskKey = "Web Generation";

        private readonly IPreferenceSet _prefs;
        private readonly IClientConfiguration _clientConfiguration;
        private readonly Lazy<MarkupGenerator> _markupGenerator;
        private readonly Lazy<IWebsiteDeployer> _websiteDeployer;
        private readonly AggregateScheduledTask _aggregateScheduledTask;

        public RetrievalModel(IPreferenceSet prefs, IClientConfiguration clientConfiguration,
                              Lazy<MarkupGenerator> markupGenerator, Lazy<IWebsiteDeployer> websiteDeployer)
        {
            _prefs = prefs;
            _clientConfiguration = clientConfiguration;
            _markupGenerator = markupGenerator;
            _websiteDeployer = websiteDeployer;
            _aggregateScheduledTask = new AggregateScheduledTask();
            _aggregateScheduledTask.Changed += TaskChanged;

            _prefs.PreferenceChanged += (s, e) =>
            {
                switch (e.Preference)
                {
                    case Preference.ClientRetrievalTask:
                        if (_prefs.Get<bool>(Preference.ClientRetrievalTaskEnabled))
                        {
                            if (_clientConfiguration.Count != 0)
                            {
                                _aggregateScheduledTask.Restart(ClientTaskKey, ClientInterval);
                            }
                        }
                        else
                        {
                            _aggregateScheduledTask.Stop(ClientTaskKey);
                        }
                        break;
                    case Preference.WebGenerationTask:
                        if (_prefs.Get<bool>(Preference.WebGenerationTaskEnabled) &&
                            _prefs.Get<bool>(Preference.WebGenerationTaskAfterClientRetrieval) == false)
                        {
                            if (_clientConfiguration.Count != 0)
                            {
                                _aggregateScheduledTask.Restart(WebTaskKey, WebInterval);
                            }
                        }
                        else
                        {
                            _aggregateScheduledTask.Stop(WebTaskKey);
                        }
                        break;
                }
            };

            _clientConfiguration.ConfigurationChanged += (s, e) =>
            {
                if (e.Action == ConfigurationChangedAction.Remove ||
                    e.Action == ConfigurationChangedAction.Clear)
                {
                    // Disable timers if no hosts
                    if (_aggregateScheduledTask.Enabled && _clientConfiguration.Count == 0)
                    {
                        Logger.Info("No clients... stopping all scheduled tasks");
                        //_aggregateScheduledTask.Stop();
                        _aggregateScheduledTask.Cancel();
                    }
                }
                else if (e.Action == ConfigurationChangedAction.Add)
                {
                    var clientTaskEnabled = _prefs.Get<bool>(Preference.ClientRetrievalTaskEnabled);
                    if (e.Client == null)
                    {
                        // no client specified - retrieve all
                        _aggregateScheduledTask.Run(ClientTaskKey, ClientInterval, clientTaskEnabled);
                    }
                    else if (clientTaskEnabled)
                    {
                        _aggregateScheduledTask.Start(ClientTaskKey, ClientInterval);
                    }

                    if (_prefs.Get<bool>(Preference.WebGenerationTaskEnabled) &&
                        _prefs.Get<bool>(Preference.WebGenerationTaskAfterClientRetrieval) == false)
                    {
                        _aggregateScheduledTask.Start(WebTaskKey, WebInterval);
                    }
                }
            };

            _aggregateScheduledTask.Add(new DelegateScheduledTask(ClientTaskKey, ClientRetrievalAction, ClientInterval));
            _aggregateScheduledTask.Add(new DelegateScheduledTask(WebTaskKey, WebGenerationAction, WebInterval));
        }

        private void TaskChanged(object sender, ScheduledTaskChangedEventArgs e)
        {
            switch (e.Action)
            {
                case ScheduledTaskChangedAction.Started:
                    Logger.Info(e.ToString(i => $"{(int)(i.GetValueOrDefault() / Constants.MinToMillisec)} minutes"));
                    break;
                case ScheduledTaskChangedAction.Faulted:
                    Logger.Error(e.ToString());
                    break;
                case ScheduledTaskChangedAction.AlreadyInProgress:
                    Logger.Warn(e.ToString());
                    break;
                default:
                    Logger.Info(e.ToString());
                    break;
            }
        }

        private double ClientInterval
        {
            get { return _prefs.Get<int>(Preference.ClientRetrievalTaskInterval) * Constants.MinToMillisec; }
        }

        private double WebInterval
        {
            get { return _prefs.Get<int>(Preference.WebGenerationTaskInterval) * Constants.MinToMillisec; }
        }

        public void RunClientRetrieval()
        {
            _aggregateScheduledTask.Run(ClientTaskKey, false);
        }

        //public void RunWebGeneration()
        //{
        //   _aggregateScheduledTask.Run(WebTaskKey, false);
        //}

        private void ClientRetrievalAction(CancellationToken ct)
        {
            // get flag synchronous or asynchronous - we don't want this flag to change on us
            // in the middle of a retrieve, so grab it now and use the local copy
            var mode = _prefs.Get<ProcessingMode>(Preference.ClientRetrievalTaskType);

            ct.ThrowIfCancellationRequested();

            var clientsEnumerable = _clientConfiguration.GetClients();
            var clients = clientsEnumerable as IList<IClient> ?? clientsEnumerable.ToList();
            if (mode == ProcessingMode.Serial)
            {
                // do the individual retrieves on a single thread
                foreach (var client in clients)
                {
                    ct.ThrowIfCancellationRequested();
                    client.Retrieve();
                }
            }
            else
            {
                // fire individual threads to do the their own retrieve simultaneously
                Parallel.ForEach(clients, x =>
                {
                    ct.ThrowIfCancellationRequested();
                    x.Retrieve();
                });
            }

            if (_prefs.Get<bool>(Preference.WebGenerationTaskEnabled) &&
                _prefs.Get<bool>(Preference.WebGenerationTaskAfterClientRetrieval))
            {
                ct.ThrowIfCancellationRequested();
                _aggregateScheduledTask.Run(WebTaskKey, false);
            }
        }

        private void WebGenerationAction(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var slots = _clientConfiguration.Slots as IList<SlotModel> ?? _clientConfiguration.Slots.ToList();
            _markupGenerator.Value.Generate(slots);

            ct.ThrowIfCancellationRequested();
            _websiteDeployer.Value.DeployWebsite(_markupGenerator.Value.HtmlFilePaths, _markupGenerator.Value.XmlFilePaths, slots);
        }
    }
}
