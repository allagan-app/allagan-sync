using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace AllaganSync.Services;

public class BlueMageSpellService
{
    private readonly IDataManager dataManager;
    private readonly IUnlockState unlockState;

    public BlueMageSpellService(IDataManager dataManager, IUnlockState unlockState)
    {
        this.dataManager = dataManager;
        this.unlockState = unlockState;
    }

    private bool IsValidAction(uint actionId)
    {
        if (actionId == 0)
            return false;

        var actionSheet = dataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>();
        if (actionSheet == null)
            return false;

        var action = actionSheet.GetRowOrDefault(actionId);
        if (action == null)
            return false;

        if (action.Value.Name.IsEmpty)
            return false;

        if (action.Value.ClassJobLevel == 0)
            return false;

        return true;
    }

    // Validator: AozAction is valid if it references a valid Action
    private bool IsValid(AozAction aozAction)
    {
        return IsValidAction(aozAction.Action.RowId);
    }

    public int GetTotalCount()
    {
        var sheet = dataManager.GetExcelSheet<AozAction>();
        return sheet?.Count(IsValid) ?? 0;
    }

    public List<uint> GetUnlockedIds()
    {
        var unlockedIds = new List<uint>();
        var aozActionSheet = dataManager.GetExcelSheet<AozAction>();

        if (aozActionSheet == null)
            return unlockedIds;

        foreach (var row in aozActionSheet)
        {
            if (!IsValid(row))
                continue;

            if (unlockState.IsAozActionUnlocked(row))
            {
                unlockedIds.Add(row.RowId);
            }
        }

        return unlockedIds;
    }
}
