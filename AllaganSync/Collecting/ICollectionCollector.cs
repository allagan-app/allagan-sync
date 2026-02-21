using System.Collections.Generic;

namespace AllaganSync.Collecting;

public interface ICollectionCollector
{
    string CollectionKey { get; }
    string DisplayName { get; }
    bool NeedsDataRequest { get; }
    bool IsDataReady { get; }
    void RequestData();
    int GetTotalCount();
    List<uint> GetUnlockedIds();
}
