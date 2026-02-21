using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace AllaganSync.Collecting.Collectors;

public class FashionAccessoryCollector(IDataManager dataManager, IUnlockState unlockState)
    : UnlockStateCollector<Ornament>(dataManager, unlockState)
{
    public override string CollectionKey => "fashion_accessories";
    public override string DisplayName => "Fashion Accessories";

    protected override bool IsValid(Ornament row)
    {
        if (row.Singular.IsEmpty)
            return false;

        // Transient 200-299 = old glasses (converted to GlassesStyle)
        if (row.Transient >= 200 && row.Transient < 300)
            return false;

        return row.Order >= 0;
    }

    protected override bool IsUnlocked(Ornament row)
    {
        return unlockState.IsOrnamentUnlocked(row);
    }
}
