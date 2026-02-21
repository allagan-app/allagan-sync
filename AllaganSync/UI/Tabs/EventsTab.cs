using System;
using System.Numerics;
using System.Text.Json;
using AllaganSync.Models;
using AllaganSync.Services;
using Dalamud.Bindings.ImGui;

namespace AllaganSync.UI.Tabs;

public class EventsTab
{
    private readonly ConfigurationService configService;
    private readonly EventTrackingService eventTrackingService;
    private readonly Action? openSettings;
    private int selectedHistoryIndex = -1;

    public EventsTab(ConfigurationService configService, EventTrackingService eventTrackingService, Action? openSettings = null)
    {
        this.configService = configService;
        this.eventTrackingService = eventTrackingService;
        this.openSettings = openSettings;
    }

    public void Draw()
    {
        DrawDescription();

        if (!SetupGuard.Draw(configService, openSettings))
            return;

        var charConfig = configService.CurrentCharacter!;
        if (!charConfig.TrackingEnabled)
        {
            ImGui.TextDisabled("Event tracking is disabled.");
            if (openSettings != null)
            {
                ImGui.SameLine();
                if (ImGui.SmallButton("Open Settings"))
                    openSettings();
            }

            return;
        }

        DrawBackoffBanner();
        DrawActions(charConfig);

        ImGui.Spacing();

        DrawEventBuffer();

        ImGui.Spacing();

        DrawSendHistory();
    }

    // ── Description ───────────────────────────────────────────────────

    private static void DrawDescription()
    {
        ImGui.TextWrapped("When enabled, the plugin automatically captures certain in-game activities " +
            "such as desynthesis results and contributes them to Allagan's community-driven " +
            "statistics like drop rates. Your data is only used anonymously in aggregate statistics.");

        ImGui.Spacing();
        ImGui.Spacing();
    }

    // ── Backoff Banner ───────────────────────────────────────────────────

    private void DrawBackoffBanner()
    {
        if (!eventTrackingService.IsBackingOff)
            return;

        ImGui.TextColored(new Vector4(1, 0.5f, 0, 1),
            $"{eventTrackingService.BackoffReason} — retrying in {eventTrackingService.BackoffSecondsRemaining}s");
        ImGui.Spacing();
    }

    // ── Actions ────────────────────────────────────────────────────────

    private void DrawActions(CharacterConfig? charConfig)
    {
        if (charConfig == null || !charConfig.TrackingEnabled || !charConfig.HasApiToken)
            return;

        if (charConfig.TrackingPaused)
        {
            if (ImGui.Button("Resume"))
            {
                charConfig.TrackingPaused = false;
                configService.Save();
                eventTrackingService.UpdateTrackerStates();
            }
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(1, 0.5f, 0, 1), "Paused");
        }
        else
        {
            if (ImGui.Button("Pause"))
            {
                charConfig.TrackingPaused = true;
                configService.Save();
                eventTrackingService.UpdateTrackerStates();
            }
        }

        ImGui.SameLine();
        if (ImGui.Button("Send Now"))
        {
            _ = eventTrackingService.FlushAsync();
        }

        ImGui.SameLine();
        if (ImGui.Button("Clear"))
        {
            eventTrackingService.Clear();
        }
    }

    // ── Event Buffer ───────────────────────────────────────────────────

    private void DrawEventBuffer()
    {
        if (!ImGui.CollapsingHeader("Event Buffer", ImGuiTreeNodeFlags.DefaultOpen))
            return;

        var bufferedEvents = eventTrackingService.PeekBuffer();

        if (bufferedEvents.Count == 0)
        {
            ImGui.TextDisabled("No events in buffer.");
            return;
        }

        if (ImGui.BeginTable("EventBufferTable", 3,
                ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY,
                new Vector2(0, 150)))
        {
            ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.WidthFixed, 120);
            ImGui.TableSetupColumn("Timestamp", ImGuiTableColumnFlags.WidthFixed, 160);
            ImGui.TableSetupColumn("Preview", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableHeadersRow();

            foreach (var evt in bufferedEvents)
            {
                ImGui.TableNextRow();

                ImGui.TableNextColumn();
                ImGui.Text(evt.EventType);

                ImGui.TableNextColumn();
                ImGui.Text(evt.OccurredAt ?? "-");

                ImGui.TableNextColumn();
                var preview = GetPayloadPreview(evt);
                ImGui.TextDisabled(preview);
            }

            ImGui.EndTable();
        }
    }

    private static string GetPayloadPreview(TrackedEvent evt)
    {
        if (evt.Payload is DesynthResultPayload desynth)
            return $"Item {desynth.SourceItemId} -> {desynth.Results.Count} result(s)";

        try
        {
            var json = JsonSerializer.Serialize(evt.Payload);
            return json.Length > 80 ? json[..77] + "..." : json;
        }
        catch
        {
            return evt.Payload?.ToString() ?? "(empty)";
        }
    }

    private static void DrawEventDetail(TrackedEvent evt)
    {
        ImGui.Indent();

        if (evt.Payload is DesynthResultPayload desynth)
        {
            DrawDesynthDetail(desynth);
        }
        else
        {
            try
            {
                var json = JsonSerializer.Serialize(evt.Payload, new JsonSerializerOptions { WriteIndented = true });
                ImGui.TextDisabled(json);
            }
            catch
            {
                ImGui.TextDisabled(evt.Payload?.ToString() ?? "(empty)");
            }
        }

        ImGui.Unindent();
    }

    private static void DrawDesynthDetail(DesynthResultPayload desynth)
    {
        ImGui.TextDisabled("Source Item ID:");
        ImGui.SameLine();
        ImGui.Text(desynth.SourceItemId.ToString());

        ImGui.TextDisabled("Class Job ID:");
        ImGui.SameLine();
        ImGui.Text(desynth.ClassJobId.ToString());

        ImGui.TextDisabled("Desynth Level:");
        ImGui.SameLine();
        ImGui.Text($"{desynth.DesynthLevel:F1}");

        if (desynth.Results.Count > 0 &&
            ImGui.BeginTable("DesynthResults", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
        {
            ImGui.TableSetupColumn("Item ID", ImGuiTableColumnFlags.WidthFixed, 80);
            ImGui.TableSetupColumn("Count", ImGuiTableColumnFlags.WidthFixed, 50);
            ImGui.TableSetupColumn("HQ", ImGuiTableColumnFlags.WidthFixed, 30);
            ImGui.TableHeadersRow();

            foreach (var result in desynth.Results)
            {
                ImGui.TableNextRow();

                ImGui.TableNextColumn();
                ImGui.Text(result.ItemId.ToString());

                ImGui.TableNextColumn();
                ImGui.Text(result.Count.ToString());

                ImGui.TableNextColumn();
                ImGui.Text(result.IsHq ? "Yes" : "");
            }

            ImGui.EndTable();
        }
    }

    // ── Send History ───────────────────────────────────────────────────

    private void DrawSendHistory()
    {
        if (!ImGui.CollapsingHeader("Send History", ImGuiTreeNodeFlags.DefaultOpen))
            return;

        var history = eventTrackingService.SendHistory;

        if (history.Count == 0)
        {
            ImGui.TextDisabled("No send history.");
            return;
        }

        if (selectedHistoryIndex >= history.Count)
            selectedHistoryIndex = -1;

        if (ImGui.BeginTable("SendHistoryTable", 3,
                ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY,
                new Vector2(0, 150)))
        {
            ImGui.TableSetupColumn("Time", ImGuiTableColumnFlags.WidthFixed, 80);
            ImGui.TableSetupColumn("Count", ImGuiTableColumnFlags.WidthFixed, 50);
            ImGui.TableSetupColumn("Status", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableHeadersRow();

            for (var i = 0; i < history.Count; i++)
            {
                var entry = history[i];
                ImGui.TableNextRow();

                ImGui.TableNextColumn();
                var isSelected = selectedHistoryIndex == i;
                if (ImGui.Selectable(entry.Timestamp.ToString("HH:mm:ss"), isSelected, ImGuiSelectableFlags.SpanAllColumns))
                    selectedHistoryIndex = isSelected ? -1 : i;

                ImGui.TableNextColumn();
                ImGui.Text(entry.Count.ToString());

                ImGui.TableNextColumn();
                if (entry.Success)
                    ImGui.TextColored(new Vector4(0.3f, 1, 0.3f, 1), "OK");
                else
                    ImGui.TextColored(new Vector4(1, 0.3f, 0.3f, 1), "Error");
            }

            ImGui.EndTable();
        }

        if (selectedHistoryIndex >= 0 && selectedHistoryIndex < history.Count)
        {
            DrawHistoryDetail(history[selectedHistoryIndex]);
        }
    }

    private static void DrawHistoryDetail(SendHistoryEntry entry)
    {
        ImGui.Indent();

        if (!string.IsNullOrEmpty(entry.Error))
        {
            ImGui.TextColored(new Vector4(1, 0.3f, 0.3f, 1), entry.Error);
            ImGui.Spacing();
        }

        for (var i = 0; i < entry.Events.Count; i++)
        {
            var evt = entry.Events[i];
            if (ImGui.TreeNode($"{evt.EventType}  {evt.OccurredAt ?? ""}###{i}"))
            {
                DrawEventDetail(evt);
                ImGui.TreePop();
            }
        }

        ImGui.Unindent();
    }
}
