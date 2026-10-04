using System;
using UnityEngine;

namespace Baryonyx.UI.GuideMenu
{
    /// <summary>How the right side of a guide screen is used.</summary>
    public enum GuideMenuLayout
    {
        // Menu buttons; each one opens a full list to choose from.
        List,

        // A list of destinations to set out for, shown without a menu (the travel office).
        Destinations,
    }

    /// <summary>
    /// The content of one guide screen (tavern, workshop, temple, travel office): the guide on the
    /// left and the menu or the destinations on the right. The generator bakes it into the screen
    /// prefab, so the rows can be read without entering Play Mode. The values are mock data until
    /// each feature has real data.
    /// </summary>
    [CreateAssetMenu(menuName = "Baryonyx/Guide Menu Definition")]
    public sealed class GuideMenuDefinition : ScriptableObject
    {
        public string Title = "";

        public Texture2D GuideArt;

        // Design pixels per dot of the guide. The generated portraits differ in dot count, so
        // each one is scaled to fit the screen height (an exception to the 4x dot size).
        [Min(1f)]
        public float GuideDotSize = 3f;
        public Texture2D Background;
        public GuideMenuLayout Layout = GuideMenuLayout.List;

        public GuideMenuItem[] Items = Array.Empty<GuideMenuItem>();

        public GuideDestination[] Destinations = Array.Empty<GuideDestination>();

        // The button under the destinations that sets off for the chosen one.
        public string DepartLabel = "出発";
    }

    [Serializable]
    public sealed class GuideMenuItem
    {
        // Names the item for code, e.g. the tavern's "bonus" opens the bonus settings, not a list.
        public string Key = "";
        public string Label = "";
        public string Caption = "";

        // A 24x24 pixel-art icon before the label, drawn at 4x. Optional.
        public Sprite Icon;

        // The button that applies the chosen entry, e.g. "編成する".
        public string ConfirmLabel = "決定";
        public GuideListEntry[] Entries = Array.Empty<GuideListEntry>();
    }

    [Serializable]
    public sealed class GuideListEntry
    {
        public string Name = "";

        // A short value shown at the right end of the row, e.g. "Lv 12" or "★★★".
        public string Badge = "";
        public string Detail = "";
    }

    [Serializable]
    public sealed class GuideDestination
    {
        // The destination of the adventure it sets out for (server/src/features/adventure);
        // empty for a place the adventure cannot go to yet.
        public string Id = "";
        public string Name = "";

        public string Detail = "";

        // 推奨Lv。行の右端に「推奨Lv15」のように書く。
        [Min(1)]
        public int RecommendedLevel = 1;

        // 未踏の地。地名を「？？？」に伏せ、説明を出さず、行を暗くして選べなくする。
        public bool Locked;
    }
}
