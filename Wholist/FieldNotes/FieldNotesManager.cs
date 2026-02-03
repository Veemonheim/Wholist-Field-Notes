using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using Wholist.Common;
using Wholist.Configuration;
using Wholist.Game;

namespace Wholist.FieldNotes
{
    internal sealed class FieldNotesManager : IDisposable
    {
        internal enum ScanState
        {
            Stopped,
            Running,
            Paused
        }

        private readonly Dictionary<string, SessionEntry> sessionEntries = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> selectedEntries = new(StringComparer.OrdinalIgnoreCase);
        private readonly TimeSpan updateInterval = TimeSpan.FromMilliseconds(500);
        private readonly TimeSpan saveInterval = TimeSpan.FromSeconds(10);
        private DateTime lastUpdateUtc = DateTime.MinValue;
        private DateTime lastSaveUtc = DateTime.MinValue;
        private bool pendingSave;

        internal FieldNotesManager()
        {
            InitializeDefaultSelection();
            this.PruneHistory();
            Services.Framework.Update += this.OnFrameworkUpdate;
        }

        internal ScanState State { get; private set; } = ScanState.Stopped;

        internal IReadOnlyDictionary<string, SessionEntry> SessionEntries => this.sessionEntries;

        internal IReadOnlyCollection<string> SelectedEntries => this.selectedEntries;

        internal bool HasSelection => this.selectedEntries.Count > 0;

        public void Dispose()
        {
            this.SaveIfNeeded(DateTime.UtcNow, true);
            Services.Framework.Update -= this.OnFrameworkUpdate;
        }

        internal void StartScan()
        {
            this.sessionEntries.Clear();
            this.selectedEntries.Clear();
            this.State = ScanState.Running;
        }

        internal void PauseScan()
        {
            if (this.State == ScanState.Running)
            {
                this.State = ScanState.Paused;
            }
        }

        internal void ResumeScan()
        {
            if (this.State == ScanState.Paused)
            {
                this.State = ScanState.Running;
            }
        }

        internal void StopScan()
        {
            if (this.State != ScanState.Stopped)
            {
                this.State = ScanState.Stopped;
            }

            this.SaveIfNeeded(DateTime.UtcNow, true);
        }

        internal void ClearSession()
        {
            this.sessionEntries.Clear();
            this.selectedEntries.Clear();
        }

        internal void ToggleSelection(string key, bool isSelected)
        {
            if (isSelected)
            {
                this.selectedEntries.Add(key);
            }
            else
            {
                this.selectedEntries.Remove(key);
            }
        }

        internal bool IsSelected(string key) => this.selectedEntries.Contains(key);

        internal void InitializeDefaultSelection()
        {
            this.selectedEntries.Clear();
            foreach (var entry in Services.Configuration.FieldNotes.History)
            {
                if (entry.Value.Marked && (!entry.Value.Exported || Services.Configuration.FieldNotes.IncludeExportedByDefault))
                {
                    this.selectedEntries.Add(entry.Key);
                }
            }
        }

        internal void ToggleMarked(PluginConfiguration.PersistentPlayerEntry entry, bool marked)
        {
            if (entry.Marked == marked)
            {
                return;
            }

            entry.Marked = marked;
            var now = DateTime.UtcNow;
            if (marked)
            {
                entry.FirstMarkedUtc ??= now;
                entry.LastMarkedUtc = now;
                if (!entry.Exported)
                {
                    this.selectedEntries.Add(BuildKey(entry.Name, entry.HomeWorldId));
                }
            }
            else
            {
                entry.LastMarkedUtc = now;
            }
            Services.Configuration.Save();
        }

        internal void MarkExported(IEnumerable<string> keys)
        {
            var now = DateTime.UtcNow;
            foreach (var key in keys)
            {
                if (!Services.Configuration.FieldNotes.History.TryGetValue(key, out var entry))
                {
                    continue;
                }

                entry.Exported = true;
                entry.LastExportedUtc = now;
                if (!Services.Configuration.FieldNotes.IncludeExportedByDefault)
                {
                    this.selectedEntries.Remove(key);
                }
            }
            Services.Configuration.Save();
        }

        internal void PruneHistory()
        {
            var now = DateTime.UtcNow;
            var cutoff = now.AddDays(-Services.Configuration.FieldNotes.AutoPruneDays);
            var keysToRemove = Services.Configuration.FieldNotes.History
                .Where(entry => entry.Value.LastSeenUtc < cutoff)
                .Select(entry => entry.Key)
                .ToList();

            foreach (var key in keysToRemove)
            {
                Services.Configuration.FieldNotes.History.Remove(key);
                this.selectedEntries.Remove(key);
            }

            if (keysToRemove.Count > 0)
            {
                Services.Configuration.Save();
            }
        }

        internal void ResetHistory()
        {
            Services.Configuration.FieldNotes.History.Clear();
            this.selectedEntries.Clear();
            Services.Configuration.Save();
        }

        internal IEnumerable<PluginConfiguration.PersistentPlayerEntry> GetDefaultExportEntries()
        {
            return Services.Configuration.FieldNotes.History.Values
                .Where(entry => entry.Marked && (!entry.Exported || Services.Configuration.FieldNotes.IncludeExportedByDefault))
                .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase);
        }

        internal IEnumerable<PluginConfiguration.PersistentPlayerEntry> GetSelectedExportEntries()
        {
            return this.selectedEntries
                .Select(key => Services.Configuration.FieldNotes.History.GetValueOrDefault(key))
                .Where(entry => entry != null)
                .OrderBy(entry => entry!.Name, StringComparer.OrdinalIgnoreCase)
                .Select(entry => entry!);
        }

        private void OnFrameworkUpdate(IFramework framework)
        {
            if (this.State != ScanState.Running || !Services.ClientState.IsLoggedIn || Services.ClientState.IsPvP)
            {
                return;
            }

            var now = DateTime.UtcNow;
            if (now - this.lastUpdateUtc < this.updateInterval)
            {
                return;
            }
            this.lastUpdateUtc = now;

            foreach (var entry in this.sessionEntries.Values)
            {
                entry.IsVisible = false;
            }

            foreach (var player in PlayerManager.GetNearbyPlayers(Services.Configuration.NearbyPlayers.FilterBlockedPlayers))
            {
                var key = BuildKey(player.Name.TextValue, player.HomeWorld.RowId);
                var homeWorldName = Services.WorldNames.GetValueOrDefault(player.HomeWorld.RowId) ?? player.HomeWorld.RowId.ToString();
                var currentWorldName = Services.WorldNames.GetValueOrDefault(player.CurrentWorld.RowId);

                if (!this.sessionEntries.TryGetValue(key, out var sessionEntry))
                {
                    sessionEntry = new SessionEntry
                    {
                        Name = player.Name.TextValue,
                        HomeWorldId = player.HomeWorld.RowId,
                        HomeWorldName = homeWorldName,
                        CurrentWorldName = currentWorldName,
                        FirstSeenUtc = now,
                        LastSeenUtc = now,
                        SeenCount = 1,
                        IsVisible = true
                    };
                    this.sessionEntries[key] = sessionEntry;
                    this.UpdatePersistentEntry(sessionEntry, now, true);
                }
                else
                {
                    var wasVisible = sessionEntry.IsVisible;
                    sessionEntry.IsVisible = true;
                    sessionEntry.LastSeenUtc = now;
                    sessionEntry.CurrentWorldName = currentWorldName;
                    if (!wasVisible)
                    {
                        sessionEntry.SeenCount += 1;
                        this.UpdatePersistentEntry(sessionEntry, now, true);
                    }
                    else
                    {
                        this.UpdatePersistentEntry(sessionEntry, now, false);
                    }
                }
            }

            this.SaveIfNeeded(now, false);
        }

        private void UpdatePersistentEntry(SessionEntry sessionEntry, DateTime seenUtc, bool incrementSeen)
        {
            var key = BuildKey(sessionEntry.Name, sessionEntry.HomeWorldId);
            if (!Services.Configuration.FieldNotes.History.TryGetValue(key, out var entry))
            {
                entry = new PluginConfiguration.PersistentPlayerEntry
                {
                    Name = sessionEntry.Name,
                    HomeWorldId = sessionEntry.HomeWorldId,
                    HomeWorldName = sessionEntry.HomeWorldName,
                    LastSeenWorldName = sessionEntry.CurrentWorldName,
                    FirstSeenUtc = seenUtc,
                    LastSeenUtc = seenUtc,
                    TimesSeen = incrementSeen ? 1 : 0
                };
                Services.Configuration.FieldNotes.History[key] = entry;
            }
            else
            {
                entry.LastSeenUtc = seenUtc;
                entry.LastSeenWorldName = sessionEntry.CurrentWorldName;
                entry.HomeWorldName = sessionEntry.HomeWorldName;
                if (incrementSeen)
                {
                    entry.TimesSeen += 1;
                }
            }

            this.pendingSave = true;
        }

        private void SaveIfNeeded(DateTime now, bool force)
        {
            if (!this.pendingSave || (!force && now - this.lastSaveUtc < this.saveInterval))
            {
                return;
            }

            Services.Configuration.Save();
            this.pendingSave = false;
            this.lastSaveUtc = now;
        }

        internal static string BuildKey(string name, uint homeWorldId)
            => $"{name.Trim()}@{homeWorldId}".ToLowerInvariant();
    }
}
