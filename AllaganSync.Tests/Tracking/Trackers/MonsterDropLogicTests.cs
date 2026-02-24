using AllaganSync.Models;
using AllaganSync.Tracking.Trackers;
using Xunit;

namespace AllaganSync.Tests.Tracking.Trackers;

public class MonsterDropLogicTests
{
    private long currentTick;
    private readonly MonsterDropLogic logic;

    public MonsterDropLogicTests()
    {
        currentTick = 1000;
        logic = new MonsterDropLogic(() => currentTick);
    }

    [Fact]
    public void SingleKillAndDrop_ProducesEvent()
    {
        logic.RecordDeath(bnpcBaseId: 50, territory: 500, map: 600);

        currentTick += 100;
        logic.ProcessInventoryAdd(1001, 2);

        currentTick = 1000 + MonsterDropLogic.CollectWindowMs;
        var result = logic.ProcessTick();

        Assert.NotNull(result);
        Assert.Equal("monster_drop", result.EventType);
        var payload = Assert.IsType<MonsterDropPayload>(result.Payload);
        Assert.Single(payload.Deaths);
        Assert.Equal(50u, payload.Deaths[0].BnpcBaseId);
        Assert.Single(payload.Items);
        Assert.Equal(1001u, payload.Items[0].ItemId);
        Assert.Equal(2, payload.Items[0].Count);
    }

    [Fact]
    public void WindowExtension_SecondDeathExtendsWindow()
    {
        logic.RecordDeath(bnpcBaseId: 50, territory: 500, map: 600);

        currentTick += 1000;
        logic.RecordDeath(bnpcBaseId: 51, territory: 500, map: 600);

        currentTick += 100;
        logic.ProcessInventoryAdd(1001, 1);

        // Original window (1000 + 1500 = 2500) would have expired,
        // but second death extends to (2000 + 1500 = 3500)
        currentTick = 2000 + MonsterDropLogic.CollectWindowMs;
        var result = logic.ProcessTick();

        Assert.NotNull(result);
        var payload = Assert.IsType<MonsterDropPayload>(result.Payload);
        Assert.Equal(2, payload.Deaths.Count);
        Assert.Equal(50u, payload.Deaths[0].BnpcBaseId);
        Assert.Equal(51u, payload.Deaths[1].BnpcBaseId);
    }

    [Fact]
    public void OffsetMs_RelativeToFirstDeath()
    {
        // First death at tick 1000
        logic.RecordDeath(bnpcBaseId: 50, territory: 500, map: 600);

        // Second death at tick 1500
        currentTick = 1500;
        logic.RecordDeath(bnpcBaseId: 51, territory: 500, map: 600);

        // Item at tick 1600
        currentTick = 1600;
        logic.ProcessInventoryAdd(1001, 1);

        // Item at tick 1800
        currentTick = 1800;
        logic.ProcessInventoryAdd(1002, 2);

        currentTick = 1500 + MonsterDropLogic.CollectWindowMs;
        var result = logic.ProcessTick();

        Assert.NotNull(result);
        var payload = Assert.IsType<MonsterDropPayload>(result.Payload);

        // Death offsets relative to windowStartTick (1000)
        Assert.Equal(0, payload.Deaths[0].OffsetMs);
        Assert.Equal(500, payload.Deaths[1].OffsetMs);

        // Item offsets relative to windowStartTick (1000)
        Assert.Equal(600, payload.Items[0].OffsetMs);
        Assert.Equal(800, payload.Items[1].OffsetMs);
    }

    [Fact]
    public void NoItems_StillFiresEventWithDeaths()
    {
        logic.RecordDeath(bnpcBaseId: 50, territory: 500, map: 600);

        currentTick += MonsterDropLogic.CollectWindowMs;
        var result = logic.ProcessTick();

        Assert.NotNull(result);
        var payload = Assert.IsType<MonsterDropPayload>(result.Payload);
        Assert.Empty(payload.Items);
        Assert.Single(payload.Deaths);
        Assert.Equal(50u, payload.Deaths[0].BnpcBaseId);
        Assert.Equal((ushort)500, payload.TerritoryTypeId);
        Assert.Equal(600u, payload.MapId);
    }

    [Fact]
    public void TimerTooEarly_NoEvent()
    {
        logic.RecordDeath(bnpcBaseId: 50, territory: 500, map: 600);
        logic.ProcessInventoryAdd(1001, 1);

        currentTick += MonsterDropLogic.CollectWindowMs - 1;
        var result = logic.ProcessTick();

        Assert.Null(result);
    }

    [Fact]
    public void TerritoryFromFirstDeath()
    {
        logic.RecordDeath(bnpcBaseId: 50, territory: 500, map: 600);

        currentTick += 100;
        logic.RecordDeath(bnpcBaseId: 51, territory: 501, map: 601);

        currentTick += 100;
        logic.ProcessInventoryAdd(1001, 1);

        currentTick = 1100 + MonsterDropLogic.CollectWindowMs;
        var result = logic.ProcessTick();

        Assert.NotNull(result);
        var payload = Assert.IsType<MonsterDropPayload>(result.Payload);
        Assert.Equal((ushort)500, payload.TerritoryTypeId);
        Assert.Equal(600u, payload.MapId);
    }

    [Fact]
    public void WindowReset_SecondWindowHasCleanState()
    {
        // First window
        logic.RecordDeath(bnpcBaseId: 50, territory: 500, map: 600);
        currentTick += 100;
        logic.ProcessInventoryAdd(1001, 1);

        currentTick = 1000 + MonsterDropLogic.CollectWindowMs;
        var first = logic.ProcessTick();
        Assert.NotNull(first);

        // Second window — should not carry over data from first
        currentTick += 100;
        var secondStart = currentTick;
        logic.RecordDeath(bnpcBaseId: 99, territory: 800, map: 900);
        currentTick += 50;
        logic.ProcessInventoryAdd(2002, 3);

        currentTick = secondStart + MonsterDropLogic.CollectWindowMs;
        var second = logic.ProcessTick();

        Assert.NotNull(second);
        var payload = Assert.IsType<MonsterDropPayload>(second.Payload);
        Assert.Single(payload.Deaths);
        Assert.Equal(99u, payload.Deaths[0].BnpcBaseId);
        Assert.Single(payload.Items);
        Assert.Equal(2002u, payload.Items[0].ItemId);
        Assert.Equal((ushort)800, payload.TerritoryTypeId);
    }

    [Fact]
    public void InventoryChange_SameItemId_UsesQuantityDiff()
    {
        logic.RecordDeath(bnpcBaseId: 50, territory: 500, map: 600);

        currentTick += 100;
        logic.ProcessInventoryChange(oldItemId: 1001, oldQuantity: 5, newItemId: 1001, newQuantity: 8);

        currentTick = 1000 + MonsterDropLogic.CollectWindowMs;
        var result = logic.ProcessTick();

        Assert.NotNull(result);
        var payload = Assert.IsType<MonsterDropPayload>(result.Payload);
        Assert.Single(payload.Items);
        Assert.Equal(1001u, payload.Items[0].ItemId);
        Assert.Equal(3, payload.Items[0].Count);
    }

    [Fact]
    public void InventoryChange_DifferentItemId_UsesNewQuantity()
    {
        logic.RecordDeath(bnpcBaseId: 50, territory: 500, map: 600);

        currentTick += 100;
        logic.ProcessInventoryChange(oldItemId: 1001, oldQuantity: 5, newItemId: 2002, newQuantity: 7);

        currentTick = 1000 + MonsterDropLogic.CollectWindowMs;
        var result = logic.ProcessTick();

        Assert.NotNull(result);
        var payload = Assert.IsType<MonsterDropPayload>(result.Payload);
        Assert.Single(payload.Items);
        Assert.Equal(2002u, payload.Items[0].ItemId);
        Assert.Equal(7, payload.Items[0].Count);
    }

    [Fact]
    public void InventoryAddBeforeDeath_Ignored()
    {
        logic.ProcessInventoryAdd(1001, 1);

        logic.RecordDeath(bnpcBaseId: 50, territory: 500, map: 600);
        currentTick += 100;
        logic.ProcessInventoryAdd(2002, 1);

        currentTick = 1000 + MonsterDropLogic.CollectWindowMs;
        var result = logic.ProcessTick();

        Assert.NotNull(result);
        var payload = Assert.IsType<MonsterDropPayload>(result.Payload);
        Assert.Single(payload.Items);
        Assert.Equal(2002u, payload.Items[0].ItemId);
    }

    [Fact]
    public void InventoryChange_SameItemId_NegativeDiff_NotAdded()
    {
        logic.RecordDeath(bnpcBaseId: 50, territory: 500, map: 600);

        currentTick += 100;
        logic.ProcessInventoryChange(oldItemId: 1001, oldQuantity: 10, newItemId: 1001, newQuantity: 5);

        currentTick = 1000 + MonsterDropLogic.CollectWindowMs;
        var result = logic.ProcessTick();

        Assert.NotNull(result);
        var payload = Assert.IsType<MonsterDropPayload>(result.Payload);
        Assert.Empty(payload.Items);
        Assert.Single(payload.Deaths);
    }
}
