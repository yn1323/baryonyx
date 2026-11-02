using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Combat;
using Baryonyx.Enhancement;
using Baryonyx.Equipment;
using Baryonyx.Items;
using Baryonyx.UI;
using Baryonyx.UI.GuideMenu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.Inventory
{
    /// <summary>The inventory lab's tabs, one per category of thing it adds.</summary>
    public enum InventoryLabTab
    {
        Weapons,
        Armor,
        Accessories,
        Skills,
        Items,
        Materials,
    }

    /// <summary>One kind the lab can add: its category as the server names it, id, name and stars.</summary>
    public sealed class InventoryLabEntry
    {
        public string Category = "";
        public string Id = "";
        public string Name = "";
        public int Stars;

        /// <summary>Whether it is added with a multiplier (skills and equipment), so the quality applies.</summary>
        public bool HasQuality;

        /// <summary>Every kind of <paramref name="tab"/>, in its catalog's order.</summary>
        public static IReadOnlyList<InventoryLabEntry> Of(InventoryLabTab tab)
        {
            switch (tab)
            {
                case InventoryLabTab.Skills:
                    return CardSkills
                        .All.Select(skill => new InventoryLabEntry
                        {
                            Category = "skill",
                            Id = skill.Id,
                            Name = skill.Name,
                            HasQuality = true,
                        })
                        .ToArray();
                case InventoryLabTab.Items:
                    return ItemCards
                        .All.Select(item => new InventoryLabEntry
                        {
                            Category = "item",
                            Id = item.Id,
                            Name = item.Name,
                            Stars = item.Rarity,
                        })
                        .ToArray();
                case InventoryLabTab.Materials:
                    return EnhancementMaterials
                        .All.Select(material => new InventoryLabEntry
                        {
                            Category = "material",
                            Id = material.Id,
                            Name = material.Name,
                            Stars = material.Stars,
                        })
                        .ToArray();
                default:
                    var slot = tab switch
                    {
                        InventoryLabTab.Weapons => EquipmentSlot.Weapon,
                        InventoryLabTab.Armor => EquipmentSlot.Armor,
                        _ => EquipmentSlot.Accessory,
                    };
                    return EquipmentCatalog
                        .All.Where(gear => gear.Slot == slot)
                        .Select(gear => new InventoryLabEntry
                        {
                            Category = EquipmentCatalog.KeyOf(slot),
                            Id = gear.Id,
                            Name = gear.Name,
                            Stars = gear.Rarity,
                            HasQuality = true,
                        })
                        .ToArray();
            }
        }
    }

    /// <summary>
    /// The inventory lab, a debugging room that adds things to the saved inventory: pick a
    /// category's tab, the quality and how many, then tap a kind in the list to add it at once on
    /// the server, a skill or piece of equipment with a multiplier from that quality's range.
    /// The tab, quality and count are remembered on this machine.
    /// </summary>
    public sealed class InventoryLab : MonoBehaviour
    {
        // What is remembered between Plays (on this machine only).
        public const string TabKey = "Baryonyx.InventoryLab.Tab";
        public const string QualityKey = "Baryonyx.InventoryLab.Quality";
        public const string AmountKey = "Baryonyx.InventoryLab.Amount";

        /// <summary>How many one tap adds, in the order of <see cref="AmountTabs"/>.</summary>
        public static readonly int[] Amounts = { 1, 10 };

        /// <summary>The qualities in the order of <see cref="QualityTabs"/>, highest first.</summary>
        public static readonly ItemQuality[] Qualities = Inventory.Quality.All.Reverse().ToArray();

        /// <summary>How many of each kind the button for the whole tab adds.</summary>
        public const int AllAmount = 10;

        public const string NoServerMessage = "サーバーにつながっていないため、足せません";
        public const string FailedMessage = "足せませんでした";

        /// <summary>Where the lab adds things. The app sets it to the server; without one nothing can be added.</summary>
        public static IInventoryGrants Grants { get; set; }

        [Header("一覧")]
        [Tooltip("分類のタブ（InventoryLabTab の順）。")]
        public GuideTab[] Tabs = new GuideTab[0];

        [Tooltip("品質のタブ（S・A・B・Cの順）。スキルと装備のタブでだけ出す。")]
        public GuideTab[] QualityTabs = new GuideTab[0];

        [Tooltip("品質のタブをまとめた親。アイテムと強化素材のタブでは隠す。")]
        public GameObject QualityRow;

        [Tooltip("1回に足す数のタブ（Amounts の順）。")]
        public GuideTab[] AmountTabs = new GuideTab[0];

        [Tooltip("一覧の行を並べる所。")]
        public RectTransform ListContent;
        public ScrollRect ListScroll;

        [Tooltip("一覧の行の見本（押せる札、絵、名前、★）。行ごとに複製する。")]
        public Button RowTemplate;

        [Tooltip("表示中の分類の全種類を AllAmount 個ずつ足すボタン。")]
        public Button GrantAllButton;

        [Tooltip("足した結果を出す行。")]
        public TMP_Text Status;

        [Tooltip("種類ごとの絵。")]
        public ItemArt Art;

        [Header("ホームへ戻る")]
        public Button HomeButton;
        public SceneTransitionController Transition;

        [Tooltip("戻り先のシーンの名前。空なら戻るボタンを隠す。")]
        public string HomeSceneName = "";

        // 行の子の名前。
        public const string IconName = "Icon";
        public const string LabelName = "Name";
        public const string StarsName = "Stars";

        private readonly List<Button> rows = new();
        private IReadOnlyList<InventoryLabEntry> entries = Array.Empty<InventoryLabEntry>();
        private readonly CancellationTokenSource lifetime = new();
        private bool started;

        public InventoryLabTab Tab { get; private set; }
        public ItemQuality GrantQuality { get; private set; } = ItemQuality.S;
        public int Amount { get; private set; } = 1;
        public IReadOnlyList<InventoryLabEntry> Entries => entries;
        public IReadOnlyList<Button> Rows => rows;

        /// <summary>The last addition, so tests can wait for it.</summary>
        public Task LastGrant { get; private set; } = Task.CompletedTask;

        /// <summary>Whether the home button was pressed and the lab is leaving for home.</summary>
        public bool Leaving { get; private set; }

        private void Start()
        {
            Listen(HomeButton, GoHome);
            Listen(GrantAllButton, GrantAll);
            if (HomeButton != null)
                HomeButton.gameObject.SetActive(!string.IsNullOrWhiteSpace(HomeSceneName));
            for (int t = 0; t < Tabs.Length; t++)
            {
                var tab = (InventoryLabTab)t;
                if (Tabs[t] != null)
                    Listen(Tabs[t].Button, () => ShowTab(tab));
            }
            for (int q = 0; q < QualityTabs.Length && q < Qualities.Length; q++)
            {
                var quality = Qualities[q];
                if (QualityTabs[q] != null)
                    Listen(QualityTabs[q].Button, () => SetQuality(quality));
            }
            for (int a = 0; a < AmountTabs.Length && a < Amounts.Length; a++)
            {
                var amount = Amounts[a];
                if (AmountTabs[a] != null)
                    Listen(AmountTabs[a].Button, () => SetAmount(amount));
            }
            UiText.Set(Status, "");
            started = true;
            Load();
        }

        private void OnDestroy()
        {
            lifetime.Cancel();
            lifetime.Dispose();
        }

        private static void Listen(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null)
                button.onClick.AddListener(action);
        }

        /// <summary>Shows the kinds of <paramref name="tab"/>.</summary>
        public void ShowTab(InventoryLabTab tab)
        {
            Tab = tab;
            entries = InventoryLabEntry.Of(tab);
            BuildRows(entries);
            if (ListScroll != null)
            {
                Canvas.ForceUpdateCanvases();
                ListScroll.verticalNormalizedPosition = 1f;
            }
            Refresh();
        }

        public void SetQuality(ItemQuality quality)
        {
            GrantQuality = quality;
            Refresh();
        }

        public void SetAmount(int amount)
        {
            Amount = Mathf.Max(1, amount);
            Refresh();
        }

        /// <summary>Adds the kind at <paramref name="index"/> of the tab shown, as many as chosen.</summary>
        public void Grant(int index)
        {
            if (index < 0 || index >= entries.Count)
                return;
            LastGrant = GrantAsync(entries[index], GrantQuality, Amount);
        }

        /// <summary>
        /// Adds <see cref="AllAmount"/> of every kind of the tab shown, one kind after another at
        /// the chosen quality, showing how far it has got. A second tap while adding is ignored.
        /// </summary>
        public void GrantAll()
        {
            if (!LastGrant.IsCompleted || entries.Count == 0)
                return;
            LastGrant = GrantAllAsync(entries, GrantQuality);
        }

        private async Task GrantAllAsync(IReadOnlyList<InventoryLabEntry> kinds, ItemQuality quality)
        {
            var grants = Grants;
            if (grants == null)
            {
                Show(NoServerMessage, false);
                return;
            }
            SetAdding(true);
            try
            {
                for (int i = 0; i < kinds.Count; i++)
                {
                    Show($"足しています　{i + 1}/{kinds.Count}", true);
                    try
                    {
                        await grants.GrantAsync(
                            kinds[i].Category,
                            kinds[i].Id,
                            quality,
                            AllAmount,
                            lifetime.Token
                        );
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                    catch (Exception exception)
                    {
                        Debug.LogWarning($"{FailedMessage}（{kinds[i].Id}）。{exception.Message}", this);
                        Show($"{kinds[i].Name}を{FailedMessage}（{i}/{kinds.Count}種類まで足しました）", false);
                        return;
                    }
                }
                Show($"{kinds.Count}種類を ×{AllAmount} ずつ足しました", true);
            }
            finally
            {
                SetAdding(false);
            }
        }

        private void SetAdding(bool adding)
        {
            if (this != null && GrantAllButton != null)
                GrantAllButton.interactable = !adding;
        }

        private async Task GrantAsync(InventoryLabEntry entry, ItemQuality quality, int amount)
        {
            var grants = Grants;
            if (grants == null)
            {
                Show(NoServerMessage, false);
                return;
            }
            try
            {
                var granted = await grants.GrantAsync(
                    entry.Category,
                    entry.Id,
                    quality,
                    amount,
                    lifetime.Token
                );
                Show(GrantedText(entry, granted), true);
            }
            catch (OperationCanceledException) { }
            catch (Exception exception)
            {
                Debug.LogWarning($"{FailedMessage}（{entry.Id}）。{exception.Message}", this);
                Show($"{entry.Name}を{FailedMessage}", false);
            }
        }

        /// <summary>
        /// "炎の短剣 ×10 を足しました　S ×1.72〜×1.80": the quality of what was added and the
        /// lowest to highest multiplier, or one multiplier when only one was added.
        /// </summary>
        public static string GrantedText(InventoryLabEntry entry, InventoryGranted granted)
        {
            var text = $"{entry.Name} ×{granted.Count} を足しました";
            if (granted.Held.Count == 0)
                return text;
            float low = granted.Held.Min(each => each.Multiplier);
            float high = granted.Held.Max(each => each.Multiplier);
            var qualities = string.Concat(
                granted
                    .Held.Select(each => each.Quality)
                    .Distinct()
                    .OrderByDescending(quality => quality)
                    .Select(QualityPalette.Painted)
            );
            return $"{text}　{qualities} {Inventory.Quality.Times(low)}"
                + (high > low ? $"〜{Inventory.Quality.Times(high)}" : "");
        }

        private void Show(string message, bool succeeded)
        {
            if (this == null || Status == null)
                return;
            UiText.SetRich(Status, message);
            Status.color = succeeded ? UiPalette.TextMain : UiPalette.Fall;
        }

        /// <summary>Goes back to home behind the shutter. If home cannot be loaded, the lab stays as it was.</summary>
        public void GoHome()
        {
            if (Leaving)
                return;
            Leaving = SceneLoader.Load(HomeSceneName, Transition, this, () => Leaving = false);
        }

        /// <summary>
        /// Lays out one row per kind in <paramref name="kinds"/> from the template, taking out the
        /// rows there were. Rows are tapped only after Play starts.
        /// </summary>
        public void BuildRows(IReadOnlyList<InventoryLabEntry> kinds)
        {
            if (RowTemplate == null || ListContent == null)
                return;
            RowTemplate.gameObject.SetActive(false);
            foreach (Transform child in ListContent.Cast<Transform>().ToArray())
            {
                if (child == RowTemplate.transform)
                    continue;
                if (Application.isPlaying)
                    Destroy(child.gameObject);
                else
                    DestroyImmediate(child.gameObject);
            }
            rows.Clear();
            for (int i = 0; i < kinds.Count; i++)
            {
                var kind = kinds[i];
                var row = Instantiate(RowTemplate, ListContent);
                row.name = kind.Id;
                row.gameObject.SetActive(true);
                var sprite = Art != null ? Art.Find(kind.Id) : null;
                var icon = Part<Image>(row, IconName);
                if (icon != null)
                {
                    icon.sprite = sprite;
                    icon.enabled = sprite != null;
                }
                UiText.Set(Part<TMP_Text>(row, LabelName), kind.Name);
                UiText.Set(
                    Part<TMP_Text>(row, StarsName),
                    kind.Stars > 0 ? new string('★', kind.Stars) : ""
                );
                int picked = i;
                if (Application.isPlaying)
                    row.onClick.AddListener(() => Grant(picked));
                rows.Add(row);
            }
        }

        // 行の中の部品。札の中身は枠の下に入るため、子孫から名前で探す。
        private static T Part<T>(Component row, string name)
            where T : Component =>
            row.GetComponentsInChildren<T>(true).FirstOrDefault(part => part.name == name);

        private void Refresh()
        {
            for (int i = 0; i < Tabs.Length; i++)
                if (Tabs[i] != null)
                    Tabs[i].Chosen = i == (int)Tab;
            for (int i = 0; i < QualityTabs.Length && i < Qualities.Length; i++)
                if (QualityTabs[i] != null)
                    QualityTabs[i].Chosen = Qualities[i] == GrantQuality;
            for (int i = 0; i < AmountTabs.Length && i < Amounts.Length; i++)
                if (AmountTabs[i] != null)
                    AmountTabs[i].Chosen = Amounts[i] == Amount;
            if (QualityRow != null)
                QualityRow.SetActive(entries.Count == 0 || entries[0].HasQuality);
            Save();
        }

        private void Load()
        {
            var tab = (InventoryLabTab)
                Mathf.Clamp(PlayerPrefs.GetInt(TabKey, 0), 0, (int)InventoryLabTab.Materials);
            GrantQuality = (ItemQuality)
                Mathf.Clamp(
                    PlayerPrefs.GetInt(QualityKey, (int)ItemQuality.S),
                    (int)ItemQuality.C,
                    (int)ItemQuality.S
                );
            int amount = PlayerPrefs.GetInt(AmountKey, 1);
            Amount = Amounts.Contains(amount) ? amount : 1;
            ShowTab(tab);
        }

        private void Save()
        {
            if (!started)
                return;
            PlayerPrefs.SetInt(TabKey, (int)Tab);
            PlayerPrefs.SetInt(QualityKey, (int)GrantQuality);
            PlayerPrefs.SetInt(AmountKey, Amount);
        }
    }
}
