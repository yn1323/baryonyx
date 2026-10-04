using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.Party
{
    /// <summary>One person's tab (the face above, the name below), baked by the generator.</summary>
    [Serializable]
    public sealed class PartyTab
    {
        public string Id = "";
        public Button Button;
        public GameObject Selected;
    }

    /// <summary>
    /// The row of people's tabs on the formation's per-person screens (skills, equipment): the
    /// party's slots first, a divider, then the other companions as owned, the chosen tab in a
    /// gold frame. PartyTabAssets bakes a tab for every mock character; this shows those the
    /// player has, in order, and scrolls the chosen tab into view when the person changes.
    /// </summary>
    [Serializable]
    public sealed class PartyTabStrip
    {
        public ScrollRect Scroll;
        public PartyTab[] Tabs = Array.Empty<PartyTab>();

        // パーティとほかの仲間の間の区切り。
        public GameObject Divider;

        // 最後に見えるように送った人。人が替わったときだけ送る。
        [NonSerialized]
        private string revealed;

        public PartyTab Find(string id) =>
            Array.Find(Tabs, tab => tab.Button != null && tab.Id == id);

        /// <summary>
        /// Shows <paramref name="people"/> in order with the divider after the party, and the
        /// tab at <paramref name="selected"/> in gold.
        /// </summary>
        public void Show(IReadOnlyList<string> people, int partyCount, int selected)
        {
            var order = new Dictionary<string, int>();
            for (int i = 0; i < people.Count; i++)
                order[people[i]] = i;
            var shown = new SortedList<int, Transform>();
            foreach (var tab in Tabs)
            {
                if (tab.Button == null)
                    continue;
                bool on = order.TryGetValue(tab.Id, out int index);
                tab.Button.gameObject.SetActive(on);
                if (tab.Selected != null)
                    tab.Selected.SetActive(on && index == selected);
                if (on)
                    shown[index] = tab.Button.transform;
            }
            bool divided = partyCount > 0 && partyCount < people.Count;
            if (Divider != null)
                Divider.SetActive(divided);
            int sibling = 0;
            foreach (var (index, tab) in shown)
            {
                if (divided && index == partyCount)
                    Divider.transform.SetSiblingIndex(sibling++);
                tab.SetSiblingIndex(sibling++);
            }

            string id = selected >= 0 && selected < people.Count ? people[selected] : null;
            if (id != revealed)
            {
                revealed = id;
                if (shown.TryGetValue(selected, out var tab))
                    Reveal((RectTransform)tab);
            }
        }

        // 読み込むまで、仮データの人を本当の仲間として見せない。
        public void Hide()
        {
            foreach (var tab in Tabs)
                if (tab.Button != null)
                    tab.Button.gameObject.SetActive(false);
            if (Divider != null)
                Divider.SetActive(false);
            Forget();
        }

        // 開き直したときは、選んだタブを改めて見える位置まで送る。
        public void Forget() => revealed = null;

        // 横に並んだタブのうち、選んだタブが見える位置まで中身を送る。
        private void Reveal(RectTransform tab)
        {
            // 停止中（Prefabの生成）は、並べたままにする。
            if (!Application.isPlaying)
                return;
            if (Scroll == null || Scroll.content == null || tab == null)
                return;
            var viewport =
                Scroll.viewport != null ? Scroll.viewport : (RectTransform)Scroll.transform;
            Canvas.ForceUpdateCanvases();
            var corners = new Vector3[4];
            tab.GetWorldCorners(corners);
            float left = viewport.InverseTransformPoint(corners[0]).x;
            float right = viewport.InverseTransformPoint(corners[2]).x;
            var bounds = viewport.rect;
            float shift =
                left < bounds.xMin ? bounds.xMin - left
                : right > bounds.xMax ? bounds.xMax - right
                : 0f;
            if (shift != 0f)
                Scroll.content.anchoredPosition += new Vector2(shift, 0f);
        }
    }
}
