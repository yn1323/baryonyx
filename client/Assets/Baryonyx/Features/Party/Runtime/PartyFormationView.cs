using System;
using System.Collections.Generic;
using Baryonyx.UI.GuideMenu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.Party
{
    /// <summary>One of the four party slots on the left, baked by the generator.</summary>
    [Serializable]
    public sealed class PartySlotWidget
    {
        public Button Button;
        public GameObject Selected;

        // 立ち姿（4倍）と足元の影。空きの枠では隠す。
        public RawImage Sprite;
        public GameObject Shadow;
        public TMP_Text Name;
        public TMP_Text Level;
        public PartyCardWidget[] Cards = Array.Empty<PartyCardWidget>();

        // 空いているときだけ出す「空き」。
        public TMP_Text Empty;
    }

    /// <summary>The attribute icon of one card skill under a slot.</summary>
    [Serializable]
    public sealed class PartyCardWidget
    {
        public Image Icon;

        // 属性のないカードに出す「無」。
        public GameObject None;
    }

    /// <summary>One owned character's tile on the right, baked by the generator.</summary>
    [Serializable]
    public sealed class PartyMemberWidget
    {
        public string Id = "";
        public Button Button;

        // 育成でLvが上がるので、開くたびに書き直す。
        public TMP_Text Level;
    }

    /// <summary>
    /// The tavern's formation over the whole guide screen: the four slots on the left and the
    /// owned characters not in the party on the right, ending with "外す". PartyAssets bakes
    /// the slots and tiles into the tavern prefab, so they read in the editor; this view wires
    /// them up and drives its own presenter. Changes are told on the guide screen's notice band.
    /// </summary>
    public sealed class PartyFormationView : MonoBehaviour, IPartyFormationView
    {
        // 64×64の戦闘のドット絵のうち、立ち姿が収まる範囲（ドット、絵の左上が原点）。
        // 周りの透明な余白を切り、仲間のタイルを4倍のまま4列に並べられる大きさにする。
        // 左右の余白を同じだけ切るため、左右を反転しても同じ範囲になる。
        public static readonly RectInt Figure = new(8, 12, 48, 52);

        // 押せないときの「外す」の文字の濃さ。
        private const float LeaveDisabledAlpha = 0.45f;

        public PartyMockData Data;

        // 通知は案内人の画面の通知の帯に出す。
        public GuideMenuView Guide;

        public TMP_Text Owned;
        public PartySlotWidget[] Slots = Array.Empty<PartySlotWidget>();
        public PartyMemberWidget[] Members = Array.Empty<PartyMemberWidget>();
        public Button Leave;
        public TMP_Text LeaveLabel;
        public ScrollRect List;

        private PartyFormationPresenter presenter;
        private readonly List<(Button Button, UnityEngine.Events.UnityAction Action)> bindings =
            new();

        public event Action<int> SlotPressed;
        public event Action<string> MemberPressed;
        public event Action LeavePressed;

        public PartyFormationPresenter Presenter => presenter;
        public string LastNotice { get; private set; } = "";

        private void OnEnable()
        {
            for (int i = 0; i < Slots.Length; i++)
            {
                int index = i;
                Bind(Slots[i].Button, () => SlotPressed?.Invoke(index));
            }
            foreach (var member in Members)
            {
                string id = member.Id;
                Bind(member.Button, () => MemberPressed?.Invoke(id));
            }
            Bind(Leave, () => LeavePressed?.Invoke());

            // 開くたびに、アプリを動かしている間の編成で描き直す。
            presenter?.Dispose();
            presenter = null;
            if (List != null)
                List.verticalNormalizedPosition = 1f;
            if (Data != null)
                presenter = new PartyFormationPresenter(this, PartySession.Formation(Data));
        }

        private void OnDisable()
        {
            foreach (var (button, action) in bindings)
                if (button != null)
                    button.onClick.RemoveListener(action);
            bindings.Clear();
            presenter?.Dispose();
            presenter = null;
        }

        public void Render(PartyFormationState state)
        {
            if (Owned != null)
                Owned.text = state.OwnedText;
            // Lvは育成で上げたもの、カードは酒場のカードスキルで付け替えたもの（アプリを動かしている間）。
            for (int i = 0; i < Slots.Length && i < state.Slots.Count; i++)
            {
                string id = state.Slots[i].Member;
                ShowSlot(
                    Slots[i],
                    Data,
                    Data.Find(id),
                    PartySession.LevelOf(Data, id),
                    PartySession.CardsOf(Data, id),
                    state.Slots[i].Selected
                );
            }

            // 右には、パーティにいないキャラだけを持っている順に並べる。「外す」は最後に置く。
            var bench = new HashSet<string>(state.Bench);
            foreach (var member in Members)
            {
                if (member.Button != null)
                    member.Button.gameObject.SetActive(bench.Contains(member.Id));
                Set(member.Level, LevelText(PartySession.LevelOf(Data, member.Id)));
            }
            if (Leave != null)
                Leave.interactable = state.CanLeave;
            if (LeaveLabel != null)
                LeaveLabel.alpha = state.CanLeave ? 1f : LeaveDisabledAlpha;
        }

        public void ShowNotice(string message)
        {
            LastNotice = message ?? "";
            if (Guide != null)
                Guide.ShowToast(message);
        }

        public static string LevelText(int level) => $"Lv {level}";

        /// <summary>
        /// Shows a member (or an empty slot) in a slot with the level and card skills to show.
        /// The generator calls it too, with the mock data's level and cards, so the prefab shows
        /// the mock formation in the editor.
        /// </summary>
        public static void ShowSlot(
            PartySlotWidget widget,
            PartyMockData data,
            PartyMember member,
            int level,
            IReadOnlyList<string> skills,
            bool selected
        )
        {
            bool filled = member != null;
            if (widget.Selected != null)
                widget.Selected.SetActive(selected);
            if (widget.Sprite != null)
            {
                widget.Sprite.gameObject.SetActive(filled && member.Art != null);
                if (filled)
                    Paint(widget.Sprite, member, trim: false);
            }
            if (widget.Shadow != null)
                widget.Shadow.SetActive(filled);
            Set(widget.Name, filled ? member.Name : "");
            Set(widget.Level, filled ? LevelText(level) : "");
            for (int i = 0; i < widget.Cards.Length; i++)
            {
                bool shown = filled && skills != null && i < skills.Count;
                var sprite = shown && data != null ? data.IconOf(skills[i]) : null;
                var icon = widget.Cards[i];
                if (icon.Icon != null)
                {
                    icon.Icon.gameObject.SetActive(shown);
                    icon.Icon.sprite = sprite;
                    icon.Icon.enabled = sprite != null;
                }
                if (icon.None != null)
                    icon.None.SetActive(shown && sprite == null);
            }
            if (widget.Empty != null)
                widget.Empty.gameObject.SetActive(!filled);
        }

        /// <summary>
        /// Draws the whole 64x64 sprite, or only the <see cref="Figure"/> without the clear
        /// margin, with the member's tint and facing. The pixel-perfect image sizes itself from
        /// the texture and the crop.
        /// </summary>
        public static void Paint(RawImage image, PartyMember member, bool trim)
        {
            image.texture = member.Art;
            image.color = member.Tint;
            if (member.Art == null)
                return;
            float width = member.Art.width;
            float height = member.Art.height;
            var uv = trim
                ? new Rect(
                    Figure.x / width,
                    1f - Figure.yMax / height,
                    Figure.width / width,
                    Figure.height / height
                )
                : new Rect(0f, 0f, 1f, 1f);
            if (member.Flip)
                uv = new Rect(uv.xMax, uv.y, -uv.width, uv.height);
            image.uvRect = uv;
        }

        private void Bind(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
                return;
            button.onClick.AddListener(action);
            bindings.Add((button, action));
        }

        private static void Set(TMP_Text label, string text)
        {
            if (label != null)
                label.text = text ?? "";
        }
    }
}
