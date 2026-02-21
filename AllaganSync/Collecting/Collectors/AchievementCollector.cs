using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using AchievementState = FFXIVClientStructs.FFXIV.Client.Game.UI.Achievement;

namespace AllaganSync.Collecting.Collectors;

public class AchievementCollector(IDataManager dataManager, IPluginLog log) : ICollectionCollector
{
    public string CollectionKey => "achievements";
    public string DisplayName => "Achievements";
    public bool NeedsDataRequest => true;

    public unsafe bool IsDataReady
    {
        get
        {
            var achievement = AchievementState.Instance();
            return achievement != null && achievement->IsLoaded();
        }
    }

    public unsafe void RequestData()
    {
        var achievement = AchievementState.Instance();
        if (achievement == null)
            return;

        if (!achievement->IsLoaded())
        {
            log.Info("AchievementCollector: Requesting achievement data from server...");
            achievement->RequestAchievementProgress(0);
        }
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

    public unsafe List<uint> GetUnlockedIds()
    {
        var unlockedIds = new List<uint>();
        var achievementSheet = dataManager.GetExcelSheet<Achievement>();

        if (achievementSheet == null)
        {
            log.Error("AchievementCollector: achievementSheet is null");
            return unlockedIds;
        }

        var achievement = AchievementState.Instance();
        if (achievement == null)
        {
            log.Error("AchievementCollector: achievement instance is null");
            return unlockedIds;
        }

        if (!achievement->IsLoaded())
        {
            log.Warning("AchievementCollector: Achievement data not yet loaded, requesting...");
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
