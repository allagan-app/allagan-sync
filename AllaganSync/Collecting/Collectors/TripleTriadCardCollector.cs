using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace AllaganSync.Collecting.Collectors;

public class TripleTriadCardCollector(IDataManager dataManager, IUnlockState unlockState)
    : UnlockStateCollector<TripleTriadCard>(dataManager, unlockState)
{
    public override string CollectionKey => "triple_triad_cards";
    public override string DisplayName => "Triple Triad Cards";

    protected override bool IsValid(TripleTriadCard row)
    {
        return !row.Name.IsEmpty;
    }

    protected override bool IsUnlocked(TripleTriadCard row)
    {
        return unlockState.IsTripleTriadCardUnlocked(row);
    }
}
