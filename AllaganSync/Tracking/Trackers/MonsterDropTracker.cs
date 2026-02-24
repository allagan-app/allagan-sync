using System;
using System.Collections.Generic;
using AllaganSync.Models;
using Dalamud.Game.Inventory;
using Dalamud.Game.Inventory.InventoryEventArgTypes;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Network;

namespace AllaganSync.Tracking.Trackers;

public unsafe class MonsterDropTracker : IGameEventTracker
{
    private readonly IPluginLog log;
    private readonly IClientState clientState;
    private readonly IObjectTable objectTable;
    private readonly IGameInventory gameInventory;
    private readonly IFramework framework;
    private readonly MonsterDropLogic logic = new();
    private Hook<PacketDispatcher.Delegates.HandleActorControlPacket>? actorControlHook;

    public string EventKey => "monster_drop";
    public string DisplayName => "Monster Drops";
    public bool IsAvailable { get; }
    public bool IsEnabled { get; set; }
    public string? RequiredAbility => null;

    public event Action<TrackedEvent>? EventTracked;

    public MonsterDropTracker(
        IPluginLog log,
        IClientState clientState,
        IObjectTable objectTable,
        IGameInventory gameInventory,
        IFramework framework,
        IGameInteropProvider gameInteropProvider)
    {
        this.log = log;
        this.clientState = clientState;
        this.objectTable = objectTable;
        this.gameInventory = gameInventory;
        this.framework = framework;

        try
        {
            actorControlHook = gameInteropProvider.HookFromAddress<PacketDispatcher.Delegates.HandleActorControlPacket>(
                PacketDispatcher.MemberFunctionPointers.HandleActorControlPacket,
                OnActorControlPacket);
            actorControlHook.Enable();
            IsAvailable = true;
            log.Info("MonsterDropTracker: ActorControl hook installed.");
        }
        catch (Exception ex)
        {
            IsAvailable = false;
            log.Warning($"MonsterDropTracker: Hook failed. {ex.Message}");
        }

        gameInventory.InventoryChangedRaw += OnInventoryChanged;
        framework.Update += OnFrameworkUpdate;
    }

    private void OnActorControlPacket(
        uint entityId, uint category,
        uint arg1, uint arg2, uint arg3, uint arg4,
        uint arg5, uint arg6, uint arg7, uint arg8,
        GameObjectId targetId, bool isRecorded)
    {
        actorControlHook!.Original(entityId, category, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, targetId, isRecorded);

        if (!IsEnabled || isRecorded || category != 0x6)
            return;

        try
        {
            var obj = objectTable.SearchByEntityId(entityId);
            if (obj == null || (byte)obj.ObjectKind != (byte)ObjectKind.BattleNpc)
                return;

            var subKind = (BattleNpcSubKind)obj.SubKind;
            if (subKind is BattleNpcSubKind.Pet or BattleNpcSubKind.Buddy or BattleNpcSubKind.RaceChocobo)
                return;

            logic.RecordDeath(obj.BaseId, clientState.TerritoryType, clientState.MapId);
        }
        catch (Exception ex)
        {
            log.Error($"MonsterDropTracker: Error in ActorControl: {ex}");
        }
    }

    private void OnInventoryChanged(IReadOnlyCollection<InventoryEventArgs> events)
    {
        if (!IsEnabled || !logic.IsCollecting)
            return;

        foreach (var evt in events)
        {
            if (evt.Item.ContainerType == GameInventoryType.DamagedGear)
                continue;

            switch (evt.Type)
            {
                case GameInventoryEvent.Added when evt is InventoryItemAddedArgs { Item: var item }:
                    logic.ProcessInventoryAdd(item.ItemId, item.Quantity);
                    break;
                case GameInventoryEvent.Changed when evt is InventoryItemChangedArgs { OldItemState: var oldItem, Item: var newItem }:
                    logic.ProcessInventoryChange(oldItem.ItemId, oldItem.Quantity, newItem.ItemId, newItem.Quantity);
                    break;
            }
        }
    }

    private void OnFrameworkUpdate(IFramework _)
    {
        var trackedEvent = logic.ProcessTick();
        if (trackedEvent == null)
            return;

        EventTracked?.Invoke(trackedEvent);

        if (trackedEvent.Payload is MonsterDropPayload payload)
            log.Debug($"MonsterDropTracker: Captured {payload.Deaths.Count} deaths, {payload.Items.Count} items.");
    }

    public void Dispose()
    {
        gameInventory.InventoryChangedRaw -= OnInventoryChanged;
        framework.Update -= OnFrameworkUpdate;
        actorControlHook?.Dispose();
    }
}
