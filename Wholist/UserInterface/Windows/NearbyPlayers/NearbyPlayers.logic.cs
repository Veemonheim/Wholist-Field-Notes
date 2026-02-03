using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Dalamud.Utility;
using Lumina.Excel.Sheets;
using Sirensong.Extensions;
using Wholist.Common;
using Wholist.Configuration;
using Wholist.FieldNotes;

namespace Wholist.UserInterface.Windows.NearbyPlayers
{
    internal sealed class NearbyPlayersLogic : IDisposable
    {
        /// <summary>
        ///     The search text to use when filtering session entries.
        /// </summary>
        internal string SearchText = string.Empty;

        /// <summary>
        ///     The search text to use when filtering history entries.
        /// </summary>
        internal string HistorySearchText = string.Empty;

        internal NearbyPlayersLogic()
        {
            this.Manager = new FieldNotesManager();
        }

        internal FieldNotesManager Manager { get; }

        public void Dispose() => this.Manager.Dispose();

        internal static bool IsPvP => Services.ClientState.IsPvP;

        internal static void SearchPlayerOnLodestone(string name, string homeworldName)
        {
            switch (Services.Configuration.NearbyPlayers.LodestonePlayerSearchRegion)
            {
                case PluginConfiguration.NearbyPlayersConfiguration.LodestoneSearchRegion.Europe:
                    Util.OpenLink($"https://eu.finalfantasyxiv.com/lodestone/character/?q={name}&worldname={homeworldName}");
                    break;
                case PluginConfiguration.NearbyPlayersConfiguration.LodestoneSearchRegion.Germany:
                    Util.OpenLink($"https://de.finalfantasyxiv.com/lodestone/character/?q={name}&worldname={homeworldName}");
                    break;
                case PluginConfiguration.NearbyPlayersConfiguration.LodestoneSearchRegion.France:
                    Util.OpenLink($"https://fr.finalfantasyxiv.com/lodestone/character/?q={name}&worldname={homeworldName}");
                    break;
                case PluginConfiguration.NearbyPlayersConfiguration.LodestoneSearchRegion.NorthAmerica:
                    Util.OpenLink($"https://na.finalfantasyxiv.com/lodestone/character/?q={name}&worldname={homeworldName}");
                    break;
                case PluginConfiguration.NearbyPlayersConfiguration.LodestoneSearchRegion.Japan:
                    Util.OpenLink($"https://jp.finalfantasyxiv.com/lodestone/character/?q={name}&worldname={homeworldName}");
                    break;
                default:
                    throw new NotImplementedException("No link handler for specified region");
            }
        }

        internal List<KeyValuePair<string, SessionEntry>> GetSessionEntries()
        {
            var entries = this.Manager.SessionEntries.ToList();
            if (this.SearchText.IsNullOrWhitespace())
            {
                return entries;
            }

            return entries.Where(entry =>
                    entry.Value.Name.Contains(this.SearchText, StringComparison.OrdinalIgnoreCase) ||
                    entry.Value.HomeWorldName.Contains(this.SearchText, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        internal List<KeyValuePair<string, PluginConfiguration.PersistentPlayerEntry>> GetHistoryEntries(bool includeExported, bool markedOnly)
        {
            return Services.Configuration.FieldNotes.History
                .Where(entry => includeExported || !entry.Value.Exported)
                .Where(entry => !markedOnly || entry.Value.Marked)
                .Where(entry =>
                    this.HistorySearchText.IsNullOrWhitespace() ||
                    entry.Value.Name.Contains(this.HistorySearchText, StringComparison.OrdinalIgnoreCase) ||
                    entry.Value.HomeWorldName.Contains(this.HistorySearchText, StringComparison.OrdinalIgnoreCase))
                .OrderBy(entry => entry.Value.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        internal string BuildNamesList(IEnumerable<PluginConfiguration.PersistentPlayerEntry> entries, bool includeWorld)
        {
            return string.Join("\n", entries.Select(entry => includeWorld ? $"{entry.Name}@{entry.HomeWorldName}" : entry.Name));
        }

        internal string BuildTemplateExport(IEnumerable<PluginConfiguration.PersistentPlayerEntry> entries, string template, bool includeWorld)
        {
            var names = BuildNamesList(entries, includeWorld);
            var timestampUtc = DateTime.UtcNow.ToString("u", CultureInfo.InvariantCulture);
            var world = GetLocalWorldName();
            var location = GetLocationName();

            return template
                .Replace("{{names}}", names, StringComparison.OrdinalIgnoreCase)
                .Replace("{{timestamp_utc}}", timestampUtc, StringComparison.OrdinalIgnoreCase)
                .Replace("{{world}}", world ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("{{location}}", location ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        private static string? GetLocalWorldName()
        {
            var localPlayer = Services.ClientState.LocalPlayer;
            if (localPlayer == null)
            {
                return null;
            }

            return Services.WorldNames.GetValueOrDefault(localPlayer.CurrentWorld.RowId);
        }

        private static string? GetLocationName()
        {
            var territoryType = Services.ClientState.TerritoryType;
            if (territoryType == 0)
            {
                return null;
            }

            var territory = Services.DataManager.GetExcelSheet<TerritoryType>()?.GetRow(territoryType);
            return territory?.PlaceName.Value.Name.ExtractText().ToTitleCase();
        }
    }
}
