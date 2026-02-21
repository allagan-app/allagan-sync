using System;
using System.Collections.Generic;
using AllaganSync.Models;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;

namespace AllaganSync.Tracking.Trackers;

public unsafe class DesynthTracker : IGameEventTracker
{
    private const string DesynthResultSig =
        "48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 48 83 EC ?? 49 8B D9 41 0F B6 F8 0F B7 F2 8B E9 E8 ?? ?? ?? ?? 44 0F B6 54 24 ?? 44 0F B6 CF 44 88 54 24 ?? 44 0F B7 C6 8B D5";

    // This handler fires for multiple UI events; only this value corresponds to desynth results
    private const nuint DesynthResultHandlerId = 3735552;

    private delegate void DesynthResultDelegate(nuint a1, ushort eventId, byte responseId, uint* args, byte argCount);

    private readonly IPluginLog log;
    private readonly IDataManager dataManager;
    private Hook<DesynthResultDelegate>? desynthResultHook;

    public string EventKey => "desynth_result";
    public string DisplayName => "Desynthesis Results";
    public bool IsAvailable { get; }
    public bool IsEnabled { get; set; }

    public event Action<TrackedEvent>? EventTracked;

    public DesynthTracker(
        IPluginLog log,
        IDataManager dataManager,
        IGameInteropProvider gameInteropProvider)
    {
        this.log = log;
        this.dataManager = dataManager;

        try
        {
            desynthResultHook = gameInteropProvider.HookFromSignature<DesynthResultDelegate>(
                DesynthResultSig,
                OnDesynthResult);
            desynthResultHook.Enable();
            IsAvailable = true;
            log.Info("DesynthTracker: Hook installed successfully.");
        }
        catch (Exception ex)
        {
            IsAvailable = false;
            log.Warning($"DesynthTracker: Failed to install hook, desynth tracking unavailable. {ex.Message}");
        }
    }

    private void OnDesynthResult(nuint a1, ushort eventId, byte responseId, uint* args, byte argCount)
    {
        desynthResultHook!.Original(a1, eventId, responseId, args, argCount);

        // Filter: only process actual desynth result events
        if (a1 != DesynthResultHandlerId)
            return;

        if (!IsEnabled)
            return;

        try
        {
            var agent = AgentSalvage.Instance();
            if (agent == null || agent->DesynthItemId == 0)
                return;

            var adjustedSource = ItemUtil.GetBaseId(agent->DesynthItemId);
            if (adjustedSource.Kind == ItemKind.EventItem)
                return;

            var sourceItemId = adjustedSource.ItemId;
            var itemSheet = dataManager.GetExcelSheet<Item>();
            if (itemSheet == null)
                return;

            float desynthLevel = 0;

            var item = itemSheet.GetRowOrDefault(sourceItemId);
            if (item != null)
            {
                var classJobId = item.Value.ClassJobRepair.RowId;
                desynthLevel = PlayerState.Instance()->GetDesynthesisLevel(classJobId);
            }

            var results = new List<DesynthResultItem>();
            foreach (var result in agent->DesynthResults)
            {
                if (result.ItemId == 0)
                    continue;

                var adjustedResult = ItemUtil.GetBaseId(result.ItemId);
                results.Add(new DesynthResultItem
                {
                    ItemId = adjustedResult.ItemId,
                    Count = (int)result.Quantity,
                    IsHq = adjustedResult.Kind == ItemKind.Hq,
                });
            }

            if (results.Count == 0)
                return;

            var payload = new DesynthResultPayload
            {
                SourceItemId = sourceItemId,
                DesynthLevel = desynthLevel,
                Results = results,
            };

            var trackedEvent = new TrackedEvent
            {
                EventType = EventKey,
                Payload = payload,
                OccurredAt = DateTime.UtcNow.ToString("O"),
            };

            EventTracked?.Invoke(trackedEvent);
            log.Debug($"DesynthTracker: Captured desynth of item {sourceItemId} with {results.Count} result(s).");
        }
        catch (Exception ex)
        {
            log.Error($"DesynthTracker: Error processing desynth result: {ex}");
        }
    }

    public void Dispose()
    {
        desynthResultHook?.Dispose();
    }
}
