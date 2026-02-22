using System;
using System.Collections.Generic;
using System.Linq;
using AllaganSync.Models;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;

namespace AllaganSync.Tracking.Trackers;

public unsafe class RetainerMissionTracker : IGameEventTracker
{
    private const string RetainerTaskResultSig =
        "E8 ?? ?? ?? ?? 48 89 9B ?? ?? ?? ?? 48 8B CF 48 8B 17 FF 52 48 89 83 ?? ?? ?? ?? 33 D2 48 8D 4D A0";

    private delegate void RetainerTaskResultDelegate(AgentRetainerTask* agent, nint someLuaPointer, nint packet);

    private readonly IPluginLog log;
    private readonly IDataManager dataManager;
    private readonly uint maxRetainerLevel;
    private readonly HashSet<uint> randomTaskIds;
    private Hook<RetainerTaskResultDelegate>? retainerTaskHook;

    public string EventKey => "retainer_mission_result";
    public string DisplayName => "Retainer Mission Results";
    public bool IsAvailable { get; }
    public bool IsEnabled { get; set; }
    public string? RequiredAbility => null;

    public event Action<TrackedEvent>? EventTracked;

    public RetainerMissionTracker(
        IPluginLog log,
        IDataManager dataManager,
        IGameInteropProvider gameInteropProvider)
    {
        this.log = log;
        this.dataManager = dataManager;

        var paramGrowSheet = dataManager.GetExcelSheet<ParamGrow>();
        maxRetainerLevel = paramGrowSheet?.Where(r => r.ExpToNext > 0)
            .Select(r => r.RowId)
            .DefaultIfEmpty(100u)
            .Max() ?? 100;

        var retainerTaskSheet = dataManager.GetExcelSheet<RetainerTask>();
        if (retainerTaskSheet == null)
        {
            log.Warning("RetainerMissionTracker: Failed to load RetainerTask sheet, random task filtering unavailable.");
            randomTaskIds = [];
        }
        else
        {
            randomTaskIds = retainerTaskSheet.Where(row => row.IsRandom).Select(row => row.RowId).ToHashSet();
        }

        try
        {
            retainerTaskHook = gameInteropProvider.HookFromSignature<RetainerTaskResultDelegate>(
                RetainerTaskResultSig,
                OnRetainerTaskResult);
            retainerTaskHook.Enable();
            IsAvailable = true;
            log.Info("RetainerMissionTracker: Hook installed successfully.");
        }
        catch (Exception ex)
        {
            IsAvailable = false;
            log.Warning($"RetainerMissionTracker: Failed to install hook, retainer tracking unavailable. {ex.Message}");
        }
    }

    private void OnRetainerTaskResult(AgentRetainerTask* agent, nint someLuaPointer, nint packet)
    {
        retainerTaskHook!.Original(agent, someLuaPointer, packet);

        if (!IsEnabled)
            return;

        try
        {
            var retainerManager = RetainerManager.Instance();
            if (agent == null || retainerManager == null)
                return;

            var activeRetainer = retainerManager->GetActiveRetainer();
            if (activeRetainer == null)
                return;

            if (agent->RetainerTaskId == 0)
            {
                log.Warning("RetainerMissionTracker: RetainerTaskId was 0.");
                return;
            }

            if (!randomTaskIds.Contains(agent->RetainerTaskId))
                return;

            var results = new List<RetainerMissionResultItem>();
            for (var i = 0; i < 2; i++)
            {
                var rawItemId = agent->RetainerData.RewardItemIds[i];
                if (rawItemId == 0)
                    continue;

                var adjusted = ItemUtil.GetBaseId(rawItemId);
                var count = (short)agent->RetainerData.RewardItemCount[i];
                if (count <= 0)
                    continue;

                results.Add(new RetainerMissionResultItem
                {
                    ItemId = adjusted.ItemId,
                    Count = count,
                    IsHq = adjusted.Kind == ItemKind.Hq,
                });
            }

            if (results.Count == 0)
                return;

            var payload = new RetainerMissionResultPayload
            {
                RetainerTaskId = agent->RetainerTaskId,
                RetainerLevel = activeRetainer->Level,
                MaxLevel = activeRetainer->Level >= maxRetainerLevel,
                Results = results,
            };

            var trackedEvent = new TrackedEvent
            {
                EventType = EventKey,
                Payload = payload,
                OccurredAt = DateTime.UtcNow.ToString("O"),
            };

            EventTracked?.Invoke(trackedEvent);
            log.Debug($"RetainerMissionTracker: Captured venture {agent->RetainerTaskId} with {results.Count} result(s).");
        }
        catch (Exception ex)
        {
            log.Error($"RetainerMissionTracker: Error processing retainer task result: {ex}");
        }
    }

    public void Dispose()
    {
        retainerTaskHook?.Dispose();
    }
}
