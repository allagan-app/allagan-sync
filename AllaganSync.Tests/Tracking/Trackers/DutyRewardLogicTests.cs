using AllaganSync.Models;
using AllaganSync.Tracking.Trackers;
using Xunit;

namespace AllaganSync.Tests.Tracking.Trackers;

public class DutyRewardLogicTests
{
    private long currentTick;
    private readonly DutyRewardLogic logic;

    public DutyRewardLogicTests()
    {
        currentTick = 1000;
        logic = new DutyRewardLogic(() => currentTick);
    }

    [Fact]
    public void DutyCompleted_WithItems_ProducesEvent()
    {
        logic.ProcessDutyCompleted(territory: 1042, map: 200);

        currentTick += 10;
        logic.ProcessInventoryAdd(1788, 1);
        logic.ProcessInventoryAdd(9797, 1);

        currentTick = 1000 + DutyRewardLogic.CollectWindowMs;
        var result = logic.ProcessTick();

        Assert.NotNull(result);
        Assert.Equal("duty_reward", result.EventType);
        var payload = Assert.IsType<DutyRewardPayload>(result.Payload);
        Assert.Equal((ushort)1042, payload.TerritoryTypeId);
        Assert.Equal(200u, payload.MapId);
        Assert.Equal(2, payload.Items.Count);
        Assert.Equal(1788u, payload.Items[0].ItemId);
        Assert.Equal(9797u, payload.Items[1].ItemId);
    }

    [Fact]
    public void DutyCompleted_NoItems_StillFiresEvent()
    {
        logic.ProcessDutyCompleted(territory: 1042, map: 200);

        currentTick += DutyRewardLogic.CollectWindowMs;
        var result = logic.ProcessTick();

        Assert.NotNull(result);
        var payload = Assert.IsType<DutyRewardPayload>(result.Payload);
        Assert.Empty(payload.Items);
        Assert.Equal((ushort)1042, payload.TerritoryTypeId);
    }

    [Fact]
    public void TimerTooEarly_NoEvent()
    {
        logic.ProcessDutyCompleted(territory: 1042, map: 200);
        logic.ProcessInventoryAdd(1788, 1);

        currentTick += DutyRewardLogic.CollectWindowMs - 1;
        var result = logic.ProcessTick();

        Assert.Null(result);
    }

    [Fact]
    public void InventoryAddBeforeDutyCompleted_Ignored()
    {
        logic.ProcessInventoryAdd(9999, 1);

        logic.ProcessDutyCompleted(territory: 1042, map: 200);
        currentTick += 10;
        logic.ProcessInventoryAdd(1788, 1);

        currentTick = 1000 + DutyRewardLogic.CollectWindowMs;
        var result = logic.ProcessTick();

        Assert.NotNull(result);
        var payload = Assert.IsType<DutyRewardPayload>(result.Payload);
        Assert.Single(payload.Items);
        Assert.Equal(1788u, payload.Items[0].ItemId);
    }

    [Fact]
    public void InventoryChange_SameItemId_UsesQuantityDiff()
    {
        logic.ProcessDutyCompleted(territory: 1042, map: 200);

        currentTick += 10;
        logic.ProcessInventoryChange(oldItemId: 9797, oldQuantity: 4, newItemId: 9797, newQuantity: 5);

        currentTick = 1000 + DutyRewardLogic.CollectWindowMs;
        var result = logic.ProcessTick();

        Assert.NotNull(result);
        var payload = Assert.IsType<DutyRewardPayload>(result.Payload);
        Assert.Single(payload.Items);
        Assert.Equal(9797u, payload.Items[0].ItemId);
        Assert.Equal(1, payload.Items[0].Count);
    }

    [Fact]
    public void InventoryChange_NegativeDiff_NotAdded()
    {
        logic.ProcessDutyCompleted(territory: 1042, map: 200);

        currentTick += 10;
        logic.ProcessInventoryChange(oldItemId: 9797, oldQuantity: 10, newItemId: 9797, newQuantity: 5);

        currentTick = 1000 + DutyRewardLogic.CollectWindowMs;
        var result = logic.ProcessTick();

        Assert.NotNull(result);
        var payload = Assert.IsType<DutyRewardPayload>(result.Payload);
        Assert.Empty(payload.Items);
    }

    [Fact]
    public void WindowReset_SecondDutyHasCleanState()
    {
        // First duty
        logic.ProcessDutyCompleted(territory: 1042, map: 200);
        currentTick += 10;
        logic.ProcessInventoryAdd(1788, 1);

        currentTick = 1000 + DutyRewardLogic.CollectWindowMs;
        var first = logic.ProcessTick();
        Assert.NotNull(first);

        // Second duty — clean state
        currentTick += 5000;
        var secondStart = currentTick;
        logic.ProcessDutyCompleted(territory: 1043, map: 300);
        currentTick += 10;
        logic.ProcessInventoryAdd(2000, 2);

        currentTick = secondStart + DutyRewardLogic.CollectWindowMs;
        var second = logic.ProcessTick();

        Assert.NotNull(second);
        var payload = Assert.IsType<DutyRewardPayload>(second.Payload);
        Assert.Single(payload.Items);
        Assert.Equal(2000u, payload.Items[0].ItemId);
        Assert.Equal((ushort)1043, payload.TerritoryTypeId);
        Assert.Equal(300u, payload.MapId);
    }
}
