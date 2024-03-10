using ECommons.ExcelServices;
using ECommons.GameHelpers;
using System.Numerics;
using visland.Gathering;
using static visland.Gathering.GatherRouteDB.InteractionType;

namespace visland.Questing.Presets;
internal class Gridania1_15
{
    public static string Name => "Gridania 1-15";

    public static void Run(GatherRouteExec Exec)
    {
        /*
        var route = new GatherRouteDB.Route();

        // Coming to Gridania (Level 1)
        route.Waypoints.Add(new() { Position = new Vector3(118.9895f, -12.5777f, 144.0687f), ZoneID = 132, Pathfind = true, InteractWithOID = 1001148, Interaction = PickupQuest, QuestID = 65575 });
        route.Waypoints.Add(new() { Position = new Vector3(26.905f, -8f, 115.7461f), ZoneID = 132, Pathfind = true, InteractWithOID = 1001140, Interaction = TurninQuest, QuestID = 65575 });

        if (Player.Job == Job.ARC)
        {
            // Close to Home (Level 1)
            route.Waypoints.Add(new() { Position = new Vector3(26.905f, -8f, 115.7461f), ZoneID = 132, Pathfind = true, InteractWithOID = 1001140, Interaction = PickupQuest, QuestID = 65659 });
            route.Waypoints.Add(new() { Position = new Vector3(170.4057f, 15.5f, -89.87196f), ZoneID = 133, Pathfind = true, InteractWithOID = 1001140, Interaction = TurninQuest, QuestID = 65660, ItemID = 2000119 });
            route.Waypoints.Add(new() { Position = new Vector3(198.8192f, 0f, 43.82232f), ZoneID = 132, Pathfind = true, InteractWithOID = 1000197, Interaction = QuestTalk});

            // Way of the Archer (Level 1)
            route.Waypoints.Add(new() { Position = new Vector3(198.8192f, 0f, 43.82232f), ZoneID = 132, Pathfind = true, InteractWithOID = 1000197, Interaction = PickupQuest, QuestID = 65557 });
            route.Waypoints.Add(new() { Position = new Vector3(209.5521f, 0.9999819f, 35.01941f), ZoneID = 132, Pathfind = true, InteractWithOID = 1000200, Interaction = QuestTalk });

            // Close to Home (Level 1)
            route.Waypoints.Add(new() { Position = new Vector3(26.52929f, -8f, 116.2287f), ZoneID = 132, Pathfind = true, InteractWithOID = 1000100, Interaction = TurninQuest, QuestID = 65659 });

            // Way of the Archer (Level 1)
            route.Waypoints.Add(new() { Position = new Vector3(104.0594f, 14.69139f, -262.7849f), ZoneID = 148, Pathfind = true, Interaction = Grind, MobID = 37 });
            route.Waypoints.Add(new() { Position = new Vector3(104.0594f, 14.69139f, -262.7849f), ZoneID = 148, Pathfind = true, Interaction = Grind, MobID = 49 });
            route.Waypoints.Add(new() { Position = new Vector3(113.5422f, 0.7499394f, -171.7874f), ZoneID = 148, Pathfind = true, Interaction = Grind, MobID = 47 });
            route.Waypoints.Add(new() { Position = new Vector3(209.5521f, 0.9999819f, 35.01941f), ZoneID = 132, Pathfind = true, InteractWithOID = 1000200, Interaction = TurninQuest, QuestID = 65557 });
            //route.Waypoints.Add(new() { Position = new Vector3(209.5521f, 0.9999819f, 35.01941f), ZoneID = 132, Pathfind = true, Interaction = AutoEquipGear });
        }

        if (Player.Job == Job.CNJ)
        {
            // Close to Home (Level 1)
            route.Waypoints.Add(new() { Position = new Vector3(26.905f, -8f, 115.7461f), ZoneID = 132, Pathfind = true, InteractWithOID = 1001140, Interaction = PickupQuest, QuestID = 65660 });
            route.Waypoints.Add(new() { Position = new Vector3(170.4057f, 15.5f, -89.87196f), ZoneID = 133, Pathfind = true, InteractWithOID = 1000768, Interaction = QuestTalk, QuestID = 65660, ItemID = 2000120 });
            route.Waypoints.Add(new() { Position = new Vector3(-234.0504f, -4f, -7.944408f), ZoneID = 132, Pathfind = true, InteractWithOID = 1000323, Interaction = QuestTalk });

            // Way of the Conjurer (Level 1)
            route.Waypoints.Add(new() { Position = new Vector3(-234.0276f, -4f, -11.09338f), ZoneID = 133, Pathfind = true, InteractWithOID = 1000323, Interaction = PickupQuest, QuestID = 65558 });
            route.Waypoints.Add(new() { Position = new Vector3(-258.8083f, -5.773526f, -27.26788f), ZoneID = 132, Pathfind = true, InteractWithOID = 1000692, Interaction = QuestTalk });

            // Close to Home (Level 1)
            route.Waypoints.Add(new() { Position = new Vector3(26.52929f, -8f, 116.2287f), ZoneID = 132, Pathfind = true, InteractWithOID = 1000100, Interaction = TurninQuest, QuestID = 65660 });
            //route.Waypoints.Add(new() { Position = new Vector3(26.52929f, -8f, 116.2287f), ZoneID = 132, Pathfind = true, Interaction = AutoEquipGear });

            // Way of the Conjurer (Level 1)
            route.Waypoints.Add(new() { Position = new Vector3(104.0594f, 14.69139f, -262.7849f), ZoneID = 148, Pathfind = true, Interaction = Grind, MobID = 37 });
            route.Waypoints.Add(new() { Position = new Vector3(104.0594f, 14.69139f, -262.7849f), ZoneID = 148, Pathfind = true, Interaction = Grind, MobID = 49 });
            route.Waypoints.Add(new() { Position = new Vector3(113.5422f, 0.7499394f, -171.7874f), ZoneID = 148, Pathfind = true, Interaction = Grind, MobID = 47 });
            route.Waypoints.Add(new() { Position = new Vector3(-256.5681f, -5.774275f, -24.72007f), ZoneID = 133, Pathfind = true, InteractWithOID = 1000692, Interaction = TurninQuest, QuestID = 65558 });
            //route.Waypoints.Add(new() { Position = new Vector3(-256.5681f, -5.774275f, -24.72007f), ZoneID = 133, Pathfind = true, Interaction = AutoEquipGear });
        }

        if (Player.Job == Job.LNC)
        {
            // Close to Home (Level 1)
            route.Waypoints.Add(new() { Position = new Vector3(26.905f, -8f, 115.7461f), ZoneID = 132, Pathfind = true, InteractWithOID = 1001140, Interaction = PickupQuest, QuestID = 65621 });
            route.Waypoints.Add(new() { Position = new Vector3(144.8345f, 15.5f, -268.0915f), ZoneID = 133, Pathfind = true, InteractWithOID = 1000251, Interaction = QuestTalk });

            // Way of the Lancer (Level 1)
            route.Waypoints.Add(new() { Position = new Vector3(147.0817f, 15.5f, -267.9943f), ZoneID = 133, Pathfind = true, InteractWithOID = 1000251, Interaction = PickupQuest, QuestID = 65559 });
            route.Waypoints.Add(new() { Position = new Vector3(157.7019f, 15.90038f, -270.3442f), ZoneID = 133, Pathfind = true, InteractWithOID = 1000254, Interaction = QuestTalk });

            // Close to Home (Level 1)
            route.Waypoints.Add(new() { Position = new Vector3(172.3506f, 15.5f, -89.95197f), ZoneID = 132, Pathfind = true, InteractWithOID = 1000768, Interaction = QuestTalk, QuestID = 65621, ItemID = 2000074 });
            route.Waypoints.Add(new() { Position = new Vector3(23.81927f, -8f, 115.9227f), ZoneID = 132, Pathfind = true, InteractWithOID = 1000100, Interaction = TurninQuest, QuestID = 65621, });
            //route.Waypoints.Add(new() { Position = new Vector3(23.81927f, -8f, 115.9227f), ZoneID = 132, Pathfind = true, Interaction = AutoEquipGear });

            // Way of the Lancer (Level 1)
            route.Waypoints.Add(new() { Position = new Vector3(103.3762f, 14.36276f, -255.1773f), ZoneID = 148, Pathfind = true, Interaction = Grind, MobID = 37 });
            route.Waypoints.Add(new() { Position = new Vector3(103.3762f, 14.36276f, -255.1773f), ZoneID = 148, Pathfind = true, Interaction = Grind, MobID = 49 });
            route.Waypoints.Add(new() { Position = new Vector3(116.4344f, 0.7488447f, -178.9012f), ZoneID = 148, Pathfind = true, Interaction = Grind, MobID = 47 });
            route.Waypoints.Add(new() { Position = new Vector3(157.7019f, 15.90038f, -270.3442f), ZoneID = 133, Pathfind = true, InteractWithOID = 1000254, Interaction = TurninQuest, QuestID = 65559 });
            //route.Waypoints.Add(new() { Position = new Vector3(157.7019f, 15.90038f, -270.3442f), ZoneID = 133, Pathfind = true, Interaction = AutoEquipGear });
        }

        Exec.Start(route, 0, false, false);
        */
    }
}
