using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Sirensong.UserInterface;
using Wholist.Common;
using Wholist.Configuration;
using Wholist.FieldNotes;

namespace Wholist.UserInterface.Windows.NearbyPlayers
{
    internal sealed class NearbyPlayersWindow : Window, IDisposable
    {
        /// <inheritdoc cref="NearbyPlayersLogic" />
        private readonly NearbyPlayersLogic logic = new();

        private bool includeExportedHistory;
        private bool historyMarkedOnly;
        private bool showExportPreview = true;

        internal NearbyPlayersWindow() : base(Constants.PluginName)
        {
            this.Size = new Vector2(900, 600);
            this.SizeCondition = ImGuiCond.FirstUseEver;
            this.TitleBarButtons =
            [
                new()
                {
                    Icon = FontAwesomeIcon.Cog,
                    ShowTooltip = () => SiGui.AddTooltip("Settings"),
                    Click = (btn) => Services.WindowManager.ToggleConfigWindow()
                }
            ];
        }

        public void Dispose() => this.logic.Dispose();

        public override bool DrawConditions()
        {
            if (Services.Configuration.NearbyPlayers.HideInCombat && Services.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.InCombat])
            {
                return false;
            }

            if (Services.Configuration.NearbyPlayers.HideInInstance && (Sirensong.Game.Helpers.ConditionHelper.IsBoundByDuty() || Sirensong.Game.Helpers.ConditionHelper.IsInIslandSanctuary()))
            {
                return false;
            }

            return !NearbyPlayersLogic.IsPvP;
        }

        public override void Draw()
        {
            using var tabBar = ImRaii.TabBar("##FieldNotesTabs");
            if (!tabBar)
            {
                return;
            }

            if (ImGui.BeginTabItem("Session Scan"))
            {
                this.DrawSessionTab();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Marked / History"))
            {
                this.DrawHistoryTab();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Template / Export"))
            {
                this.DrawExportTab();
                ImGui.EndTabItem();
            }
        }

        private void DrawSessionTab()
        {
            DrawScanControls();

            ImGui.Separator();
            DrawSearchBar("##SessionSearch", "Search name or world...", ref this.logic.SearchText);

            var entries = this.logic.GetSessionEntries();
            var tableSize = new Vector2(0, -ImGui.GetTextLineHeightWithSpacing() * 2);
            using (var table = ImRaii.Table("##SessionTable", 9, ImGuiTableFlags.ScrollY | ImGuiTableFlags.Borders | ImGuiTableFlags.Resizable, tableSize))
            {
                if (table)
                {
                    ImGui.TableSetupColumn("Include", ImGuiTableColumnFlags.WidthFixed, 60);
                    ImGui.TableSetupColumn("Marked", ImGuiTableColumnFlags.WidthFixed, 60);
                    ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch, 180);
                    ImGui.TableSetupColumn("Home World", ImGuiTableColumnFlags.WidthStretch, 140);
                    ImGui.TableSetupColumn("First Seen (UTC)", ImGuiTableColumnFlags.WidthStretch, 140);
                    ImGui.TableSetupColumn("Last Seen (UTC)", ImGuiTableColumnFlags.WidthStretch, 140);
                    ImGui.TableSetupColumn("Seen Count", ImGuiTableColumnFlags.WidthFixed, 80);
                    ImGui.TableSetupColumn("Visible", ImGuiTableColumnFlags.WidthFixed, 60);
                    ImGui.TableSetupColumn("Lodestone", ImGuiTableColumnFlags.WidthFixed, 80);
                    ImGui.TableSetupScrollFreeze(0, 1);
                    ImGui.TableHeadersRow();

                    foreach (var (key, entry) in entries.OrderBy(entry => entry.Value.Name, StringComparer.OrdinalIgnoreCase))
                    {
                        DrawSessionRow(key, entry);
                    }
                }
            }

            ImGuiHelpers.CenteredText($"Session entries: {entries.Count}");
        }

        private void DrawHistoryTab()
        {
            ImGui.Text("Persistent history (local only).");
            ImGui.Spacing();

            using (var row = ImRaii.Group())
            {
                DrawSearchBar("##HistorySearch", "Search name or world...", ref this.logic.HistorySearchText);
                ImGui.SameLine();
                ImGui.Checkbox("Show exported", ref this.includeExportedHistory);
                ImGui.SameLine();
                ImGui.Checkbox("Marked only", ref this.historyMarkedOnly);
            }

            ImGui.Separator();

            DrawHistoryControls();

            ImGui.Separator();

            var entries = this.logic.GetHistoryEntries(this.includeExportedHistory, this.historyMarkedOnly);
            var tableSize = new Vector2(0, -ImGui.GetTextLineHeightWithSpacing() * 2);
            using (var table = ImRaii.Table("##HistoryTable", 10, ImGuiTableFlags.ScrollY | ImGuiTableFlags.Borders | ImGuiTableFlags.Resizable, tableSize))
            {
                if (table)
                {
                    ImGui.TableSetupColumn("Include", ImGuiTableColumnFlags.WidthFixed, 60);
                    ImGui.TableSetupColumn("Marked", ImGuiTableColumnFlags.WidthFixed, 60);
                    ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch, 160);
                    ImGui.TableSetupColumn("Home World", ImGuiTableColumnFlags.WidthStretch, 140);
                    ImGui.TableSetupColumn("First Seen (UTC)", ImGuiTableColumnFlags.WidthStretch, 140);
                    ImGui.TableSetupColumn("Last Seen (UTC)", ImGuiTableColumnFlags.WidthStretch, 140);
                    ImGui.TableSetupColumn("Times Seen", ImGuiTableColumnFlags.WidthFixed, 80);
                    ImGui.TableSetupColumn("Exported", ImGuiTableColumnFlags.WidthFixed, 70);
                    ImGui.TableSetupColumn("Last Export (UTC)", ImGuiTableColumnFlags.WidthStretch, 140);
                    ImGui.TableSetupColumn("Lodestone", ImGuiTableColumnFlags.WidthFixed, 80);
                    ImGui.TableSetupScrollFreeze(0, 1);
                    ImGui.TableHeadersRow();

                    foreach (var (key, entry) in entries)
                    {
                        DrawHistoryRow(key, entry);
                    }
                }
            }

            ImGuiHelpers.CenteredText($"History entries: {entries.Count}");
        }

        private void DrawExportTab()
        {
            var config = Services.Configuration.FieldNotes;

            ImGui.Text("Template tokens:");
            ImGui.BulletText("{{names}} — selected names, one per line");
            ImGui.BulletText("{{timestamp_utc}} — export timestamp (UTC)");
            ImGui.BulletText("{{world}} — your current world (if available)");
            ImGui.BulletText("{{location}} — current location (if available)");
            ImGui.Spacing();

            var template = config.ExportTemplate;
            var templateSize = new Vector2(-1, 200 * ImGuiHelpers.GlobalScale);
            if (ImGui.InputTextMultiline("##ExportTemplate", ref template, 4000, templateSize))
            {
                config.ExportTemplate = template;
                Services.Configuration.Save();
            }

            ImGui.Spacing();
            var exportConfigChanged = ImGui.Checkbox("Include exported entries by default", ref config.IncludeExportedByDefault);
            ImGui.SameLine();
            exportConfigChanged |= ImGui.Checkbox("Include world in names list", ref config.IncludeWorldInNames);
            ImGui.SameLine();
            exportConfigChanged |= ImGui.Checkbox("Mark exported after copy", ref config.MarkExportedAfterCopy);

            if (exportConfigChanged)
            {
                Services.Configuration.Save();
                this.logic.Manager.InitializeDefaultSelection();
            }

            ImGui.Spacing();

            var exportEntries = GetExportEntries(out var exportKeys);
            using (ImRaii.Disabled(exportEntries.Count == 0))
            {
                if (ImGui.Button("Copy names only"))
                {
                    var text = this.logic.BuildNamesList(exportEntries, false);
                    ImGui.SetClipboardText(text);
                    HandleExportedFlag(exportKeys);
                }

                ImGui.SameLine();
                if (ImGui.Button("Copy names + world"))
                {
                    var text = this.logic.BuildNamesList(exportEntries, true);
                    ImGui.SetClipboardText(text);
                    HandleExportedFlag(exportKeys);
                }

                ImGui.SameLine();
                if (ImGui.Button("Copy report snippet"))
                {
                    var text = this.logic.BuildTemplateExport(exportEntries, config.ExportTemplate, config.IncludeWorldInNames);
                    ImGui.SetClipboardText(text);
                    HandleExportedFlag(exportKeys);
                }
            }

            ImGui.SameLine();
            ImGui.Checkbox("Show preview", ref this.showExportPreview);

            if (this.showExportPreview)
            {
                ImGui.Separator();
                var preview = this.logic.BuildTemplateExport(exportEntries, config.ExportTemplate, config.IncludeWorldInNames);
                ImGui.InputTextMultiline("##ExportPreview", ref preview, 4000, new Vector2(-1, 200 * ImGuiHelpers.GlobalScale), ImGuiInputTextFlags.ReadOnly);
            }
        }

        private void DrawScanControls()
        {
            var state = this.logic.Manager.State;
            var isPvP = NearbyPlayersLogic.IsPvP;
            var isLoggedIn = Services.ClientState.IsLoggedIn;

            ImGui.Text($"Scan status: {state}");
            ImGui.TextDisabled("Only characters currently loaded by your client can be captured.");

            using (ImRaii.Disabled(!isLoggedIn || isPvP))
            {
                if (ImGui.Button("Start Scan"))
                {
                    this.logic.Manager.StartScan();
                }
            }

            ImGui.SameLine();
            using (ImRaii.Disabled(state != FieldNotesManager.ScanState.Running))
            {
                if (ImGui.Button("Pause"))
                {
                    this.logic.Manager.PauseScan();
                }
            }

            ImGui.SameLine();
            using (ImRaii.Disabled(state != FieldNotesManager.ScanState.Paused))
            {
                if (ImGui.Button("Resume"))
                {
                    this.logic.Manager.ResumeScan();
                }
            }

            ImGui.SameLine();
            using (ImRaii.Disabled(state == FieldNotesManager.ScanState.Stopped))
            {
                if (ImGui.Button("Stop"))
                {
                    this.logic.Manager.StopScan();
                }
            }

            ImGui.SameLine();
            if (ImGui.Button("Clear Session"))
            {
                this.logic.Manager.ClearSession();
            }

            if (isPvP)
            {
                ImGui.TextColored(new Vector4(1f, 0.4f, 0.4f, 1f), "Scanning is disabled in PvP.");
            }
        }

        private void DrawHistoryControls()
        {
            var config = Services.Configuration.FieldNotes;
            var pruneDays = config.AutoPruneDays;
            ImGui.SetNextItemWidth(120);
            if (ImGui.InputInt("Auto-prune days", ref pruneDays))
            {
                config.AutoPruneDays = Math.Max(1, pruneDays);
                pruneDays = config.AutoPruneDays;
                Services.Configuration.Save();
            }

            ImGui.SameLine();
            if (ImGui.Button("Prune now"))
            {
                this.logic.Manager.PruneHistory();
            }

            ImGui.SameLine();
            if (ImGui.Button("Reset history"))
            {
                ImGui.OpenPopup("##ResetHistoryConfirm");
            }

            if (ImGui.BeginPopup("##ResetHistoryConfirm"))
            {
                ImGui.Text("Clear all stored history?");
                if (ImGui.Button("Confirm"))
                {
                    this.logic.Manager.ResetHistory();
                    ImGui.CloseCurrentPopup();
                }
                ImGui.SameLine();
                if (ImGui.Button("Cancel"))
                {
                    ImGui.CloseCurrentPopup();
                }
                ImGui.EndPopup();
            }
        }

        private void DrawSessionRow(string key, SessionEntry entry)
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            var selected = this.logic.Manager.IsSelected(key);
            if (ImGui.Checkbox($"##select-session-{key}", ref selected))
            {
                this.logic.Manager.ToggleSelection(key, selected);
            }

            ImGui.TableNextColumn();
            var persistentEntry = Services.Configuration.FieldNotes.History.GetValueOrDefault(key);
            var marked = persistentEntry?.Marked ?? false;
            if (ImGui.Checkbox($"##marked-session-{key}", ref marked) && persistentEntry != null)
            {
                this.logic.Manager.ToggleMarked(persistentEntry, marked);
            }

            ImGui.TableNextColumn();
            ImGui.Text(entry.Name);
            ImGui.TableNextColumn();
            ImGui.Text(entry.HomeWorldName);
            ImGui.TableNextColumn();
            ImGui.Text(entry.FirstSeenUtc.ToString("u"));
            ImGui.TableNextColumn();
            ImGui.Text(entry.LastSeenUtc.ToString("u"));
            ImGui.TableNextColumn();
            ImGui.Text(entry.SeenCount.ToString());
            ImGui.TableNextColumn();
            ImGui.Text(entry.IsVisible ? "Yes" : "No");
            ImGui.TableNextColumn();
            if (ImGui.SmallButton($"Search##session-{key}"))
            {
                NearbyPlayersLogic.SearchPlayerOnLodestone(entry.Name, entry.HomeWorldName);
            }
        }

        private void DrawHistoryRow(string key, PluginConfiguration.PersistentPlayerEntry entry)
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            var selected = this.logic.Manager.IsSelected(key);
            if (ImGui.Checkbox($"##select-history-{key}", ref selected))
            {
                this.logic.Manager.ToggleSelection(key, selected);
            }

            ImGui.TableNextColumn();
            var marked = entry.Marked;
            if (ImGui.Checkbox($"##marked-history-{key}", ref marked))
            {
                this.logic.Manager.ToggleMarked(entry, marked);
            }

            ImGui.TableNextColumn();
            ImGui.Text(entry.Name);
            ImGui.TableNextColumn();
            ImGui.Text(entry.HomeWorldName);
            ImGui.TableNextColumn();
            ImGui.Text(entry.FirstSeenUtc.ToString("u"));
            ImGui.TableNextColumn();
            ImGui.Text(entry.LastSeenUtc.ToString("u"));
            ImGui.TableNextColumn();
            ImGui.Text(entry.TimesSeen.ToString());
            ImGui.TableNextColumn();
            var exported = entry.Exported;
            if (ImGui.Checkbox($"##exported-history-{key}", ref exported))
            {
                entry.Exported = exported;
                entry.LastExportedUtc = exported ? entry.LastExportedUtc ?? DateTime.UtcNow : null;
                if (exported && !Services.Configuration.FieldNotes.IncludeExportedByDefault)
                {
                    this.logic.Manager.ToggleSelection(key, false);
                }
                Services.Configuration.Save();
            }

            ImGui.TableNextColumn();
            ImGui.Text(entry.LastExportedUtc?.ToString("u") ?? "-");
            ImGui.TableNextColumn();
            if (ImGui.SmallButton($"Search##history-{key}"))
            {
                NearbyPlayersLogic.SearchPlayerOnLodestone(entry.Name, entry.HomeWorldName);
            }
        }

        private static void DrawSearchBar(string id, string hint, ref string searchText)
        {
            ImGui.SetNextItemWidth(-1);
            ImGui.InputTextWithHint(id, hint, ref searchText, 200);
        }

        private List<PluginConfiguration.PersistentPlayerEntry> GetExportEntries(out List<string> exportKeys)
        {
            IReadOnlyCollection<PluginConfiguration.PersistentPlayerEntry> entries;
            if (this.logic.Manager.HasSelection)
            {
                entries = this.logic.Manager.GetSelectedExportEntries().ToList();
            }
            else
            {
                entries = this.logic.Manager.GetDefaultExportEntries().ToList();
            }

            exportKeys = entries
                .Select(entry => FieldNotesManager.BuildKey(entry.Name, entry.HomeWorldId))
                .ToList();

            return entries.ToList();
        }

        private void HandleExportedFlag(List<string> exportKeys)
        {
            if (Services.Configuration.FieldNotes.MarkExportedAfterCopy && exportKeys.Count > 0)
            {
                this.logic.Manager.MarkExported(exportKeys);
            }
        }
    }
}
