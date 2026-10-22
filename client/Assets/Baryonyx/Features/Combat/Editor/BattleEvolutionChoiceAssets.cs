using Baryonyx.Combat.Presentation;
using Baryonyx.UI;
using Baryonyx.UI.Editor;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Baryonyx.Editor.UI.UiBuild;

namespace Baryonyx.Combat.Editor
{
    /// <summary>
    /// Builds where the next stage of an evolving unique skill is picked
    /// (<see cref="BattleEvolutionChoice"/>) on a screen's controls canvas: a branch of light and
    /// a battle card for each of up to three stages in a row under the middle, an "acquired" tag
    /// on each card, and the button that confirms the stage picked under the row.
    /// </summary>
    public static class BattleEvolutionChoiceAssets
    {
        private static readonly Color Gold = new(0.98f, 0.8f, 0.36f);

        // The stages' cards: the battle card (70x98 dots at 3 px) a size up, so their text reads.
        private const float CardScale = 2.1f;

        // The light round the stage picked: wisps rising from its edges and motes among them.
        private const int Wisps = 22;
        private const int Motes = 9;
        private static readonly Vector2 SlotSize = new(70 * 3 * CardScale, 98 * 3 * CardScale);

        // As tall as a finger needs (doc/rules/ui-design.md).
        private const float ButtonHeight = 128f;

        /// <summary>
        /// The choice over <paramref name="screen"/>, hidden until opened, drawn with the
        /// effects' materials: <paramref name="beam"/> the branches and the rim round the stage
        /// picked, and <paramref name="glow"/>, <paramref name="flame"/> (its tongues are the
        /// rising wisps) and <paramref name="sparkle"/> the rest of its light.
        /// </summary>
        public static BattleEvolutionChoice Build(
            RectTransform screen,
            Material beam,
            Material glow,
            Material flame,
            Material sparkle
        )
        {
            var root = Rect("EvolutionChoice", screen);
            Stretch(root);
            // Takes the taps, so nothing behind it is pressed while choosing.
            AddImage(root, Color.clear, true);
            var choice = root.gameObject.AddComponent<BattleEvolutionChoice>();
            choice.Panel = root.gameObject.AddComponent<CanvasGroup>();
            BattleVfxImage.EnableShapeChannel(root);

            const int count = 3;
            choice.Branches = new BattleVfxImage[count];
            for (int i = 0; i < count; i++)
            {
                var branch = Rect("Branch" + i, root);
                branch.anchorMin = branch.anchorMax = new Vector2(0.5f, 0.5f);
                branch.pivot = new Vector2(0f, 0.5f);
                var image = branch.gameObject.AddComponent<BattleVfxImage>();
                image.material = beam;
                image.raycastTarget = false;
                choice.Branches[i] = image;
            }

            choice.Slots = new Button[count];
            choice.Cards = new BattleInspectCardView[count];
            choice.Auras = new RectTransform[count];
            choice.Veils = new Graphic[count];
            choice.AcquiredTags = new GameObject[count];
            // The mists first, so each lies behind every stage's card, not over its neighbours.
            for (int i = 0; i < count; i++)
                choice.Auras[i] = BuildAura(root, i, beam, glow, flame, sparkle);
            for (int i = 0; i < count; i++)
                BuildSlot(root, choice, i);

            var confirm = Rect("Confirm", root);
            confirm.anchorMin = confirm.anchorMax = new Vector2(0.5f, 0.5f);
            confirm.sizeDelta = new Vector2(420f, ButtonHeight);
            confirm.anchoredPosition = new Vector2(
                0f,
                choice.RowY - SlotSize.y * 0.5f - 12f - ButtonHeight * 0.5f
            );
            var (button, _, content) = GlassCardAssets.Choice(
                confirm,
                pattern: false,
                fill: GlassCardAssets.SceneFillColor
            );
            Stretch(
                Label(
                    content,
                    "Label",
                    "この進化にする",
                    36,
                    UiPalette.TextMain,
                    TextAlignmentOptions.Center
                ).rectTransform
            );
            choice.ConfirmButton = button;

            root.gameObject.SetActive(false);
            return choice;
        }

        // The light round a stage while it is picked: a soft light, a beam along each edge, wisps
        // rising from their roots (tongues of flame in pale gold) and motes.
        private static RectTransform BuildAura(
            RectTransform root,
            int i,
            Material beam,
            Material glow,
            Material flame,
            Material sparkle
        )
        {
            var aura = Rect("Aura" + i, root);
            aura.anchorMin = aura.anchorMax = aura.pivot = new Vector2(0.5f, 0.5f);
            aura.sizeDelta = SlotSize;
            AuraPart(aura, "Glow", glow);
            for (int p = 0; p < Wisps; p++)
                AuraPart(aura, "Wisp" + p, flame).rectTransform.pivot = new Vector2(0.5f, 0.04f);
            for (int p = 0; p < 4; p++)
                AuraPart(aura, "Rim" + p, beam).rectTransform.pivot = new Vector2(0f, 0.5f);
            for (int p = 0; p < Motes; p++)
                AuraPart(aura, "Mote" + p, sparkle).rectTransform.sizeDelta = Vector2.one * 24f;
            aura.gameObject.SetActive(false);
            return aura;
        }

        // One stage: a button the size of its card, the battle's card over it, a veil that
        // darkens it while another is picked, and the acquired tag.
        private static void BuildSlot(RectTransform root, BattleEvolutionChoice choice, int i)
        {
            var slot = Rect("Option" + i, root);
            slot.anchorMin = slot.anchorMax = slot.pivot = new Vector2(0.5f, 0.5f);
            slot.sizeDelta = SlotSize;
            var hit = AddImage(slot, Color.clear, true);
            var button = slot.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;
            choice.Slots[i] = button;

            var instance = InstantiatePrefab(BattleInspectAssets.CardPrefabPath, "Card", slot);
            var card = (RectTransform)instance.transform;
            card.anchorMin = card.anchorMax = card.pivot = new Vector2(0.5f, 0.5f);
            card.anchoredPosition = Vector2.zero;
            card.localScale = Vector3.one * CardScale;
            var input = instance.GetComponent<BattleInspectCardInput>();
            if (input != null)
                Object.DestroyImmediate(input);
            foreach (var graphic in instance.GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = false;
            choice.Cards[i] = instance.GetComponent<BattleInspectCardView>();

            var veil = Rect("Veil", slot);
            Stretch(veil);
            choice.Veils[i] = AddImage(veil, new Color(0f, 0f, 0f, 0f), false);

            var tag = Rect("Acquired", slot);
            tag.anchorMin = tag.anchorMax = tag.pivot = new Vector2(1f, 1f);
            tag.anchoredPosition = new Vector2(10f, 14f);
            tag.sizeDelta = new Vector2(200f, 60f);
            AddImage(tag, new Color(0.06f, 0.06f, 0.14f, 0.92f), false);
            Stretch(
                Label(tag, "Label", "取得済み", 36, Gold, TextAlignmentOptions.Center).rectTransform
            );
            tag.gameObject.SetActive(false);
            choice.AcquiredTags[i] = tag.gameObject;
        }

        // One shape of the mist; the choice moves and colours it.
        private static BattleVfxImage AuraPart(RectTransform aura, string name, Material material)
        {
            var part = Rect(name, aura);
            part.anchorMin = part.anchorMax = part.pivot = new Vector2(0.5f, 0.5f);
            var image = part.gameObject.AddComponent<BattleVfxImage>();
            image.material = material;
            image.raycastTarget = false;
            image.color = Color.clear;
            return image;
        }
    }
}
