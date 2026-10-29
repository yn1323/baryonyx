using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Networking;
using UnityEngine;

namespace Baryonyx.WorldMap
{
    /// <summary>The result of paying for one square: paid (or already open), or why not.</summary>
    public readonly struct WorldAtlasPayment
    {
        private WorldAtlasPayment(bool paid, string message)
        {
            Paid = paid;
            Message = message;
        }

        public bool Paid { get; }

        // 払えなかったときに地図の下に出す知らせ。
        public string Message { get; }

        public static WorldAtlasPayment Done => new(true, null);

        public static WorldAtlasPayment Failed(string message) => new(false, message);
    }

    /// <summary>
    /// The runes of the nested world map while the app runs
    /// (doc/features/world-atlas.md#マスを開くルーン): the squares paid for and the balance, read
    /// from <see cref="Source"/> (the game server, set by the app) when the map opens, and the
    /// payment for one square just before the party hops onto it. Without a server, a local
    /// stand-in keeps them while the app runs. The switch that stops charging and the choice to
    /// skip the confirmation also live here.
    /// </summary>
    public static class WorldAtlasSession
    {
        // 残高が足りないときにサーバーが断る理由。
        public const string InsufficientRunesError = "insufficient_runes";
        public const string ShortMessage = "ルーンが足りません";
        public const string FailedMessage = "ルーンを払えませんでした";
        public const string LoadFailedMessage = "開けたマスを読み込めませんでした";

        // サーバーがないときに持たせるルーン。
        public const int LocalRunes = 5000;

        // 消費のオンオフを端末に残すキー（開発用のビルドとEditorだけで切り替える）。
        public const string ChargingKey = "WorldAtlas.Charging";

        private static readonly Dictionary<string, HashSet<string>> opened = new();
        private static readonly Dictionary<string, Dictionary<string, int>> prices = new();
        private static WorldAtlasMemorySource local;
        private static Task loading;

        /// <summary>The server that keeps the squares. Without it, a local stand-in is used.</summary>
        public static IWorldAtlasSource Source { get; set; }

        public static IWorldAtlasSource SourceOrLocal =>
            Source ?? (local ??= new WorldAtlasMemorySource(LocalRunes, PriceOf));

        /// <summary>The squares paid for and the balance have been read.</summary>
        public static bool Loaded { get; private set; }

        /// <summary>The balance last read or left after a payment.</summary>
        public static int Runes { get; private set; }

        /// <summary>
        /// Whether moving onto a new square costs runes. Only development builds and the Editor
        /// can turn it off (<see cref="CanSwitch"/>); it is kept on the device.
        /// </summary>
        public static bool Charging
        {
            get => !CanSwitch || PlayerPrefs.GetInt(ChargingKey, 1) == 1;
            set
            {
                if (!CanSwitch)
                    return;
                PlayerPrefs.SetInt(ChargingKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        public static bool CanSwitch => Debug.isDebugBuild || Application.isEditor;

        /// <summary>
        /// The player chose not to be asked before a walk of several squares until the app starts
        /// again. Kept only in memory, so it is back to asking after a restart.
        /// </summary>
        public static bool SkipConfirm { get; set; }

        /// <summary>Called when the squares or the balance change.</summary>
        public static event Action Changed;

        /// <summary>Called with the runes taken each time a square is paid for.</summary>
        public static event Action<int> Spent;

        /// <summary>Remembers a map's prices, for the local stand-in.</summary>
        public static void Register(WorldAtlasLayout layout)
        {
            if (layout == null || prices.ContainsKey(layout.key))
                return;
            var map = new Dictionary<string, int>();
            foreach (var node in layout.nodes)
                map[node.id] = node.price;
            prices[layout.key] = map;
        }

        public static bool IsOpened(string map, string square) =>
            opened.TryGetValue(map, out var squares) && squares.Contains(square);

        /// <summary>The squares paid for on <paramref name="map"/>.</summary>
        public static IReadOnlyCollection<string> OpenedOn(string map) =>
            opened.TryGetValue(map, out var squares) ? squares : Array.Empty<string>();

        /// <summary>Reads the squares and the balance once; later calls wait for the same read.</summary>
        public static Task LoadAsync(CancellationToken token)
        {
            if (Loaded)
                return Task.CompletedTask;
            return loading ??= ReadAsync(token);
        }

        private static async Task ReadAsync(CancellationToken token)
        {
            try
            {
                var state = await SourceOrLocal.LoadAsync(token);
                opened.Clear();
                foreach (var (map, square) in state.Opened)
                    Keep(map, square);
                Runes = state.Runes;
                Loaded = true;
                Changed?.Invoke();
            }
            finally
            {
                // 読めなかったときは、次に呼ばれたときに読み直す。
                loading = null;
            }
        }

        /// <summary>
        /// Pays for the square <paramref name="square"/> of <paramref name="map"/>. A square
        /// already paid for costs nothing. Never throws: a failure comes back with what to tell.
        /// </summary>
        public static async Task<WorldAtlasPayment> OpenAsync(string map, string square)
        {
            if (IsOpened(map, square))
                return WorldAtlasPayment.Done;
            try
            {
                int before = Runes;
                Runes = await SourceOrLocal.OpenAsync(
                    Guid.NewGuid().ToString(),
                    map,
                    square,
                    CancellationToken.None
                );
                Keep(map, square);
                Changed?.Invoke();
                if (before > Runes)
                    Spent?.Invoke(before - Runes);
                return WorldAtlasPayment.Done;
            }
            catch (ServerApiException exception) when (exception.Error == InsufficientRunesError)
            {
                return WorldAtlasPayment.Failed(ShortMessage);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(FailedMessage + "。" + exception.Message);
                return WorldAtlasPayment.Failed(FailedMessage);
            }
        }

        private static void Keep(string map, string square)
        {
            if (!opened.TryGetValue(map, out var squares))
                opened[map] = squares = new HashSet<string>();
            squares.Add(square);
        }

        private static int PriceOf(string map, string square) =>
            prices.TryGetValue(map, out var squares) && squares.TryGetValue(square, out int price)
                ? price
                : 0;

        // Play Modeに入るたびに、前の接続先・開けたマス・確認を省く選択を持ち越さない。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset()
        {
            Source = null;
            local = null;
            loading = null;
            opened.Clear();
            prices.Clear();
            Loaded = false;
            Runes = 0;
            SkipConfirm = false;
            Changed = null;
            Spent = null;
        }
    }
}
