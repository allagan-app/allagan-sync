using System;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace AllaganSync.Collecting;

public record InventorySource(string Key, string DisplayName, InventoryType[] Types)
{
    public const string RetainerKeyPrefix = "retainer_";

    public unsafe Action? OpenGameUi { get; init; }

    public static readonly InventorySource[] All =
    [
        new("inventory", "Inventory", [
            InventoryType.Inventory1,
            InventoryType.Inventory2,
            InventoryType.Inventory3,
            InventoryType.Inventory4,
        ]),
        new("equipped", "Equipped", [
            InventoryType.EquippedItems,
        ]),
        new("armoury", "Armoury Chest", [
            InventoryType.ArmoryOffHand,
            InventoryType.ArmoryHead,
            InventoryType.ArmoryBody,
            InventoryType.ArmoryHands,
            InventoryType.ArmoryLegs,
            InventoryType.ArmoryFeets,
            InventoryType.ArmoryEar,
            InventoryType.ArmoryNeck,
            InventoryType.ArmoryWrist,
            InventoryType.ArmoryRings,
            InventoryType.ArmorySoulCrystal,
            InventoryType.ArmoryMainHand,
        ]),
        new("saddlebag", "Saddlebag", [
            InventoryType.SaddleBag1,
            InventoryType.SaddleBag2,
        ])
        {
            OpenGameUi = OpenAgent(AgentId.InventoryBuddy),
        },
        new("premium_saddlebag", "Premium Saddlebag", [
            InventoryType.PremiumSaddleBag1,
            InventoryType.PremiumSaddleBag2,
        ])
        {
            OpenGameUi = OpenAgent(AgentId.InventoryBuddy),
        },
    ];

    private static unsafe Action OpenAgent(AgentId agentId)
    {
        return () =>
        {
            var agentModule = AgentModule.Instance();
            if (agentModule == null)
                return;

            var agent = agentModule->GetAgentByInternalId(agentId);
            if (agent != null)
                agent->Show();
        };
    }
}
