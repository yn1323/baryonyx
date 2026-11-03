using System.Collections.Generic;
using Baryonyx.UI.EditModePreview;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.UI
{
    /// <summary>
    /// The small window that shows a card's detail when the card itself has no detail beside it
    /// (doc/rules/ui-design.md の装備の見せ方): the summon's results, the treasures' clear rewards,
    /// the adventure's results and rewards. It holds the same face as the detail panels
    /// (<see cref="ItemDetail"/>) and closes with × or a tap outside. ItemDetailAssets.Window
    /// bakes it hidden; a screen calls <see cref="Show"/> from its card's tap.
    /// </summary>
    public sealed class ItemDetailWindow : MonoBehaviour, IEditModePreview
    {
        // 幕と窓をまとめた根。隠すと窓が閉じる。
        public GameObject Root;
        public Button Veil;
        public Button Close;
        // 窓の中身。生成処理が見本の品を焼き込み、停止中の表示の切り替えはそれを見せる。
        public ItemDetail Face;

        public bool IsOpen => Root != null && Root.activeSelf;

        private void Awake()
        {
            if (Veil != null)
                Veil.onClick.AddListener(Hide);
            if (Close != null)
                Close.onClick.AddListener(Hide);
        }

        public void Show(ItemFace face)
        {
            if (Face != null)
                Face.Show(face);
            if (Root != null)
                Root.SetActive(true);
        }

        public void Hide()
        {
            if (Root != null)
                Root.SetActive(false);
        }

        // 窓を開いている間は、「もどる」と端末の戻るキーで窓を閉じる。
        public bool HandleBack()
        {
            if (!IsOpen)
                return false;
            Hide();
            return true;
        }

        public IReadOnlyList<string> PreviewStates => new[] { "閉じている", "開いている" };

        public void ShowPreviewState(int index)
        {
            if (Root != null)
                Root.SetActive(index == 1);
        }
    }
}
