using System;
using System.Collections.Generic;
using AllaganSync.Models;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Network;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Network;

namespace AllaganSync.Tracking.Trackers;

public unsafe class MonsterSpawnTracker : IGameEventTracker
{
    private readonly IPluginLog log;
    private readonly IClientState clientState;
    private readonly HashSet<string> seenSpawns = [];
    private Hook<PacketDispatcher.Delegates.HandleSpawnNpcPacket>? spawnNpcHook;

    public string EventKey => "monster_spawn";
    public string DisplayName => "Monster Spawns";
    public bool IsAvailable { get; }
    public bool IsEnabled { get; set; }
    public string? RequiredAbility => "mapping:contribute";

    public event Action<TrackedEvent>? EventTracked;

    public MonsterSpawnTracker(
        IPluginLog log,
        IClientState clientState,
        IGameInteropProvider gameInteropProvider)
    {
        this.log = log;
        this.clientState = clientState;

        try
        {
            spawnNpcHook = gameInteropProvider.HookFromAddress<PacketDispatcher.Delegates.HandleSpawnNpcPacket>(
                PacketDispatcher.MemberFunctionPointers.HandleSpawnNpcPacket,
                OnSpawnNpcPacket);
            spawnNpcHook.Enable();
            IsAvailable = true;
            log.Info("MonsterSpawnTracker: Hook installed successfully.");
        }
        catch (Exception ex)
        {
            IsAvailable = false;
            log.Warning($"MonsterSpawnTracker: Failed to install hook, monster spawn tracking unavailable. {ex.Message}");
        }

        clientState.TerritoryChanged += OnTerritoryChanged;
    }

    private void OnSpawnNpcPacket(uint targetId, SpawnNpcPacket* packet)
    {
        try
        {
            if (packet->Common.ObjectKind == ObjectKind.BattleNpc && IsEnabled)
            {
                var subKind = (BattleNpcSubKind)packet->Common.SubKind;
                if (subKind is not (BattleNpcSubKind.Pet or BattleNpcSubKind.Buddy or BattleNpcSubKind.RaceChocobo))
                {
                    var baseId = packet->Common.BaseId;
                    var hash = $"{baseId}_{clientState.TerritoryType}_{packet->Common.LayoutId}";

                    if (seenSpawns.Add(hash))
                    {
                        var payload = new MonsterSpawnPayload
                        {
                            BnpcBaseId = baseId,
                            TerritoryTypeId = clientState.TerritoryType,
                            LayoutId = packet->Common.LayoutId,
                            PositionX = packet->Common.Position.X,
                            PositionY = packet->Common.Position.Y,
                            PositionZ = packet->Common.Position.Z,
                            Level = packet->Common.Level,
                        };

                        var trackedEvent = new TrackedEvent
                        {
                            EventType = EventKey,
                            Payload = payload,
                            OccurredAt = DateTime.UtcNow.ToString("O"),
                        };

                        EventTracked?.Invoke(trackedEvent);
                        log.Debug($"MonsterSpawnTracker: Captured BNpcBase {baseId} Lv{packet->Common.Level} in territory {clientState.TerritoryType}.");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            log.Error($"MonsterSpawnTracker: Error processing spawn packet: {ex}");
        }

        spawnNpcHook!.Original(targetId, packet);
    }

    private void OnTerritoryChanged(ushort territoryId)
    {
        seenSpawns.Clear();
    }

    public void Dispose()
    {
        clientState.TerritoryChanged -= OnTerritoryChanged;
        spawnNpcHook?.Dispose();
    }
}
