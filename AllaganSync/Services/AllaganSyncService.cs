using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AllaganSync.Collecting;
using Dalamud.Plugin.Services;

namespace AllaganSync.Services;

public class AllaganSyncService(IPluginLog log, ConfigurationService configService, AllaganApiClient apiClient)
{
    private const string SyncEndpoint = "/api/v1/character/collection/sync";

    private readonly List<ICollectionCollector> collectors = new();
    private readonly ConcurrentDictionary<string, (int unlocked, int total)> counts = new();

    public IReadOnlyList<ICollectionCollector> Collectors => collectors;
    public IReadOnlyDictionary<string, (int unlocked, int total)> Counts => counts;

    public bool IsSyncing { get; private set; }
    public string? LastError { get; private set; }
    public DateTime? LastSyncTime { get; private set; }
    public bool IsRefreshing { get; private set; }

    public void RegisterCollector(ICollectionCollector collector)
    {
        collectors.Add(collector);
    }

    public void RequestData()
    {
        foreach (var collector in collectors.Where(c => c.NeedsDataRequest))
        {
            collector.RequestData();
        }
    }

    public (int unlocked, int total) GetCounts(string collectionKey)
    {
        return counts.TryGetValue(collectionKey, out var value) ? value : (0, 0);
    }

    public void RefreshCounts()
    {
        if (IsRefreshing)
            return;

        IsRefreshing = true;
        Task.Run(() =>
        {
            try
            {
                foreach (var collector in collectors)
                {
                    counts[collector.CollectionKey] = (collector.GetUnlockedIds().Count, collector.GetTotalCount());
                }
            }
            finally
            {
                IsRefreshing = false;
            }
        });
    }

    public async Task<bool> SyncAsync()
    {
        var charConfig = configService.CurrentCharacter;
        if (charConfig == null)
        {
            LastError = "No character logged in";
            return false;
        }

        if (!apiClient.HasToken())
        {
            LastError = "No API token configured";
            return false;
        }

        if (IsSyncing)
        {
            LastError = "Sync already in progress";
            return false;
        }

        IsSyncing = true;
        LastError = null;

        try
        {
            var payload = await Task.Run(() =>
            {
                var data = new Dictionary<string, List<uint>>();

                foreach (var collector in collectors)
                {
                    if (!charConfig.IsCollectionEnabled(collector.CollectionKey))
                        continue;

                    if (collector.NeedsDataRequest && !collector.IsDataReady)
                    {
                        log.Warning($"Skipping {collector.DisplayName}: data not ready");
                        continue;
                    }

                    var ids = collector.GetUnlockedIds();
                    data[collector.CollectionKey] = ids;
                    log.Info($"Syncing {ids.Count} {collector.DisplayName.ToLowerInvariant()}");
                }

                return data;
            });

            var response = await apiClient.PostAsync(SyncEndpoint, payload);

            if (!response.IsSuccessStatusCode)
            {
                LastError = $"API error: {response.StatusCode}";
                log.Error($"Sync failed: {response.StatusCode}");
                return false;
            }

            LastSyncTime = DateTime.Now;
            log.Info("Sync completed successfully");
            return true;
        }
        catch (Exception ex)
        {
            LastError = $"Error: {ex.Message}";
            log.Error($"Sync exception: {ex}");
            return false;
        }
        finally
        {
            IsSyncing = false;
        }
    }
}
