using ECommons.ExcelServices;
using ECommons.GameHelpers;
using System.Numerics;
using static visland.Helpers.QuestsHelper;

namespace visland.Questing.Presets;
internal class Gridania1_15
{
    public static string Name => "Gridania 1-15";
    public static void Run()
    {
        // Coming to Gridania (Level 1)
        if (!IsQuestCompleted(65643) && !IsQuestCompleted(66130) && !HasQuest(65575) && !IsQuestCompleted(65575))
        {
            GetTo(132, new Vector3(118.9895f, -12.5777f, 144.0687f));
            if (!IsQuestAccepted(65575))
                PickUpQuest(65575, 1001148);
            if (HasQuest(65575) && GetQuestStep(65575) == 255)
            {
                GetTo(132, new Vector3(26.905f, -8f, 115.7461f));
                TurnInQuest(65575, 1001140);
            }
        }

        #region Archer
        if (Player.Job == Job.ARC)
        {
            // Close to Home (Level 1)
            if (IsQuestCompleted(65575) && !HasQuest(65659) && !IsQuestCompleted(65659))
            {
                GetTo(132, new Vector3(26.905f, -8f, 115.7461f));
                if (!IsQuestAccepted(65659))
                    PickUpQuest(65659, 1001140);
            }
            if (HasQuest(65659) && !IsQuestCompleted(65659) && GetQuestStep(65659) == 1 && !IsTodoChecked(65659, 1, 2))
            {
                GetTo(133, new Vector3(170.4057f, 15.5f, -89.87196f));
                TurnInQuest(65660, 1001140); // handover item 2000119
            }
            if (HasQuest(65659) && !IsQuestCompleted(65659) && GetQuestStep(65659) == 1 && !IsTodoChecked(65659, 1, 1))
            {
                GetTo(132, new Vector3(198.8192f, 0f, 43.82232f));
                TalkTo(1000197);
            }

            // Way of the Archer (Level 1)
            if (!HasQuest(65557) && !IsQuestCompleted(65557))
            {
                GetTo(132, new Vector3(198.8192f, 0f, 43.82232f));
                if (IsQuestAccepted(65557))
                    PickUpQuest(65557, 1000197);
            }
            if (HasQuest(65557) && !IsQuestCompleted(65557) && GetQuestStep(65557) == 1)
            {
                GetTo(132, new Vector3(209.5521f, 0.9999819f, 35.01941f));
                TalkTo(1000200);
            }

            // Close to Home (Level 1)
            if (HasQuest(65659) && !IsQuestCompleted(65659) && GetQuestStep(65659) == 255)
            {
                GetTo(132, new Vector3(26.52929f, -8f, 116.2287f));
                TurnInQuest(65659, 1000100);
            }

            // Way of the Archer (Level 1)
            if (HasQuest(65557) && !IsQuestCompleted(65557) && GetQuestStep(65557) == 2 && !IsTodoChecked(65557, 2, 0))
            {
                GetTo(148, new Vector3(104.0594f, 14.69139f, -262.7849f));
                Grind(GetMobName(37)); // ground squirrel
            }
            if (HasQuest(65557) && !IsQuestCompleted(65557) && GetQuestStep(65557) == 2 && !IsTodoChecked(65557, 2, 1))
            {
                GetTo(148, new Vector3(104.0594f, 14.69139f, -262.7849f));
                Grind(GetMobName(49)); // little ladybug
            }
            if (HasQuest(65557) && !IsQuestCompleted(65557) && GetQuestStep(65557) == 2 && !IsTodoChecked(65557, 2, 2))
            {
                GetTo(148, new Vector3(113.5422f, 0.7499394f, -171.7874f));
                Grind(GetMobName(47)); // forest funguar
            }
            if (HasQuest(65557) && !IsQuestCompleted(65557) && GetQuestStep(65557) == 255)
            {
                GetTo(132, new Vector3(209.5521f, 0.9999819f, 35.01941f));
                TurnInQuest(65557, 1000200);
                AutoEquip(true);
            }
        }
        #endregion

        #region Conjurer
        if (Player.Job == Job.CNJ)
        {
            // Close to Home (Level 1)
            if (IsQuestCompleted(65575) && !HasQuest(65660) && !IsQuestCompleted(65660))
            {
                GetTo(132, new Vector3(26.905f, -8f, 115.7461f));
                if (!IsQuestAccepted(65660))
                    PickUpQuest(65660, 1001140);
            }
            if (HasQuest(65660) && !IsQuestCompleted(65660) && GetQuestStep(65660) == 1 && !IsTodoChecked(65660, 1, 0))
            {
                GetTo(132, new Vector3(35.13748f, 1.690557f, 34.49274f));
            }
            if (HasQuest(65660) && !IsQuestCompleted(65660) && GetQuestStep(65660) == 1 && !IsTodoChecked(65660, 1, 2))
            {
                GetTo(133, new Vector3(170.4057f, 15.5f, -89.87196f));
                TalkTo(2000120); // hand in item 2000120
            }
            if (HasQuest(65660) && !IsQuestCompleted(65660) && GetQuestStep(65660) == 1 && !IsTodoChecked(65660, 1, 1))
            {
                GetTo(133, new Vector3(-234.0504f, -4f, -7.944408f));
                TalkTo(1000323);
            }

            // Way of the Conjurer (Level 1)
            if (!HasQuest(65558) && !IsQuestCompleted(65558))
            {
                GetTo(133, new Vector3(-234.1575f, -4.000001f, -7.586702f));
                if (!IsQuestAccepted(65558))
                    PickUpQuest(65558, 1000323);
            }
            if (HasQuest(65558) && !IsQuestCompleted(65558) && GetQuestStep(65558) == 1)
            {
                GetTo(133, new Vector3(-256.8806f, -5.774293f, -25.05456f));
                TalkTo(1000692);
            }

            // Close to Home (Level 1)
            if (HasQuest(65660) && !IsQuestCompleted(65660) && GetQuestStep(65660) == 255)
            {
                GetTo(132, new Vector3(26.52929f, -8f, 116.2287f));
                TurnInQuest(65660, 1000100);
                AutoEquip(true);
            }

            // Way of the Conjurer (Level 1)
            if (HasQuest(65558) && !IsQuestCompleted(65558) && GetQuestStep(65558) == 2 && !IsTodoChecked(65558, 2, 0))
            {
                GetTo(148, new Vector3(104.0594f, 14.69139f, -262.7849f));
                Grind(GetMobName(37)); // ground squirrel
            }
            if (HasQuest(65558) && !IsQuestCompleted(65558) && GetQuestStep(65558) == 2 && !IsTodoChecked(65558, 2, 1))
            {
                GetTo(148, new Vector3(104.0594f, 14.69139f, -262.7849f));
                Grind(GetMobName(49)); // little ladybug
            }
            if (HasQuest(65558) && !IsQuestCompleted(65558) && GetQuestStep(65558) == 2 && !IsTodoChecked(65558, 2, 2))
            {
                GetTo(148, new Vector3(113.5422f, 0.7499394f, -171.7874f));
                Grind(GetMobName(47)); // forest funguar
            }
            if (HasQuest(65558) && !IsQuestCompleted(65558) && GetQuestStep(65558) == 255)
            {
                GetTo(133, new Vector3(-256.5681f, -5.774275f, -24.72007f));
                TurnInQuest(65558, 1000692);
                AutoEquip(true);
            }
        }
        #endregion

        #region Lancer
        if (Player.Job == Job.LNC)
        {
            // Close to Home (Level 1)
            if (IsQuestCompleted(65575) && !HasQuest(65621) && !IsQuestCompleted(65621))
            {
                GetTo(132, new Vector3(26.905f, -8f, 115.7461f));
                if (!IsQuestAccepted(65621))
                    PickUpQuest(1001140, 65621);
            }

            if (HasQuest(65621) && !IsQuestCompleted(65621) && GetQuestStep(65621) == 1 && !IsTodoChecked(65621, 1, 1))
            {
                GetTo(133, new Vector3(144.8345f, 15.5f, -268.0915f));
                TalkTo(1000251);
            }

            // Way of the Lancer (Level 1)
            if (!HasQuest(65559) && !IsQuestCompleted(65559))
            {
                GetTo(133, new Vector3(144.8345f, 15.5f, -268.0915f));
                if (!IsQuestAccepted(65559))
                    PickUpQuest(1000251, 65559);
            }

            if (HasQuest(65559) && !IsQuestCompleted(65559) && GetQuestStep(65559) == 1)
            {
                GetTo(133, new Vector3(157.7019f, 15.90038f, -270.3442f));
                TalkTo(1000254);
            }

            // Close to Home (Level 1)
            if (HasQuest(65621) && !IsQuestCompleted(65621) && GetQuestStep(65621) == 1 && !IsTodoChecked(65621, 1, 2))
            {
                GetTo(133, new Vector3(172.3506f, 15.5f, -89.95197f));
                TalkTo(1000768); // hand over item 2000074
            }

            if (HasQuest(65621) && !IsQuestCompleted(65621) && GetQuestStep(65621) == 255)
            {
                GetTo(132, new Vector3(26.52929f, -8f, 116.2287f));
                TurnInQuest(65621, 1000100);
                AutoEquip(true);
            }

            // Way of the Lancer (Level 1)
            if (HasQuest(65559) && !IsQuestCompleted(65559) && GetQuestStep(65559) == 2 && !IsTodoChecked(65559, 2, 0))
            {
                GetTo(148, new Vector3(103.3762f, 14.36276f, -255.1773f));
                Grind(GetMobName(37)); // ground squirrel
            }

            if (HasQuest(65559) && !IsQuestCompleted(65559) && GetQuestStep(65559) == 2 && !IsTodoChecked(65559, 2, 1))
            {
                GetTo(148, new Vector3(103.3762f, 14.36276f, -255.1773f));
                Grind(GetMobName(49)); // little ladybug
            }

            if (HasQuest(65559) && !IsQuestCompleted(65559) && GetQuestStep(65559) == 2 && !IsTodoChecked(65559, 2, 2))
            {
                GetTo(148, new Vector3(116.4344f, 0.7488447f, -178.9012f));
                Grind(GetMobName(47)); // forest funguar
            }

            if (HasQuest(65559) && !IsQuestCompleted(65559) && GetQuestStep(65559) == 255)
            {
                GetTo(133, new Vector3(157.7019f, 15.90038f, -270.3442f));
                TurnInQuest(65559, 1000254); // hand over item 2000074
                AutoEquip(true);
            }
        }
        #endregion

        #region Buy Boiled Eggs
        if (!HasItem(4650))
        {
            GetTo(133, new Vector3(162.4625f, 15.69993f, -133.2141f));
            BuyItem(4650u, 10, 1000232u);
        }
        #endregion
    }
}
