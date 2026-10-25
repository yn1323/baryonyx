using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Baryonyx.Combat;
using Baryonyx.Combat.Presentation;
using Baryonyx.Party;
using Baryonyx.Training;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Baryonyx.Adventure
{
    /// <summary>
    /// Fights the battles a <see cref="BattleBalanceRequest"/> asks for, one after another, on the
    /// battle screen itself (<see cref="BattleInspectView"/>, the same code as the adventure's
    /// battle), and sums them up (<see cref="Result"/>). Each battle loads the battle lab scene,
    /// which holds the same battle screen, and sets it up as the adventure's battle does before it
    /// begins: the room's encounter of <see cref="AdventureCatalog"/>, the party of
    /// <see cref="BattleBalanceParty"/>, ending with its outcome. The effects are left out (the
    /// screen's no-effect path lands each blow at once) and the time runs faster; the unique skills
    /// do not evolve, as they need the effects. The cards are played through the screen's own
    /// controls, chosen by an <see cref="IBattleAutoPolicy"/>. Runs in Play Mode (a PlayMode test).
    /// Each battle's seed sets Unity's random numbers and the rolls of <see cref="BattleOdds"/>, so
    /// the same request fights the same battles again; with fixed odds asked for, the rolls are
    /// fixed instead and left so for the caller to put back. The levels it sets, the time scale and
    /// the seeded rolls (back to the shared ones the game uses) are put back when it ends.
    /// </summary>
    public sealed class BattleBalanceSession
    {
        public const string LabScenePath = "Assets/Baryonyx/App/Scenes/Debug/BattleLab.unity";

        // 実時間でこれだけ何も進まなければ、止まったとみなして打ち切る。
        public const float StuckSeconds = 30f;

        private readonly BattleBalanceRequest request;
        private readonly PartyMockData party;
        private readonly TrainingMockData training;
        private readonly IBattleAutoPolicy policy;
        private readonly List<BattleBalanceRecord> battles = new();
        private readonly List<BattleBalanceRun> runs = new();

        public BattleBalanceSession(
            BattleBalanceRequest request,
            PartyMockData party,
            TrainingMockData training,
            IBattleAutoPolicy policy = null
        )
        {
            this.request = request ?? new BattleBalanceRequest();
            this.party = party != null ? party : throw new ArgumentNullException(nameof(party));
            this.training =
                training != null ? training : throw new ArgumentNullException(nameof(training));
            this.policy = policy ?? BattleAutoPolicies.Of(this.request.policy);
        }

        /// <summary>The battles fought so far.</summary>
        public IReadOnlyList<BattleBalanceRecord> Battles => battles;

        /// <summary>The sums, once <see cref="Run"/> has ended.</summary>
        public BattleBalanceResult Result { get; private set; }

        public IEnumerator Run()
        {
            float started = Time.realtimeSinceStartup;
            string startedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            float timeScale = Time.timeScale;
            var levels = PartySession
                .Members(PartySession.Formation(party))
                .Where(member => member != null)
                .ToDictionary(
                    member => member.Id,
                    member => PartySession.LevelOf(party, member.Id)
                );
            if (FixedOdds)
                BattleOdds.Fix(critical: false, lands: true);
            Time.timeScale = Mathf.Max(1f, request.timeScale);
            try
            {
                if (request.IsThrough)
                    yield return Walk();
                else
                    yield return Repeat();
            }
            finally
            {
                // 種で振った確率を、本物の戦闘と同じ共有の乱数へ戻す。
                if (!FixedOdds)
                    BattleOdds.Unfix();
                Time.timeScale = timeScale;
                foreach (var level in levels)
                    PartySession.SetLevel(level.Key, level.Value);
            }
            Result = BattleBalanceResult.Of(request, battles, runs);
            Result.startedAt = startedAt;
            Result.seconds = Time.realtimeSinceStartup - started;
        }

        // 1つの部屋の戦闘を、組み合わせごとに決めた回数だけ戦う。
        private IEnumerator Repeat()
        {
            var encounters = BattleBalanceEncounters.Of(request);
            int total = encounters.Count * Math.Max(1, request.repeats);
            bool boss = request.RoomKind == AdventureRoomKind.Boss;
            var start = Enumerable.Repeat(request.startHpShare, AdventureVitals.Slots).ToArray();
            int number = 0;
            for (int repeat = 0; repeat < Math.Max(1, request.repeats); repeat++)
                foreach (string encounter in encounters)
                {
                    BattleBalanceRecord record = null;
                    yield return Fight(
                        encounter,
                        request.seed + number,
                        start,
                        boss,
                        (fought, _) => record = fought
                    );
                    record.run = number;
                    battles.Add(record);
                    number++;
                    Debug.Log($"[balance] {number}/{total} {Line(record)}");
                }
        }

        // 冒険の道を入口から最奥まで歩き、戦闘のマスで戦ってHPを持ち越す。休憩のマスでは休む。
        // 戦闘後の強化と休憩の鍛える・研鑽、「？」の中身はまだ入れていない。
        private IEnumerator Walk()
        {
            for (int number = 0; number < Math.Max(1, request.runs); number++)
            {
                int seed = request.seed + number;
                var route = AdventureCatalog.Route(request.destination, seed, 0);
                var random = new System.Random(seed);
                var shares = Enumerable
                    .Repeat(request.startHpShare, AdventureVitals.Slots)
                    .ToArray();
                var run = new BattleBalanceRun { run = number, seed = seed };
                var at = route.Find(AdventureRouteMap.EntranceId);
                while (at != null && at.Next.Count > 0)
                {
                    var room = route.Find(at.Next[random.Next(at.Next.Count)]);
                    if (room == null)
                        break;
                    run.floor = room.Floor;
                    bool fight =
                        !string.IsNullOrEmpty(room.Encounter)
                        && (room.Kind != AdventureRoomKind.ShadyChest || request.openShadyChests);
                    if (fight)
                    {
                        BattleBalanceRecord record = null;
                        float[] after = null;
                        yield return Fight(
                            room.Encounter,
                            seed * 100 + room.Floor,
                            shares,
                            room.Kind == AdventureRoomKind.Boss,
                            (fought, left) =>
                            {
                                record = fought;
                                after = left;
                            }
                        );
                        record.run = number;
                        record.roomId = room.Id;
                        battles.Add(record);
                        run.battles++;
                        shares = after;
                        Debug.Log($"[balance] run {number} {room.Id} {Line(record)}");
                        if (!record.Won)
                        {
                            run.lostIn = room.Id;
                            break;
                        }
                        if (room.Kind == AdventureRoomKind.Boss)
                            run.cleared = true;
                    }
                    else if (room.Kind == AdventureRoomKind.Rest)
                        shares = shares
                            .Select(share => Mathf.Min(1f, share + AdventureVitals.RestHeal))
                            .ToArray();
                    at = room;
                }
                run.hpEnd = shares.Average();
                runs.Add(run);
            }
        }

        private bool FixedOdds =>
            string.Equals(request.odds, "fixed", StringComparison.OrdinalIgnoreCase);

        private static string Line(BattleBalanceRecord record) =>
            $"{record.encounter} ({record.enemies}) {record.outcome} {record.turns}T hp {record.hpEnd:0.00}";

        /// <summary>
        /// Fights one battle of <paramref name="encounter"/> with the party starting at
        /// <paramref name="shares"/>, and hands <paramref name="done"/> its record and the HP
        /// shares it ended with.
        /// </summary>
        private IEnumerator Fight(
            string encounter,
            int seed,
            IReadOnlyList<float> shares,
            bool boss,
            Action<BattleBalanceRecord, float[]> done
        )
        {
            var members = BattleBalanceParty.Of(request, party, training, shares);
            BattleInspectView view = null;
            // 戦闘画面が自分の準備（Awake）を終え、戦闘を始める（Start）前に、冒険の戦闘と同じく敵と編成を入れる。
            void Prepare(Scene scene, LoadSceneMode mode)
            {
                if (scene.path != LabScenePath)
                    return;
                view = scene
                    .GetRootGameObjects()
                    .Select(root => root.GetComponentInChildren<BattleInspectView>(true))
                    .FirstOrDefault(found => found != null);
                if (view == null)
                    return;
                // 敵の行動・山札・狙い・2回発動は Unity の乱数、会心と状態異常は BattleOdds で振る。どちらも種から決める。
                UnityEngine.Random.InitState(seed);
                if (!FixedOdds)
                    BattleOdds.Seed(seed);
                SetUp(view, encounter, boss, members);
            }
            SceneManager.sceneLoaded += Prepare;
            try
            {
                yield return SceneManager.LoadSceneAsync(LabScenePath, LoadSceneMode.Additive);
            }
            finally
            {
                SceneManager.sceneLoaded -= Prepare;
            }
            var loaded = SceneManager.GetSceneByPath(LabScenePath);
            if (view == null)
                throw new InvalidOperationException("The battle lab has no battle screen.");
            var record = new BattleBalanceRecord
            {
                seed = seed,
                encounter = encounter,
                enemies = BattleBalanceEncounters.EnemiesOf(encounter),
            };
            try
            {
                yield return Play(view, record);
                done(record, view.HpSharesBySlot(AdventureVitals.Slots));
            }
            finally
            {
                if (loaded.IsValid() && loaded.isLoaded)
                    SceneManager.UnloadSceneAsync(loaded);
            }
            // 片付けを1フレーム待つ。次の戦闘のシーンと重ならないようにする。
            while (loaded.IsValid() && loaded.isLoaded)
                yield return null;
        }

        private void SetUp(
            BattleInspectView view,
            string encounter,
            bool boss,
            IReadOnlyList<BattlePartyMember> members
        )
        {
            var enemies = AdventureCatalog.Encounter(encounter);
            view.Vfx = null;
            view.Evolution = null;
            view.HoldEntrance = null;
            view.EndsWithOutcome = true;
            view.BossEncounter = boss;
            view.UseEncounter(enemies.Kinds, enemies.Level, enemies.HpScale, enemies.PowerScale);
            bool known = request.knowWeaknesses;
            view.UseRevealedWeaknesses((_, _) => known);
            view.UseParty(members);
            // 手札を隠す。味方が倒れたときに手札が崩れる演出は、実時間で進みながら Unity の乱数を使い、
            // 戦闘の乱数の順番を実時間で変えてしまうため。手札を隠すと自動のターン終了も止まるため、
            // 使えるカードがないときと手札を使い切ったときは、Play がターンを終える。
            view.Hand.gameObject.SetActive(false);
        }

        private IEnumerator Play(BattleInspectView view, BattleBalanceRecord record)
        {
            var watch = new Watch(view);
            record.enemyHp = view.Enemies.Where(enemy => enemy.Alive).Sum(enemy => enemy.Hp);
            record.enemyStats = string.Join(
                " ; ",
                view.Enemies.Where(enemy => enemy.Alive).Select(StatsOf)
            );
            record.partyStats = string.Join(
                " ; ",
                view.Allies.Where(ally => !ally.Absent).Select(StatsOf)
            );
            record.hpStart = watch.PartyShare();
            var refused = new HashSet<int>();
            int turn = int.MinValue;
            int played = 0;
            float progress = Time.realtimeSinceStartup;
            string outcome = null;
            while (view.Outcome == BattleOutcome.None)
            {
                if (watch.Step())
                    progress = Time.realtimeSinceStartup;
                if (view.BattleTurn > Math.Max(1, request.maxTurns))
                {
                    outcome = BattleBalanceRecord.Timeout;
                    break;
                }
                if (Time.realtimeSinceStartup - progress > StuckSeconds)
                {
                    Debug.LogWarning(
                        $"[balance] {record.encounter} stopped moving at turn {view.BattleTurn}."
                    );
                    outcome = BattleBalanceRecord.Timeout;
                    break;
                }
                if (!Ready(view))
                {
                    yield return null;
                    continue;
                }
                if (view.Turn != turn)
                {
                    turn = view.Turn;
                    refused.Clear();
                    played = 0;
                }
                // 手札を隠しているため、使い切ったら自分でターンを終える。始めの手札を配る前の空の手札では待つ。
                if (view.HandCards.Count == 0)
                {
                    if (played > 0)
                        view.EndTurn();
                    yield return null;
                    continue;
                }
                var move = policy.Choose(view, refused);
                string step = request.verbose ? Describe(view, move) : null;
                bool used = false;
                if (move.EndsTurn)
                    view.EndTurn();
                else if (Use(view, move))
                {
                    used = true;
                    played++;
                    record.cards++;
                    progress = Time.realtimeSinceStartup;
                }
                else
                    refused.Add(move.Card);
                if (step != null)
                    Debug.Log(
                        $"[balance] step {step}{(move.EndsTurn || used ? "" : " (did not play)")}"
                    );
                yield return null;
            }
            watch.Step();
            record.outcome =
                outcome
                ?? (
                    view.Outcome == BattleOutcome.Victory
                        ? BattleBalanceRecord.Victory
                        : BattleBalanceRecord.Defeat
                );
            record.turns = view.BattleTurn;
            record.dealt = watch.Dealt;
            record.enemyHealed = watch.EnemyHealed;
            record.taken = watch.Taken;
            record.partyHealed = watch.PartyHealed;
            record.hpLowest = watch.Lowest;
            record.hpEnd = watch.PartyShare();
            record.downAtEnd = view.Allies.Count(ally => !ally.Absent && ally.Down);
            record.fell = watch.Fell;
            record.dealtByTurn = watch.DealtByTurn.ToArray();
        }

        // 1手の記録：ターン・エネルギー・カード・対象・敵と味方のHP。
        private static string Describe(BattleInspectView view, BattleAutoMove move)
        {
            string card = "end turn";
            if (!move.EndsTurn)
            {
                var data = view.Deck[view.Cards[move.Card].DeckIndex];
                card =
                    $"{data.Name}({data.Owner}, cost {view.CostOf(view.Cards[move.Card])}) -> {move.Target}";
            }
            string enemies = string.Join(
                " ",
                view.Enemies.Where(enemy => enemy.Alive).Select(enemy => $"{enemy.Id}:{enemy.Hp}")
            );
            string allies = string.Join(
                " ",
                view.Allies.Where(ally => !ally.Absent).Select(ally => $"{ally.Name}:{ally.Hp}")
            );
            return $"T{view.BattleTurn} E{view.Energy}/{view.MaxEnergy} {card} | {enemies} | {allies}";
        }

        // 味方のターンで、カードを受け付けるとき。
        private static bool Ready(BattleInspectView view) =>
            view.enabled && !view.Dealing && !view.Acting && !view.EnemyTurn && !view.Evolving;

        /// <summary>
        /// Plays the card as a player does: a tap raises it, then a tap on the target plays it on
        /// that one, or a second tap on the card plays a card for a whole side on all of it.
        /// True when the card left the hand.
        /// </summary>
        private static bool Use(BattleInspectView view, BattleAutoMove move)
        {
            var card = view.Cards[move.Card];
            var rect = card.Body.rect;
            var point = BattleInspectView.ScreenPointOf(
                card.Body,
                new Vector2(rect.center.x, rect.yMax - 40f)
            );
            view.PressCard(move.Card, point);
            view.ReleaseCard(point);
            if (move.Target < 0)
            {
                view.PressCard(move.Card, point);
                view.ReleaseCard(point);
            }
            else
            {
                var area = card.TargetsEnemies
                    ? view.Enemies[move.Target].TargetArea
                    : view.Allies[move.Target].TargetArea;
                view.TapScreen(BattleInspectView.ScreenPointOf(area, area.rect.center));
            }
            return !card.InHand || view.Acting;
        }

        private static string StatsOf(BattleInspectAlly ally) =>
            $"{ally.Name} Lv{ally.Level} HP{ally.MaxHp} 物攻{ally.Stats.PhysicalAttack} 属攻{ally.Stats.MagicAttack}"
            + $" 物防{ally.Stats.PhysicalDefense} 属防{ally.Stats.MagicDefense} 速度{ally.Stats.Speed}"
            + $" 会心{ally.Stats.Critical} 2回{ally.TwiceChance}%";

        private static string StatsOf(BattleInspectEnemy enemy) =>
            $"{enemy.Id} Lv{enemy.Level} HP{enemy.MaxHp} 物防{enemy.PhysicalDefense}/属防{enemy.MagicDefense}"
            + $" {(enemy.Magic ? "属攻" : "物攻")}{(enemy.Magic ? enemy.MagicAttack : enemy.PhysicalAttack)}×{enemy.AttackScale:0.00}"
            + $" 速度{enemy.Speed}";

        /// <summary>Follows the HP of both sides frame by frame, counting what each lost and got back.</summary>
        private sealed class Watch
        {
            private readonly BattleInspectView view;
            private readonly int[] enemyHp;
            private readonly int[] allyHp;
            private readonly bool[] fell;

            public Watch(BattleInspectView view)
            {
                this.view = view;
                enemyHp = view.Enemies.Select(enemy => enemy.Hp).ToArray();
                allyHp = view.Allies.Select(ally => ally.Hp).ToArray();
                fell = new bool[view.Allies.Length];
                Lowest = PartyShare();
            }

            public int Dealt { get; private set; }
            public int EnemyHealed { get; private set; }
            public int Taken { get; private set; }
            public int PartyHealed { get; private set; }
            public float Lowest { get; private set; }
            public int Fell => fell.Count(value => value);
            public List<int> DealtByTurn { get; } = new();

            /// <summary>Counts the changes since the last frame; true when anything changed.</summary>
            public bool Step()
            {
                bool changed = false;
                int turn = Math.Max(1, view.BattleTurn);
                while (DealtByTurn.Count < turn)
                    DealtByTurn.Add(0);
                for (int i = 0; i < enemyHp.Length; i++)
                {
                    int now = view.Enemies[i].Hp;
                    int lost = enemyHp[i] - now;
                    if (lost > 0)
                    {
                        Dealt += lost;
                        DealtByTurn[turn - 1] += lost;
                    }
                    else if (lost < 0)
                        EnemyHealed -= lost;
                    changed |= lost != 0;
                    enemyHp[i] = now;
                }
                for (int i = 0; i < allyHp.Length; i++)
                {
                    var ally = view.Allies[i];
                    int lost = allyHp[i] - ally.Hp;
                    if (lost > 0)
                        Taken += lost;
                    else if (lost < 0)
                        PartyHealed -= lost;
                    changed |= lost != 0;
                    // 戦闘の中で倒れた味方。倒れたまま持ち越して始めた味方は数えない。
                    if (!ally.Absent && allyHp[i] > 0 && ally.Hp <= 0)
                        fell[i] = true;
                    allyHp[i] = ally.Hp;
                }
                Lowest = Mathf.Min(Lowest, PartyShare());
                return changed;
            }

            public float PartyShare()
            {
                int hp = 0;
                int max = 0;
                foreach (var ally in view.Allies)
                {
                    if (ally.Absent)
                        continue;
                    hp += Mathf.Max(0, ally.Hp);
                    max += ally.MaxHp;
                }
                return max > 0 ? hp / (float)max : 0f;
            }
        }
    }
}
