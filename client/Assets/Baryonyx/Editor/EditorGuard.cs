using System;
using UnityEditor;

namespace Baryonyx.Editor
{
    public static class EditorGuard
    {
        // シーンやアセットを作り直すメニューは、Play中に実行させない（Play中の変更はシーンに残らないため）。
        public static void RequireEditMode()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Stop Play Mode first.");
        }
    }
}
