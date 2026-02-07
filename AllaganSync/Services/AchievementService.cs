using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using AchievementState = FFXIVClientStructs.FFXIV.Client.Game.UI.Achievement;

namespace AllaganSync.Services;

public class AchievementService
{
    private readonly IDataManager dataManager;
    private readonly IPluginLog log;

    public AchievementService(IDataManager dataManager, IPluginLog log)
    {
        this.dataManager = dataManager;
        this.log = log;
    }

    private bool IsValidAchievementKind(uint? kindId)
    {
        if (kindId is null or 0)
            return false;

        var kindSheet = dataManager.GetExcelSheet<AchievementKind>();
        if (kindSheet == null)
            return false;

        var kind = kindSheet.GetRowOrDefault(kindId.Value);
        return kind != null && !kind.Value.Name.IsEmpty;
    }

    private bool IsValidAchievementCategory(uint? categoryId)
    {
        if (categoryId is null or 0)
            return false;

        var categorySheet = dataManager.GetExcelSheet<AchievementCategory>();
        if (categorySheet == null)
            return false;

        var category = categorySheet.GetRowOrDefault(categoryId.Value);
        if (category == null)
            return false;

        return IsValidAchievementKind(category.Value.AchievementKind.RowId);
    }

    private bool IsValid(Achievement achievement)
    {
        return IsValidAchievementCategory(achievement.AchievementCategory.RowId);
    }

    public int GetTotalCount()
    {
        var sheet = dataManager.GetExcelSheet<Achievement>();
        return sheet?.Count(IsValid) ?? 0;
    }

    public unsafe bool IsLoaded
    {
        get
        {
            var achievement = AchievementState.Instance();
            return achievement != null && achievement->IsLoaded();
        }
    }

    public unsafe void RequestAchievementData()
    {
        var achievement = AchievementState.Instance();
        if (achievement == null)
            return;

        if (!achievement->IsLoaded())
        {
            log.Info("AchievementService: Requesting achievement data from server...");
            achievement->RequestAchievementProgress(0);
        }
    }

    public unsafe List<uint> GetUnlockedIds()
    {
        var unlockedIds = new List<uint>();
        var achievementSheet = dataManager.GetExcelSheet<Achievement>();

        if (achievementSheet == null)
        {
            log.Error("AchievementService: achievementSheet is null");
            return unlockedIds;
        }

        var achievement = AchievementState.Instance();
        if (achievement == null)
        {
            log.Error("AchievementService: achievement instance is null");
            return unlockedIds;
        }

        if (!achievement->IsLoaded())
        {
            log.Warning("AchievementService: Achievement data not yet loaded, requesting...");
            achievement->RequestAchievementProgress(0);
            return unlockedIds;
        }

        foreach (var row in achievementSheet)
        {
            if (!IsValid(row))
                continue;

            if (achievement->IsComplete((int)row.RowId))
            {
                unlockedIds.Add(row.RowId);
            }
        }

        return unlockedIds;
    }
}
