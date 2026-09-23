using UnityEngine;

namespace Baryonyx.Showcase
{
    [CreateAssetMenu(menuName = "Baryonyx/Showcase/Entry")]
    public sealed class ShowcaseEntry : ScriptableObject
    {
        public string Id;
        public string DisplayName;

        [TextArea(2, 5)]
        public string Description;
        public ShowcaseCategory Category = ShowcaseCategory.Other;
        public UnityEngine.Object Asset;
        public GameObject PreviewPrefab;
        public AnimationClip PreviewAnimation;
        public string AnimationStateName;
        public string ScenePath;
        public bool Enabled = true;

        public string Label => string.IsNullOrWhiteSpace(DisplayName) ? name : DisplayName;
    }
}
