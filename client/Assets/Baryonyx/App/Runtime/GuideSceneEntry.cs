using UnityEngine;

namespace Baryonyx.App
{
    /// <summary>
    /// How the next guide screen opens when another screen sends the player there for one
    /// thing (doc/features/screens/adventure.md, the adventure's equipment): straight at one of its items,
    /// with another first word in the breadcrumb, and with back and the close button returning
    /// to another scene. The guide screen that opens takes it once.
    /// </summary>
    public static class GuideSceneEntry
    {
        public sealed class Request
        {
            public Request(string itemKey, string title, string returnScene)
            {
                ItemKey = itemKey;
                Title = title;
                ReturnScene = returnScene;
            }

            public string ItemKey { get; }
            public string Title { get; }
            public string ReturnScene { get; }
        }

        private static Request pending;

        /// <summary>Asks the next guide screen to open at <paramref name="itemKey"/> and go back to <paramref name="returnScene"/>.</summary>
        public static void Open(string itemKey, string title, string returnScene) =>
            pending = new Request(itemKey, title, returnScene);

        /// <summary>The request for the screen that opens now, which forgets it; null when there is none.</summary>
        public static Request Take()
        {
            var request = pending;
            pending = null;
            return request;
        }

        // Play Modeに入るたびに、前の頼みを持ち越さない。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Clear() => pending = null;
    }
}
