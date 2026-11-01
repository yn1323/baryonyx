using System.Collections;
using System.Collections.Generic;
using Baryonyx.Combat;
using Baryonyx.WorldMap;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Baryonyx.Tests.PlayMode
{
    // 地図エディター。マスを押すと選び、出来事を置き換え、戦闘の敵・Lv・強さを設定し、出来事を試せる。
    // 地図を選び替え、出来事のマスの一覧から選べる。
    public sealed class WorldEventLabSceneTests
    {
        private const string ScenePath = SceneTests.ScenesFolder + "/Debug/WorldEventLab.unity";

        // 世界地図の、始まりから7マス先の戦闘のマス。
        private const string BattleSquare = "world-38";

        private bool charging;
        private System.Func<string, string, string, string, WorldEventLab.Saved> save;
        private System.Func<string, string, string, string, WorldEventLab.Saved> saveSetting;
        private readonly List<string> saved = new();

        [SetUp]
        public void Prepare()
        {
            // 付け替えと設定は配置のJSONへ書くため、テストの間は書かない代役にする。
            save = WorldEventLab.Save;
            saveSetting = WorldEventLab.SaveSetting;
            saved.Clear();
            WorldEventLab.Save = (map, square, name, form) =>
            {
                saved.Add($"{map} {square} {name} {form}".TrimEnd());
                return name == "battle"
                    ? new WorldEventLab.Saved("ほかの出来事と近すぎます", 0)
                    : new WorldEventLab.Saved(null, 250);
            };
            WorldEventLab.SaveSetting = (map, square, field, value) =>
            {
                saved.Add($"{map} {square} {field}={value}");
                return new WorldEventLab.Saved(null, 0);
            };
            charging = WorldAtlasSession.Charging;
            WorldAtlasSession.Reset();
            WorldAtlasNavigator.Forget();
        }

        [UnityTearDown]
        public IEnumerator Clean()
        {
            yield return SceneTests.UnloadAll(nameof(WorldEventLabSceneTests));
            WorldEventLab.Save = save;
            WorldEventLab.SaveSetting = saveSetting;
            WorldAtlasSession.Reset();
            WorldAtlasSession.Charging = charging;
            WorldAtlasNavigator.Forget();
        }

        [UnityTest]
        public IEnumerator SquaresAreChosenChangedSetAndTried()
        {
            WorldEventLab lab = null;
            yield return SceneTests.Load<WorldEventLab>(
                ScenePath,
                found => found.Navigator != null && found.Navigator.Current != null,
                found => lab = found
            );
            foreach (var button in lab.Buttons)
                SceneTests.AssertTouchSize(button.transform);
            foreach (
                var button in new[]
                {
                    lab.NoneButton,
                    lab.MapButton,
                    lab.ListButton,
                    lab.HelpButton,
                    lab.TryButton,
                }
            )
                SceneTests.AssertTouchSize(button.transform);

            // マスを押すと選ぶ。歩かない。
            var map = lab.Navigator.Current;
            int stood = map.PartyAt;
            int empty = -1;
            for (int i = 0; i < map.Graph.Nodes.Count && empty < 0; i++)
                if (
                    i != stood
                    && !map.TryEventAt(i, out _)
                    && map.Graph.Nodes[i].Kind == WorldNodeKind.Empty
                )
                    empty = i;
            Assert.That(map.Tap(empty), Is.False);
            Assert.That(lab.Chosen, Is.EqualTo(empty));
            Assert.That(map.PartyAt, Is.EqualTo(stood), "Choosing a square does not walk.");
            yield return SceneTests.WaitUntil(() => lab.InspectorGroup.interactable);

            // 出来事を置き換える。保存が断ったときは変わらない。
            Assert.That(lab.Assign(WorldEventKind.Pickup, 0), Is.True);
            Assert.That(
                map.TryEventAt(empty, out var put) && put == WorldEventKind.Pickup,
                Is.True
            );
            Assert.That(lab.Assign(WorldEventKind.Battle, 0), Is.False);
            Assert.That(map.TryEventAt(empty, out put) && put == WorldEventKind.Pickup, Is.True);
            lab.NoneButton.onClick.Invoke();
            Assert.That(map.TryEventAt(empty, out _), Is.False);
            string id = map.Graph.Nodes[empty].Id;
            Assert.That(
                saved,
                Is.EqualTo(new[] { $"world {id} pickup", $"world {id} battle", $"world {id} none" })
            );

            // 戦闘のマスは、敵を言葉で、Lvと強さを設定でき、戦闘はその設定から敵を引く。
            saved.Clear();
            int battle = map.Graph.IndexOf(BattleSquare);
            Assert.That(lab.Choose(battle), Is.True);
            Assert.That(lab.BattleGroup.interactable, Is.True);
            Assert.That(lab.SetEnemies("いない敵"), Is.False);
            string slime = CombatEnemies.Find("moss-slime").Name;
            Assert.That(lab.SetEnemies($"{slime}、ランダム"), Is.True);
            Assert.That(lab.SetLevel("12"), Is.True);
            Assert.That(lab.SetLevel("0"), Is.False);
            lab.StrengthButton.onClick.Invoke();
            Assert.That(
                lab.StrengthLabel.text,
                Is.EqualTo(WorldEventLab.StrengthText(WorldBattleStrength.Elite))
            );
            Assert.That(
                saved,
                Is.EqualTo(
                    new[]
                    {
                        $"world {BattleSquare} enemies={slime}、ランダム",
                        $"world {BattleSquare} enemyLevel=12",
                        $"world {BattleSquare} strength=elite",
                    }
                )
            );
            var drawn = lab.Navigator.BattleAt(BattleSquare, 3);
            Assert.That(drawn.Encounter.Enemies.Level, Is.EqualTo(12));
            Assert.That(drawn.Encounter.Enemies.Kinds, Does.Contain("moss-slime"));

            // 試す：選んだマスの出来事の窓を開く。拾い物は自分で閉じる。
            int pickup = -1;
            for (int i = 0; i < map.Graph.Nodes.Count && pickup < 0; i++)
                if (map.TryEventAt(i, out var kind) && kind == WorldEventKind.Pickup)
                    pickup = i;
            lab.Choose(pickup);
            Assert.That(lab.Try(), Is.True);
            var window = map.EventWindow;
            Assert.That(window.IsShown, Is.True);
            Assert.That(window.Panel.Kind, Is.EqualTo(WorldEventKind.Pickup));
            Assert.That(
                lab.Group.interactable,
                Is.False,
                "The buttons hide while a window is open."
            );
            window.FinishOpening();
            yield return SceneTests.WaitUntil(() => !window.IsShown, seconds: 5f);

            // マスの一覧：出来事を置いたマスが始まりから近い順に並び、押すとそのマスを選ぶ。
            lab.ListButton.onClick.Invoke();
            Assert.That(lab.Listing, Is.True);
            int placed = 0;
            for (int i = 0; i < map.Graph.Nodes.Count; i++)
                if (map.TryEventAt(i, out _))
                    placed++;
            Assert.That(lab.ListedSquares, Has.Count.EqualTo(placed));
            Assert.That(map.TryEventAt(lab.ListedSquares[0], out var nearest), Is.True);
            Assert.That(nearest, Is.EqualTo(WorldEventKind.Pickup));
            Assert.That(lab.ShowSquare(placed - 1), Is.True);
            Assert.That(lab.Chosen, Is.EqualTo(lab.ListedSquares[placed - 1]));

            // 「得意な仲間」は、次に開く窓の助けを切り替える。
            lab.HelpButton.onClick.Invoke();
            Assert.That(lab.Helped, Is.False);
            Assert.That(lab.HelpLabel.text, Is.EqualTo(WorldEventLab.HelpText(false)));

            // 地図の一覧から、地域のエリアマップへ移る。
            lab.MapButton.onClick.Invoke();
            Assert.That(lab.ListingMaps, Is.True);
            Assert.That(lab.MapKeys, Does.Contain("marina"));
            Assert.That(lab.OpenMap("marina"), Is.True);
            Assert.That(lab.Navigator.CurrentKey, Is.EqualTo("marina"));
            Assert.That(lab.Chosen, Is.EqualTo(-1));
            yield return null;
            Assert.That(lab.MapLabel.text, Does.Contain(lab.Navigator.Current.Atlas.name));
        }
    }
}
