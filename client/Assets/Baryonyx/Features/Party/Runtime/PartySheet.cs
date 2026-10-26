using System;
using System.Collections.Generic;
using Baryonyx.UI.Buttons;
using Baryonyx.UI.GuideMenu;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.Party
{
    /// <summary>One person's tile in the column on the left of the adventurer's tabs.</summary>
    [Serializable]
    public sealed class PartySheetPersonWidget
    {
        public string Id = "";
        public Button Button;

        // 選んでいる人の金の縁と光。
        public GameObject Selected;

        // 戦闘のドット絵の上半身。
        public RawImage Figure;
    }

    /// <summary>
    /// The frame the adventurer's three tabs share (the page's stats, the equipment and the card
    /// skills): a column of the people on the left (the party, a line, then the others) and the
    /// tabs down the right. Each tab is a guide screen item of its own; pressing another tab
    /// opens that item with the same person, and pressing a person tells the open tab to show
    /// them. PartySheetAssets bakes it into each tab's panel.
    /// </summary>
    public sealed class PartySheet : MonoBehaviour
    {
        public GuideMenuView Guide;

        // 左の列：持っている全員の札（生成時の仮データの順）と、パーティと控えの間の線。
        public PartySheetPersonWidget[] People = Array.Empty<PartySheetPersonWidget>();
        public RectTransform Divider;
        public ScrollRect List;

        // 右の縦のタブと、それぞれが開く案内人の画面の項目のキー。Current がこのパネルの項目。
        public GuideTab[] Tabs = Array.Empty<GuideTab>();
        public string[] TabKeys = Array.Empty<string>();
        public string Current = "";

        private readonly ButtonBindings bindings = new();

        // 左の列の人を押した。
        public event Action<string> PersonPressed;

        private void OnEnable()
        {
            foreach (var person in People)
            {
                string id = person.Id;
                bindings.Bind(person.Button, () => PersonPressed?.Invoke(id));
            }
            for (int i = 0; i < Tabs.Length && i < TabKeys.Length; i++)
            {
                string key = TabKeys[i];
                if (Tabs[i] != null)
                    bindings.Bind(Tabs[i].Button, () => Open(key));
            }
            ShowTabs();
        }

        private void OnDisable() => bindings.Clear();

        // 同じ案内人の画面の、ほかのタブの項目へ切り替える。人は PartySession.Selected で引き継ぐ。
        private void Open(string key)
        {
            if (Guide != null && !string.IsNullOrEmpty(key) && key != Current && Guide.HasItem(key))
                Guide.OpenItem(key);
        }

        public void ShowTabs()
        {
            for (int i = 0; i < Tabs.Length; i++)
                if (Tabs[i] != null)
                    Tabs[i].Chosen = i < TabKeys.Length && TabKeys[i] == Current;
        }

        /// <summary>
        /// Lines the people up as <paramref name="people"/> (the party's first
        /// <paramref name="partyCount"/>, then the others) with the line between, and lights
        /// <paramref name="selected"/>. The generator calls it too, with the mock party.
        /// </summary>
        public void Show(IReadOnlyList<PartyMember> people, int partyCount, string selected)
        {
            var tiles = new Dictionary<string, PartySheetPersonWidget>();
            foreach (var person in People)
            {
                if (person.Button == null)
                    continue;
                tiles[person.Id] = person;
                person.Button.gameObject.SetActive(false);
            }
            // 前から順に並べ直す。パーティの人のあとに線を置き、控えを続ける。
            int sibling = 0;
            for (int i = 0; i < people.Count; i++)
            {
                if (i == partyCount && Divider != null)
                    Divider.SetSiblingIndex(sibling++);
                if (!tiles.TryGetValue(people[i].Id, out var person))
                    continue;
                person.Button.gameObject.SetActive(true);
                person.Button.transform.SetSiblingIndex(sibling++);
                if (person.Selected != null)
                    person.Selected.SetActive(person.Id == selected);
                if (person.Figure != null)
                    PartyArt.PaintBust(person.Figure, people[i]);
            }
            if (Divider != null)
                Divider.gameObject.SetActive(partyCount > 0 && partyCount < people.Count);
        }
    }
}
