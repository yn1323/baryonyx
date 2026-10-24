using System.Linq;
using Baryonyx.Adventure;
using Baryonyx.Adventure.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Baryonyx.Tests.EditMode
{
    // 探索画面のPrefabは、停止中も見本の道を、地面・光の道・マス・パーティ・文字で見せる。
    public sealed class ExplorationScreenTests
    {
        [Test]
        public void TheScreenShowsTheSampleRouteOutsidePlayMode()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                AdventureAssets.ExplorationPrefabPath
            );
            Assert.That(prefab, Is.Not.Null);
            var view = prefab.GetComponent<ExplorationView>();

            // 行き先ごとの地面を、繰り返して敷ける画像で持つ。
            Assert.That(view.Grounds, Is.Not.Empty);
            foreach (var ground in view.Grounds)
            {
                Assert.That(ground.Texture, Is.Not.Null, ground.DestinationId);
                Assert.That(ground.Texture.wrapMode, Is.EqualTo(TextureWrapMode.Repeat));
            }
            Assert.That(
                view.Grounds.Select(ground => ground.DestinationId),
                Does.Contain(AdventureCatalog.ForestRuins)
            );

            Assert.That(
                view.LocationText,
                Is.EqualTo(
                    AdventureCatalog.DestinationName(AdventureCatalog.ForestRuins) + " 第1層"
                )
            );
            Assert.That(view.PromptText, Is.Empty);

            // 型は隠し、入口から行けるマスの大きい札と、マスどうしを結ぶ道を並べておく。
            Assert.That(view.MarkerTemplate.gameObject.activeSelf, Is.False);
            Assert.That(view.RoadTemplate.gameObject.activeSelf, Is.False);
            var exits = view
                .Markers.GetComponentsInChildren<ExplorationMapMarker>()
                .Where(marker => marker.Button.interactable)
                .ToArray();
            Assert.That(exits, Is.Not.Empty);
            Assert.That(
                exits.Select(marker => marker.Ring.rectTransform.sizeDelta.x),
                Has.All.EqualTo(ExplorationView.LargeRing)
            );
            Assert.That(
                view.Roads.Cast<Transform>().Count(child => child.gameObject.activeSelf),
                Is.GreaterThan(10)
            );

            // 凡例は「？」で開き、停止中は閉じておく。マスの種類を11示す。
            Assert.That(view.LegendButton, Is.Not.Null);
            Assert.That(view.Legend.gameObject.activeSelf, Is.False);
            Assert.That(
                view.Legend.GetComponentsInChildren<TMPro.TMP_Text>(true).Length,
                Is.EqualTo(11)
            );
            // どのマスの種類にもアイコンがある（入口を除く）。
            foreach (AdventureRoomKind kind in System.Enum.GetValues(typeof(AdventureRoomKind)))
                if (kind != AdventureRoomKind.Start)
                    Assert.That(view.KindIcon(kind), Is.Not.Null, kind.ToString());

            // 休憩の焚き火は停止中は閉じておき、夜の野営地の絵と、押せる3つの物（テント・切り株と薪・本）、
            // 鍛える4人の台座、研鑽の戦闘のカード（1人2枚）を持つ。
            var campfire = view.Campfire;
            Assert.That(campfire, Is.Not.Null);
            Assert.That(campfire.IsOpen, Is.False);
            Assert.That(campfire.Scene.texture, Is.Not.Null);
            Assert.That(
                new[] { campfire.RestButton, campfire.TrainButton, campfire.StudyButton },
                Is.All.Not.Null
            );
            Assert.That(campfire.Party, Has.Length.EqualTo(4));
            Assert.That(campfire.TrainSlots, Has.Length.EqualTo(4));
            Assert.That(
                campfire.StudyFaces,
                Has.Length.EqualTo(4 * ExplorationCampfire.CardsPerMember).And.All.Not.Null
            );
            Assert.That(campfire.CardFrames, Is.All.Not.Null);

            // 入口は地図の左にあり、地図は画面より広く、左右に動かして先を見る。
            // （Prefabには印の部屋のIDが残らないため、入口の台は位置で探す。）
            var entrance = view
                .Markers.GetComponentsInChildren<ExplorationMapMarker>()
                .FirstOrDefault(marker =>
                    Mathf.Approximately(marker.Body.anchoredPosition.x, ExplorationView.EdgeMargin)
                );
            Assert.That(entrance, Is.Not.Null);
            Assert.That(view.Content.sizeDelta.x, Is.GreaterThan(1920f));

            // パーティは、木の駒1つで入口の台に立つ（4人の絵は出さない）。
            Assert.That(view.Party, Has.Length.EqualTo(1));
            var pawn = view.Party[0].GetComponentInChildren<UnityEngine.UI.RawImage>(true);
            Assert.That(pawn.texture.name, Is.EqualTo("MapPawn"));
            Assert.That(view.Formation, Has.Length.EqualTo(view.Party.Length));
            for (int i = 0; i < view.Party.Length; i++)
                Assert.That(
                    Vector2.Distance(
                        view.Party[i].anchoredPosition - view.Formation[i],
                        entrance.Body.anchoredPosition
                    ),
                    Is.LessThanOrEqualTo(1f)
                );
        }
    }
}
