using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Baryonyx.WorldMap
{
    /// <summary>
    /// Runs the nested world map prototype
    /// (doc/plans/2026-10-27-world-map-rune-squares.md#2026-10-27入れ子の地図の試作):
    /// the world map shows a square for each region, with squares along the roads between them
    /// that open one by one. Stopping on a region's square dives into that region's area map at
    /// its first town; stopping on an area map's gate climbs back to the world map, on that
    /// region's square. Each map is loaded from Resources only while it is shown, since its
    /// background is large, and each map's cleared squares are kept only while the app runs.
    /// </summary>
    public sealed class WorldAtlasNavigator : MonoBehaviour
    {
        // 地図のPrefabを読む Resources のフォルダー。地図ごとに <キー>.prefab を置く。
        public const string ResourceFolder = "WorldAtlas/";

        // 切り替えの片道の秒数と、近づく・離れる倍率。
        public const float HalfSeconds = 0.5f;
        private const float DiveZoom = 2.4f;
        private const float ClimbZoom = 0.55f;

        private readonly Dictionary<string, HashSet<string>> cleared = new();
        private WorldMapView current;
        private string currentKey;

        public WorldMapView Current => current;
        public string CurrentKey => currentKey;

        // 地図を切り替える演出の途中。テストが待つ。
        public bool Switching { get; private set; }

        private void Start()
        {
            if (current == null)
                Open(WorldAtlasLayout.WorldKey, WorldAtlasLayout.RegionPrefix + "verde");
        }

        /// <summary>
        /// Opens the map <paramref name="key"/> at once with the party on <paramref name="at"/>
        /// (the first town when null).
        /// </summary>
        public bool Open(string key, string at)
        {
            var prefab = Resources.Load<WorldMapView>(ResourceFolder + key);
            if (prefab == null)
            {
                Debug.LogWarning("入れ子の地図がありません: " + key, this);
                return false;
            }
            if (current != null)
            {
                Keep();
                Destroy(current.gameObject);
            }
            current = Instantiate(prefab, transform);
            currentKey = key;
            current.Arrived += Arrive;
            current.BackPressed += Back;
            // 世界地図からは、もどるで出る先がない。
            if (current.Back != null)
                current.Back.gameObject.SetActive(key != WorldAtlasLayout.WorldKey);
            at ??= EntryOf(current);
            if (!cleared.TryGetValue(key, out var squares))
                cleared[key] = squares = new HashSet<string>();
            squares.Add(at);
            current.StartAt(at, squares);
            // 前の地図の大きな背景を、メモリーから外す。
            Resources.UnloadUnusedAssets();
            return true;
        }

        private void Keep()
        {
            if (current != null && currentKey != null)
                cleared[currentKey] = new HashSet<string>(current.Cleared);
        }

        private void Arrive(string id)
        {
            if (Switching)
                return;
            if (currentKey == WorldAtlasLayout.WorldKey)
            {
                if (id.StartsWith(WorldAtlasLayout.RegionPrefix, StringComparison.Ordinal))
                    StartCoroutine(
                        Switch(id.Substring(WorldAtlasLayout.RegionPrefix.Length), null, dive: true)
                    );
            }
            else if (id.StartsWith(WorldAtlasLayout.GatePrefix, StringComparison.Ordinal))
            {
                Back();
            }
        }

        private void Back()
        {
            if (!Switching && currentKey != WorldAtlasLayout.WorldKey)
                StartCoroutine(
                    Switch(
                        WorldAtlasLayout.WorldKey,
                        WorldAtlasLayout.RegionPrefix + currentKey,
                        dive: false
                    )
                );
        }

        // 地域へ入るときは、駒へ近づきながら暗くし、地域の地図は少し引いた所から落ち着かせる。
        // 世界地図へ戻るときは、引きながら暗くし、世界地図は寄った所から引いていく。
        private IEnumerator Switch(string key, string at, bool dive)
        {
            Switching = true;
            var leaving = Fader(current);
            leaving.blocksRaycasts = false;
            float from = current.Zoom;
            float to = from * (dive ? DiveZoom : ClimbZoom);
            for (float t = 0f; t < HalfSeconds; t += Time.unscaledDeltaTime)
            {
                float x = t / HalfSeconds;
                current.FrameAt(Mathf.Lerp(from, to, x * x));
                leaving.alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 1f, x));
                yield return null;
            }
            if (!Open(key, at))
            {
                leaving.alpha = 1f;
                leaving.blocksRaycasts = true;
                Switching = false;
                yield break;
            }
            var arriving = Fader(current);
            arriving.blocksRaycasts = false;
            float settle = current.Zoom;
            float start = settle * (dive ? 1f / ClimbZoom : DiveZoom * 0.75f);
            for (float t = 0f; t < HalfSeconds; t += Time.unscaledDeltaTime)
            {
                float x = t / HalfSeconds;
                float eased = 1f - (1f - x) * (1f - x);
                current.FrameAt(Mathf.Lerp(start, settle, eased));
                arriving.alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.6f, x));
                yield return null;
            }
            current.FrameAt(settle);
            arriving.alpha = 1f;
            arriving.blocksRaycasts = true;
            Switching = false;
        }

        private static CanvasGroup Fader(WorldMapView view) =>
            view.TryGetComponent<CanvasGroup>(out var group)
                ? group
                : view.gameObject.AddComponent<CanvasGroup>();

        // 世界地図から地域に入ったときに立つマス：その地域の最初の町（なければ最初のマス）。
        private static string EntryOf(WorldMapView view)
        {
            var layout = WorldAtlasLayout.Parse(view.Layout.text);
            foreach (var node in layout.nodes)
                if (node.kind == "town")
                    return node.id;
            return layout.nodes[0].id;
        }
    }
}
