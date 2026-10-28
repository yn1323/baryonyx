using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.WorldMap
{
    /// <summary>The kinds of what happens on a square (doc/features/world-atlas.md#マスの出来事の見本).</summary>
    public enum WorldEventKind
    {
        Meet,
        Choice,
        HiddenChest,
        Pickup,
        Peddler,
        Bet,
        Battle,
    }

    /// <summary>One line of a talk, said by <see cref="Speaker"/> (an index into the panel's bubbles).</summary>
    public readonly struct WorldEventLine
    {
        public readonly int Speaker;
        public readonly string Text;

        public WorldEventLine(int speaker, string text)
        {
            Speaker = speaker;
            Text = text;
        }
    }

    /// <summary>
    /// The samples' words and numbers, one event of each kind, shown in the nested world map
    /// prototype. Nothing they give reaches the inventory.
    /// </summary>
    public static class WorldEventSamples
    {
        // 出会い：0がアリア（左）、1がルカ（右）。読み終えると、ルカが仲間になる。
        public static readonly WorldEventLine[] MeetTalk =
        {
            new(1, "あの……この道、ヴェルデの街まで続いていますか？"),
            new(0, "続いてるよ。でも、この先は魔物が多いんだ。"),
            new(1, "やっぱり……。一人だと心細くて。"),
            new(0, "それなら一緒に行こう。にぎやかな方が楽しいし！"),
            new(1, "いいんですか！ よろしくお願いします！"),
        };
        public const string MeetJoined = "ルカが仲間になった！";

        // 選択：倒れた荷車。3つ目は、打の属性のカードを持つ仲間がいるときだけ出る選択肢の見本。
        public const string ChoicePrompt = "荷車が倒れてる。持ち主は見当たらないな……。";
        public static readonly string[] ChoiceOptions =
        {
            "起こして直す",
            "積み荷を探る",
            "荷車ごと持ち上げる",
        };
        public static readonly string[] ChoiceResults =
        {
            "荷台の奥に、お礼の手紙と剣が置いてあった。",
            "こぼれた積み荷から、使えそうな物を拾った。",
            "荷車を持ち上げて直した。積み荷も無事だ！",
        };

        public const string PickupName = "薬草";
        public const int PickupCount = 2;

        // 行商人：曜日で品ぞろえが変わる。見本は火曜（武器と防具）。
        public const string PeddlerGreeting = "いらっしゃい！ 今日は火曜日、武器と防具の日だよ。";
        public static readonly string[] PeddlerGoods =
        {
            "鉄の剣",
            "炎のダガー",
            "氷晶のワンド",
            "古王国の鎧",
        };
        public static readonly int[] PeddlerPrices = { 300, 600, 600, 900 };
        public const int PeddlerRunes = 1500;

        // 賭け：伏せた3枚から当たりを引く。外れても小さな物はもらえる。
        public const int BetStake = 100;
        public const int BetPrize = 300;
        public const string BetPlay = "100ルーンで遊ぶ";
        public const string BetConsolation = "どんぐり";

        public const string BattleFight = "たたかう";
    }

    /// <summary>
    /// What one kind of event shows inside the <see cref="WorldMapEventWindow"/>: the window's
    /// size, whether the scenery fills it, how dark the map behind goes, when the cross shows,
    /// and the things that drop in while the window opens. The window calls <see cref="Tick"/>
    /// every frame once it has opened and <see cref="Advance"/> when its inside is tapped.
    /// </summary>
    public abstract class WorldEventPanel : MonoBehaviour
    {
        public WorldEventKind Kind;
        public Vector2 WindowSize = new(1600f, 860f);
        public bool ShowScenery = true;
        public float Veil = 0.68f;
        public WorldMapEventWindow.CloseRule Rule = WorldMapEventWindow.CloseRule.AfterDone;

        // 窓が開くときに、上から1つずつ降り立つ物（キャラ・宝箱・敵など）の足元。
        public RectTransform[] Arrivals = Array.Empty<RectTransform>();

        protected WorldMapEventWindow Host { get; private set; }

        // 窓が開き終えてからの時間（秒）。
        protected float Clock { get; private set; }

        /// <summary>The event has reached its end (what it gives is shown), so the cross may show.</summary>
        public bool Done { get; protected set; }

        public void Begin(WorldMapEventWindow host)
        {
            Host = host;
            Clock = 0f;
            Done = false;
            Restart();
        }

        public void Tick(float dt)
        {
            Clock += dt;
            Draw(dt);
        }

        /// <summary>A tap on the window's inside, outside the panel's own buttons.</summary>
        public virtual void Advance() { }

        // 開く前の形に戻す。窓が開いている間（開き終える前）も、この形のまま見える。
        protected abstract void Restart();

        protected abstract void Draw(float dt);
    }

    /// <summary>
    /// Speech bubbles over the speakers: only the one speaking shows, growing from its tail's
    /// tip each time the line changes, and its words are written a few at a time. Once a line is
    /// all written, a small diamond lights at its corner while another line waits.
    /// </summary>
    [Serializable]
    public sealed class EventBubbles
    {
        public RectTransform[] Bubbles = Array.Empty<RectTransform>();
        public TMP_Text[] Says = Array.Empty<TMP_Text>();
        public Graphic[] More = Array.Empty<Graphic>();

        private const float GrowSeconds = 0.16f;
        private const float CharsPerSecond = 30f;

        private WorldEventLine[] lines = Array.Empty<WorldEventLine>();
        private bool tapAtEnd;
        private int line;
        private int shown = -1;
        private float time;

        /// <summary>A line is still to be read (shown, or waiting behind the one shown).</summary>
        public bool Talking => line < lines.Length;

        /// <summary>The line shown is all written.</summary>
        public bool Written => !Talking || Typed >= lines[line].Text.Length;

        private int Typed => Mathf.FloorToInt(Mathf.Max(0f, time - GrowSeconds) * CharsPerSecond);

        // waitAtEnd：最後のせりふも、押して読み終える（次の場面へ進む）か。
        public void Start(WorldEventLine[] say, bool waitAtEnd)
        {
            lines = say ?? Array.Empty<WorldEventLine>();
            tapAtEnd = waitAtEnd;
            line = 0;
            shown = -1;
            time = 0f;
            Hide();
        }

        /// <summary>Writes the rest of the line at once, or moves to the next line.</summary>
        public void Advance()
        {
            if (!Talking)
                return;
            if (!Written)
            {
                Complete();
                return;
            }
            line++;
            time = 0f;
        }

        /// <summary>Writes the rest of the line at once.</summary>
        public void Complete()
        {
            if (Talking)
                time = GrowSeconds + lines[line].Text.Length / CharsPerSecond;
        }

        public void Hide()
        {
            foreach (var bubble in Bubbles)
                bubble.gameObject.SetActive(false);
        }

        // keepLast：読み終えたあとも、最後の吹き出しを残すか。
        public void Tick(float dt, bool keepLast)
        {
            time += dt;
            if (lines.Length == 0 || (!Talking && !keepLast))
            {
                Hide();
                return;
            }
            int at = Mathf.Min(line, lines.Length - 1);
            if (at != shown)
            {
                shown = at;
                Says[lines[at].Speaker].text = lines[at].Text;
            }
            bool waiting = Talking && Written && (tapAtEnd || line < lines.Length - 1);
            for (int i = 0; i < Bubbles.Length; i++)
            {
                bool live = lines[at].Speaker == i;
                Bubbles[i].gameObject.SetActive(live);
                if (!live)
                    continue;
                float grow = Talking ? Mathf.Clamp01(time / GrowSeconds) : 1f;
                Bubbles[i].localScale =
                    Vector3.one * Mathf.LerpUnclamped(0.6f, 1f, EventMotion.EaseOutBack(grow));
                Says[i].maxVisibleCharacters = Talking ? Typed : int.MaxValue;
                More[i].enabled = waiting;
                if (waiting)
                    More[i].color = EventMotion.WithAlpha(
                        More[i].color,
                        0.55f + 0.45f * Mathf.Sin(time * 6f)
                    );
            }
        }
    }

    /// <summary>The curves the event window and its panels move by.</summary>
    public static class EventMotion
    {
        // ease-out-back の行き過ぎの強さ（0.9で約3%行き過ぎる）。
        public const float Overshoot = 0.9f;

        public static float EaseOut(float x) => 1f - (1f - x) * (1f - x);

        public static float EaseIn(float x) => x * x;

        // 出だしが速く、止まり際が長い減速（Material の emphasized decelerate に近い形）。
        public static float Decelerate(float x) => 1f - Mathf.Pow(1f - x, 4f);

        public static float EaseOutBack(float x, float overshoot = Overshoot)
        {
            float y = x - 1f;
            return 1f + (overshoot + 1f) * y * y * y + overshoot * y * y;
        }

        public static Color WithAlpha(Color color, float alpha) =>
            new(color.r, color.g, color.b, Mathf.Clamp01(alpha));

        /// <summary>
        /// Pops the children of <paramref name="row"/> (the things an event gives) in one by one,
        /// <paramref name="t"/> seconds after the first started.
        /// </summary>
        public static bool PopRow(RectTransform row, float t)
        {
            const float stagger = 0.12f;
            const float each = 0.32f;
            row.gameObject.SetActive(t > 0f);
            for (int i = 0; i < row.childCount; i++)
            {
                float x = Mathf.Clamp01((t - i * stagger) / each);
                row.GetChild(i).localScale = Vector3.one * EaseOutBack(x, 1.6f);
            }
            return t >= (row.childCount - 1) * stagger + each;
        }
    }
}
