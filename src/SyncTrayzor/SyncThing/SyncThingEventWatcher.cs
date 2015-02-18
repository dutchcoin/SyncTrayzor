﻿using SyncTrayzor.SyncThing.Api;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace SyncTrayzor.SyncThing
{
    public interface ISyncThingEventWatcher : ISyncThingPoller
    {
        event EventHandler<SyncStateChangedEventArgs> SyncStateChanged;
        event EventHandler StartupComplete;
    }

    public class SyncThingEventWatcher : SyncThingPoller, ISyncThingEventWatcher, IEventVisitor
    {
        private readonly ISyncThingApiClient apiClient;

        private int lastEventId;

        public event EventHandler<SyncStateChangedEventArgs> SyncStateChanged;
        public event EventHandler StartupComplete;

        public SyncThingEventWatcher(ISyncThingApiClient apiClient)
            : base(TimeSpan.Zero)
        {
            this.apiClient = apiClient;
        }

        protected override void Start()
        {
            this.lastEventId = 0;
            base.Start();
        }

        protected override async Task PollAsync()
        {
            try
            {
                List<Event> events;
                // If we don't know what the latest event ID is (disconnection? new connection?), make sure we find out
                if (this.lastEventId == 0)
                    events = await this.apiClient.FetchEventsAsync(0, 1);
                else
                    events = await this.apiClient.FetchEventsAsync(this.lastEventId);

                foreach (var evt in events)
                {
                    this.lastEventId = Math.Max(this.lastEventId, evt.Id);
                    System.Diagnostics.Debug.WriteLine(evt);
                    evt.Visit(this);
                }
            }
            catch (IOException)
            {
                // A restart means the lastEventId will be reset
                this.lastEventId = 0;

                // Need the base method to do the error handling
                throw; 
            }
        }

        private void OnSyncStateChanged(string folderId, FolderSyncState oldState, FolderSyncState syncState)
        {
            var handler = this.SyncStateChanged;
            if (handler != null)
                handler(this, new SyncStateChangedEventArgs(folderId, oldState, syncState));
        }

        private void OnStartupComplete()
        {
            var handler = this.StartupComplete;
            if (handler != null)
                handler(this, EventArgs.Empty);
        }

        #region IEventVisitor

        public void Accept(GenericEvent evt)
        {
        }

        public void Accept(RemoteIndexUpdatedEvent evt)
        {
        }

        public void Accept(LocalIndexUpdatedEvent evt)
        {
        }

        public void Accept(StateChangedEvent evt)
        {
            var oldState = evt.Data.From == "syncing" ? FolderSyncState.Syncing : FolderSyncState.Idle;
            var state = evt.Data.To == "syncing" ? FolderSyncState.Syncing : FolderSyncState.Idle;
            this.OnSyncStateChanged(evt.Data.Folder, oldState, state);
        }

        public void Accept(ItemStartedEvent evt)
        {
        }

        public void Accept(ItemFinishedEvent evt)
        {
        }

        public void Accept(StartupCompleteEvent evt)
        {
            this.OnStartupComplete();
        }

        #endregion
    }
}