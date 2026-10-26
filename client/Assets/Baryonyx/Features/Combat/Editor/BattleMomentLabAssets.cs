using System;
using System.Collections.Generic;
using System.Linq;
using Baryonyx.Combat.Presentation;
using Baryonyx.Editor;
using Baryonyx.Editor.UI;
using Baryonyx.Equipment;
using Baryonyx.Inventory;
using Baryonyx.Inventory.Editor;
using Baryonyx.UI;
using Baryonyx.UI.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Baryonyx.Editor.UI.UiBuild;

namespace Baryonyx.Combat.Editor
{
    /// <summary>
    /// The battle moment lab's controls (BattleMomentLabPanel.prefab), laid over the battle
    /// screen in the lab's scene where the hand and its controls are put away: the room's name at
    /// the top right, the plate that shows what a drop held over the middle, and along the bottom
    /// two rows of buttons, one for each moment of the battle (<see cref="BattleMomentLab"/>),
    /// with the drop's quality, the repeat and the reset.
    /// </summary>
    public static class BattleMomentLabAssets
    {
        public const string PrefabPath =
            "Assets/Baryonyx/Features/Combat/UI/BattleMomentLabPanel.prefab";

        // How many pieces of each slot the drops take in turn.
        private const int SamplesPerSlot = 2;

        // The words on the moment buttons, in BattleMoment's order.
        private static readonly string[] MomentLabels =
        {
            "魔物が現れた",
            "強敵が現れた",
            "味方のターン",
            "連続ターン",
            "敵のターン",
            "エネルギー不足",
            "勝利",
            "強敵に勝利",
            "ドロップ",
            "勝利→ドロップ",
            "全滅",
            "ルーン復活",
        };

        // Which row each moment's button is on: the battle going on above, its end below.
        private const int FirstEndingMoment = (int)BattleMoment.Victory;

        // The drop's plate, high over the middle of the battlefield, above the party's heads.
        private static readonly Vector2 PlateCenter = new(40f, 250f);

        [MenuItem("Baryonyx/Combat/Create Battle Moment Lab Assets")]
        public static void CreateAssets()
        {
            EditorGuard.RequireEditMode();
            if (MomentLabels.Length != Enum.GetValues(typeof(BattleMoment)).Length)
                throw new InvalidOperationException("Every battle moment needs a label.");

            UiArt.EnsureAll();
            var font = GameFontAssets.GetOrCreate();
            using (UiBuild.Begin(font, UiArt.EnsureTextShadow(font)))
            {
                var root = CanvasRoot("BattleMomentLabPanel");
                // Over the battle screen's own overlay (sorting order 1).
                root.GetComponent<Canvas>().sortingOrder = 2;
                var lab = root.gameObject.AddComponent<BattleMomentLab>();
                var safe = SafeArea(root);
                EnemyLabAssets.Title(safe, "戦闘の演出デバッグルーム");
                lab.Reveal = RewardRevealAssets.Build(safe, PlateCenter);
                EnemyLabAssets.BuildStrip(safe);
                BuildRows(safe, lab);
                lab.Samples = RewardSamples();
                PrefabUtility.SaveAsPrefabAsset(root.gameObject, PrefabPath);
                AssetDatabase.SaveAssetIfDirty(font);
            }
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// The rarest weapons, armour and accessories with art, the slots in turn: what the labs'
        /// drops and chests hold, shown as a real drop of them is.
        /// </summary>
        public static RewardItem[] RewardSamples()
        {
            var art = ItemArtAssets.Load();
            var slots = EquipmentCatalog
                .All.Where(gear => art.Find(gear.Id) != null)
                .GroupBy(gear => gear.Slot)
                .OrderBy(slot => slot.Key)
                .Select(slot =>
                    slot.OrderByDescending(gear => gear.Rarity).Take(SamplesPerSlot).ToArray()
                )
                .ToArray();
            var samples = new List<RewardItem>();
            for (int i = 0; i < SamplesPerSlot; i++)
            {
                foreach (var slot in slots)
                {
                    if (i >= slot.Length)
                        continue;
                    var gear = slot[i];
                    samples.Add(
                        new RewardItem
                        {
                            Kind =
                                $"{EquipmentCatalog.NameOf(gear.Slot)}　{EquipmentCatalog.Stars(gear.Rarity)}",
                            Name = gear.Name,
                            Icon = art.Find(gear.Id),
                            FoilStyle = EquipmentCatalog.FoilStyleOf(gear.Id),
                        }
                    );
                }
            }
            if (samples.Count == 0)
                throw new InvalidOperationException("No weapon, armour or accessory has art.");
            return samples.ToArray();
        }

        /// <summary>
        /// The upper row: the moments of the battle going on, the repeat and the reset. The lower
        /// row: the moments of its end, the drop's quality and the way to the exploration moment
        /// lab.
        /// </summary>
        private static void BuildRows(RectTransform safe, BattleMomentLab lab)
        {
            var upper = EnemyLabAssets.Row(safe, "Battle", EnemyLabAssets.ChooserBottom);
            var lower = EnemyLabAssets.Row(safe, "Ending", EnemyLabAssets.ActionsBottom);
            var buttons = new Button[MomentLabels.Length];
            for (int i = 0; i < MomentLabels.Length; i++)
                buttons[i] = EnemyLabAssets.SharedWidthButton(
                    i < FirstEndingMoment ? upper : lower,
                    ((BattleMoment)i).ToString(),
                    MomentLabels[i]
                );
            lab.MomentButtons = buttons;
            (lab.RepeatButton, lab.RepeatLabel) = EnemyLabAssets.ControlButton(
                upper,
                "Repeat",
                "くり返し：切",
                250f
            );
            lab.ResetButton = EnemyLabAssets.ControlButton(upper, "Reset", "元に戻す", 210f).button;
            (lab.QualityButton, lab.QualityLabel) = EnemyLabAssets.ControlButton(
                lower,
                "Quality",
                BattleMomentLab.QualityText(ItemQuality.S),
                280f
            );
            // The quality's letter is painted in its colour.
            lab.QualityLabel.richText = true;
            lab.OtherLabButton = EnemyLabAssets
                .ControlButton(lower, "OtherLab", "探索の演出へ", 250f)
                .button;
        }
    }
}
