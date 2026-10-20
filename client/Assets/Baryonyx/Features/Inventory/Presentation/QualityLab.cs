using System;
using System.Collections.Generic;
using System.Linq;
using Baryonyx.Combat;
using Baryonyx.Combat.Presentation;
using Baryonyx.StepBonus;
using Baryonyx.UI;
using Baryonyx.UI.Cards;
using Baryonyx.UI.EditModePreview;
using Baryonyx.UI.GuideMenu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.Inventory
{
    /// <summary>A picture of one thing that is not a card (equipment, a bonus): its icon with the sparkle, name, stars and quality.</summary>
    [Serializable]
    public sealed class QualityLabItemFace
    {
        public GameObject Root;
        public RawImage Icon;
        public CardFoil Foil;
        public TMP_Text Name;
        public TMP_Text Stars;
        public TMP_Text Quality;

        public void Show(string name, int stars, Sprite icon, ItemQuality quality, int foilStyle)
        {
            var texture = icon != null ? icon.texture : null;
            if (Icon != null)
            {
                Icon.texture = texture;
                Icon.uvRect = CardFoil.UvOf(icon);
                Icon.enabled = texture != null;
            }
            if (Foil != null)
                Foil.Show(icon, quality, foilStyle);
            UiText.Set(Name, name);
            UiText.Set(Stars, stars > 0 ? new string('★', stars) : "");
            if (Quality != null)
            {
                Quality.text = Inventory.Quality.Letter(quality);
                Quality.color = QualityPalette.Of(quality);
            }
        }
    }

    /// <summary>One place a thing is shown: as its card (a skill) or its picture (the others), with a caption.</summary>
    [Serializable]
    public sealed class QualityLabPreview
    {
        public BattleInspectCardView Card;
        public QualityLabItemFace Item = new();
        public TMP_Text Caption;
    }

    /// <summary>
    /// The quality lab, a debugging room for quality (doc/features/inventory.md, quality): pick a
    /// skill, a unique skill's stage, a weapon, a piece of armour, a sample accessory or an ACT
    /// bonus from the tabs, then move its multiplier (and the kind's upper limit) to see the
    /// quality letter, the sparkle on its card or picture and the numbers of its effect change.
    /// The four qualities' sparkles are shown side by side along the bottom. The tab, the thing
    /// and the multiplier are remembered on this machine, so the next Play shows them again.
    /// </summary>
    public sealed class QualityLab : MonoBehaviour, IEditModePreview
    {
        // What is remembered between Plays (on this machine only).
        public const string TabKey = "Baryonyx.QualityLab.Tab";
        public const string EntryKey = "Baryonyx.QualityLab.Entry";
        public const string MultiplierKey = "Baryonyx.QualityLab.Multiplier";

        /// <summary>The lowest and highest upper limit the lab lets the limit slider try.</summary>
        public const float MinLimit = 1.05f;
        public const float MaxLimit = 4f;

        [Header("一覧")]
        [Tooltip("種類のタブ（QualityLabTab の順）。")]
        public GuideTab[] Tabs = new GuideTab[0];

        [Tooltip("一覧の行を並べる所。")]
        public RectTransform ListContent;
        public ScrollRect ListScroll;

        [Tooltip("一覧の行の見本（押せるカード、名前、選んだときの金の枠）。行ごとに複製する。")]
        public Button RowTemplate;

        [Header("選んでいる物")]
        public TMP_Text Title;

        [Tooltip("種類・レア度・上限の行。")]
        public TMP_Text Detail;

        [Tooltip("大きく出す品質の文字。")]
        public TMP_Text QualityLetter;

        [Tooltip("倍率と、幅の中の位置。")]
        public TMP_Text MultiplierText;

        [Tooltip("品質ごとの幅（S ×1.45〜×1.50 など）。")]
        public TMP_Text RangeText;

        [Tooltip("幅の中の今の位置を示す印。親（帯）の左端が×1.00、右端が上限。")]
        public RectTransform Marker;

        [Tooltip("帯の品質ごとの区間（C・B・A・Sの順）。")]
        public RectTransform[] Segments = new RectTransform[0];

        public Slider MultiplierSlider;
        public Slider LimitSlider;
        public TMP_Text LimitText;

        [Tooltip("品質の中ほどへ飛ぶボタン（C・B・A・Sの順）。")]
        public Button[] QualityButtons = new Button[0];
        public Button StepDownButton;
        public Button StepUpButton;
        public Button LimitResetButton;

        [Tooltip("倍率を掛けた効果と、×1.00の効果。")]
        public TMP_Text EffectText;
        public TMP_Text BaseText;

        [Tooltip("倍率で変わる数字の、×1.00から今の倍率への変化。")]
        public TMP_Text NumbersText;

        [Header("見た目")]
        [Tooltip("大きく出す、今の倍率のカードか絵。")]
        public QualityLabPreview Main = new();

        [Tooltip("品質ごとの見比べ（C・B・A・Sの順）。それぞれの品質の中ほどの倍率で出す。")]
        public QualityLabPreview[] Compare = new QualityLabPreview[0];

        [Tooltip(
            "カードの挿絵（QualityLabEntry.Cards と同じ順）、枠と属性のアイコン（CardElementの順）。"
        )]
        public Texture[] CardArts = new Texture[0];
        public Texture[] CardFrames = new Texture[0];
        public Texture[] ElementIcons = new Texture[0];

        [Tooltip("カードの使い手の名前（CardUserの順）。")]
        public string[] PartyNames = new string[0];

        public Sprite WeaponIcon;
        public Sprite ArmorIcon;

        [Tooltip("見本のアクセサリーの絵（仮）。")]
        public Sprite AccessoryIcon;

        [Tooltip("ACTボーナスの定義（仮データ）。")]
        public StepBonusMockData Bonuses;

        [Header("ホームへ戻る")]
        public Button HomeButton;
        public SceneTransitionController Transition;

        [Tooltip("戻り先のシーンの名前。空なら戻るボタンを隠す。")]
        public string HomeSceneName = "";

        private readonly List<Button> rows = new();
        private IReadOnlyList<QualityLabEntry> entries = Array.Empty<QualityLabEntry>();
        private int entry;
        private float multiplier = Inventory.Quality.MinMultiplier;
        private float limit = Inventory.Quality.MinMultiplier;
        private bool started;

        public QualityLabTab Tab { get; private set; }
        public IReadOnlyList<QualityLabEntry> Entries => entries;
        public QualityLabEntry Current =>
            entry >= 0 && entry < entries.Count ? entries[entry] : null;
        public float Multiplier => multiplier;
        public float Limit => limit;
        public ItemQuality Quality => Inventory.Quality.Of(multiplier, limit);
        public IReadOnlyList<Button> Rows => rows;

        /// <summary>Whether the home button was pressed and the lab is leaving for home.</summary>
        public bool Leaving { get; private set; }

        private bool HasHome => !string.IsNullOrWhiteSpace(HomeSceneName);

        private void Start()
        {
            Listen(HomeButton, GoHome);
            if (HomeButton != null)
                HomeButton.gameObject.SetActive(HasHome);
            for (int t = 0; t < Tabs.Length; t++)
            {
                var tab = (QualityLabTab)t;
                if (Tabs[t] != null)
                    Listen(Tabs[t].Button, () => ShowTab(tab));
            }
            for (int q = 0; q < QualityButtons.Length && q < Inventory.Quality.All.Length; q++)
            {
                var quality = Inventory.Quality.All[q];
                Listen(QualityButtons[q], () => JumpTo(quality));
            }
            Listen(StepDownButton, () => SetMultiplier(multiplier - 0.01f));
            Listen(StepUpButton, () => SetMultiplier(multiplier + 0.01f));
            Listen(LimitResetButton, ResetLimit);
            if (MultiplierSlider != null)
                MultiplierSlider.onValueChanged.AddListener(SetMultiplier);
            if (LimitSlider != null)
            {
                LimitSlider.minValue = MinLimit;
                LimitSlider.maxValue = MaxLimit;
                LimitSlider.onValueChanged.AddListener(SetLimit);
            }
            started = true;
            Load();
        }

        private static void Listen(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null)
                button.onClick.AddListener(action);
        }

        /// <summary>Shows the things of <paramref name="tab"/> and picks the first.</summary>
        public void ShowTab(QualityLabTab tab) => Select(tab, 0, null);

        /// <summary>Picks the thing at <paramref name="index"/> of the tab shown, keeping the multiplier where it fits.</summary>
        public void Pick(int index) => Select(Tab, index, multiplier);

        /// <summary>The multiplier, rounded to a hundredth and kept between ×1.00 and the limit.</summary>
        public void SetMultiplier(float value)
        {
            multiplier = Mathf.Clamp(
                Inventory.Quality.Percent(value) / 100f,
                Inventory.Quality.MinMultiplier,
                limit
            );
            Save();
            Refresh();
        }

        /// <summary>Moves the multiplier to the middle of <paramref name="quality"/>'s range.</summary>
        public void JumpTo(ItemQuality quality) =>
            SetMultiplier(Inventory.Quality.Middle(quality, limit));

        /// <summary>Tries another upper limit for this kind (only in the lab); the multiplier stays within it.</summary>
        public void SetLimit(float value)
        {
            limit = Mathf.Clamp(Inventory.Quality.Percent(value) / 100f, MinLimit, MaxLimit);
            SetMultiplier(multiplier);
        }

        /// <summary>Back to the kind's own upper limit.</summary>
        public void ResetLimit()
        {
            if (Current != null)
                SetLimit(Current.Limit);
        }

        /// <summary>Goes back to home behind the shutter. If home cannot be loaded, the lab stays as it was.</summary>
        public void GoHome()
        {
            if (Leaving)
                return;
            Leaving = SceneLoader.Load(HomeSceneName, Transition, this, () => Leaving = false);
        }

        private void Select(QualityLabTab tab, int index, float? keep)
        {
            bool tabChanged = tab != Tab || entries.Count == 0;
            Tab = tab;
            if (tabChanged)
            {
                entries = QualityLabEntry.Of(tab, Bonuses);
                if (started)
                    BuildRows();
            }
            entry = entries.Count == 0 ? -1 : Mathf.Clamp(index, 0, entries.Count - 1);
            limit = Current?.Limit ?? Inventory.Quality.MinMultiplier;
            multiplier = Mathf.Clamp(
                keep ?? Inventory.Quality.Middle(ItemQuality.S, limit),
                Inventory.Quality.MinMultiplier,
                limit
            );
            for (int i = 0; i < Tabs.Length; i++)
                if (Tabs[i] != null)
                    Tabs[i].Chosen = i == (int)Tab;
            Save();
            Refresh();
        }

        private void BuildRows()
        {
            foreach (var row in rows)
                if (row != null)
                    Destroy(row.gameObject);
            rows.Clear();
            if (RowTemplate == null || ListContent == null)
                return;
            RowTemplate.gameObject.SetActive(false);
            for (int i = 0; i < entries.Count; i++)
            {
                var row = Instantiate(RowTemplate, ListContent);
                row.name = entries[i].Id;
                row.gameObject.SetActive(true);
                var label = row.GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                    label.text = $"{i + 1}　{entries[i].Name}";
                int picked = i;
                row.onClick.AddListener(() => Pick(picked));
                rows.Add(row);
            }
            if (ListScroll != null)
            {
                Canvas.ForceUpdateCanvases();
                ListScroll.verticalNormalizedPosition = 1f;
            }
        }

        /// <summary>Shows everything for the thing picked at the multiplier and limit set now.</summary>
        private void Refresh()
        {
            var current = Current;
            var quality = Quality;
            for (int i = 0; i < rows.Count; i++)
            {
                var selected = rows[i].transform.Find(SelectedName);
                if (selected != null)
                    selected.gameObject.SetActive(i == entry);
            }
            if (current == null)
                return;

            UiText.Set(Title, current.Name);
            UiText.Set(
                Detail,
                $"{current.KindName}"
                    + (current.Stars > 0 ? $"　レア度 {new string('★', current.Stars)}" : "")
                    + $"　上限 {Inventory.Quality.Times(limit)}"
                    + (Mathf.Approximately(limit, current.Limit) ? "" : "（試し）")
            );
            if (QualityLetter != null)
            {
                QualityLetter.text = Inventory.Quality.Letter(quality);
                QualityLetter.color = QualityPalette.Of(quality);
            }
            float place = Inventory.Quality.Place(multiplier, limit);
            UiText.Set(
                MultiplierText,
                $"{Inventory.Quality.Times(multiplier)}　（幅の上から {Mathf.RoundToInt((1f - place) * 100f)}%）"
            );
            UiText.SetRich(
                RangeText,
                string.Join(
                    "　",
                    Inventory
                        .Quality.All.Reverse()
                        .Select(q =>
                        {
                            var (low, high) = Inventory.Quality.Range(q, limit);
                            return $"{QualityPalette.Painted(q)} {Inventory.Quality.Times(low)}〜{high:0.00}";
                        })
                )
            );
            ShowBar(place);
            if (MultiplierSlider != null)
            {
                MultiplierSlider.minValue = Inventory.Quality.MinMultiplier;
                MultiplierSlider.maxValue = limit;
                MultiplierSlider.SetValueWithoutNotify(multiplier);
            }
            if (LimitSlider != null)
                LimitSlider.SetValueWithoutNotify(limit);
            UiText.Set(LimitText, $"上限 {Inventory.Quality.Times(limit)}");

            UiText.SetRich(EffectText, current.EffectAt(multiplier, limit));
            UiText.SetRich(BaseText, "×1.00のとき：" + current.EffectAt(1f, limit));
            UiText.Set(NumbersText, string.Join("　／　", current.NumbersAt(multiplier, limit)));

            ShowPreview(
                Main,
                current,
                multiplier,
                $"{Inventory.Quality.Letter(quality)}　{Inventory.Quality.Times(multiplier)}"
            );
            for (int i = 0; i < Compare.Length && i < Inventory.Quality.All.Length; i++)
            {
                var each = Inventory.Quality.All[i];
                float middle = Inventory.Quality.Middle(each, limit);
                ShowPreview(
                    Compare[i],
                    current,
                    middle,
                    $"{Inventory.Quality.Letter(each)}　{Inventory.Quality.Times(middle)}"
                );
            }
        }

        // 帯の区間を幅の中の位置（C 0〜0.4、B 0.4〜0.7、A 0.7〜0.9、S 0.9〜1）に置き、印を今の位置へ動かす。
        private void ShowBar(float place)
        {
            float[] edges = { 0f, 0.4f, 0.7f, 0.9f, 1f };
            for (int i = 0; i < Segments.Length && i < 4; i++)
            {
                var segment = Segments[i];
                if (segment == null)
                    continue;
                segment.anchorMin = new Vector2(edges[i], 0f);
                segment.anchorMax = new Vector2(edges[i + 1], 1f);
                segment.offsetMin = segment.offsetMax = Vector2.zero;
            }
            if (Marker != null)
            {
                Marker.anchorMin = Marker.anchorMax = new Vector2(place, 0.5f);
                Marker.anchoredPosition = Vector2.zero;
            }
        }

        private void ShowPreview(
            QualityLabPreview preview,
            QualityLabEntry current,
            float at,
            string caption
        )
        {
            if (preview == null)
                return;
            var quality = Inventory.Quality.Of(at, limit);
            bool card = current.IsSkill;
            if (preview.Card != null)
                preview.Card.gameObject.SetActive(card);
            if (preview.Item?.Root != null)
                preview.Item.Root.SetActive(!card);
            UiText.Set(preview.Caption, caption);
            if (preview.Caption != null)
                preview.Caption.color = QualityPalette.Of(quality);
            if (card && preview.Card != null)
            {
                var skill = current.SkillAt(at, limit);
                preview.Card.Show(
                    skill.Name,
                    At(PartyNames, (int)skill.User),
                    CardText.KindLine(skill),
                    CardText.Description(skill),
                    At(CardArts, current.ArtIndex),
                    At(CardFrames, (int)skill.Element),
                    At(ElementIcons, (int)skill.Element),
                    skill.Cost,
                    quality,
                    skill.FoilStyle
                );
                preview.Card.SetPlayable(true, 0f);
            }
            else if (!card)
                preview.Item?.Show(
                    current.Name,
                    current.Stars,
                    IconOf(current),
                    quality,
                    current.FoilStyle
                );
        }

        private Sprite IconOf(QualityLabEntry current) =>
            current.Tab switch
            {
                QualityLabTab.Weapons => WeaponIcon,
                QualityLabTab.Armor => ArmorIcon,
                QualityLabTab.Accessories => AccessoryIcon,
                _ => current.Bonus != null ? current.Bonus.Icon : null,
            };

        private static T At<T>(T[] items, int at)
            where T : class => items != null && at >= 0 && at < items.Length ? items[at] : null;

        /// <summary>The name of the gold frame of a row of the list, shown while it is picked.</summary>
        public const string SelectedName = "Selected";

        // 停止中の表示の切り替え：スキル・装備・ボーナスを、それぞれ見せたい品質で出す。
        // 一覧の行は作らず、選んでいる物の表示と見比べだけを変える。保存はしない。
        public IReadOnlyList<string> PreviewStates =>
            new[] { "スキル（品質S）", "武器（品質A）", "ACTボーナス（品質C）" };

        public void ShowPreviewState(int state)
        {
            var (tab, quality) = state switch
            {
                1 => (QualityLabTab.Weapons, ItemQuality.A),
                2 => (QualityLabTab.Bonuses, ItemQuality.C),
                _ => (QualityLabTab.Skills, ItemQuality.S),
            };
            Tab = tab;
            entries = QualityLabEntry.Of(tab, Bonuses);
            entry = entries.Count > 0 ? 0 : -1;
            limit = Current?.Limit ?? Inventory.Quality.MinMultiplier;
            multiplier = Inventory.Quality.Middle(quality, limit);
            for (int i = 0; i < Tabs.Length; i++)
                if (Tabs[i] != null)
                    Tabs[i].Chosen = i == (int)Tab;
            Refresh();
        }

        private void Load()
        {
            var tab = (QualityLabTab)
                Mathf.Clamp(
                    PlayerPrefs.GetInt(TabKey, 0),
                    0,
                    Tabs.Length > 0 ? Tabs.Length - 1 : 5
                );
            var id = PlayerPrefs.GetString(EntryKey, "");
            float saved = PlayerPrefs.GetFloat(MultiplierKey, -1f);
            Tab = tab;
            entries = QualityLabEntry.Of(tab, Bonuses);
            BuildRows();
            int index = Mathf.Max(0, entries.ToList().FindIndex(each => each.Id == id));
            Select(tab, index, saved >= Inventory.Quality.MinMultiplier ? saved : null);
        }

        private void Save()
        {
            if (!started)
                return;
            PlayerPrefs.SetInt(TabKey, (int)Tab);
            PlayerPrefs.SetString(EntryKey, Current?.Id ?? "");
            PlayerPrefs.SetFloat(MultiplierKey, multiplier);
        }
    }
}
