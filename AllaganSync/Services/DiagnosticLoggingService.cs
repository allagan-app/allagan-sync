#if DEBUG
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Inventory;
using Dalamud.Game.Inventory.InventoryEventArgTypes;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Network;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.Network;

namespace AllaganSync.Services;

public unsafe class DiagnosticLoggingService : IDisposable
{
    private const string OpenTreasureSig =
        "40 53 48 83 EC ?? 48 8B DA 48 8D 0D ?? ?? ?? ?? 8B 52 ?? E8 ?? ?? ?? ?? 48 85 C0 74 ?? F3 0F 10 5B";

    private const string LootAddedSig =
        "48 89 5C 24 ?? 55 56 57 41 54 41 55 41 56 41 57 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 ?? ?? ?? ?? 44 89 4C 24";

    private delegate void OpenTreasureDelegate(uint targetId, byte* packet);

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
    private readonly ICondition condition;
    private readonly IDutyState dutyState;
    private readonly IObjectTable objectTable;
    private readonly IGameInventory gameInventory;

    private Hook<PacketDispatcher.Delegates.HandleActorControlPacket>? actorControlHook;
    private Hook<OpenTreasureDelegate>? openTreasureHook;
    private Hook<LootAddedDelegate>? lootAddedHook;

    private readonly Stopwatch stopwatch = new();
    private bool enabled;

    public bool IsEnabled
    {
        get => enabled;
        set
        {
            if (enabled == value)
                return;

            enabled = value;

            if (value)
            {
                ResetBaseline();
            }
        }
    }

    public DiagnosticLoggingService(
        IPluginLog log,
        IClientState clientState,
        ICondition condition,
        IDutyState dutyState,
        IObjectTable objectTable,
        IGameInventory gameInventory,
        IGameInteropProvider gameInteropProvider)
    {
        this.log = log;
        this.clientState = clientState;
        this.condition = condition;
        this.dutyState = dutyState;
        this.objectTable = objectTable;
        this.gameInventory = gameInventory;

        // Subscribe to Dalamud events (always subscribed, but only log when enabled)
        clientState.TerritoryChanged += OnTerritoryChanged;
        condition.ConditionChange += OnConditionChange;
        dutyState.DutyStarted += OnDutyStarted;
        dutyState.DutyCompleted += OnDutyCompleted;
        dutyState.DutyWiped += OnDutyWiped;
        dutyState.DutyRecommenced += OnDutyRecommenced;
        gameInventory.InventoryChangedRaw += OnInventoryChanged;

        // Install own hooks (chain-safe — Dalamud supports multiple hooks on same address)
        try
        {
            actorControlHook = gameInteropProvider.HookFromAddress<PacketDispatcher.Delegates.HandleActorControlPacket>(
                PacketDispatcher.MemberFunctionPointers.HandleActorControlPacket,
                OnActorControlPacket);
            actorControlHook.Enable();
        }
        catch (Exception ex)
        {
            log.Warning($"DiagnosticLogging: ActorControl hook failed. {ex.Message}");
        }

        try
        {
            openTreasureHook = gameInteropProvider.HookFromSignature<OpenTreasureDelegate>(
                OpenTreasureSig,
                OnOpenTreasure);
            openTreasureHook.Enable();
        }
        catch (Exception ex)
        {
            log.Warning($"DiagnosticLogging: OpenTreasure hook failed. {ex.Message}");
        }

        try
        {
            lootAddedHook = gameInteropProvider.HookFromSignature<LootAddedDelegate>(
                LootAddedSig,
                OnLootAdded);
            lootAddedHook.Enable();
        }
        catch (Exception ex)
        {
            log.Warning($"DiagnosticLogging: LootAdded hook failed. {ex.Message}");
        }
    }

    private void ResetBaseline()
    {
        stopwatch.Restart();
    }

    private long OffsetMs => stopwatch.ElapsedMilliseconds;

    private void Log(string category, string data)
    {
        if (!enabled)
            return;

        log.Info($"[DIAG +{OffsetMs}ms] {category} | {data}");
    }

    // ── Territory ────────────────────────────────────────────────────

    private void OnTerritoryChanged(ushort territoryId)
    {
        if (!enabled)
            return;

        ResetBaseline();
        Log("TERRITORY", $"territoryId={territoryId}");
    }

    // ── Condition ────────────────────────────────────────────────────

    private void OnConditionChange(ConditionFlag flag, bool value)
    {
        if (!enabled)
            return;

        if (flag is ConditionFlag.BoundByDuty or ConditionFlag.BoundByDuty56)
        {
            Log("CONDITION", $"flag={flag} | value={value}");
        }
    }

    // ── Duty Lifecycle ───────────────────────────────────────────────

    private void OnDutyStarted(object? sender, ushort territoryId)
    {
        if (!enabled)
            return;

        ResetBaseline();
        Log("DUTY_STARTED", $"territoryId={territoryId}");
    }

    private void OnDutyCompleted(object? sender, ushort territoryId)
    {
        Log("DUTY_COMPLETED", $"territoryId={territoryId}");
    }

    private void OnDutyWiped(object? sender, ushort territoryId)
    {
        Log("DUTY_WIPED", $"territoryId={territoryId}");
    }

    private void OnDutyRecommenced(object? sender, ushort territoryId)
    {
        Log("DUTY_RECOMMENCED", $"territoryId={territoryId}");
    }

    // ── Monster Death (ActorControl category 0x6) ────────────────────

    private void OnActorControlPacket(
        uint entityId, uint category,
        uint arg1, uint arg2, uint arg3, uint arg4,
        uint arg5, uint arg6, uint arg7, uint arg8,
        GameObjectId targetId, bool isRecorded)
    {
        actorControlHook!.Original(entityId, category, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, targetId, isRecorded);

        if (!enabled || isRecorded || category != 0x6)
            return;

        try
        {
            var obj = objectTable.SearchByEntityId(entityId);
            if (obj is not ICharacter character || (byte)character.ObjectKind != (byte)ObjectKind.BattleNpc)
                return;

            var subKind = (BattleNpcSubKind)character.SubKind;
            if (subKind is BattleNpcSubKind.Pet or BattleNpcSubKind.Buddy or BattleNpcSubKind.RaceChocobo)
                return;

            Log("DEATH", $"entityId=0x{entityId:X} | bnpcBaseId={character.BaseId} | bnpcNameId={character.NameId}");
        }
        catch (Exception ex)
        {
            log.Error($"DiagnosticLogging: Error in ActorControl: {ex}");
        }
    }

    // ── Chest Open ───────────────────────────────────────────────────

    private void OnOpenTreasure(uint targetId, byte* packet)
    {
        openTreasureHook!.Original(targetId, packet);

        if (!enabled)
            return;

        try
        {
            var chestObject = objectTable.SearchByEntityId(targetId);
            if (chestObject == null)
                return;

            var treasure = (Treasure*)chestObject.Address;
            Log("CHEST_OPEN", $"baseId={treasure->BaseId} | entityId=0x{targetId:X} | cofferKind={treasure->CofferKind}");
        }
        catch (Exception ex)
        {
            log.Error($"DiagnosticLogging: Error in OpenTreasure: {ex}");
        }
    }

    // ── Loot Added ───────────────────────────────────────────────────

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

        if (!enabled)
            return result;

        Log("LOOT_ADDED", $"chestObjectId=0x{chestObjectId:X} | itemId={itemId} | qty={itemCount} | rollState={rollState} | lootMode={lootMode}");

        return result;
    }

    // ── Inventory ────────────────────────────────────────────────────

    private void OnInventoryChanged(IReadOnlyCollection<InventoryEventArgs> events)
    {
        if (!enabled)
            return;

        foreach (var evt in events)
        {
            switch (evt.Type)
            {
                case GameInventoryEvent.Added when evt is InventoryItemAddedArgs { Item: var item }:
                    Log("INVENTORY", $"type=Added | itemId={item.ItemId} | qty={item.Quantity} | container={item.ContainerType}");
                    break;
                case GameInventoryEvent.Changed when evt is InventoryItemChangedArgs { OldItemState: var oldItem, Item: var newItem }:
                    Log("INVENTORY", $"type=Changed | itemId={newItem.ItemId} | oldQty={oldItem.Quantity} | newQty={newItem.Quantity} | container={newItem.ContainerType}");
                    break;
                case GameInventoryEvent.Removed when evt is InventoryItemRemovedArgs { Item: var item }:
                    Log("INVENTORY", $"type=Removed | itemId={item.ItemId} | qty={item.Quantity} | container={item.ContainerType}");
                    break;
            }
        }
    }

    // ── Dispose ──────────────────────────────────────────────────────

    public void Dispose()
    {
        clientState.TerritoryChanged -= OnTerritoryChanged;
        condition.ConditionChange -= OnConditionChange;
        dutyState.DutyStarted -= OnDutyStarted;
        dutyState.DutyCompleted -= OnDutyCompleted;
        dutyState.DutyWiped -= OnDutyWiped;
        dutyState.DutyRecommenced -= OnDutyRecommenced;
        gameInventory.InventoryChangedRaw -= OnInventoryChanged;
        actorControlHook?.Dispose();
        openTreasureHook?.Dispose();
        lootAddedHook?.Dispose();
    }
}
#endif
