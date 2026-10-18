using System.Linq;
using Baryonyx.Adventure;
using Baryonyx.Adventure.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Baryonyx.Tests.EditMode
{
    // 探索画面のPrefabは、停止中も見本の道の地図・印・パーティ・文字を見せる。
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

            // 地図の絵は停止中だけ見せ、ビルドには入れない。実行中に描く絵は空のまま保存する。
            Assert.That(view.MapPreview, Is.Not.Null);
            Assert.That(view.MapPreview.gameObject.activeSelf, Is.True);
            Assert.That(view.MapPreview.CompareTag("EditorOnly"), Is.True);
            Assert.That(view.MapPreview.texture, Is.Not.Null);
            Assert.That(
                AssetDatabase.GetAssetPath(view.MapPreview.texture),
                Is.EqualTo(AdventureAssets.MapPreviewPath)
            );
            Assert.That(view.MapPreview.texture.width, Is.EqualTo(ExplorationMapProjection.Width));
            Assert.That(
                view.MapPreview.texture.height,
                Is.EqualTo(ExplorationMapProjection.Height)
            );
            Assert.That(view.MapImage.texture, Is.Null);

            Assert.That(
                view.LocationText,
                Is.EqualTo(
                    AdventureCatalog.DestinationName(AdventureCatalog.ForestRuins) + " 第1層"
                )
            );
            Assert.That(view.PromptText, Is.EqualTo(ExplorationRoom.ChoosePrompt));

            // 型は隠し、入口から行ける部屋の大きい札を並べておく。右下の凡例は部屋の種類を4つ示す。
            Assert.That(view.MarkerTemplate.gameObject.activeSelf, Is.False);
            var exits = view
                .Markers.GetComponentsInChildren<ExplorationMapMarker>()
                .Where(marker => marker.Button.interactable)
                .ToArray();
            Assert.That(exits, Is.Not.Empty);
            Assert.That(
                exits.Select(marker => marker.Ring.rectTransform.sizeDelta.x),
                Has.All.EqualTo(ExplorationView.LargeRing)
            );
            Assert.That(view.Legend, Is.Not.Null);
            Assert.That(view.Legend.childCount, Is.EqualTo(5));
            Assert.That(view.Floors.Find("FloorTemplate").gameObject.activeSelf, Is.False);
            Assert.That(
                view.Floors.Cast<Transform>()
                    .Where(child => child.gameObject.activeSelf)
                    .Select(child => child.name),
                Does.Contain("Floor0")
            );

            // パーティは入口の空き地に、隊列のまま立つ。
            Assert.That(view.Formation, Has.Length.EqualTo(view.Party.Length));
            var shift = view.Party[0].anchoredPosition - view.Formation[0];
            Assert.That(shift, Is.Not.EqualTo(Vector2.zero));
            for (int i = 1; i < view.Party.Length; i++)
                Assert.That(
                    Vector2.Distance(view.Party[i].anchoredPosition - view.Formation[i], shift),
                    Is.LessThanOrEqualTo(ExplorationMapProjection.Dot * 1.5f)
                );
        }
    }
}
