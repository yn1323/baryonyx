using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Adventure;
using Baryonyx.Networking;
using Baryonyx.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.App
{
    /// <summary>
    /// The adventure debugging room (doc/features/screens/debug-rooms.md#冒険のデバッグルーム):
    /// sets an adventure up just before a room of the kind chosen, on the app's own stand-in for
    /// the server (nothing is saved to the game server), and opens the exploration there, so the
    /// real screens go on from it: the room's event, the battle, the win, the drops and the boons,
    /// the way back, the end of the adventure. The destination, the party's HP and how the
    /// requests go (<see cref="DebugNetwork"/>) can be chosen too. The stand-in is used until
    /// Home opens again (<see cref="End"/>).
    /// </summary>
    public sealed class AdventureLab : MonoBehaviour
    {
        /// <summary>A room to go into next: its words on the button, its kind and how the stand-in draws its contents.</summary>
        public readonly struct NextRoom
        {
            public NextRoom(string label, AdventureRoomKind kind, Action<AdventureLocalSource> draw = null)
            {
                Label = label;
                Kind = kind;
                Draw = draw;
            }

            public string Label { get; }
            public AdventureRoomKind Kind { get; }
            public Action<AdventureLocalSource> Draw { get; }
        }

        /// <summary>The rooms the room offers to go into next, in the order the button steps through.</summary>
        public static readonly NextRoom[] NextRooms =
        {
            new("魔物", AdventureRoomKind.Battle),
            new("強い魔物", AdventureRoomKind.Elite),
            new("最奥の主", AdventureRoomKind.Boss),
            new("宝箱", AdventureRoomKind.Treasure, source => source.NextAwakening = false),
            new("宝箱（覚醒が出る）", AdventureRoomKind.Treasure, source => source.NextAwakening = true),
            new("怪しい宝箱（中身）", AdventureRoomKind.ShadyChest, source => source.NextMimic = false),
            new("怪しい宝箱（ミミック）", AdventureRoomKind.ShadyChest, source => source.NextMimic = true),
            new("休憩", AdventureRoomKind.Rest),
            new("行商人", AdventureRoomKind.Merchant),
            new("覚醒の祭壇", AdventureRoomKind.Altar),
            new("歩数の扉", AdventureRoomKind.StepDoor),
            new("試練", AdventureRoomKind.Trial),
            new("「？」（魔物）", AdventureRoomKind.Mystery, source => source.MysteryKind = AdventureRoomKind.Battle),
            new("「？」（宝箱）", AdventureRoomKind.Mystery, source => source.MysteryKind = AdventureRoomKind.Treasure),
        };

        /// <summary>The party's HP as it comes in, in the order the button steps through.</summary>
        public static readonly (string Label, float[] Shares)[] HpChoices =
        {
            ("満タン", new[] { 1f, 1f, 1f, 1f }),
            ("半分", new[] { 0.5f, 0.5f, 0.5f, 0.5f }),
            ("ひとり倒れている", new[] { 0.6f, 0f, 0.6f, 0.6f }),
            ("瀕死", new[] { 0.08f, 0.08f, 0.08f, 0.08f }),
        };

        // 行き先ごとに道を作り直して探す数。祭壇のように少ないマスも、ほぼ見つかる数にする。
        private const int Tries = 3000;

        // ルーン復活や行商人で困らないだけのルーン。
        private const long LabRunes = 100000;

        public const string NotFoundMessage = "その行き先では、そのマスの手前まで行けませんでした";

        public Button DestinationButton;
        public TMP_Text DestinationLabel;
        public Button NextButton;
        public TMP_Text NextLabel;
        public Button HpButton;
        public TMP_Text HpLabel;
        public Button NetworkButton;
        public TMP_Text NetworkLabel;
        public Button StartButton;

        [Tooltip("見つからなかったときなどの知らせ。")]
        public TMP_Text Notice;

        private static IAdventureSource saved;
        private int destination;
        private int next;
        private int hp;

        /// <summary>True while the adventures are played on the room's stand-in.</summary>
        public static bool Active { get; private set; }

        public int Destination => destination;
        public NextRoom Next => NextRooms[next];

        private void Start()
        {
            Listen(DestinationButton, () => Step(ref destination, AdventureCatalog.Areas.Count));
            Listen(NextButton, () => Step(ref next, NextRooms.Length));
            Listen(HpButton, () => Step(ref hp, HpChoices.Length));
            Listen(NetworkButton, () =>
            {
                DebugNetwork.Next();
                Refresh();
            });
            Listen(StartButton, () => Go());
            Refresh();
        }

        private static void Listen(Button button, UnityEngine.Events.UnityAction call)
        {
            if (button != null)
                button.onClick.AddListener(call);
        }

        private void Step(ref int index, int count)
        {
            index = (index + 1) % count;
            Refresh();
        }

        /// <summary>Chooses the destination, the room next and the HP (indexes into their lists), as the buttons do.</summary>
        public void Choose(int destinationIndex, int nextIndex, int hpIndex)
        {
            destination = Mathf.Clamp(destinationIndex, 0, AdventureCatalog.Areas.Count - 1);
            next = Mathf.Clamp(nextIndex, 0, NextRooms.Length - 1);
            hp = Mathf.Clamp(hpIndex, 0, HpChoices.Length - 1);
            Refresh();
        }

        private void Refresh()
        {
            Set(DestinationLabel, "行き先：" + AdventureCatalog.Areas[destination].Name);
            Set(NextLabel, "次のマス：" + NextRooms[next].Label);
            Set(HpLabel, "HP：" + HpChoices[hp].Label);
            Set(NetworkLabel, DebugNetwork.Label(DebugNetwork.Current));
        }

        private static void Set(TMP_Text label, string text)
        {
            if (label != null)
                label.text = text;
        }

        /// <summary>
        /// Sets the adventure up just before a room of the kind chosen and opens the exploration
        /// there. False, with a notice, when no route of the destination reaches such a room.
        /// </summary>
        public bool Go()
        {
            string area = AdventureCatalog.Areas[destination].Id;
            var source = Prepare(area, NextRooms[next], out var state);
            if (source == null)
            {
                Set(Notice, NotFoundMessage);
                return false;
            }
            Begin(source, state, HpChoices[hp].Shares);
            return SceneLoader.Load(SceneNames.Exploration, null, this, () => End());
        }

        /// <summary>
        /// A stand-in adventure of <paramref name="area"/> whose party stands, its room done, next
        /// to a room of the kind <paramref name="room"/> asks for; null when none of the routes
        /// tried has one. <paramref name="state"/> is the adventure there.
        /// </summary>
        public static AdventureLocalSource Prepare(string area, NextRoom room, out AdventureState state)
        {
            for (int seed = 1; seed <= Tries; seed++)
            {
                var source = new AdventureLocalSource(LabRunes, seed);
                state = Do(source.StartAsync(area, CancellationToken.None));
                var path = PathBefore(state.Run.Map, room.Kind);
                if (path == null)
                    continue;
                // 道中の「？」は魔物にして、ふつうに勝ち進む。
                source.MysteryKind = AdventureRoomKind.Battle;
                foreach (var id in path)
                {
                    var on = state.Run.Map.Find(id);
                    state = Do(source.MoveAsync(on, CancellationToken.None));
                    state = Do(
                        source.ClearAsync(
                            id,
                            CancellationToken.None,
                            null,
                            new AdventureClearOptions { Pass = on.Kind == AdventureRoomKind.ShadyChest }
                        )
                    );
                }
                source.MysteryKind = null;
                room.Draw?.Invoke(source);
                state = Do(source.LoadAsync(CancellationToken.None));
                if (state.Run.Exits.Any(exit => exit.Kind == room.Kind))
                    return source;
            }
            state = null;
            return null;
        }

        // 入口から、指定の種類のマスへ進める部屋までの道（入口を含まず、その部屋を含む）。入口の隣なら空。
        private static List<string> PathBefore(AdventureRouteMap map, AdventureRoomKind kind)
        {
            var entrance = map.Find(AdventureRouteMap.EntranceId);
            if (entrance == null)
                return null;
            var from = new Dictionary<string, string> { [entrance.Id] = null };
            var queue = new Queue<AdventureRoom>();
            queue.Enqueue(entrance);
            while (queue.Count > 0)
            {
                var room = queue.Dequeue();
                if (room.Next.Select(map.Find).Any(next => next != null && next.Kind == kind))
                {
                    var path = new List<string>();
                    for (string at = room.Id; at != entrance.Id; at = from[at])
                        path.Insert(0, at);
                    return path;
                }
                foreach (var id in room.Next)
                    if (!from.ContainsKey(id) && map.Find(id) is { } next)
                    {
                        from[id] = room.Id;
                        queue.Enqueue(next);
                    }
            }
            return null;
        }

        private static AdventureState Do(Task<AdventureState> task) => task.GetAwaiter().GetResult();

        /// <summary>
        /// Plays the adventures on <paramref name="source"/> from <paramref name="state"/> (through
        /// <see cref="DebugNetwork"/>), the party carrying <paramref name="shares"/> of its HP.
        /// </summary>
        public static void Begin(AdventureLocalSource source, AdventureState state, IReadOnlyList<float> shares)
        {
            if (!Active)
                saved = AdventureSession.Source;
            Active = true;
            AdventureSession.Source = new LabSource(source);
            AdventureSession.Use(state);
            if (state?.Run != null)
                AdventureVitals.Keep(state.Run.Id, shares);
        }

        /// <summary>
        /// The adventures to use when the app's services set <paramref name="server"/>: the room's
        /// stand-in while it is in use (the server kept for <see cref="End"/>), otherwise the server.
        /// </summary>
        public static IAdventureSource Keep(IAdventureSource server)
        {
            if (!Active)
                return server;
            saved = server;
            return AdventureSession.Source;
        }

        /// <summary>Back to the game server's adventures (Home calls it as it opens).</summary>
        public static void End()
        {
            if (!Active)
                return;
            Active = false;
            AdventureSession.Source = saved;
            AdventureSession.Use(null);
            saved = null;
        }

        // Play Modeに入るたびに、前の部屋の代役を持ち越さない。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            Active = false;
            saved = null;
        }

        // 部屋の代役への要求も、通信の遅れと失敗のとおりに遅らせ、失敗させる。
        private sealed class LabSource : IAdventureSource
        {
            private readonly AdventureLocalSource inner;

            public LabSource(AdventureLocalSource inner) => this.inner = inner;

            private static async Task<AdventureState> After(
                Func<Task<AdventureState>> call,
                CancellationToken token
            )
            {
                await DebugNetwork.BeforeAsync(token);
                return await call();
            }

            public Task<AdventureState> LoadAsync(CancellationToken token) =>
                After(() => inner.LoadAsync(token), token);

            public Task<AdventureState> StartAsync(string destinationId, CancellationToken token) =>
                After(() => inner.StartAsync(destinationId, token), token);

            public Task<AdventureState> MoveAsync(AdventureRoom room, CancellationToken token) =>
                After(() => inner.MoveAsync(room, token), token);

            public Task<AdventureState> ClearAsync(
                string roomId,
                CancellationToken token,
                IReadOnlyList<AdventureEnemyTally> enemies = null,
                AdventureClearOptions options = null
            ) => After(() => inner.ClearAsync(roomId, token, enemies, options), token);

            public Task<AdventureState> OpenAsync(string roomId, CancellationToken token) =>
                After(() => inner.OpenAsync(roomId, token), token);

            public Task<AdventureState> BuyAsync(string roomId, int index, CancellationToken token) =>
                After(() => inner.BuyAsync(roomId, index, token), token);

            public Task<AdventureState> ReviveAsync(string roomId, CancellationToken token) =>
                After(() => inner.ReviveAsync(roomId, token), token);

            public Task<AdventureState> EndAsync(
                AdventureEndReason reason,
                CancellationToken token,
                IReadOnlyList<AdventureEnemyTally> enemies = null
            ) => After(() => inner.EndAsync(reason, token, enemies), token);
        }
    }
}
