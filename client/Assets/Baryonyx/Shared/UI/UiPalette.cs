using UnityEngine;

namespace Baryonyx.UI
{
    /// <summary>
    /// The text and accent colours the screens share: the warm off-white body text, the dimmer
    /// sub text, the faint hint text, the teal of linked/selected states and the gold of rewards.
    /// </summary>
    public static class UiPalette
    {
        public static readonly Color TextMain = new(0.953f, 0.914f, 0.824f);
        public static readonly Color TextSub = new(0.788f, 0.749f, 0.659f);
        public static readonly Color TextFaint = new(0.604f, 0.580f, 0.514f);
        public static readonly Color Teal = new(0.498f, 0.890f, 0.839f);
        public static readonly Color Gold = new(1f, 0.843f, 0.4f);

        // 見出しの明るい金（枠つきの画面の「パーティ」「一覧」など）。
        public static readonly Color GoldLight = new(0.992f, 0.894f, 0.353f);

        // --- 役割の名前で持つ色（doc/rules/ui-design.md の基本の値）。画面の生成処理は、ここから選ぶ。

        // 幕は2段階。窓（ダイアログ・一覧の窓・確かめる窓）の後ろは背景が少し見える濃さ、
        // 全画面で選ばせる重ね（山札の一覧・戦闘後の強化・覚醒の演出）は後ろの画面がほぼ隠れる濃さにする。
        public static readonly Color Veil = new(0.012f, 0.02f, 0.04f, 0.72f);
        public static readonly Color VeilDeep = new(0.01f, 0.015f, 0.045f, 0.93f);

        // 面。枠つきの画面の景色が透ける面と、その上に重ねる濃い紺。窓は濃い紺を不透明に塗る
        // （リニア色空間では少しの透けでも後ろの文字が目立つ）。
        public static readonly Color SurfaceLight = new(0.063f, 0.086f, 0.196f, 0.55f);
        public static readonly Color SurfaceDark = new(0.024f, 0.035f, 0.094f, 0.965f);
        public static readonly Color WindowFill = new(0.024f, 0.035f, 0.094f, 1f);

        // 枠つきの画面のヘッダーの帯。
        public static readonly Color Band = new(0.024f, 0.035f, 0.094f, 0.84f);

        // 札（すりガラスのカード）の板。透ける面の上はほぼ透明、景色や絵の上に直に置く札は濃い紺。
        public static readonly Color PlateGlass = new(0.62f, 0.72f, 1f, 0.05f);
        public static readonly Color PlateScene = new(0.035f, 0.05f, 0.12f, 0.82f);

        // 金の細い線。区切り（見出しの下・ヘッダーの下）は淡く、ヘッダーの仕切りは少し濃く、
        // 面の仕切りの2本線と窓の縁はいちばん濃い。
        public static readonly Color LineFaint = new(1f, 0.86f, 0.59f, 0.43f);
        public static readonly Color LineSeparator = new(1f, 0.86f, 0.59f, 0.5f);
        public static readonly Color LineDivider = new(1f, 0.86f, 0.59f, 0.6f);

        // 決めるボタン（doc/rules/ui-design.md#決めるボタン）。前に進める操作は青い板、取り返せない操作の
        // 確認は赤い板に白い文字。上の縁だけ少し明るくして、板の厚みを出す（2026-11-03、ユーザー）。
        public static readonly Color PrimaryFill = new(44f / 255f, 90f / 255f, 150f / 255f);
        public static readonly Color PrimaryEdge = new(92f / 255f, 138f / 255f, 196f / 255f);
        public static readonly Color DangerFill = new(140f / 255f, 44f / 255f, 44f / 255f);
        public static readonly Color DangerEdge = new(178f / 255f, 78f / 255f, 70f / 255f);
        public static readonly Color ButtonText = new(242f / 255f, 236f / 255f, 222f / 255f);

        // 押せないボタンに掛ける色（板も文字も灰色に沈める）。すりガラスの札の押せないときと同じ。
        public static readonly Color ButtonDisabled = new(0.55f, 0.55f, 0.6f, 0.7f);

        // ステータスが上がる数字の緑と、下がる数字の赤（装備の上昇分と増減）。
        public static readonly Color Rise = new(0.49f, 0.88f, 0.42f);
        public static readonly Color Fall = new(1f, 0.45f, 0.4f);
    }
}
