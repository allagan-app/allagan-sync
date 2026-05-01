using System;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;

namespace AllaganSync.Services;

public class InstanceSessionService : IDisposable
{
    private readonly IClientState clientState;
    private readonly ICondition condition;
    private readonly IPluginLog log;

    public string? CurrentSessionId { get; private set; }

    public InstanceSessionService(IClientState clientState, ICondition condition, IPluginLog log)
    {
        this.clientState = clientState;
        this.condition = condition;
        this.log = log;

        clientState.TerritoryChanged += OnTerritoryChanged;
        condition.ConditionChange += OnConditionChange;
        EvaluateInstanceState();
    }

    private void OnTerritoryChanged(uint territoryId)
    {
        EvaluateInstanceState();
    }

    private void OnConditionChange(ConditionFlag flag, bool value)
    {
        if (flag is ConditionFlag.BoundByDuty or ConditionFlag.BoundByDuty56)
        {
            EvaluateInstanceState();
        }
    }

    private void EvaluateInstanceState()
    {
        var inDuty = condition[ConditionFlag.BoundByDuty] || condition[ConditionFlag.BoundByDuty56];

        if (inDuty && CurrentSessionId == null)
        {
            CurrentSessionId = Guid.NewGuid().ToString();
            log.Info($"Instance session started: {CurrentSessionId}");
        }
        else if (!inDuty && CurrentSessionId != null)
        {
            log.Info($"Instance session ended: {CurrentSessionId}");
            CurrentSessionId = null;
        }
    }

    public void Dispose()
    {
        clientState.TerritoryChanged -= OnTerritoryChanged;
        condition.ConditionChange -= OnConditionChange;
    }
}
