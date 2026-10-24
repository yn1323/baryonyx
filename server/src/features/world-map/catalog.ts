// 街道の世界地図（doc/features/stage-progression.md#街道とダンジョン）。
// マスと道の形はクライアントの WorldMapCatalog が持ち、サーバーは進み具合だけを預かる。

// 最初の街。初めて開くユーザーは、ここをクリアした状態でここに立つ。
export const START_SQUARE = "porta";

// マスのID：街は英小文字の名前（porta など）、街道のマスは方眼の位置（n12-4 など）。
export const SQUARE_ID_PATTERN = /^(?:n\d{1,2}-\d{1,2}|[a-z]{2,20})$/;
