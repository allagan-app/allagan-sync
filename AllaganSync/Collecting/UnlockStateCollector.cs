using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using Lumina.Excel;

namespace AllaganSync.Collecting;

public abstract class UnlockStateCollector<TSheet> : ICollectionCollector where TSheet : struct, IExcelRow<TSheet>
{
    protected readonly IDataManager dataManager;
    protected readonly IUnlockState unlockState;

    private int? cachedTotalCount;

    protected UnlockStateCollector(IDataManager dataManager, IUnlockState unlockState)
    {
        this.dataManager = dataManager;
        this.unlockState = unlockState;
    }

    public abstract string CollectionKey { get; }
    public abstract string DisplayName { get; }
    public virtual bool NeedsDataRequest => false;
    public virtual bool IsDataReady => true;
    public virtual void RequestData() { }

    protected abstract bool IsValid(TSheet row);
    protected abstract bool IsUnlocked(TSheet row);

    public int GetTotalCount()
    {
        if (cachedTotalCount.HasValue)
            return cachedTotalCount.Value;

        var sheet = dataManager.GetExcelSheet<TSheet>();
        cachedTotalCount = sheet?.Count(IsValid) ?? 0;
        return cachedTotalCount.Value;
    }

    public void InvalidateTotalCount()
    {
        cachedTotalCount = null;
    }

    public List<uint> GetUnlockedIds()
    {
        var unlockedIds = new List<uint>();
        var sheet = dataManager.GetExcelSheet<TSheet>();

        if (sheet == null)
            return unlockedIds;

        foreach (var row in sheet)
        {
            if (!IsValid(row))
                continue;

            if (IsUnlocked(row))
            {
                unlockedIds.Add(row.RowId);
            }
        }

        return unlockedIds;
    }
}
