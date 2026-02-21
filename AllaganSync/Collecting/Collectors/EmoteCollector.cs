using System.Collections.Generic;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace AllaganSync.Collecting.Collectors;

public class EmoteCollector(IDataManager dataManager, IUnlockState unlockState, IPlayerState playerState)
    : UnlockStateCollector<Emote>(dataManager, unlockState)
{
    private const uint MaelstromCompanyId = 1;
    private const uint TwinAdderCompanyId = 2;
    private const uint ImmortalFlamesCompanyId = 3;

    private const uint FlameSaluteEmoteId = 57;
    private const uint SerpentSaluteEmoteId = 56;
    private const uint StormSaluteEmoteId = 55;

    private static readonly HashSet<uint> DefaultUnlockExceptions = new()
    {
        FlameSaluteEmoteId,
        SerpentSaluteEmoteId,
        StormSaluteEmoteId
    };

    public override string CollectionKey => "emotes";
    public override string DisplayName => "Emotes";

    protected override bool IsValid(Emote row)
    {
        return !row.Name.IsEmpty && row.Order > 0;
    }

    protected override bool IsUnlocked(Emote row)
    {
        // Default emotes often have no unlock link and should count as collected.
        if (row.UnlockLink == 0)
        {
            if (DefaultUnlockExceptions.Contains(row.RowId))
                return IsGrandCompanySaluteUnlocked(row);

            return true;
        }

        return unlockState.IsEmoteUnlocked(row);
    }

    /// <summary>
    /// Reports "currently usable" salutes based on the player's active Grand Company,
    /// not "ever unlocked". Switching GCs changes which salute is considered unlocked.
    /// </summary>
    private bool IsGrandCompanySaluteUnlocked(Emote emote)
    {
        var currentCompanyId = playerState.GrandCompany.RowId;
        if (currentCompanyId == 0)
            return unlockState.IsEmoteUnlocked(emote);

        return emote.RowId switch
        {
            FlameSaluteEmoteId => currentCompanyId == ImmortalFlamesCompanyId,
            SerpentSaluteEmoteId => currentCompanyId == TwinAdderCompanyId,
            StormSaluteEmoteId => currentCompanyId == MaelstromCompanyId,
            _ => unlockState.IsEmoteUnlocked(emote)
        };
    }
}
