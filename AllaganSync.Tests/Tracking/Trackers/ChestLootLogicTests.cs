using AllaganSync.Models;
using AllaganSync.Tracking.Trackers;
using Xunit;

namespace AllaganSync.Tests.Tracking.Trackers;

public class ChestLootLogicTests
{
    private long currentTick;
    private readonly ChestLootLogic logic;

    public ChestLootLogicTests()
    {
        currentTick = 1000;
        logic = new ChestLootLogic(() => currentTick);
    }

    private void OpenChest(
        uint baseId = 100, uint entityId = 200, byte cofferKind = 1,
        float posX = 10f, float posY = 20f, float posZ = 30f,
        ushort territory = 500, uint map = 600)
    {
        logic.ProcessChestOpen(baseId, entityId, cofferKind, posX, posY, posZ, territory, map);
    }

    [Fact]
    public void SoloHappyPath_InventoryAdd_ProducesEvent()
    {
        OpenChest();
        logic.ProcessInventoryAdd(1001, 3);

        currentTick += ChestLootLogic.CollectWindowMs;
        var result = logic.ProcessTick();

        Assert.NotNull(result);
        Assert.Equal("chest_loot", result.EventType);
        var payload = Assert.IsType<ChestLootPayload>(result.Payload);
        Assert.Single(payload.Items);
        Assert.Equal(1001u, payload.Items[0].ItemId);
        Assert.Equal(3, payload.Items[0].Count);
    }

    [Fact]
    public void TimerTooEarly_NoEvent()
    {
        OpenChest();
        logic.ProcessInventoryAdd(1001, 1);

        currentTick += ChestLootLogic.CollectWindowMs - 1;
        var result = logic.ProcessTick();

        Assert.Null(result);
    }

    [Fact]
    public void LootAdded_DedupByChestItemIndex()
    {
        OpenChest(entityId: 200);

        logic.ProcessLootAdded(200, chestItemIndex: 0, itemId: 1001, itemCount: 1, time: 60f, maxTime: 60f);
        logic.ProcessLootAdded(200, chestItemIndex: 0, itemId: 1001, itemCount: 1, time: 60f, maxTime: 60f);

        currentTick += ChestLootLogic.CollectWindowMs;
        var result = logic.ProcessTick();

        Assert.NotNull(result);
        var payload = Assert.IsType<ChestLootPayload>(result.Payload);
        Assert.Single(payload.Items);
    }

    [Fact]
    public void LootAdded_TimeLessThanMaxTime_Ignored()
    {
        OpenChest(entityId: 200);

        logic.ProcessLootAdded(200, chestItemIndex: 0, itemId: 1001, itemCount: 1, time: 30f, maxTime: 60f);

        currentTick += ChestLootLogic.CollectWindowMs;
        var result = logic.ProcessTick();

        Assert.NotNull(result);
        var payload = Assert.IsType<ChestLootPayload>(result.Payload);
        Assert.Empty(payload.Items);
    }

    [Fact]
    public void LootAdded_PreferredOverInventory()
    {
        OpenChest(entityId: 200);

        logic.ProcessLootAdded(200, chestItemIndex: 0, itemId: 2001, itemCount: 5, time: 60f, maxTime: 60f);
        logic.ProcessInventoryAdd(3001, 1);

        currentTick += ChestLootLogic.CollectWindowMs;
        var result = logic.ProcessTick();

        Assert.NotNull(result);
        var payload = Assert.IsType<ChestLootPayload>(result.Payload);
        Assert.Single(payload.Items);
        Assert.Equal(2001u, payload.Items[0].ItemId);
        Assert.Equal(5, payload.Items[0].Count);
    }

    [Fact]
    public void FallbackToInventory_WhenNoLootAdded()
    {
        OpenChest(entityId: 200);

        logic.ProcessInventoryAdd(3001, 2);
        logic.ProcessInventoryAdd(3002, 1);

        currentTick += ChestLootLogic.CollectWindowMs;
        var result = logic.ProcessTick();

        Assert.NotNull(result);
        var payload = Assert.IsType<ChestLootPayload>(result.Payload);
        Assert.Equal(2, payload.Items.Count);
        Assert.Equal(3001u, payload.Items[0].ItemId);
        Assert.Equal(2, payload.Items[0].Count);
        Assert.Equal(3002u, payload.Items[1].ItemId);
    }

    [Fact]
    public void NoItems_StillFiresEventWithCoordinates()
    {
        OpenChest(baseId: 42, cofferKind: 3, posX: 1.5f, posY: 2.5f, posZ: 3.5f, territory: 800, map: 900);

        currentTick += ChestLootLogic.CollectWindowMs;
        var result = logic.ProcessTick();

        Assert.NotNull(result);
        var payload = Assert.IsType<ChestLootPayload>(result.Payload);
        Assert.Empty(payload.Items);
        Assert.Equal(42u, payload.ChestBaseId);
        Assert.Equal(3, payload.CofferKind);
        Assert.Equal(1.5f, payload.PositionX);
        Assert.Equal(2.5f, payload.PositionY);
        Assert.Equal(3.5f, payload.PositionZ);
        Assert.Equal((ushort)800, payload.TerritoryTypeId);
        Assert.Equal(900u, payload.MapId);
    }

    [Fact]
    public void InventoryChange_SameItemId_UsesQuantityDiff()
    {
        OpenChest();

        logic.ProcessInventoryChange(oldItemId: 1001, oldQuantity: 5, newItemId: 1001, newQuantity: 8);

        currentTick += ChestLootLogic.CollectWindowMs;
        var result = logic.ProcessTick();

        Assert.NotNull(result);
        var payload = Assert.IsType<ChestLootPayload>(result.Payload);
        Assert.Single(payload.Items);
        Assert.Equal(1001u, payload.Items[0].ItemId);
        Assert.Equal(3, payload.Items[0].Count);
    }

    [Fact]
    public void InventoryChange_DifferentItemId_UsesNewQuantity()
    {
        OpenChest();

        logic.ProcessInventoryChange(oldItemId: 1001, oldQuantity: 5, newItemId: 2002, newQuantity: 7);

        currentTick += ChestLootLogic.CollectWindowMs;
        var result = logic.ProcessTick();

        Assert.NotNull(result);
        var payload = Assert.IsType<ChestLootPayload>(result.Payload);
        Assert.Single(payload.Items);
        Assert.Equal(2002u, payload.Items[0].ItemId);
        Assert.Equal(7, payload.Items[0].Count);
    }

    [Fact]
    public void PayloadMetadata_MatchesChestOpenParams()
    {
        OpenChest(baseId: 42, entityId: 99, cofferKind: 3, posX: 1.5f, posY: 2.5f, posZ: 3.5f, territory: 800, map: 900);
        logic.ProcessInventoryAdd(1001, 1);

        currentTick += ChestLootLogic.CollectWindowMs;
        var result = logic.ProcessTick();

        Assert.NotNull(result);
        var payload = Assert.IsType<ChestLootPayload>(result.Payload);
        Assert.Equal(42u, payload.ChestBaseId);
        Assert.Equal(3, payload.CofferKind);
        Assert.Equal(1.5f, payload.PositionX);
        Assert.Equal(2.5f, payload.PositionY);
        Assert.Equal(3.5f, payload.PositionZ);
        Assert.Equal((ushort)800, payload.TerritoryTypeId);
        Assert.Equal(900u, payload.MapId);
    }

    [Fact]
    public void InventoryAddBeforeChestOpen_Ignored()
    {
        logic.ProcessInventoryAdd(1001, 1);

        OpenChest();
        logic.ProcessInventoryAdd(2002, 1);

        currentTick += ChestLootLogic.CollectWindowMs;
        var result = logic.ProcessTick();

        Assert.NotNull(result);
        var payload = Assert.IsType<ChestLootPayload>(result.Payload);
        Assert.Single(payload.Items);
        Assert.Equal(2002u, payload.Items[0].ItemId);
    }

    [Fact]
    public void InventoryChange_SameItemId_NegativeDiff_NotAdded()
    {
        OpenChest();

        logic.ProcessInventoryChange(oldItemId: 1001, oldQuantity: 10, newItemId: 1001, newQuantity: 5);

        currentTick += ChestLootLogic.CollectWindowMs;
        var result = logic.ProcessTick();

        Assert.NotNull(result);
        var payload = Assert.IsType<ChestLootPayload>(result.Payload);
        Assert.Empty(payload.Items);
    }

    [Fact]
    public void ChatItem_UsedWhenNoLootAdded()
    {
        OpenChest();
        logic.ProcessChatItem(5001);

        currentTick += ChestLootLogic.CollectWindowMs;
        var result = logic.ProcessTick();

        Assert.NotNull(result);
        var payload = Assert.IsType<ChestLootPayload>(result.Payload);
        Assert.Single(payload.Items);
        Assert.Equal(5001u, payload.Items[0].ItemId);
        Assert.Equal(1, payload.Items[0].Count);
    }

    [Fact]
    public void ChatItem_AlwaysHasCountOfOne()
    {
        OpenChest();
        logic.ProcessChatItem(5001);
        logic.ProcessChatItem(5002);

        currentTick += ChestLootLogic.CollectWindowMs;
        var result = logic.ProcessTick();

        Assert.NotNull(result);
        var payload = Assert.IsType<ChestLootPayload>(result.Payload);
        Assert.Equal(2, payload.Items.Count);
        Assert.All(payload.Items, item => Assert.Equal(1, item.Count));
    }

    [Fact]
    public void LootAdded_PreferredOverChat()
    {
        OpenChest(entityId: 200);

        logic.ProcessLootAdded(200, chestItemIndex: 0, itemId: 2001, itemCount: 5, time: 60f, maxTime: 60f);
        logic.ProcessChatItem(5001);

        currentTick += ChestLootLogic.CollectWindowMs;
        var result = logic.ProcessTick();

        Assert.NotNull(result);
        var payload = Assert.IsType<ChestLootPayload>(result.Payload);
        Assert.Single(payload.Items);
        Assert.Equal(2001u, payload.Items[0].ItemId);
        Assert.Equal(5, payload.Items[0].Count);
    }

    [Fact]
    public void Chat_PreferredOverInventory()
    {
        OpenChest();

        logic.ProcessChatItem(5001);
        logic.ProcessInventoryAdd(3001, 1);

        currentTick += ChestLootLogic.CollectWindowMs;
        var result = logic.ProcessTick();

        Assert.NotNull(result);
        var payload = Assert.IsType<ChestLootPayload>(result.Payload);
        Assert.Single(payload.Items);
        Assert.Equal(5001u, payload.Items[0].ItemId);
        Assert.Equal(1, payload.Items[0].Count);
    }

    [Fact]
    public void ChatItemBeforeChestOpen_Ignored()
    {
        logic.ProcessChatItem(5001);

        OpenChest();
        logic.ProcessChatItem(5002);

        currentTick += ChestLootLogic.CollectWindowMs;
        var result = logic.ProcessTick();

        Assert.NotNull(result);
        var payload = Assert.IsType<ChestLootPayload>(result.Payload);
        Assert.Single(payload.Items);
        Assert.Equal(5002u, payload.Items[0].ItemId);
    }

    [Fact]
    public void WindowReset_SecondChestHasCleanChatState()
    {
        // First chest
        OpenChest(baseId: 1, entityId: 10);
        logic.ProcessChatItem(5001);
        currentTick += ChestLootLogic.CollectWindowMs;
        var first = logic.ProcessTick();
        Assert.NotNull(first);

        // Second chest — should not carry over chat items from the first
        currentTick += 100;
        OpenChest(baseId: 2, entityId: 20);
        logic.ProcessChatItem(6001);
        currentTick += ChestLootLogic.CollectWindowMs;
        var second = logic.ProcessTick();

        Assert.NotNull(second);
        var payload = Assert.IsType<ChestLootPayload>(second.Payload);
        Assert.Single(payload.Items);
        Assert.Equal(6001u, payload.Items[0].ItemId);
        Assert.Equal(2u, payload.ChestBaseId);
    }

    [Fact]
    public void LootAdded_MultipleDistinctItems()
    {
        OpenChest(entityId: 200);

        logic.ProcessLootAdded(200, chestItemIndex: 0, itemId: 1001, itemCount: 1, time: 60f, maxTime: 60f);
        logic.ProcessLootAdded(200, chestItemIndex: 1, itemId: 1002, itemCount: 2, time: 60f, maxTime: 60f);
        logic.ProcessLootAdded(200, chestItemIndex: 2, itemId: 1003, itemCount: 3, time: 60f, maxTime: 60f);

        currentTick += ChestLootLogic.CollectWindowMs;
        var result = logic.ProcessTick();

        Assert.NotNull(result);
        var payload = Assert.IsType<ChestLootPayload>(result.Payload);
        Assert.Equal(3, payload.Items.Count);
    }

    [Fact]
    public void WindowReset_SecondChestHasCleanState()
    {
        // First chest
        OpenChest(baseId: 1, entityId: 10);
        logic.ProcessInventoryAdd(1001, 1);
        currentTick += ChestLootLogic.CollectWindowMs;
        var first = logic.ProcessTick();
        Assert.NotNull(first);

        // Second chest — should not carry over items from the first
        currentTick += 100;
        OpenChest(baseId: 2, entityId: 20);
        logic.ProcessInventoryAdd(2002, 1);
        currentTick += ChestLootLogic.CollectWindowMs;
        var second = logic.ProcessTick();

        Assert.NotNull(second);
        var payload = Assert.IsType<ChestLootPayload>(second.Payload);
        Assert.Single(payload.Items);
        Assert.Equal(2002u, payload.Items[0].ItemId);
        Assert.Equal(2u, payload.ChestBaseId);
    }
}
