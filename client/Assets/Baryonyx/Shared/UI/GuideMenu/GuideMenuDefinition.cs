using System;
using UnityEngine;

namespace Baryonyx.UI.GuideMenu
{
    /// <summary>How the right side of a guide screen is used.</summary>
    public enum GuideMenuLayout
    {
        // Menu buttons; each one opens a full list to choose from.
        List,

        // A world map with destinations to choose from.
        Map,
    }

    /// <summary>
    /// The content of one guide screen (tavern, workshop, temple, travel office): the guide on the
    /// left and the menu or map on the right. The generator bakes it into the screen prefab, so
    /// the rows can be read without entering Play Mode. The values are mock data until each
    /// feature has real data.
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

        public Texture2D MapArt;
        public GuideMapPoint[] MapPoints = Array.Empty<GuideMapPoint>();

        // The button under the map that sets off for the chosen destination.
        public string DepartLabel = "出発";
    }

    [Serializable]
    public sealed class GuideMenuItem
    {
        // Lets another screen open this item directly, e.g. Home's bonus button → "bonus".
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
    public sealed class GuideMapPoint
    {
        public string Name = "";

        // Position on the map image (0-1, bottom left is the origin).
        public Vector2 Position = new(0.5f, 0.5f);
        public string Detail = "";
        public bool Locked;
    }
}
