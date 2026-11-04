using System;

namespace Baryonyx.Editor.UI
{
    // 画面を組み立てる間だけ切り替える見た目。今は「線のない形」だけを持つ（doc/rules/ui-design.md の線のない形）。
    // 線のない形の間は、面と分類の区切りの金の線、ヘッダーの線、分類の下の細い線、一覧の上の見出しの段（ListHeading のときは残す）を置かず、
    // 行の板を描かない。部品の生成処理は Lineless を見て、引数を変えずに形を変える。
    public static class UiLook
    {
        public static bool Lineless { get; private set; }

        // 線のない形でも、一覧の上の見出しの段を残す（ライブラリの見つけた数）。見出しの菱形は付けない。
        public static bool ListHeading { get; private set; }

        // 商会・酒場・ライブラリ・旅の案内所・神殿・冒険の画面を組み立てる間に開く。Dispose で前の見た目に戻す。
        public static Scope BeginLineless(bool listHeading = false)
        {
            var scope = new Scope(Lineless, ListHeading);
            Lineless = true;
            ListHeading = listHeading;
            return scope;
        }

        public readonly struct Scope : IDisposable
        {
            private readonly bool previous;
            private readonly bool previousHeading;

            public Scope(bool previous, bool previousHeading)
            {
                this.previous = previous;
                this.previousHeading = previousHeading;
            }

            public void Dispose()
            {
                Lineless = previous;
                ListHeading = previousHeading;
            }
        }
    }
}
