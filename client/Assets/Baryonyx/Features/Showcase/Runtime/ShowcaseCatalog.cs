using System.Collections.Generic;
using UnityEngine;

namespace Baryonyx.Showcase
{
    [CreateAssetMenu(menuName = "Baryonyx/Showcase/Catalog")]
    public sealed class ShowcaseCatalog : ScriptableObject
    {
        [SerializeField]
        private List<ShowcaseEntry> entries = new();

        public IReadOnlyList<ShowcaseEntry> Entries => entries;

        public void SetEntries(IEnumerable<ShowcaseEntry> values)
        {
            entries = new List<ShowcaseEntry>(values);
        }
    }
}
