using System;
using System.Collections.Generic;
using AllaganSync.Models;
using Dalamud.Game.Inventory;
using Dalamud.Game.Inventory.InventoryEventArgTypes;
using Dalamud.Game.Chat;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Network;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.Network;

namespace AllaganSync.Tracking.Trackers;

public unsafe class ChestLootTracker : IGameEventTracker
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

    /// <summary>Chat type for "Unable to obtain X. You already possess one." (unique item already owned).</summary>
    private const int ChatTypeAlreadyObtained = 2108;

    /// <summary>Chat type for "You obtain X." (item received from chest).</summary>
    private const int ChatTypeItemObtained = 2110;

    private readonly IPluginLog log;
    private readonly IClientState clientState;
    private readonly IObjectTable objectTable;
    private readonly IGameInventory gameInventory;
    private readonly IFramework framework;
    private readonly IChatGui chatGui;
    private readonly ChestLootLogic logic = new();
    private Hook<PacketDispatcher.Delegates.HandleSpawnTreasurePacket>? spawnTreasureHook;
    private Hook<OpenTreasureDelegate>? openTreasureHook;
    private Hook<LootAddedDelegate>? lootAddedHook;

    public string EventKey => "chest_loot";
    public string DisplayName => "Chest Loot";
    public bool IsAvailable { get; }
    public bool IsEnabled { get; set; }
    public string? RequiredAbility => null;

    public event Action<TrackedEvent>? EventTracked;

    public ChestLootTracker(
        IPluginLog log,
        IClientState clientState,
        IObjectTable objectTable,
        IGameInventory gameInventory,
        IFramework framework,
        IChatGui chatGui,
        IGameInteropProvider gameInteropProvider)
    {
        this.log = log;
        this.clientState = clientState;
        this.objectTable = objectTable;
        this.gameInventory = gameInventory;
        this.framework = framework;
        this.chatGui = chatGui;

        var hooksInstalled = 0;

        try
        {
            spawnTreasureHook = gameInteropProvider.HookFromAddress<PacketDispatcher.Delegates.HandleSpawnTreasurePacket>(
                PacketDispatcher.MemberFunctionPointers.HandleSpawnTreasurePacket,
                OnSpawnTreasurePacket);
            spawnTreasureHook.Enable();
            hooksInstalled++;
        }
        catch (Exception ex)
        {
            log.Warning($"ChestLootTracker: SpawnTreasure hook failed. {ex.Message}");
        }

        try
        {
            openTreasureHook = gameInteropProvider.HookFromSignature<OpenTreasureDelegate>(
                OpenTreasureSig,
                OnOpenTreasure);
            openTreasureHook.Enable();
            hooksInstalled++;
        }
        catch (Exception ex)
        {
            log.Warning($"ChestLootTracker: OpenTreasure hook failed. {ex.Message}");
        }

        try
        {
            lootAddedHook = gameInteropProvider.HookFromSignature<LootAddedDelegate>(
                LootAddedSig,
                OnLootAdded);
            lootAddedHook.Enable();
            hooksInstalled++;
        }
        catch (Exception ex)
        {
            log.Warning($"ChestLootTracker: LootAdded hook failed. {ex.Message}");
        }

        gameInventory.InventoryChangedRaw += OnInventoryChanged;
        chatGui.ChatMessage += OnChatMessage;
        framework.Update += OnFrameworkUpdate;

        IsAvailable = hooksInstalled > 0;
        log.Info($"ChestLootTracker: {hooksInstalled}/3 hooks installed.");
    }

    private void OnSpawnTreasurePacket(uint targetId, SpawnTreasurePacket* packet)
    {
        spawnTreasureHook!.Original(targetId, packet);
    }

    private void OnOpenTreasure(uint targetId, byte* packet)
    {
        openTreasureHook!.Original(targetId, packet);

        if (!IsEnabled)
            return;

        try
        {
            var chestObject = objectTable.SearchByEntityId(targetId);
            if (chestObject == null)
                return;

            var treasure = (Treasure*)chestObject.Address;
            var pos = treasure->Position;

            logic.ProcessChestOpen(
                treasure->BaseId, targetId, (byte)treasure->CofferKind,
                pos.X, pos.Y, pos.Z,
                (ushort)clientState.TerritoryType, clientState.MapId);

            log.Debug($"ChestLootTracker: Chest opened — BaseId={treasure->BaseId}, CofferKind={treasure->CofferKind}");
        }
        catch (Exception ex)
        {
            log.Error($"ChestLootTracker: Error in OpenTreasure: {ex}");
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
            logic.ProcessLootAdded(chestObjectId, chestItemIndex, itemId, itemCount, time, maxTime);
        }
        catch (Exception ex)
        {
            log.Error($"ChestLootTracker: Error in LootAdded: {ex}");
        }

        return result;
    }

    private void OnInventoryChanged(IReadOnlyCollection<InventoryEventArgs> events)
    {
        if (!IsEnabled || !logic.IsCollecting)
            return;

        foreach (var evt in events)
        {
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

    private void OnChatMessage(IHandleableChatMessage message)
    {
        if (!IsEnabled || !logic.IsCollecting)
            return;

        if ((int)message.LogKind is not (ChatTypeAlreadyObtained or ChatTypeItemObtained))
            return;

        foreach (var payload in message.Message.Payloads)
        {
            if (payload is ItemPayload itemPayload)
            {
                logic.ProcessChatItem(itemPayload.ItemId);
                log.Debug($"ChestLootTracker: Chat item detected — type={(int)message.LogKind}, itemId={itemPayload.ItemId}");
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

        if (trackedEvent.Payload is ChestLootPayload payload)
            log.Debug($"ChestLootTracker: Captured {payload.Items.Count} items from chest BaseId={payload.ChestBaseId}.");
    }

    public void Dispose()
    {
        gameInventory.InventoryChangedRaw -= OnInventoryChanged;
        chatGui.ChatMessage -= OnChatMessage;
        framework.Update -= OnFrameworkUpdate;
        spawnTreasureHook?.Dispose();
        openTreasureHook?.Dispose();
        lootAddedHook?.Dispose();
    }
}
