using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Baryonyx.Showcase
{
    public sealed class ShowcaseRuntimeView : MonoBehaviour
    {
        private static readonly ShowcaseCategory[] Categories =
        {
            ShowcaseCategory.All,
            ShowcaseCategory.Image,
            ShowcaseCategory.Character,
            ShowcaseCategory.Environment,
            ShowcaseCategory.Weapon,
            ShowcaseCategory.Item,
            ShowcaseCategory.Vfx,
            ShowcaseCategory.Animation,
            ShowcaseCategory.Audio,
            ShowcaseCategory.Ui,
            ShowcaseCategory.Scene,
            ShowcaseCategory.Material,
            ShowcaseCategory.Data,
            ShowcaseCategory.Other,
        };

        public ShowcaseCatalog Catalog;

        private readonly List<Button> entryButtons = new();
        private readonly List<ShowcaseEntry> visibleEntries = new();
        private ShowcaseCategory selectedCategory;
        private Transform entryContent;
        private Transform previewContent;
        private TextMeshProUGUI entryCount;
        private TextMeshProUGUI title;
        private TextMeshProUGUI description;
        private TextMeshProUGUI typeLabel;
        private Image imagePreview;
        private RawImage rawImagePreview;
        private RenderTexture renderTexture;
        private GameObject previewInstance;
        private AudioSource audioSource;
        private Button actionButton;
        private TextMeshProUGUI actionLabel;

        public void Build()
        {
            EnsureEventSystem();
            var canvas = CreateCanvas();
            CreateLayout(canvas.transform);
            SelectCategory(ShowcaseCategory.All);
        }

        private void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null)
                return;

            var events = new GameObject("EventSystem");
            events.AddComponent<EventSystem>();
            var inputModule = events.AddComponent<InputSystemUIInputModule>();
            inputModule.AssignDefaultActions();
        }

        private Canvas CreateCanvas()
        {
            var canvasObject = new GameObject("ShowcaseCanvas");
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            var background = canvasObject.AddComponent<Image>();
            background.color = new Color(0.035f, 0.047f, 0.075f, 1f);
            Stretch(background.rectTransform);
            return canvas;
        }

        private void CreateLayout(Transform root)
        {
            var header = CreatePanel(root, "Header", new Color(0.07f, 0.09f, 0.14f, 1f));
            SetTop(header.rectTransform, 0, 72);
            var headerTitle = CreateText(header.transform, "Title", "Baryonyx Showcase", 30, Color.white);
            Stretch(headerTitle.rectTransform, 32, 0, 0, 0, true);
            headerTitle.alignment = TextAlignmentOptions.MidlineLeft;

            var body = CreatePanel(root, "Body", Color.clear);
            Stretch(body.rectTransform, 0, 0, 72, 0);

            var categoryPanel = CreatePanel(body.transform, "Categories", new Color(0.055f, 0.07f, 0.11f, 1f));
            SetLeft(categoryPanel.rectTransform, 0, 245);
            var categoryLayout = categoryPanel.gameObject.AddComponent<VerticalLayoutGroup>();
            categoryLayout.padding = new RectOffset(16, 16, 16, 16);
            categoryLayout.spacing = 6;
            categoryLayout.childControlHeight = true;
            categoryLayout.childForceExpandHeight = false;

            var categoryTitle = CreateText(categoryPanel.transform, "CategoryTitle", "カテゴリ", 20, Color.white);
            categoryTitle.gameObject.AddComponent<LayoutElement>().preferredHeight = 34;
            foreach (var category in Categories)
            {
                var button = CreateButton(categoryPanel.transform, CategoryLabel(category), 34);
                button.onClick.AddListener(() => SelectCategory(category));
            }

            var listPanel = CreatePanel(body.transform, "Entries", new Color(0.045f, 0.06f, 0.095f, 1f));
            SetLeft(listPanel.rectTransform, 245, 390);
            var listHeader = CreateText(listPanel.transform, "ListHeader", "登録アセット", 20, Color.white);
            SetTop(listHeader.rectTransform, 16, 50);
            entryCount = CreateText(listPanel.transform, "EntryCount", "", 14, new Color(0.65f, 0.7f, 0.8f));
            SetTop(entryCount.rectTransform, 16, 24);
            var scroll = CreateScrollView(listPanel.transform, "EntryScroll");
            Stretch(scroll.GetComponent<RectTransform>(), 16, 16, 88, 16);
            entryContent = scroll.content;

            var previewPanel = CreatePanel(body.transform, "Preview", new Color(0.075f, 0.09f, 0.135f, 1f));
            Stretch(previewPanel.rectTransform, 635, 16, 0, 16);
            var previewHeader = CreateText(previewPanel.transform, "PreviewHeader", "プレビュー", 20, Color.white);
            SetTop(previewHeader.rectTransform, 24, 36);
            previewContent = new GameObject("PreviewContent", typeof(RectTransform)).transform;
            previewContent.SetParent(previewPanel.transform, false);
            Stretch((RectTransform)previewContent, 24, 24, 72, 112);
            imagePreview = CreateImage(previewContent, "ImagePreview", Color.white);
            Stretch(imagePreview.rectTransform, 0, 0, 0, 0);
            imagePreview.preserveAspect = true;
            rawImagePreview = CreateRawImage(previewContent, "RawImagePreview");
            Stretch(rawImagePreview.rectTransform, 0, 0, 0, 0);
            rawImagePreview.gameObject.SetActive(false);

            title = CreateText(previewPanel.transform, "SelectedTitle", "アセットを選択してください", 26, Color.white);
            SetBottom(title.rectTransform, 160, 56);
            description = CreateText(previewPanel.transform, "SelectedDescription", "", 15, new Color(0.75f, 0.78f, 0.86f));
            SetBottom(description.rectTransform, 104, 48);
            typeLabel = CreateText(previewPanel.transform, "SelectedType", "", 14, new Color(0.55f, 0.65f, 0.8f));
            SetBottom(typeLabel.rectTransform, 72, 24);
            actionButton = CreateButton(previewPanel.transform, "", 40);
            SetBottom(actionButton.GetComponent<RectTransform>(), 24, 40);
            actionLabel = actionButton.GetComponentInChildren<TextMeshProUGUI>();
            actionButton.gameObject.SetActive(false);
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }

        private void SelectCategory(ShowcaseCategory category)
        {
            selectedCategory = category;
            visibleEntries.Clear();
            if (Catalog != null && Catalog.Entries != null)
            {
                visibleEntries.AddRange(
                    Catalog.Entries.Where(entry => entry != null && entry.Enabled)
                        .Where(entry => category == ShowcaseCategory.All || entry.Category == category)
                        .OrderBy(entry => entry.Label)
                );
            }

            if (previewContent != null)
            {
                ClearPreview();
                title.text = "アセットを選択してください";
                description.text = string.Empty;
                typeLabel.text = string.Empty;
            }

            foreach (var button in entryButtons)
                Destroy(button.gameObject);
            entryButtons.Clear();
            if (entryContent == null)
                return;

            var layout = entryContent.GetComponent<VerticalLayoutGroup>();
            if (layout == null)
            {
                layout = entryContent.gameObject.AddComponent<VerticalLayoutGroup>();
                layout.padding = new RectOffset(8, 8, 8, 8);
                layout.spacing = 5;
                layout.childControlHeight = true;
                layout.childForceExpandHeight = false;
                var fitter = entryContent.gameObject.AddComponent<ContentSizeFitter>();
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }

            foreach (var entry in visibleEntries)
            {
                var button = CreateButton(entryContent, entry.Label, 48);
                button.onClick.AddListener(() => SelectEntry(entry));
                entryButtons.Add(button);
            }

            entryCount.text = $"{visibleEntries.Count}件  /  {CategoryLabel(selectedCategory)}";
            if (visibleEntries.Count == 0)
            {
                ShowEmptyPreview("このカテゴリには登録されたアセットがありません。\n生成後にカタログが自動更新されます。");
            }
        }

        private void SelectEntry(ShowcaseEntry entry)
        {
            ClearPreview();
            title.text = entry.Label;
            description.text = entry.Description ?? string.Empty;
            typeLabel.text = $"{CategoryLabel(entry.Category)}  |  {AssetLabel(entry)}";

            if (entry.Asset is Sprite sprite)
            {
                imagePreview.sprite = sprite;
                imagePreview.color = Color.white;
                imagePreview.gameObject.SetActive(true);
            }
            else if (entry.Asset is Texture2D texture)
            {
                rawImagePreview.texture = texture;
                rawImagePreview.color = Color.white;
                rawImagePreview.gameObject.SetActive(true);
            }
            else if (entry.Asset is GameObject || entry.PreviewPrefab != null)
            {
                var prefab = entry.PreviewPrefab != null ? entry.PreviewPrefab : entry.Asset as GameObject;
                ShowPrefab(prefab);
            }
            else if (entry.Asset is Material material)
            {
                ShowMaterial(material);
            }
            else if (entry.Asset is AudioClip clip)
            {
                ShowEmptyPreview("音声アセット\n再生ボタンで確認できます。");
                ConfigureAudioAction(clip);
            }
            else if (entry.Category == ShowcaseCategory.Scene && !string.IsNullOrWhiteSpace(entry.ScenePath))
            {
                ShowEmptyPreview("シーンを読み込んで確認できます。\n展示室に戻るにはシーンを再度開いてください。");
                ConfigureSceneAction(entry.ScenePath);
            }
            else
            {
                ShowEmptyPreview("このアセットは情報表示のみ対応しています。\nプレビューPrefabを登録すると実物を表示できます。");
            }

            if (entry.PreviewAnimation != null && previewInstance != null)
                PlayAnimation(entry.PreviewAnimation, entry.AnimationStateName);
        }

        private void ShowMaterial(Material material)
        {
            var sample = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sample.name = "MaterialSample";
            sample.GetComponent<Renderer>().sharedMaterial = material;
            ShowPrefab(sample);
            Destroy(sample);
        }

        private void ShowPrefab(GameObject prefab)
        {
            if (prefab == null)
            {
                ShowEmptyPreview("プレビューPrefabがありません。");
                return;
            }

            rawImagePreview.gameObject.SetActive(true);
            renderTexture = new RenderTexture(768, 512, 24);
            rawImagePreview.texture = renderTexture;
            var cameraObject = new GameObject("PreviewCamera");
            cameraObject.transform.SetParent(previewContent, false);
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.1f, 0.12f, 0.17f, 1f);
            camera.targetTexture = renderTexture;
            camera.transform.position = new Vector3(0, 0.5f, -6f);
            camera.transform.LookAt(Vector3.zero);
            previewInstance = Instantiate(prefab, Vector3.zero, Quaternion.identity);
            previewInstance.name = "PreviewInstance";
            SetPreviewLayer(previewInstance, camera.gameObject.layer);
            ConfigureCanvasPreview(previewInstance, camera);
            var bounds = CalculateBounds(previewInstance);
            camera.transform.position = bounds.center + new Vector3(0, bounds.extents.y * 0.15f, -Mathf.Max(3f, bounds.extents.magnitude * 2.5f));
            camera.transform.LookAt(bounds.center);
            var lightObject = new GameObject("PreviewLight");
            lightObject.transform.SetParent(previewContent, false);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.transform.rotation = Quaternion.Euler(35, -30, 0);
        }

        private static void ConfigureCanvasPreview(GameObject target, Camera camera)
        {
            foreach (var canvas in target.GetComponentsInChildren<Canvas>(true))
            {
                if (canvas.renderMode == RenderMode.ScreenSpaceOverlay
                    || canvas.renderMode == RenderMode.ScreenSpaceCamera)
                {
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = camera;
                    canvas.planeDistance = 2f;
                }
            }
        }

        private void PlayAnimation(AnimationClip clip, string stateName)
        {
            var animator = previewInstance != null ? previewInstance.GetComponentInChildren<Animator>() : null;
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                var state = string.IsNullOrWhiteSpace(stateName) ? clip.name : stateName;
                if (animator.HasState(0, Animator.StringToHash(state)))
                    animator.Play(state, 0, 0f);
            }
            var animation = previewInstance != null ? previewInstance.GetComponentInChildren<Animation>() : null;
            if (animation != null)
            {
                animation.AddClip(clip, clip.name);
                animation.Play(clip.name);
            }
        }

        private void ConfigureAudioAction(AudioClip clip)
        {
            actionLabel.text = "▶ 再生 / 停止";
            actionButton.gameObject.SetActive(true);
            actionButton.onClick.AddListener(() =>
            {
                if (audioSource.isPlaying)
                    audioSource.Stop();
                else
                    audioSource.PlayOneShot(clip);
            });
        }

        private void ConfigureSceneAction(string path)
        {
            actionLabel.text = "シーンを開く";
            actionButton.gameObject.SetActive(true);
            actionButton.onClick.AddListener(() => SceneManager.LoadSceneAsync(path));
        }

        private void ShowEmptyPreview(string message)
        {
            imagePreview.gameObject.SetActive(false);
            rawImagePreview.gameObject.SetActive(false);
            var label = CreateText(previewContent, "EmptyPreview", message, 18, new Color(0.6f, 0.65f, 0.75f));
            label.alignment = TextAlignmentOptions.Center;
            Stretch(label.rectTransform, 48, 48, 48, 48, true);
        }

        private void ClearPreview()
        {
            if (audioSource != null)
                audioSource.Stop();
            if (previewInstance != null)
                Destroy(previewInstance);
            previewInstance = null;
            if (renderTexture != null)
            {
                renderTexture.Release();
                Destroy(renderTexture);
            }
            renderTexture = null;
            for (var i = previewContent.childCount - 1; i >= 0; i--)
                Destroy(previewContent.GetChild(i).gameObject);
            imagePreview = CreateImage(previewContent, "ImagePreview", Color.white);
            Stretch(imagePreview.rectTransform, 0, 0, 0, 0);
            imagePreview.preserveAspect = true;
            rawImagePreview = CreateRawImage(previewContent, "RawImagePreview");
            Stretch(rawImagePreview.rectTransform, 0, 0, 0, 0);
            rawImagePreview.gameObject.SetActive(false);
            actionButton.gameObject.SetActive(false);
            actionButton.onClick.RemoveAllListeners();
        }

        private void OnDestroy()
        {
            if (audioSource != null)
                audioSource.Stop();
            if (previewInstance != null)
                Destroy(previewInstance);
            if (renderTexture != null)
            {
                renderTexture.Release();
                Destroy(renderTexture);
            }
        }

        private static Bounds CalculateBounds(GameObject target)
        {
            var renderers = target.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return new Bounds(target.transform.position, Vector3.one);
            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        private static void SetPreviewLayer(GameObject target, int layer)
        {
            target.layer = layer;
            foreach (Transform child in target.transform)
                SetPreviewLayer(child.gameObject, layer);
        }

        private static string AssetLabel(ShowcaseEntry entry)
        {
            if (entry.Asset == null)
                return "Scene";
            return entry.Asset.GetType().Name;
        }

        private static string CategoryLabel(ShowcaseCategory category) => category switch
        {
            ShowcaseCategory.All => "すべて",
            ShowcaseCategory.Image => "画像",
            ShowcaseCategory.Character => "キャラ",
            ShowcaseCategory.Environment => "背景・環境",
            ShowcaseCategory.Weapon => "武器",
            ShowcaseCategory.Item => "アイテム",
            ShowcaseCategory.Vfx => "VFX・エフェクト",
            ShowcaseCategory.Animation => "アニメーション",
            ShowcaseCategory.Audio => "音声・音楽",
            ShowcaseCategory.Ui => "UI",
            ShowcaseCategory.Scene => "シーン",
            ShowcaseCategory.Material => "マテリアル・シェーダー",
            ShowcaseCategory.Data => "データ",
            _ => "その他",
        };

        private static Image CreatePanel(Transform parent, string name, Color color)
        {
            return CreateImage(parent, name, color);
        }

        private static TextMeshProUGUI CreateText(Transform parent, string name, string value, float size, Color color)
        {
            var object_ = new GameObject(name, typeof(RectTransform));
            object_.transform.SetParent(parent, false);
            var text = object_.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            return text;
        }

        private static Button CreateButton(Transform parent, string value, float height)
        {
            var object_ = new GameObject(value, typeof(RectTransform));
            object_.transform.SetParent(parent, false);
            var image = object_.AddComponent<Image>();
            image.color = new Color(0.13f, 0.17f, 0.25f, 1f);
            var button = object_.AddComponent<Button>();
            var label = CreateText(object_.transform, "Label", value, 15, Color.white);
            Stretch(label.rectTransform, 12, 12, 0, 0, true);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            object_.AddComponent<LayoutElement>().preferredHeight = height;
            return button;
        }

        private static Image CreateImage(Transform parent, string name, Color color)
        {
            var object_ = new GameObject(name, typeof(RectTransform));
            object_.transform.SetParent(parent, false);
            var image = object_.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static RawImage CreateRawImage(Transform parent, string name)
        {
            var object_ = new GameObject(name, typeof(RectTransform));
            object_.transform.SetParent(parent, false);
            var image = object_.AddComponent<RawImage>();
            image.color = Color.white;
            image.raycastTarget = false;
            return image;
        }

        private static ScrollRect CreateScrollView(Transform parent, string name)
        {
            var object_ = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Mask), typeof(ScrollRect));
            object_.transform.SetParent(parent, false);
            object_.GetComponent<Image>().color = new Color(0.025f, 0.035f, 0.06f, 1f);
            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewport.transform.SetParent(object_.transform, false);
            Stretch(viewport.GetComponent<RectTransform>(), 0, 0, 0, 0);
            viewport.GetComponent<Image>().color = Color.white;
            viewport.GetComponent<Image>().raycastTarget = false;
            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.sizeDelta = new Vector2(0, 0);
            var scroll = object_.GetComponent<ScrollRect>();
            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.content = contentRect;
            scroll.horizontal = false;
            scroll.vertical = true;
            return scroll;
        }

        private static void Stretch(RectTransform rect, float left = 0, float right = 0, float top = 0, float bottom = 0, bool center = false)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = center ? new Vector2(.5f, .5f) : new Vector2(.5f, .5f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static void SetTop(RectTransform rect, float margin, float height)
        {
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(.5f, 1);
            rect.offsetMin = new Vector2(margin, -margin - height);
            rect.offsetMax = new Vector2(-margin, -margin);
        }

        private static void SetLeft(RectTransform rect, float left, float width)
        {
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, .5f);
            rect.offsetMin = new Vector2(left, 0);
            rect.offsetMax = new Vector2(left + width, 0);
        }

        private static void SetBottom(RectTransform rect, float margin, float height)
        {
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(1, 0);
            rect.pivot = new Vector2(.5f, 0);
            rect.offsetMin = new Vector2(margin, margin);
            rect.offsetMax = new Vector2(-margin, margin + height);
        }
    }
}
