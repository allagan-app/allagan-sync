using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace AllaganSync.Collecting.Collectors;

public class OrchestrionCollector(IDataManager dataManager, IUnlockState unlockState)
    : UnlockStateCollector<Orchestrion>(dataManager, unlockState)
{
    public override string CollectionKey => "orchestrions";
    public override string DisplayName => "Orchestrions";

    protected override bool IsValid(Orchestrion row)
    {
        return !row.Name.IsEmpty;
    }

    protected override bool IsUnlocked(Orchestrion row)
    {
        return unlockState.IsOrchestrionUnlocked(row);
    }
}
