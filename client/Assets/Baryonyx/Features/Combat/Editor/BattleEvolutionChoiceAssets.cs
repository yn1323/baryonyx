using Baryonyx.Combat.Presentation;
using Baryonyx.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Baryonyx.Editor.UI.UiBuild;

namespace Baryonyx.Combat.Editor
{
    /// <summary>
    /// Builds where the next stage of an evolving unique skill is picked
    /// (<see cref="BattleEvolutionChoice"/>) on a screen's controls canvas: a branch of light and
    /// a battle card for each of up to three stages in a row under the middle, an "acquired" tag
    /// on each card, and the check that confirms the stage picked where the end of the turn is.
    /// </summary>
    public static class BattleEvolutionChoiceAssets
    {
        private static readonly Color Gold = new(0.98f, 0.8f, 0.36f);

        // The stages' cards: the battle card (70x98 dots at 3 px) as large as fits between the
        // hovering card above and the check at the right of the hand.
        private const float CardScale = 2.45f;
        private static readonly Vector2 SlotSize = new(70 * 3 * CardScale, 98 * 3 * CardScale);

        // How far past its card the haze behind the stage picked may reach (px): the board it is
        // drawn on, on every side. The haze is an oval about twice the card's size (2026-10-29).
        private const float AuraMargin = 330f;

        private const string AuraShaderPath =
            "Assets/Baryonyx/Features/Combat/Vfx/Shaders/BattleCardAura.shader";
        private const string AuraMaterialPath =
            "Assets/Baryonyx/Features/Combat/Vfx/Materials/VfxCardAura.mat";

        /// <summary>
        /// The choice over <paramref name="screen"/>, hidden until opened, drawn with the
        /// effects' material <paramref name="beam"/> for the branches; the haze behind the stage
        /// picked has its own (<see cref="EnsureAuraMaterial"/>).
        /// </summary>
        public static BattleEvolutionChoice Build(RectTransform screen, Material beam)
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
            choice.Auras = new RawImage[count];
            var haze = EnsureAuraMaterial();
            choice.Veils = new Graphic[count];
            choice.AcquiredTags = new GameObject[count];
            // The lights first, so each lies behind every stage's card, not over its neighbours.
            for (int i = 0; i < count; i++)
                choice.Auras[i] = BuildAura(root, i, haze);
            for (int i = 0; i < count; i++)
                BuildSlot(root, choice, i);

            BuildConfirm(root, choice);

            root.gameObject.SetActive(false);
            return choice;
        }

        // The check that confirms, in the end-turn button's place and shape (its circle, a gold
        // check where its white tick is), over a glow that breathes while it can be pressed.
        private static void BuildConfirm(RectTransform root, BattleEvolutionChoice choice)
        {
            var safe = SafeArea(root);
            var art = BattleInspectAssets.Art("EndTurnCircle");
            float circle = art.width - BattleInspectAssets.EndTurnGlow * 2f;

            // The glow round the circle (white, coloured by the choice), as far out on every side.
            var haloArt = BattleInspectAssets.Art("EndTurnHalo");
            float spread = (haloArt.width - circle) * 0.5f;
            var glow = Rect("ConfirmGlow", safe);
            BattleInspectAssets.BesideHand(
                glow,
                true,
                BattleInspectView.EndTurnBottom + BattleInspectAssets.EndTurnGlow - spread,
                new Vector2(haloArt.width, haloArt.height),
                -spread
            );
            var light = BattleInspectAssets.AddRaw(glow, haloArt);
            light.raycastTarget = false;
            light.color = Color.clear;
            choice.ConfirmGlow = light;

            var button = Rect("Confirm", safe);
            var face = BattleInspectAssets.EndTurnCircle(button);
            var checkArt = BattleInspectAssets.Art("ConfirmCheck");
            var check = Rect("Check", button);
            Place(check, Vector2.zero, new Vector2(checkArt.width, checkArt.height));
            var mark = BattleInspectAssets.AddRaw(check, checkArt);
            mark.raycastTarget = false;
            mark.color = Gold;
            var confirm = (TintGroupButton)AddTintButton(button, face);
            confirm.SetTintGraphics(new Graphic[] { mark });
            choice.ConfirmButton = confirm;
        }

        /// <summary>The material of the haze behind the stage picked (BattleCardAura.shader).</summary>
        public static Material EnsureAuraMaterial()
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(AuraShaderPath);
            if (shader == null)
                throw new System.InvalidOperationException("Missing shader: " + AuraShaderPath);
            var material = AssetDatabase.LoadAssetAtPath<Material>(AuraMaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "VfxCardAura" };
                AssetDatabase.CreateAsset(material, AuraMaterialPath);
            }
            // Back to the shader's own values each time, so names it no longer has do not stay.
            var fresh = new Material(shader) { name = "VfxCardAura" };
            EditorUtility.CopySerialized(fresh, material);
            Object.DestroyImmediate(fresh);
            EditorUtility.SetDirty(material);
            return material;
        }

        // The haze behind a stage while it is picked: a board larger than its card on every side,
        // the card's shape in its middle.
        private static RawImage BuildAura(RectTransform root, int i, Material haze)
        {
            var aura = Rect("Aura" + i, root);
            aura.anchorMin = aura.anchorMax = aura.pivot = new Vector2(0.5f, 0.5f);
            aura.sizeDelta = SlotSize + Vector2.one * AuraMargin * 2f;
            var image = aura.gameObject.AddComponent<RawImage>();
            image.material = haze;
            image.raycastTarget = false;
            image.color = Color.clear;
            aura.gameObject.SetActive(false);
            return image;
        }

        // A sliced picture of the glass card's, its border drawn <paramref name="scale"/> times as large.

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
    }
}
