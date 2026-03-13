using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AllaganSync.Models;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace AllaganSync.Tracking.Trackers;

public unsafe class TripleTriadDuelTracker : IGameEventTracker
{
    /// <summary>
    /// Minimal agent struct for reading the reward item ID.
    /// Offset sourced from Saucy: https://github.com/PunishXIV/Saucy
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 0x1D0)]
    private struct AgentTripleTriadReward
    {
        [FieldOffset(0x1C8)] public uint RewardItemId;
    }

    private const int MaxMgpReadAttempts = 30;

    private readonly IPluginLog log;
    private readonly IAddonLifecycle addonLifecycle;
    private readonly IGameGui gameGui;

    // State captured during the TripleTriad game addon
    private uint pendingNpcDataId;
    private int lastBlueCount;
    private int lastRedCount;
    private bool resultEmitted;
    private int resultUpdateAttempts;

    public string EventKey => "triple_triad_duel";
    public string DisplayName => "Triple Triad Duels";
    public bool IsAvailable { get; }
    public bool IsEnabled { get; set; }
    public string? RequiredAbility => null;

    public event Action<TrackedEvent>? EventTracked;

    public TripleTriadDuelTracker(
        IPluginLog log,
        IAddonLifecycle addonLifecycle,
        IGameGui gameGui)
    {
        this.log = log;
        this.addonLifecycle = addonLifecycle;
        this.gameGui = gameGui;

        try
        {
            // TripleTriad addon: capture NPC and board state during the game
            addonLifecycle.RegisterListener(AddonEvent.PostSetup, "TripleTriad", OnGameSetup);
            addonLifecycle.RegisterListener(AddonEvent.PostUpdate, "TripleTriad", OnGameUpdate);

            // TripleTriadResult addon: read reward and emit event (PostUpdate to wait for UI data)
            addonLifecycle.RegisterListener(AddonEvent.PostUpdate, "TripleTriadResult", OnResultUpdate);

            IsAvailable = true;
            log.Info("TripleTriadDuelTracker: Addon lifecycle listeners installed.");
        }
        catch (Exception ex)
        {
            IsAvailable = false;
            log.Warning($"TripleTriadDuelTracker: Setup failed. {ex.Message}");
        }
    }

    private void OnGameSetup(AddonEvent type, AddonArgs args)
    {
        pendingNpcDataId = 0;
        lastBlueCount = 0;
        lastRedCount = 0;
        resultEmitted = false;
        resultUpdateAttempts = 0;

        try
        {
            var targetSystem = TargetSystem.Instance();
            if (targetSystem != null && targetSystem->Target != null)
            {
                pendingNpcDataId = targetSystem->Target->BaseId;
                log.Info($"TripleTriadDuelTracker: Game started — NPC DataId={pendingNpcDataId}");
            }
            else
            {
                log.Warning("TripleTriadDuelTracker: No target found at game setup.");
            }
        }
        catch (Exception ex)
        {
            log.Error($"TripleTriadDuelTracker: Error reading NPC on game setup: {ex}");
        }
    }

    private void OnGameUpdate(AddonEvent type, AddonArgs args)
    {
        try
        {
            var addon = (AddonTripleTriad*)(nint)args.Addon;
            if (addon == null)
                return;

            // Continuously snapshot the board so we have the final state
            var blueCount = 0;
            var redCount = 0;
            foreach (var card in addon->Board)
            {
                if (!card.HasCard)
                    continue;

                switch (card.CardOwner)
                {
                    case CardOwner.Blue:
                        blueCount++;
                        break;
                    case CardOwner.Red:
                        redCount++;
                        break;
                }
            }

            if (blueCount + redCount > 0)
            {
                lastBlueCount = blueCount;
                lastRedCount = redCount;
            }
        }
        catch (Exception ex)
        {
            log.Error($"TripleTriadDuelTracker: Error in game update: {ex}");
        }
    }

    private void OnResultUpdate(AddonEvent type, AddonArgs args)
    {
        if (resultEmitted || !IsEnabled)
            return;

        try
        {
            // Wait until board data is available
            if (lastBlueCount + lastRedCount == 0)
                return;

            var addon = (AtkUnitBase*)(nint)args.Addon;
            if (addon == null)
                return;

            resultUpdateAttempts++;

            // Try to read MGP from addon nodes
            var mgpReward = ReadMgpFromResultAddon(addon);

            // Wait for MGP node to be initialized, emit without MGP after max attempts
            if (mgpReward < 0 && resultUpdateAttempts < MaxMgpReadAttempts)
                return;

            resultEmitted = true;

            var result = lastBlueCount > lastRedCount ? "win"
                       : lastBlueCount < lastRedCount ? "loss"
                       : "draw";

            // Read reward card from agent
            var cardsWon = new List<TripleTriadCardWon>();
            var agentPtr = (nint)gameGui.FindAgentInterface((nint)args.Addon);
            if (agentPtr != nint.Zero)
            {
                var agent = (AgentTripleTriadReward*)agentPtr;
                var rewardItemId = agent->RewardItemId;

                if (rewardItemId > 0)
                {
                    cardsWon.Add(new TripleTriadCardWon { ItemId = rewardItemId });
                }
            }

            // Re-read NPC if needed (target may still be set at result time)
            if (pendingNpcDataId == 0)
            {
                var targetSystem = TargetSystem.Instance();
                if (targetSystem != null && targetSystem->Target != null)
                    pendingNpcDataId = targetSystem->Target->BaseId;
            }

            if (pendingNpcDataId == 0)
            {
                log.Warning("TripleTriadDuelTracker: Could not determine NPC, skipping.");
                return;
            }

            var payload = new TripleTriadDuelPayload
            {
                NpcXivId = pendingNpcDataId,
                Result = result,
                CardsWon = cardsWon,
                MgpReward = mgpReward > 0 ? mgpReward : null,
            };

            var trackedEvent = new TrackedEvent
            {
                EventType = EventKey,
                Payload = payload,
                OccurredAt = DateTime.UtcNow.ToString("O"),
            };

            EventTracked?.Invoke(trackedEvent);
            log.Info($"TripleTriadDuelTracker: Emitted — NPC DataId={pendingNpcDataId}, Result={result}, CardsWon={cardsWon.Count}, MGP={Math.Max(0, mgpReward)}");
        }
        catch (Exception ex)
        {
            log.Error($"TripleTriadDuelTracker: Error processing result: {ex}");
        }
    }

    /// <summary>
    /// Reads the MGP reward value from the TripleTriadResult addon.
    ///
    /// Traversal path (sourced from Saucy UIReaderTriadResults):
    ///   RootNode → sibling[8] (rewards container, 8 children)
    ///     → sibling[6] (comp node, 6 in NodeList)
    ///       → NodeList[5] (textninegrid comp, 2 in NodeList)
    ///         → NodeList[1] (text node with MGP amount)
    /// </summary>
    private int ReadMgpFromResultAddon(AtkUnitBase* addon)
    {
        var rootNode = addon->RootNode;
        if (rootNode == null)
            return -1;

        var rewardsNode = GetSiblingChild(rootNode, 8, 10);
        if (rewardsNode == null)
            return -1;

        var compNode = GetSiblingChild(rewardsNode, 6, 8);
        if (compNode == null || (int)compNode->Type < 1000)
            return -1;

        var comp = ((AtkComponentNode*)compNode)->Component;
        if (comp == null || comp->UldManager.NodeListCount < 6)
            return -1;

        var textNineGridNode = comp->UldManager.NodeList[5];
        if (textNineGridNode == null || (int)textNineGridNode->Type < 1000)
            return -1;

        var textNineGridComp = ((AtkComponentNode*)textNineGridNode)->Component;
        if (textNineGridComp == null || textNineGridComp->UldManager.NodeListCount < 2)
            return -1;

        var textNode = textNineGridComp->UldManager.NodeList[1];
        if (textNode == null || textNode->Type != NodeType.Text)
            return -1;

        var atkTextNode = (AtkTextNode*)textNode;
        var text = Marshal.PtrToStringUTF8(new IntPtr(atkTextNode->NodeText.StringPtr));
        return AddonTextHelper.ParseNumericText(text) ?? -1;
    }

    /// <summary>
    /// Walks the sibling chain from a node's first child to pick a specific index.
    /// Returns null if the chain has fewer than expectedCount siblings.
    /// </summary>
    private static AtkResNode* GetSiblingChild(AtkResNode* parent, int index, int expectedCount)
    {
        var node = parent->ChildNode;
        if (node == null)
            return null;

        var count = 1;
        var current = node;
        while (current->PrevSiblingNode != null)
        {
            count++;
            current = current->PrevSiblingNode;
        }

        if (count != expectedCount || index >= count)
            return null;

        for (var idx = 0; idx < index; idx++)
        {
            node = node->PrevSiblingNode;
            if (node == null)
                return null;
        }

        return node;
    }

    public void Dispose()
    {
        addonLifecycle.UnregisterListener(AddonEvent.PostSetup, "TripleTriad", OnGameSetup);
        addonLifecycle.UnregisterListener(AddonEvent.PostUpdate, "TripleTriad", OnGameUpdate);
        addonLifecycle.UnregisterListener(AddonEvent.PostUpdate, "TripleTriadResult", OnResultUpdate);
    }
}
