using UnityEngine;

namespace Baryonyx.App
{
    /// <summary>
    /// How the next guide screen opens when another screen sends the player there for one
    /// thing (doc/features/screens/adventure.md, the adventure's equipment): straight at one of its items,
    /// with another first word in the breadcrumb, and with back and the close button returning
    /// to another scene. It may also name the entry to show in that item (a material's page of
    /// the library opened from the craft) and what to open when coming back. The guide screen
    /// that opens takes it once.
    /// </summary>
    public static class GuideSceneEntry
    {
        public sealed class Request
        {
            public Request(
                string itemKey,
                string title,
                string returnScene,
                string entry = null,
                Request back = null
            )
            {
                ItemKey = itemKey;
                Title = title;
                ReturnScene = returnScene;
                Entry = entry;
                Back = back;
            }

            public string ItemKey { get; }
            public string Title { get; }

            // 空なら、開いた項目からの「もどる」はふだんどおりメニューへ戻る（戻ってきたときの開き方）。
            public string ReturnScene { get; }

            // 開いた項目で見せる物（IGuideEntryTarget に渡す）。空なら項目のふだんの見せ方。
            public string Entry { get; }

            // 「もどる」で元の画面へ戻るときに、元の画面で開き直す項目と物。
            public Request Back { get; }
        }

        private static Request pending;

        /// <summary>Asks the next guide screen to open at <paramref name="itemKey"/> and go back to <paramref name="returnScene"/>.</summary>
        public static void Open(string itemKey, string title, string returnScene) =>
            pending = new Request(itemKey, title, returnScene);

        /// <summary>Asks the next guide screen to open as <paramref name="request"/> says.</summary>
        public static void Open(Request request) => pending = request;

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
