using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.WorldMap
{
    /// <summary>
    /// The map editor (doc/features/screens/debug-rooms.md#地図エディター), laid over the nested
    /// world map. A tapped square is chosen instead of walked to, and the panel on the right shows
    /// what it holds and its settings: a battle's enemies in words, level and strength
    /// (<see cref="WorldAtlasEncounters"/>), with what they come to. The row of event buttons (and
    /// "なし") puts that event on the chosen square, and the settings are changed in the panel; in
    /// the Editor both are kept in the map's layout by <see cref="Save"/> and
    /// <see cref="SaveSetting"/>, elsewhere they last only while the app runs. The row below picks
    /// the map (the world map or a region's), lists the squares that hold an event, says whether a
    /// party member good at it is there (the helped answer and the bets' help), and tries the
    /// chosen square's event in its window. The buttons hide while a window is open.
    /// On a phone nothing is saved, so every square changed is remembered (put back when its map
    /// opens again) and "コピー" copies them as JSON (<see cref="ChangesJson"/>), to be pasted to
    /// the AI and written into the layouts with place_events.py apply.
    /// </summary>
    public sealed class WorldEventLab : MonoBehaviour
    {
        /// <summary>The events that can be put on a square, in their order: the name shown, the kind and how it is played.</summary>
        public static readonly (string Name, WorldEventKind Kind, int Form)[] Entries =
        {
            ("出会い", WorldEventKind.Meet, 0),
            ("選択", WorldEventKind.Choice, 0),
            ("隠し宝箱", WorldEventKind.HiddenChest, 0),
            ("拾い物", WorldEventKind.Pickup, 0),
            ("行商人", WorldEventKind.Peddler, 0),
            ("賭け：サイコロ", WorldEventKind.Bet, (int)WorldBetGame.Dice),
            ("賭け：宝箱", WorldEventKind.Bet, (int)WorldBetGame.Chests),
            ("戦闘", WorldEventKind.Battle, 0),
        };

        // これから設定できるようにするもの（2026-11-01、ユーザーの「他にも何か設定できそうなものあれば記載して」）。
        public const string LaterSettings =
            "これから設定できるようにするもの\n"
            + "・拾い物：物と数\n"
            + "・隠し宝箱：中身と、出る日（日替わり・週替わり）\n"
            + "・行商人：品ぞろえと値段、曜日ごとの品\n"
            + "・出会い：加わる仲間と会話\n"
            + "・選択：場面・答え・もらう物、答えを出す仲間の属性\n"
            + "・賭け：賭け金、倍になる回数・宝箱の数\n"
            + "・戦闘：背景、もらう物、勝つまで道をふさぐか\n"
            + "・どのマスも：値段の上書き、復活の周期（日・週・月）";

        public WorldAtlasNavigator Navigator;
        public CanvasGroup Group;
        public Button[] Buttons = Array.Empty<Button>();
        public Button NoneButton;
        public Button MapButton;
        public TMP_Text MapLabel;
        public Button ListButton;
        public Button HelpButton;
        public TMP_Text HelpLabel;
        public Button TryButton;
        public Button CopyButton;

        // 左の一覧（地図か、出来事を置いたマス）：一覧の枠、行を並べる所、行の見本。
        public CanvasGroup ListGroup;
        public RectTransform ListContent;
        public Button RowTemplate;

        // 地図の一覧に出す地図のキーと名前（世界地図と地域のエリアマップ）。Prefab を作るときに配置から入れる。
        public string[] MapKeys = Array.Empty<string>();
        public string[] MapNames = Array.Empty<string>();

        // 右のマスの設定。戦闘の設定は戦闘のマスを、これから足す設定の一覧はほかのマスを選んだときに出す。
        public CanvasGroup InspectorGroup;
        public TMP_Text InspectorTitle;
        public CanvasGroup BattleGroup;
        public TMP_InputField EnemiesInput;
        public TMP_InputField LevelInput;
        public Button StrengthButton;
        public TMP_Text StrengthLabel;
        public TMP_Text Preview;
        public TMP_Text Notes;

        private static readonly Color ErrorColor = new(1f, 0.55f, 0.5f, 1f);

        /// <summary>What saving a change gave: why it was refused, or the runes the square now costs.</summary>
        public readonly struct Saved
        {
            public readonly string Error;
            public readonly int Price;

            public Saved(string error, int price)
            {
                Error = error;
                Price = price;
            }
        }

        // 付け替えを配置のJSONへ残す処理（地図のキー、マスのID、出来事の名前か "none"、賭けの遊び方）。
        // Editorだけが渡す（WorldEventLabSave）。ないとき（端末）は、アプリを閉じるまでだけ変える。
        public static Func<string, string, string, string, Saved> Save;

        // マスの設定を配置のJSONへ残す処理（地図のキー、マスのID、項目の名前、値。空の値は消す）。
        public static Func<string, string, string, string, Saved> SaveSetting;

        private enum ListMode
        {
            None,
            Maps,
            Squares,
        }

        // 変えたマス（地図ごと・マスごとに1つ。変えた順）。コピーでJSONにし、端末では地図を開き直したときに当て直す。
        private sealed class Change
        {
            public string Map;
            public string Square;
            public string Label;
            public string Event;
            public string Form;
            public string Enemies;
            public int EnemyLevel;
            public string Strength;
        }

        private readonly List<Change> changes = new();
        private WorldMapView shownView;

        /// <summary>The marker the copied JSON starts with, so a paste is known for what it is.</summary>
        public const string ChangesFormat = "baryonyx-world-event-lab";

        // 出来事どうしの道のりの最小のマス数（place_events.py の GAP と同じ）。
        private const int EventGap = 3;

        private readonly List<Button> rows = new();
        private readonly List<int> rowSquares = new();
        private bool helped = true;
        private ListMode mode;
        private int chosen = -1;
        private Color previewColor;
        private Func<int, bool> picking;

        /// <summary>A party member good at it is there.</summary>
        public bool Helped => helped;

        /// <summary>The square chosen, or -1.</summary>
        public int Chosen => chosen;

        /// <summary>The list of squares is shown.</summary>
        public bool Listing => mode == ListMode.Squares;

        /// <summary>The list of maps is shown.</summary>
        public bool ListingMaps => mode == ListMode.Maps;

        /// <summary>The squares the list shows (indexes on the shown map), in its order.</summary>
        public IReadOnlyList<int> ListedSquares => rowSquares;

        public static string HelpText(bool helped) =>
            helped ? "得意な仲間：いる" : "得意な仲間：いない";

        public static string MapText(string name) => "地図：" + name;

        public static string StrengthText(WorldBattleStrength strength) =>
            "強さ：" + WorldAtlasEncounters.NameOf(strength);

        private WorldMapView View => Navigator != null ? Navigator.Current : null;

        private void Awake()
        {
            for (int i = 0; i < Buttons.Length; i++)
            {
                int entry = i;
                Buttons[i]
                    .onClick.AddListener(() => Assign(Entries[entry].Kind, Entries[entry].Form));
            }
            NoneButton.onClick.AddListener(() => Assign(null, 0));
            HelpButton.onClick.AddListener(() =>
            {
                helped = !helped;
                HelpLabel.text = HelpText(helped);
            });
            HelpLabel.text = HelpText(helped);
            MapButton.onClick.AddListener(() => Toggle(ListMode.Maps));
            ListButton.onClick.AddListener(() => Toggle(ListMode.Squares));
            TryButton.onClick.AddListener(() => Try());
            if (CopyButton != null)
                CopyButton.onClick.AddListener(() => Copy());
            EnemiesInput.onEndEdit.AddListener(words => SetEnemies(words));
            LevelInput.onEndEdit.AddListener(text => SetLevel(text));
            StrengthButton.onClick.AddListener(NextStrength);
            RowTemplate.gameObject.SetActive(false);
            Notes.text = LaterSettings;
            previewColor = Preview.color;
            picking = Choose;
            ShowInspector();
        }

        private void Update()
        {
            var view = View;
            bool open = view != null && view.EventWindow != null && view.EventWindow.IsShown;
            Fade(Group, !open);
            Fade(ListGroup, mode != ListMode.None && !open);
            Fade(InspectorGroup, chosen >= 0 && !open);
            if (view == null)
                return;
            if (view != shownView)
            {
                shownView = view;
                PutBack(view);
            }
            // 地図を移っても、マスを押すと選ぶ。選んだマスは息をするように大きさを変える。
            view.Picking = picking;
            if (MapLabel != null && view.Atlas != null)
                MapLabel.text = MapText(view.Atlas.name);
            if (chosen >= 0 && chosen < view.Nodes.Length)
                view.Nodes[chosen].localScale =
                    Vector3.one * (1.1f + 0.12f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4f)));
        }

        private static void Fade(CanvasGroup group, bool shown)
        {
            group.alpha = Mathf.MoveTowards(
                group.alpha,
                shown ? 1f : 0f,
                Time.unscaledDeltaTime / 0.15f
            );
            group.interactable = group.blocksRaycasts = shown;
        }

        /// <summary>Chooses the square <paramref name="index"/> on the shown map (a tap on it). It does not walk.</summary>
        public bool Choose(int index)
        {
            var view = View;
            if (view == null || index < 0 || index >= view.Nodes.Length)
                return false;
            Forget();
            chosen = index;
            ShowInspector();
            return true;
        }

        private void Forget()
        {
            var view = View;
            if (chosen >= 0 && view != null && chosen < view.Nodes.Length)
                view.Nodes[chosen].localScale = Vector3.one;
            chosen = -1;
        }

        private WorldAtlasLayout.Node ChosenNode =>
            chosen >= 0 && View != null && View.Atlas != null && chosen < View.Atlas.nodes.Length
                ? View.Atlas.nodes[chosen]
                : null;

        /// <summary>
        /// Puts <paramref name="kind"/> played as <paramref name="form"/> (nothing when it is null)
        /// on the chosen square. A change the layout's rules refuse is told at the bottom of the map
        /// and leaves the square as it was.
        /// </summary>
        public bool Assign(WorldEventKind? kind, int form)
        {
            var view = View;
            var node = ChosenNode;
            if (view == null || node == null)
            {
                if (view != null)
                    view.ShowNotice("先にマスを押します");
                return false;
            }
            string name = kind is { } placed ? WorldAtlasLayout.NameOf(placed) : "none";
            int price = node.price;
            // 端末では配置へ残せないため、配置の決まり（place_events.py の set）をここで確かめる。
            string refused = Save == null && kind != null ? RuleError(view, chosen, node, name) : null;
            if (refused != null)
            {
                view.ShowNotice(refused);
                return false;
            }
            if (Save != null)
            {
                var saved = Save(
                    Navigator.CurrentKey,
                    node.id,
                    name,
                    kind == WorldEventKind.Bet ? WorldAtlasLayout.FormNameOf(form) : null
                );
                if (saved.Error != null)
                {
                    view.ShowNotice(saved.Error);
                    return false;
                }
                price = saved.Price;
            }
            // 出来事が替わると、前の出来事の設定は消える（配置の set と同じ）。
            if (node.@event != (kind == null ? null : name))
            {
                node.enemies = null;
                node.enemyLevel = 0;
                node.strength = null;
            }
            node.@event = kind == null ? null : name;
            node.form = kind == WorldEventKind.Bet ? WorldAtlasLayout.FormNameOf(form) : null;
            node.price = price;
            view.SetEvent(chosen, kind, form, price);
            if (mode == ListMode.Squares)
                FillSquares();
            ShowInspector();
            Remember(node);
            return true;
        }

        // 出来事を置ける所か（place_events.py の set と同じ決まり）。置けないときは理由を返す。
        private string RuleError(WorldMapView view, int index, WorldAtlasLayout.Node node, string name)
        {
            var graph = view.Graph;
            int start = graph != null ? graph.IndexOf(StartSquare()) : -1;
            if (node.kind != "empty" || index == start)
                return "出来事は、何もないマスにだけ置けます";
            if (graph == null)
                return null;
            if (start >= 0)
                foreach (int next in graph.LinksOf(start))
                    if (next == index)
                        return "始まりの隣には置けません";
            // 道のりで GAP-1 マス以内に、ほかの出来事がないか。
            var steps = new Dictionary<int, int> { [index] = 0 };
            var queue = new Queue<int>();
            queue.Enqueue(index);
            while (queue.Count > 0)
            {
                int at = queue.Dequeue();
                if (at != index && view.TryEventAt(at, out _))
                    return "ほかの出来事と近すぎます（間に何もないマスを2つ以上空けます）";
                if (steps[at] >= EventGap - 1)
                    continue;
                foreach (int next in graph.LinksOf(at))
                    if (!steps.ContainsKey(next))
                    {
                        steps[next] = steps[at] + 1;
                        queue.Enqueue(next);
                    }
            }
            return null;
        }

        private string StartSquare() =>
            Navigator.CurrentKey == WorldAtlasLayout.WorldKey
                ? WorldAtlasLayout.StartSquare
                : WorldAtlasLayout.GatePrefix + Navigator.CurrentKey;

        /// <summary>Opens the chosen square's event in its window, as stopping there does.</summary>
        public bool Try()
        {
            var view = View;
            if (view == null || view.EventWindow == null || view.EventWindow.IsShown)
                return false;
            if (Navigator.Switching || chosen < 0)
                return false;
            if (!view.TryEventAt(chosen, out var kind))
            {
                view.ShowNotice("このマスには出来事がありません");
                return false;
            }
            view.EventWindow.Helped = helped;
            view.EventWindow.Open(view.Nodes[chosen], kind, null, null, view.EventFormAt(chosen));
            Group.interactable = Group.blocksRaycasts = false;
            ListGroup.interactable = ListGroup.blocksRaycasts = false;
            InspectorGroup.interactable = InspectorGroup.blocksRaycasts = false;
            return true;
        }

        /// <summary>Shows the map <paramref name="key"/> from its start (the list of maps).</summary>
        public bool OpenMap(string key)
        {
            if (Navigator == null || Navigator.Switching)
                return false;
            Forget();
            string start =
                key == WorldAtlasLayout.WorldKey
                    ? WorldAtlasLayout.StartSquare
                    : WorldAtlasLayout.GatePrefix + key;
            if (!Navigator.Open(key, start))
                return false;
            mode = ListMode.None;
            ShowInspector();
            return true;
        }

        private void Toggle(ListMode list)
        {
            mode = mode == list ? ListMode.None : list;
            if (mode == ListMode.Maps)
                FillMaps();
            else if (mode == ListMode.Squares)
                FillSquares();
        }

        // --- マスの設定 -----------------------------------------------------------------

        private void ShowInspector()
        {
            var view = View;
            var node = ChosenNode;
            if (node == null || view == null)
                return;
            view.TryEventAt(chosen, out var kind);
            bool happens = view.TryEventAt(chosen, out _);
            string what = happens ? NameOf(kind, view.EventFormAt(chosen)) : "なし";
            int steps = StepsFromStart(view)[chosen];
            InspectorTitle.text = steps == int.MaxValue ? what : $"{steps}マス先　{what}";
            bool battle = happens && kind == WorldEventKind.Battle;
            BattleGroup.alpha = battle ? 1f : 0f;
            BattleGroup.interactable = BattleGroup.blocksRaycasts = battle;
            // 戦闘の設定と重ならないよう、これから足す設定の一覧は戦闘でないマスだけに出す。
            Notes.enabled = !battle;
            if (!battle)
                return;
            EnemiesInput.SetTextWithoutNotify(node.enemies ?? "");
            LevelInput.SetTextWithoutNotify(node.enemyLevel > 0 ? node.enemyLevel.ToString() : "");
            if (LevelInput.placeholder is TMP_Text hint)
                hint.text = view.Atlas.level.ToString();
            StrengthLabel.text = StrengthText(WorldAtlasEncounters.StrengthOf(node.strength));
            Describe(node);
        }

        private void Describe(WorldAtlasLayout.Node node)
        {
            int level = node.enemyLevel > 0 ? node.enemyLevel : View.Atlas.level;
            var read = WorldAtlasEncounters.Read(node.enemies);
            Preview.color = read.Errors.Count > 0 ? ErrorColor : previewColor;
            Preview.text = WorldAtlasEncounters.Describe(
                node.enemies,
                level,
                WorldAtlasEncounters.StrengthOf(node.strength)
            );
        }

        /// <summary>Sets the chosen battle's enemies in words. Words that cannot be read are shown, not kept.</summary>
        public bool SetEnemies(string words)
        {
            var node = ChosenNode;
            if (node == null)
                return false;
            words = (words ?? "").Trim();
            var read = WorldAtlasEncounters.Read(words);
            if (read.Errors.Count > 0)
            {
                Preview.color = ErrorColor;
                Preview.text = string.Join("\n", read.Errors);
                return false;
            }
            if (!Keep(node, "enemies", words))
                return false;
            node.enemies = words.Length > 0 ? words : null;
            Describe(node);
            Remember(node);
            return true;
        }

        /// <summary>Sets the chosen battle's level (empty for the map's recommended level).</summary>
        public bool SetLevel(string text)
        {
            var node = ChosenNode;
            if (node == null)
                return false;
            text = (text ?? "").Trim();
            int level = 0;
            if (text.Length > 0 && (!int.TryParse(text, out level) || level < 1 || level > 100))
            {
                Preview.color = ErrorColor;
                Preview.text = "Lvは1〜100で書きます";
                return false;
            }
            if (!Keep(node, "enemyLevel", level > 0 ? level.ToString() : ""))
                return false;
            node.enemyLevel = level;
            Describe(node);
            Remember(node);
            return true;
        }

        // 強さを、ふつう→強い→主→ふつうと替える。
        private void NextStrength()
        {
            var node = ChosenNode;
            if (node == null)
                return;
            var next = (WorldBattleStrength)(
                ((int)WorldAtlasEncounters.StrengthOf(node.strength) + 1) % 3
            );
            string name = WorldAtlasEncounters.LayoutNameOf(next);
            if (!Keep(node, "strength", name ?? ""))
                return;
            node.strength = name;
            StrengthLabel.text = StrengthText(next);
            Describe(node);
            Remember(node);
        }

        // 設定を配置へ残す。残せないとき（Editorの外）は、アプリを閉じるまでだけ変える。
        private bool Keep(WorldAtlasLayout.Node node, string field, string value)
        {
            if (SaveSetting == null)
                return true;
            var saved = SaveSetting(Navigator.CurrentKey, node.id, field, value);
            if (saved.Error == null)
                return true;
            View.ShowNotice(saved.Error);
            return false;
        }

        // --- 変えたマスの控えとコピー ---------------------------------------------------------

        /// <summary>How many squares have been changed.</summary>
        public int ChangeCount => changes.Count;

        // 変えたマスの今の出来事と設定を控える（同じマスは上書きし、変えた順を保つ）。
        private void Remember(WorldAtlasLayout.Node node)
        {
            string map = Navigator.CurrentKey;
            var change = changes.Find(c => c.Map == map && c.Square == node.id);
            if (change == null)
            {
                change = new Change { Map = map, Square = node.id };
                changes.Add(change);
            }
            change.Label = InspectorTitle != null ? InspectorTitle.text : "";
            change.Event = node.@event ?? "none";
            change.Form = node.form;
            change.Enemies = node.enemies;
            change.EnemyLevel = node.enemyLevel;
            change.Strength = node.strength;
        }

        // 端末では、地図を開くたびに配置から読み直されるため、その地図で変えたマスを当て直す。
        private void PutBack(WorldMapView view)
        {
            if (Save != null || view.Atlas == null)
                return;
            foreach (var change in changes)
            {
                if (change.Map != Navigator.CurrentKey)
                    continue;
                int index = Array.FindIndex(view.Atlas.nodes, n => n.id == change.Square);
                if (index < 0 || index >= view.Nodes.Length)
                    continue;
                var node = view.Atlas.nodes[index];
                var kind = change.Event == "none" ? null : WorldAtlasLayout.EventOf(change.Event);
                int form = WorldAtlasLayout.FormOf(change.Form) ?? 0;
                node.@event = kind == null ? null : change.Event;
                node.form = change.Form;
                node.enemies = change.Enemies;
                node.enemyLevel = change.EnemyLevel;
                node.strength = change.Strength;
                view.SetEvent(index, kind, form, node.price);
            }
        }

        /// <summary>
        /// Copies the squares changed as JSON to the clipboard, and says how many; with none
        /// changed it says so and copies nothing.
        /// </summary>
        public bool Copy()
        {
            var view = View;
            if (changes.Count == 0)
            {
                if (view != null)
                    view.ShowNotice("変えたマスはまだありません");
                return false;
            }
            GUIUtility.systemCopyBuffer = ChangesJson();
            if (view != null)
                view.ShowNotice($"変えた{changes.Count}マスをコピーしました");
            return true;
        }

        /// <summary>
        /// The squares changed, as the JSON place_events.py apply reads: for each square its
        /// map, ID, title in the editor, event ("none" for nothing), a bet's way of playing, and a
        /// battle's settings (an empty or 0 value is the map's own).
        /// </summary>
        public string ChangesJson()
        {
            var json = new StringBuilder();
            json.Append("{\n  \"format\": \"").Append(ChangesFormat).Append("\",\n  \"changes\": [");
            for (int i = 0; i < changes.Count; i++)
            {
                var change = changes[i];
                json.Append(i == 0 ? "\n" : ",\n").Append("    {");
                Field(json, "map", change.Map, true);
                Field(json, "square", change.Square, false);
                Field(json, "label", change.Label, false);
                Field(json, "event", change.Event, false);
                if (change.Form != null)
                    Field(json, "form", change.Form, false);
                if (change.Event == "battle")
                {
                    Field(json, "enemies", change.Enemies ?? "", false);
                    json.Append(", \"enemyLevel\": ")
                        .Append(change.EnemyLevel.ToString(CultureInfo.InvariantCulture));
                    Field(json, "strength", change.Strength ?? "", false);
                }
                json.Append(" }");
            }
            json.Append(changes.Count > 0 ? "\n  ]\n}" : "]\n}");
            return json.ToString();
        }

        private static void Field(StringBuilder json, string name, string value, bool first)
        {
            if (!first)
                json.Append(',');
            json.Append(" \"").Append(name).Append("\": \"");
            foreach (char c in value ?? "")
            {
                if (c == '"' || c == '\\')
                    json.Append('\\').Append(c);
                else if (c == '\n')
                    json.Append("\\n");
                else if (c < ' ')
                    json.Append(' ');
                else
                    json.Append(c);
            }
            json.Append('"');
        }

        // --- 左の一覧 -------------------------------------------------------------------

        /// <summary>The name an event is listed by.</summary>
        public static string NameOf(WorldEventKind kind, int form)
        {
            foreach (var entry in Entries)
                if (entry.Kind == kind && entry.Form == form)
                    return entry.Name;
            foreach (var entry in Entries)
                if (entry.Kind == kind)
                    return entry.Name;
            return kind.ToString();
        }

        /// <summary>Chooses the listed square <paramref name="row"/> and brings it to the middle of the screen.</summary>
        public bool ShowSquare(int row)
        {
            var view = View;
            if (view == null || row < 0 || row >= rowSquares.Count)
                return false;
            view.LookAt(rowSquares[row]);
            return Choose(rowSquares[row]);
        }

        private void FillMaps()
        {
            for (int i = 0; i < MapKeys.Length; i++)
            {
                string key = MapKeys[i];
                Row(i, MapNames[i], () => OpenMap(key));
            }
            for (int i = MapKeys.Length; i < rows.Count; i++)
                rows[i].gameObject.SetActive(false);
        }

        // 今の地図の出来事を置いたマスを、始まりから近い順に並べる。
        private void FillSquares()
        {
            rowSquares.Clear();
            var view = View;
            if (view != null && view.Graph != null)
            {
                var steps = StepsFromStart(view);
                for (int i = 0; i < view.Graph.Nodes.Count; i++)
                    if (view.TryEventAt(i, out _))
                        rowSquares.Add(i);
                rowSquares.Sort(
                    (a, b) => steps[a] != steps[b] ? steps[a].CompareTo(steps[b]) : a.CompareTo(b)
                );
                for (int i = 0; i < rowSquares.Count; i++)
                {
                    int square = rowSquares[i];
                    int row = i;
                    view.TryEventAt(square, out var kind);
                    Row(
                        i,
                        steps[square] + "マス先　" + NameOf(kind, view.EventFormAt(square)),
                        () => ShowSquare(row)
                    );
                }
            }
            for (int i = rowSquares.Count; i < rows.Count; i++)
                rows[i].gameObject.SetActive(false);
        }

        // 一覧の i 行目に、文字と押したときの処理を入れる。足りない行は見本から作る。
        private void Row(int i, string text, UnityEngine.Events.UnityAction pressed)
        {
            while (rows.Count <= i)
                rows.Add(Instantiate(RowTemplate, ListContent));
            var row = rows[i];
            row.gameObject.SetActive(true);
            row.onClick.RemoveAllListeners();
            row.onClick.AddListener(pressed);
            row.GetComponentInChildren<TMP_Text>(true).text = text;
        }

        // 地図の始まり（世界地図は冒険の始まり、エリアマップは入口）から、道のりで何マスか。
        private int[] StepsFromStart(WorldMapView view)
        {
            var graph = view.Graph;
            var steps = new int[graph.Nodes.Count];
            for (int i = 0; i < steps.Length; i++)
                steps[i] = int.MaxValue;
            int from = graph.IndexOf(StartSquare());
            if (from < 0)
                return steps;
            var queue = new Queue<int>();
            steps[from] = 0;
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                int at = queue.Dequeue();
                foreach (int next in graph.LinksOf(at))
                    if (steps[next] == int.MaxValue)
                    {
                        steps[next] = steps[at] + 1;
                        queue.Enqueue(next);
                    }
            }
            return steps;
        }
    }
}
