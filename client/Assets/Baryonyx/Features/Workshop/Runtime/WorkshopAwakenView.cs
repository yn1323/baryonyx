using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Combat;
using Baryonyx.Combat.Presentation;
using Baryonyx.Economy;
using Baryonyx.Enhancement;
using Baryonyx.Inventory;
using Baryonyx.Party;
using Baryonyx.SkillEvolution;
using Baryonyx.Training;
using Baryonyx.UI;
using Baryonyx.UI.Cards;
using Baryonyx.UI.EditModePreview;
using Baryonyx.UI.GuideMenu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.Workshop
{
    /// <summary>
    /// The trading house's skill awakening (doc/features/screens/shop.md#スキル覚醒): one list of
    /// what can still awaken, person by person (their unique skills, then the cards in their
    /// custom skill slots), then the cards no one holds, each row a glass card with the person
    /// in it at the left (<see cref="AwakeningRows"/>). Pressing a row opens the awakening scene
    /// over the panel: the card above and the stages it can become below, the battle's own
    /// choice (<see cref="BattleEvolutionChoice"/>). The check awakens the stage picked with an
    /// awakening shard and runes; the card turns into it, and the list is drawn again without it.
    /// </summary>
    public sealed class WorkshopAwakenView : GuidePanel, IGuideBackHandler, IEditModePreview
    {
        public const string LoadFailedMessage = "覚醒できる物を取得できませんでした";
        public const string FailedMessage = "覚醒できませんでした";
        public const string EmptyText = "覚醒できるスキルがありません";

        // 読み込む間に伏せて並べる行の数（一覧の見える所を埋める）。
        public const int CoverRows = 7;

        public PartyMockData Party;
        public TrainingMockData Training;

        public ScrollRect List;
        public RectTransform Content;
        public WorkshopAwakenRow Template;
        public List<WorkshopAwakenRow> Rows = new();
        public TMP_Text Status;

        // 行の高さと、キャラが替わる行の上に空ける間（設計座標）。
        public float RowHeight = 132f;
        public float GroupGap = 36f;

        // 行の属性のアイコン（CardElement の順、属性なしは空）。
        public Sprite[] ElementIcons = Array.Empty<Sprite>();

        // 覚醒の場面の戦闘のカード：属性ごとの枠と属性のアイコン（CardElement の順）。
        public Texture[] CardFrames = Array.Empty<Texture>();
        public Texture[] ElementArts = Array.Empty<Texture>();

        // 覚醒の場面：一覧を暗く覆う幕、戦闘と同じ覚醒の演出（Vfx）と覚醒先の選択、左下の払う物の札。
        // Origin は覚醒するカード。実行中は演出の層へ移り、行から浮き上がって上に浮かぶ。
        public GameObject Scene;
        public BattleSkillVfx Vfx;
        public BattleInspectCardView Origin;
        public BattleEvolutionChoice Choice;

        // 払う物の札：覚醒の欠片の絵と「使う数/持っている数」、払うルーン。足りない数は赤くする。
        // 覚醒先を選ぶ間と、欠片が飛ぶ間だけ出す。
        public GameObject CostCard;
        public RawImage CostShard;
        public TMP_Text ShardCount;
        public TMP_Text RuneCost;

        // チェックを押すと、欠片の札からカードへ飛び込む欠片の絵。
        public RawImage ShardFlight;

        private readonly HashSet<Component> bound = new();
        private readonly GlassCardCover cover = new();
        private IReadOnlyList<AwakeningRow> rows = Array.Empty<AwakeningRow>();
        private IReadOnlyList<PartyMember> people = Array.Empty<PartyMember>();
        private List<PartySkillCard> ownedCards = new();
        private PartyState party;
        private AwakeningRow opened;
        private Coroutine running;
        private bool choosing;
        private bool busy;

        /// <summary>The rows listed, in order.</summary>
        public IReadOnlyList<AwakeningRow> Entries => rows;

        /// <summary>The row whose awakening scene is open, or null.</summary>
        public AwakeningRow Opened => opened;

        public bool SceneOpen => Scene != null && Scene.activeSelf;

        // 直前の覚醒。テストで完了を待つために公開する。
        public Task Work { get; private set; } = Task.CompletedTask;

        private void OnEnable()
        {
            BindRows();
            RuneBalance.Changed += RefreshCost;
            CloseScene();
            ShowLoading();
            StartLoad(LoadAsync, LoadFailedMessage, ShowLoadFailed);
        }

        private void OnDisable()
        {
            Load.Cancel();
            RuneBalance.Changed -= RefreshCost;
            StopScene();
        }

        private async Task LoadAsync(CancellationToken token)
        {
            // 編成と素材は互いに依存しないため、同時に読む。
            var reading = PartySession.LoadAsync(Party, PartySession.Source, token);
            var materials = EnhancementSession.LoadAsync(token);
            await Task.WhenAll(reading, materials);
            if (token.IsCancellationRequested)
                return;
            Use(await reading, PartySession.State);
            Render();
            RefreshShards();
        }

        /// <summary>Takes the people and the cards the list is made of.</summary>
        public void Use(PartyFormation formation, PartyState state)
        {
            party = state;
            people = formation != null ? formation.TabOrder().People : Array.Empty<PartyMember>();
            ownedCards = (state?.SkillCards ?? MockCards(formation)).ToList();
            rows = AwakeningRows.Of(people, Training, ProgressOf, ownedCards, CardsOf);
        }

        // サーバーがないときは、仮データの4人のカードをその人の枠に×1.00で入れ、誰にも付けていない
        // 1枚の見本として、先頭の人のカードの写しを品質違いで2枚持っている扱いにする。
        private static IEnumerable<PartySkillCard> MockCards(PartyFormation formation)
        {
            var roster = formation?.Roster ?? Array.Empty<PartyMember>();
            foreach (var member in roster)
                for (int i = 0; i < member.Cards.Length; i++)
                    yield return new PartySkillCard(
                        MockId(member.Id, i),
                        member.Cards[i].Skill,
                        1f
                    );
            var first = roster.FirstOrDefault(member => member.Cards.Length > 0);
            if (first == null)
                yield break;
            yield return new PartySkillCard("mock:spare:0", first.Cards[0].Skill, 1.15f);
            yield return new PartySkillCard("mock:spare:1", first.Cards[0].Skill, 1f);
        }

        private static string MockId(string person, int slot) => $"mock:{person}:{slot}";

        // その人のカスタムスキルの枠に入っている、持っているスキルの1枚（枠の順）。
        private IEnumerable<string> CardsOf(string person)
        {
            if (party != null)
                return (party.Find(person)?.Cards ?? Array.Empty<string>())
                    .Where(key => key != null && PartyCardKey.IsCard(key))
                    .Select(PartyCardKey.IdOf);
            var member = people.FirstOrDefault(entry => entry.Id == person);
            return member == null
                ? Array.Empty<string>()
                : member.Cards.Select((_, i) => MockId(person, i));
        }

        private UniqueSkillProgress ProgressOf(string id, int index) =>
            UniqueSkillSession.ProgressOf(
                id,
                index,
                Training != null ? Training.Find(id)?.MockProgressOf(index) : null
            );

        // --- The list -----------------------------------------------------------------------

        private void Render()
        {
            EnsureRows(rows.Count);
            for (int i = 0; i < Rows.Count; i++)
            {
                bool shown = i < rows.Count;
                Rows[i].gameObject.SetActive(shown);
                if (shown)
                    Paint(Rows[i], rows[i], i > 0 && rows[i].Character != rows[i - 1].Character);
            }
            Set(Status, rows.Count == 0 ? EmptyText : "");
            // 覚醒できる物のない場所の伏せた行も、めくってから静かに消す。
            cover.Reveal(
                Rows.Take(rows.Count).Select(row => row.Card),
                Rows.Skip(rows.Count).Take(CoverRows - rows.Count).Select(row => row.Card)
            );
            if (List != null)
                List.verticalNormalizedPosition = 1f;
        }

        /// <summary>
        /// Draws <paramref name="entry"/> into <paramref name="row"/>; with
        /// <paramref name="groupStart"/> it is the first of a new person (or of the cards no
        /// one holds), and has a gap above it.
        /// </summary>
        public void Paint(WorkshopAwakenRow row, AwakeningRow entry, bool groupStart)
        {
            if (row.Size != null)
                row.Size.minHeight = row.Size.preferredHeight =
                    RowHeight + (groupStart ? GroupGap : 0f);
            var person = entry.Character;
            if (row.Character != null)
            {
                row.Character.texture = person != null ? person.Art : null;
                row.Character.enabled = person != null && person.Art != null;
                row.Character.color = person != null ? person.Tint : Color.white;
                row.Character.uvRect =
                    person != null && person.Flip
                        ? new Rect(1f, 0f, -1f, 1f)
                        : new Rect(0, 0, 1, 1);
            }
            var card = entry.Card;
            var art = card != null && Party != null ? Party.ArtOf(card.Id) : null;
            if (row.Art != null)
            {
                row.Art.texture = art;
                row.Art.enabled = art != null;
            }
            if (row.Foil != null)
                row.Foil.Show(art, entry.Quality, card?.FoilStyle ?? 0);
            Set(row.Cost, card != null ? card.Cost.ToString() : "");
            if (row.Element != null)
            {
                var icon = card != null ? At(ElementIcons, (int)card.Element) : null;
                row.Element.sprite = icon;
                row.Element.enabled = icon != null;
            }
            Set(row.Name, card?.Name ?? "");
            Set(row.Kind, card != null ? CardText.KindLine(card) : "");
            Set(row.Description, card != null ? CardText.Description(card) : "");
            Set(row.QualityLetter, Quality.Letter(entry.Quality));
            if (row.QualityLetter != null)
                row.QualityLetter.color = QualityPalette.Of(entry.Quality);
            Set(row.Multiplier, Quality.Times(entry.Multiplier));
        }

        private void EnsureRows(int count)
        {
            Rows.RemoveAll(row => row == null);
            while (Rows.Count < count && Template != null && Content != null)
                Rows.Add(Instantiate(Template, Content));
            BindRows();
        }

        private void BindRows()
        {
            for (int i = 0; i < Rows.Count; i++)
            {
                var row = Rows[i];
                row.name = "Row" + i;
                if (row.Button == null || !bound.Add(row))
                    continue;
                int index = i;
                row.Button.onClick.AddListener(() => Open(index));
            }
        }

        // 読み込むまで、一覧の見える所をカードの裏で埋める。
        private void ShowLoading()
        {
            EnsureRows(CoverRows);
            for (int i = 0; i < Rows.Count; i++)
                Rows[i].gameObject.SetActive(i < CoverRows);
            cover.Cover(Rows.Take(CoverRows).Select(row => row.Card));
            Set(Status, "");
        }

        private void ShowLoadFailed()
        {
            cover.Clear();
            foreach (var row in Rows)
                if (row != null)
                    row.gameObject.SetActive(false);
            Set(Status, LoadFailedText);
        }

        // --- The awakening scene ------------------------------------------------------------

        /// <summary>
        /// Opens the awakening scene of the row at <paramref name="index"/>: in play, the card
        /// rises out of the row and cracks, then hovers at the top over the stages it can become
        /// (the battle's omen); in the editor's preview, it is shown there at once.
        /// </summary>
        public void Open(int index)
        {
            if (busy || running != null || index < 0 || index >= rows.Count)
                return;
            opened = rows[index];
            if (Scene != null)
                Scene.SetActive(true);
            RefreshCost();
            if (Application.isPlaying && Vfx != null && Origin != null)
            {
                running = StartCoroutine(Run(opened, CasterOf(Rows[index])));
                return;
            }
            ShowCard(Origin, opened.Card, opened.Character, opened.Quality);
            ShowCost(true);
            if (Choice != null)
                Choice.Open(OptionsOf(opened), OriginPoint(), _ => CloseScene());
        }

        /// <summary>Closes the awakening scene without awakening (not once a stage is confirmed).</summary>
        public void CloseScene()
        {
            if (busy)
                return;
            StopScene();
        }

        private void StopScene()
        {
            if (running != null)
                StopCoroutine(running);
            running = null;
            choosing = busy = false;
            opened = null;
            if (Choice != null)
                Choice.Close();
            if (Vfx != null && Application.isPlaying)
                Vfx.Clear();
            if (ShardFlight != null)
                ShardFlight.gameObject.SetActive(false);
            if (Scene != null)
                Scene.SetActive(false);
        }

        // 覚醒の場面を開いていれば、それを閉じる（ヘッダーの「もどる」と端末の戻るキー）。
        // 覚醒先を決めたあとは、演出が終わるまで閉じない。
        public bool HandleBack()
        {
            if (!SceneOpen)
                return false;
            CloseScene();
            return true;
        }

        // カードが浮き上がる所：行のキャラの絵。誰にも付けていないスキルは行の挿絵。
        private static RectTransform CasterOf(WorkshopAwakenRow row) =>
            row.Character != null && row.Character.enabled ? row.Character.rectTransform
            : row.Art != null ? row.Art.rectTransform
            : (RectTransform)row.transform;

        private IEnumerator Run(AwakeningRow row, RectTransform caster)
        {
            var card = (RectTransform)Origin.transform;
            card.SetParent(Vfx.EvolutionLayer, false);
            // カードは一覧の行から浮き上がるため、戦闘のようにキャラの足元に魔法陣や光を置かない。
            Vfx.GroundUnderCaster = false;
            ShowCost(false);
            ShowCard(Origin, row.Card, row.Character, row.Quality);
            // 枝の2段目へ進むときは、戦闘と同じく深い演出にする。
            bool deep = !row.IsSkillCard && row.Options.All(option => Step(option) == 2);
            yield return Vfx.EvolutionOmen(card, caster, deep);
            yield return Vfx.EvolutionPresent();

            const int none = -1;
            int pick = none;
            TaskCompletionSource<bool> done = null;
            choosing = true;
            ShowCost(true);
            Choice.Open(
                OptionsOf(row),
                Vfx.EvolvingScreenPoint(),
                p =>
                {
                    // 決めた時点から、演出と一覧の作り直しまでを1つの仕事にする（テストで待つ）。
                    pick = p;
                    busy = true;
                    done = new TaskCompletionSource<bool>();
                    Work = done.Task;
                }
            );
            while (pick == none)
                yield return null;
            choosing = false;
            var option = row.Options[pick];
            var request = EnhancementSession.AwakenAsync(option);
            // 欠片が札からカードへ飛び込む間に、サーバーの答えを待つ。
            yield return FlyShard(card);
            ShowCost(false);
            while (!request.IsCompleted)
                yield return null;
            if (request.IsFaulted || request.IsCanceled)
            {
                var error = request.Exception?.GetBaseException();
                Debug.LogWarning(FailedMessage + "。" + error?.Message, this);
                ShowNotice(FailedMessage);
                busy = false;
                running = null;
                StopScene();
                // 覚醒できなかったら、持ち物と素材を読み直す。
                ShowLoading();
                StartLoad(LoadAsync, LoadFailedMessage, ShowLoadFailed);
                done.TrySetResult(false);
                yield break;
            }
            // 覚醒したスキルの1枚は、その段階のカードになり、もう一覧に出さない。
            if (option.IsSkillCard)
                ownedCards = ownedCards
                    .Select(owned =>
                        owned.Id == option.OwnedCard ? owned.Awakened(request.Result.Stage) : owned
                    )
                    .ToList();
            RefreshShards();
            var next = option.Card?.WithMultiplier(row.Multiplier);
            // 割れて覚醒した段階が現れ、行のキャラへ飛び込む。
            yield return Vfx.EvolutionBurst(
                BattleSkillVfx.ColorOf(next?.Id ?? ""),
                Step(option) == 2,
                () => ShowCard(Origin, next, row.Character, row.Quality)
            );
            ShowNotice(AwakenedText(option));
            busy = false;
            running = null;
            StopScene();
            rows = AwakeningRows.Of(people, Training, ProgressOf, ownedCards, CardsOf);
            Render();
            done.TrySetResult(true);
        }

        private static int Step(AwakeningOption option) =>
            option.IsSkillCard ? 1 : UniqueSkillTreeShape.Step(option.Stage);

        // 欠片の札から、上に浮かぶカードへ、弧を描いて飛び込む。
        private IEnumerator FlyShard(RectTransform card)
        {
            if (ShardFlight == null || CostShard == null)
                yield break;
            var flight = ShardFlight.rectTransform;
            var parent = (RectTransform)flight.parent;
            Vector2 Local(RectTransform target) =>
                parent.InverseTransformPoint(target.TransformPoint(target.rect.center));
            var from = Local(CostShard.rectTransform);
            var to = Local(card);
            flight.gameObject.SetActive(true);
            const float seconds = 0.55f;
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                float k = t / seconds;
                float ease = k * k * (3f - 2f * k);
                var at = Vector2.Lerp(from, to, ease) + Vector2.up * Mathf.Sin(k * Mathf.PI) * 220f;
                flight.anchoredPosition = at;
                flight.localScale = Vector3.one * Mathf.Lerp(1.2f, 0.4f, ease);
                flight.localEulerAngles = new Vector3(0f, 0f, k * 360f);
                ShardFlight.color = new Color(1f, 1f, 1f, k < 0.85f ? 1f : (1f - k) / 0.15f);
                yield return null;
            }
            flight.gameObject.SetActive(false);
        }

        private List<BattleEvolutionChoice.Option> OptionsOf(AwakeningRow row)
        {
            // スキルの枝は、同じ種類の別の1枚で取った物を「取得済み」にする。
            var taken = new HashSet<string>(
                ownedCards.Where(card => card.AwakenedBranch > 0).Select(card => card.StageId)
            );
            return row
                .Options.Select(option =>
                {
                    var card = option.Card?.WithMultiplier(row.Multiplier);
                    return new BattleEvolutionChoice.Option
                    {
                        Name = card?.Name ?? "",
                        Owner = row.Character?.Name ?? "",
                        Kind = card != null ? CardText.KindLine(card) : "",
                        Description = card != null ? CardText.Description(card) : "",
                        Art = card != null && Party != null ? Party.ArtOf(card.Id) : null,
                        Frame = card != null ? At(CardFrames, (int)card.Element) : null,
                        Element = card != null ? At(ElementArts, (int)card.Element) : null,
                        Cost = card?.Cost ?? 0,
                        Quality = row.Quality,
                        FoilStyle = card?.FoilStyle ?? 0,
                        Acquired = option.IsSkillCard && card != null && taken.Contains(card.Id),
                    };
                })
                .ToList();
        }

        private void ShowCard(
            BattleInspectCardView view,
            CardSkill card,
            PartyMember owner,
            ItemQuality quality
        )
        {
            if (view == null || card == null)
                return;
            view.Show(
                card.Name,
                owner?.Name ?? "",
                CardText.KindLine(card),
                CardText.Description(card),
                Party != null ? Party.ArtOf(card.Id) : null,
                At(CardFrames, (int)card.Element),
                At(ElementArts, (int)card.Element),
                card.Cost,
                quality,
                card.FoilStyle
            );
            view.SetPlayable(true, 0f);
        }

        // 覚醒先の光の線は、上に浮かぶ今のカードの下端から伸ばす。
        private Vector2 OriginPoint()
        {
            if (Origin == null)
                return new Vector2(Screen.width * 0.5f, Screen.height * 0.8f);
            var rect = (RectTransform)Origin.transform;
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var bottom = (corners[0] + corners[3]) * 0.5f;
            var canvas = rect.GetComponentInParent<Canvas>();
            var eye =
                canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                    ? canvas.worldCamera
                    : null;
            return RectTransformUtility.WorldToScreenPoint(eye, bottom);
        }

        private int Runes => opened?.Options.FirstOrDefault()?.Runes ?? 0;

        private static int Shards => EnhancementSession.CountOf(EnhancementMaterials.Shard);

        private bool CanAwaken =>
            opened != null
            && Shards >= EnhancementMaterials.ShardsPerStage
            && (RuneBalance.Known is not { } balance || balance >= Runes);

        private void RefreshCost()
        {
            if (opened == null)
                return;
            bool shards = Shards >= EnhancementMaterials.ShardsPerStage;
            bool runes = RuneBalance.Known is not { } balance || balance >= Runes;
            Set(ShardCount, $"{EnhancementMaterials.ShardsPerStage}/{Shards}");
            if (ShardCount != null)
                ShardCount.color = shards ? UiPalette.TextMain : Short;
            Set(RuneCost, $"{Runes:N0}");
            if (RuneCost != null)
                RuneCost.color = runes ? UiPalette.TextMain : Short;
            if (Choice != null)
                Choice.CanConfirm = CanAwaken;
        }

        private void RefreshShards() => RefreshCost();

        private void ShowCost(bool shown)
        {
            if (CostCard != null)
                CostCard.SetActive(shown);
        }

        // 足りない数の色（品質強化の「足りません」と同じ赤）。
        private static readonly Color Short = new(1f, 0.54f, 0.48f);

        public static string AwakenedText(AwakeningOption option) =>
            $"「{option.From?.Name}」が「{option.Card?.Name}」に覚醒した";

        private static T At<T>(T[] items, int index)
            where T : class =>
            items != null && index >= 0 && index < items.Length ? items[index] : null;

        private static void Set(TMP_Text label, string text)
        {
            if (label != null)
                label.text = text ?? "";
        }

        // --- Previews in the editor ---------------------------------------------------------

        // 停止中の表示の切り替え：仮データの一覧、先頭の行の覚醒の場面、読み込む間、読めなかったとき。
        public IReadOnlyList<string> PreviewStates =>
            new[] { "一覧", "覚醒の場面" }.Concat(GuidePanelLoad.PreviewStates).ToArray();

        public void ShowPreviewState(int index)
        {
            StopScene();
            if (index <= 1)
            {
                cover.Clear();
                Use(PartyFormation.From(Party), null);
                Render();
                RefreshShards();
                if (index == 1 && rows.Count > 0)
                {
                    Open(0);
                    if (Choice != null)
                    {
                        Choice.Select(0);
                        Choice.Settle();
                    }
                }
                return;
            }
            ShowLoading();
            if (index == 3)
                ShowLoadFailed();
        }
    }
}
