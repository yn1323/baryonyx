using System;
using System.Collections.Generic;
using System.Linq;
using Baryonyx.Showcase;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Baryonyx.Showcase.Editor
{
    public sealed class ShowcasePreviewWindow : EditorWindow
    {
        private const string StylePath = ShowcaseCatalogBuilder.RootPath + "/Editor/ShowcasePreviewWindow.uss";

        private readonly List<ShowcaseEntry> visibleEntries = new();
        private ShowcaseCatalog catalog;
        private ListView entryList;
        private PopupField<string> categoryField;
        private TextField searchField;
        private Image previewImage;
        private Label titleLabel;
        private Label categoryLabel;
        private Label typeLabel;
        private Label sourceLabel;
        private Label descriptionLabel;
        private Label emptyLabel;
        private VisualElement detailsContainer;
        private ShowcaseEntry selectedEntry;
        private Texture2D pendingPreview;
        private bool previewPolling;

        private static readonly string[] CategoryNames =
            Enum.GetNames(typeof(ShowcaseCategory));

        [MenuItem("Baryonyx/Showcase/Open Preview Window")]
        public static void OpenWindow()
        {
            var window = GetWindow<ShowcasePreviewWindow>();
            window.titleContent = new GUIContent("Showcase Preview");
            window.minSize = new Vector2(760, 420);
            window.Show();
        }

        private void OnEnable()
        {
            EditorApplication.projectChanged += OnProjectChanged;
        }

        private void OnDisable()
        {
            EditorApplication.projectChanged -= OnProjectChanged;
            StopPreviewPolling();
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.AddToClassList("showcase-preview-window");

            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(StylePath);
            if (styleSheet != null)
                rootVisualElement.styleSheets.Add(styleSheet);

            BuildToolbar();
            BuildContent();
            LoadCatalog();
            RefreshList();
        }

        private void BuildToolbar()
        {
            var toolbar = new Toolbar();
            toolbar.AddToClassList("showcase-toolbar");

            var refreshButton = new ToolbarButton(RefreshCatalog)
            {
                text = "Refresh Catalog",
                tooltip = "Assets/Baryonyx のアセット一覧を更新します"
            };
            toolbar.Add(refreshButton);

            categoryField = new PopupField<string>("Category", CategoryNames.ToList(), 0);
            categoryField.AddToClassList("showcase-category-field");
            categoryField.RegisterValueChangedCallback(_ => RefreshList());
            toolbar.Add(categoryField);

            searchField = new TextField("Search");
            searchField.AddToClassList("showcase-search-field");
            searchField.RegisterValueChangedCallback(_ => RefreshList());
            toolbar.Add(searchField);

            rootVisualElement.Add(toolbar);
        }

        private void BuildContent()
        {
            var splitView = new TwoPaneSplitView(0, 285, TwoPaneSplitViewOrientation.Horizontal);
            splitView.AddToClassList("showcase-content");

            entryList = new ListView(visibleEntries, 24, MakeEntryItem, BindEntryItem)
            {
                selectionType = SelectionType.Single,
                showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly
            };
            entryList.name = "showcaseEntryList";
            entryList.AddToClassList("showcase-entry-list");
            entryList.selectionChanged += OnSelectionChanged;
            splitView.Add(entryList);

            var previewPane = new VisualElement();
            previewPane.AddToClassList("showcase-preview-pane");

            previewImage = new Image
            {
                scaleMode = ScaleMode.ScaleToFit
            };
            previewImage.AddToClassList("showcase-preview-image");
            previewPane.Add(previewImage);

            detailsContainer = new VisualElement();
            detailsContainer.AddToClassList("showcase-details");
            titleLabel = CreateDetailLabel("showcase-title");
            categoryLabel = CreateDetailLabel("showcase-detail");
            typeLabel = CreateDetailLabel("showcase-detail");
            sourceLabel = CreateDetailLabel("showcase-detail");
            descriptionLabel = CreateDetailLabel("showcase-description");
            detailsContainer.Add(titleLabel);
            detailsContainer.Add(categoryLabel);
            detailsContainer.Add(typeLabel);
            detailsContainer.Add(sourceLabel);
            detailsContainer.Add(descriptionLabel);

            var actions = new VisualElement();
            actions.AddToClassList("showcase-actions");
            var selectButton = new Button(SelectCurrentAsset) { text = "Select Asset" };
            selectButton.name = "selectAssetButton";
            actions.Add(selectButton);
            var openSceneButton = new Button(OpenCurrentScene) { text = "Open Scene" };
            openSceneButton.name = "openSceneButton";
            actions.Add(openSceneButton);
            detailsContainer.Add(actions);
            previewPane.Add(detailsContainer);

            emptyLabel = new Label("Catalog を読み込むと、ここにプレビューが表示されます。");
            emptyLabel.AddToClassList("showcase-empty-label");
            previewPane.Add(emptyLabel);

            splitView.Add(previewPane);
            rootVisualElement.Add(splitView);
        }

        private static Label CreateDetailLabel(string className)
        {
            var label = new Label();
            label.AddToClassList(className);
            return label;
        }

        private VisualElement MakeEntryItem()
        {
            var row = new VisualElement();
            row.AddToClassList("showcase-entry-row");
            var label = new Label();
            label.name = "entryLabel";
            row.Add(label);
            return row;
        }

        private void BindEntryItem(VisualElement element, int index)
        {
            var label = element.Q<Label>("entryLabel");
            if (label == null || index < 0 || index >= visibleEntries.Count)
                return;

            label.text = visibleEntries[index] is ShowcaseEntry entry
                ? $"{entry.Label}  [{entry.Category}]"
                : string.Empty;
        }

        private void LoadCatalog()
        {
            catalog = AssetDatabase.LoadAssetAtPath<ShowcaseCatalog>(ShowcaseCatalogBuilder.CatalogPath);
        }

        private void RefreshCatalog()
        {
            ShowcaseCatalogBuilder.RefreshCatalog();
            LoadCatalog();
            RefreshList();
        }

        private void OnProjectChanged()
        {
            if (this == null || rootVisualElement == null || entryList == null)
                return;

            LoadCatalog();
            RefreshList();
        }

        private void RefreshList()
        {
            if (entryList == null)
                return;

            var category = categoryField?.value ?? nameof(ShowcaseCategory.All);
            var search = searchField?.value?.Trim() ?? string.Empty;
            var source = catalog != null ? catalog.Entries : Array.Empty<ShowcaseEntry>();

            visibleEntries.Clear();
            visibleEntries.AddRange(source
                .Where(entry => entry != null && entry.Enabled)
                .Where(entry => category == nameof(ShowcaseCategory.All) || entry.Category.ToString() == category)
                .Where(entry => string.IsNullOrWhiteSpace(search)
                    || entry.Label.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0
                    || (entry.Description ?? string.Empty).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(entry => entry.Label));

            entryList.Rebuild();
            if (visibleEntries.Count == 0)
            {
                selectedEntry = null;
                UpdateDetails();
                return;
            }

            var selectedIndex = selectedEntry == null ? 0 : visibleEntries.IndexOf(selectedEntry);
            entryList.SetSelection(Mathf.Max(0, selectedIndex));
        }

        private void OnSelectionChanged(IEnumerable<object> selection)
        {
            selectedEntry = selection.OfType<ShowcaseEntry>().FirstOrDefault();
            UpdateDetails();
        }

        private void UpdateDetails()
        {
            StopPreviewPolling();
            previewImage.image = null;
            pendingPreview = null;

            var hasSelection = selectedEntry != null;
            detailsContainer?.SetEnabled(hasSelection);
            if (emptyLabel != null)
            {
                if (hasSelection)
                    emptyLabel.AddToClassList("showcase-hidden");
                else
                    emptyLabel.RemoveFromClassList("showcase-hidden");
            }
            if (!hasSelection)
            {
                titleLabel.text = string.Empty;
                categoryLabel.text = string.Empty;
                typeLabel.text = string.Empty;
                sourceLabel.text = string.Empty;
                descriptionLabel.text = string.Empty;
                return;
            }

            var asset = selectedEntry.Asset != null ? selectedEntry.Asset : selectedEntry.PreviewPrefab;
            var path = selectedEntry.Category == ShowcaseCategory.Scene
                ? selectedEntry.ScenePath
                : AssetDatabase.GetAssetPath(asset);
            titleLabel.text = selectedEntry.Label;
            categoryLabel.text = $"Category: {selectedEntry.Category}";
            typeLabel.text = $"Type: {(asset == null ? "Scene" : asset.GetType().Name)}";
            sourceLabel.text = $"Source: {path}";
            descriptionLabel.text = selectedEntry.Description ?? string.Empty;

            if (asset == null)
                return;

            pendingPreview = AssetPreview.GetAssetPreview(asset);
            if (pendingPreview != null)
            {
                previewImage.image = pendingPreview;
                return;
            }

            StartPreviewPolling();
        }

        private void StartPreviewPolling()
        {
            if (previewPolling)
                return;

            previewPolling = true;
            EditorApplication.update += PollPreview;
        }

        private void StopPreviewPolling()
        {
            if (!previewPolling)
                return;

            previewPolling = false;
            EditorApplication.update -= PollPreview;
        }

        private void PollPreview()
        {
            if (this == null || selectedEntry == null)
            {
                StopPreviewPolling();
                return;
            }

            var asset = selectedEntry.Asset != null ? selectedEntry.Asset : selectedEntry.PreviewPrefab;
            pendingPreview = asset == null ? null : AssetPreview.GetAssetPreview(asset);
            if (pendingPreview != null)
            {
                previewImage.image = pendingPreview;
                StopPreviewPolling();
            }

            Repaint();
        }

        private void SelectCurrentAsset()
        {
            if (selectedEntry == null)
                return;

            var asset = selectedEntry.Asset != null ? selectedEntry.Asset : selectedEntry.PreviewPrefab;
            Selection.activeObject = asset;
            if (asset != null)
                EditorGUIUtility.PingObject(asset);
        }

        private void OpenCurrentScene()
        {
            if (selectedEntry == null || selectedEntry.Category != ShowcaseCategory.Scene
                || string.IsNullOrWhiteSpace(selectedEntry.ScenePath))
                return;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EditorSceneManager.OpenScene(selectedEntry.ScenePath, OpenSceneMode.Single);
        }
    }
}
