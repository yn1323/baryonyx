---
id: feature-screens
type: specification
status: 一部確定
updated: 2026-10-25
---

# 画面一覧と操作

[機能索引](README.md) / 根拠：[初期企画](../game/brief.md)

画面ごとの仕様は、次のファイルに分けて置く（2026-10-25に分けた。読むときは、必要な画面のファイルだけを開く）。

| 画面 | ファイル | 主な内容 |
|---|---|---|
| 共通の決まり | [common.md](screens/common.md) | 持ち物のカード風の見た目、停止中の表示の切り替え、3Dの舞台、未決事項と確認待ち |
| Topとホーム | [top-home.md](screens/top-home.md) | Top、画面遷移の演出、ホーム画面、デバッグルームの一覧、ルーン獲得の演出 |
| 戦闘画面 | [battle.md](screens/battle.md) | 戦闘画面の見た目と操作、戦闘の背景を選べる戦闘シーン |
| 戦闘のスキルの山札と手札 | [battle-deck.md](screens/battle-deck.md) | スキルの山札、カードと手札を見ながら調整する |
| 戦闘のスキルの演出と背景 | [battle-effects.md](screens/battle-effects.md) | スキルの演出と背景、カメラの中心のゆっくりした円運動 |
| デバッグルーム | [debug-rooms.md](screens/debug-rooms.md) | スキル・品質・敵の挙動・戦闘の演出・探索の演出・召喚の演出のデバッグルーム |
| 編成・商会・神殿・旅の案内所の共通 | [guide.md](screens/guide.md) | 案内人の画面の形、停止中の確認、枠つきの表示、すりガラスのカード |
| パーティと冒険者の個別の画面 | [adventurers.md](screens/adventurers.md) | パーティ、育成（冒険者の個別の画面）、絆 |
| 装備とスキルの付け替え | [loadout.md](screens/loadout.md) | 装備の人選び、装備、スキル |
| 商会 | [shop.md](screens/shop.md) | 画面の形、買う、売る、合成、品質強化とスキル覚醒 |
| 神殿 | [temple.md](screens/temple.md) | 神殿、召喚の演出の計画 |
| 冒険の画面 | [adventure.md](screens/adventure.md) | 冒険の画面 |
| 画面と実装の対応 | [implementation.md](screens/implementation.md) | 画面ごとの実装の入口 |

経緯は[画面一覧の変更と判断の記録](screens-history.md)に置く。
