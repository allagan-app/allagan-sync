using System;
using AllaganSync.Models;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace AllaganSync.Tracking.Trackers;

public unsafe class ChestLootTracker : IGameEventTracker
{
    private const string LootAddedSig =
        "48 89 5C 24 ?? 55 56 57 41 54 41 55 41 56 41 57 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 ?? ?? ?? ?? 44 89 4C 24";

    private delegate byte LootAddedDelegate(
        Loot* lootWindow,
        uint chestObjectId,
        uint chestItemIndex,
        uint itemId,
        ushort itemCount,
        nint materia,
        nint glamourStainIds,
        uint glamourItemId,
        RollState rollState,
        RollResult rollResult,
        float time,
        float maxTime,
        byte rollValue,
        byte a14,
        LootMode lootMode,
        int a16,
        uint a17);

    private readonly IPluginLog log;
    private readonly IClientState clientState;
    private readonly IObjectTable objectTable;
    private Hook<LootAddedDelegate>? lootAddedHook;

    public string EventKey => "chest_loot";
    public string DisplayName => "Chest Loot (Debug)";
    public bool IsAvailable { get; }
    public bool IsEnabled { get; set; }
    public string? RequiredAbility => null;

#pragma warning disable CS0067 // Required by IGameEventTracker interface, debug tracker does not fire events
    public event Action<TrackedEvent>? EventTracked;
#pragma warning restore CS0067

    public ChestLootTracker(
        IPluginLog log,
        IClientState clientState,
        IObjectTable objectTable,
        IGameInteropProvider gameInteropProvider)
    {
        this.log = log;
        this.clientState = clientState;
        this.objectTable = objectTable;

        try
        {
            lootAddedHook = gameInteropProvider.HookFromSignature<LootAddedDelegate>(
                LootAddedSig,
                OnLootAdded);
            lootAddedHook.Enable();
            IsAvailable = true;
            log.Info("ChestLootTracker: Hook installed successfully.");
        }
        catch (Exception ex)
        {
            IsAvailable = false;
            log.Warning($"ChestLootTracker: Failed to install hook, chest loot tracking unavailable. {ex.Message}");
        }
    }

    private byte OnLootAdded(
        Loot* lootWindow,
        uint chestObjectId,
        uint chestItemIndex,
        uint itemId,
        ushort itemCount,
        nint materia,
        nint glamourStainIds,
        uint glamourItemId,
        RollState rollState,
        RollResult rollResult,
        float time,
        float maxTime,
        byte rollValue,
        byte a14,
        LootMode lootMode,
        int a16,
        uint a17)
    {
        var result = lootAddedHook!.Original(
            lootWindow, chestObjectId, chestItemIndex, itemId, itemCount,
            materia, glamourStainIds, glamourItemId,
            rollState, rollResult, time, maxTime, rollValue, a14,
            lootMode, a16, a17);

        if (!IsEnabled)
            return result;

        try
        {
            log.Info(
                $"ChestLootTracker: LootAdded fired!\n" +
                $"  chestObjectId={chestObjectId}, chestItemIndex={chestItemIndex}, itemId={itemId}, itemCount={itemCount}\n" +
                $"  rollState={rollState} ({(int)rollState}), rollResult={rollResult} ({(int)rollResult}), rollValue={rollValue}\n" +
                $"  time={time}, maxTime={maxTime}, lootMode={lootMode} ({(int)lootMode})\n" +
                $"  glamourItemId={glamourItemId}, a14={a14}, a16={a16}, a17={a17}");

            var chestObject = objectTable.SearchByEntityId(chestObjectId);
            if (chestObject != null)
            {
                var pos = chestObject.Position;
                log.Info(
                    $"ChestLootTracker: Chest Object Details:\n" +
                    $"  BaseId={chestObject.BaseId}, ObjectKind={chestObject.ObjectKind}, SubKind={chestObject.SubKind}\n" +
                    $"  Position=({pos.X}, {pos.Y}, {pos.Z})\n" +
                    $"  Name=\"{chestObject.Name}\"");
            }
            else
            {
                log.Info($"ChestLootTracker: Chest object not found for entityId={chestObjectId}");
            }

            log.Info(
                $"ChestLootTracker: Context:\n" +
                $"  TerritoryType={clientState.TerritoryType}, MapId={clientState.MapId}");
        }
        catch (Exception ex)
        {
            log.Error($"ChestLootTracker: Error processing loot: {ex}");
        }

        return result;
    }

    public void Dispose()
    {
        lootAddedHook?.Dispose();
    }
}
