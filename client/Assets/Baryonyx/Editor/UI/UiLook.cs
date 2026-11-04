using System;

namespace Baryonyx.Editor.UI
{
    // 画面を組み立てる間だけ切り替える見た目。今は「線のない形」だけを持つ（doc/rules/ui-design.md の線のない形）。
    // 線のない形の間は、面と分類の区切りの金の線、ヘッダーの線、分類の下の細い線、一覧の上の見出しの段を置かず、
    // 行の板を描かない。部品の生成処理は Lineless を見て、引数を変えずに形を変える。
    public static class UiLook
    {
        public static bool Lineless { get; private set; }

        // 商会・酒場の画面を組み立てる間に開く。Dispose で前の見た目に戻す。
        public static Scope BeginLineless()
        {
            var scope = new Scope(Lineless);
            Lineless = true;
            return scope;
        }

        public readonly struct Scope : IDisposable
        {
            private readonly bool previous;

            public Scope(bool previous) => this.previous = previous;

            public void Dispose() => Lineless = previous;
        }
    }
}
