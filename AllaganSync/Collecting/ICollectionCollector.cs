using System;
using System.Collections.Generic;

namespace AllaganSync.Collecting;

public interface ICollectionCollector
{
    string CollectionKey { get; }
    string DisplayName { get; }
    bool NeedsDataRequest { get; }
    bool IsDataReady { get; }
    Action? OpenGameUi => null;
    void RequestData();
    int GetTotalCount();
    List<uint> GetUnlockedIds();
}
