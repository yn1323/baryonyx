# 技・カードの定義

[データ索引](../README.md) / [関連仕様](../../features/combat.md)

個別内容と決定状態は、下の索引から各定義を参照する。
画像化の要否は個別に決め、画像が必要なら画像対象テンプレートを使う。
IDの接頭辞は `skill-`、メタデータの `category` は `skills` とする。

## 個別データの索引

<!-- entries:start -->
| ID | 名称 | 状態 | 詳細 |
|---|---|---|---|
| skill-absolute-zero | 絶対零度 | 候補 | [定義](skill-absolute-zero.md) |
| skill-armor-break | 鎧砕き | 候補 | [定義](skill-armor-break.md) |
| skill-arrow-rain | アローレイン | 候補 | [定義](skill-arrow-rain.md) |
| skill-blade-dance | 千刃乱舞 | 候補 | [定義](skill-blade-dance.md) |
| skill-blast-sigil | 爆炎の刻印 | 候補 | [定義](skill-blast-sigil.md) |
| skill-blizzard | ブリザード | 候補 | [定義](skill-blizzard.md) |
| skill-chain-lightning | チェインライトニング | 候補 | [定義](skill-chain-lightning.md) |
| skill-divine-shield | ディバインシールド | 候補 | [定義](skill-divine-shield.md) |
| skill-earth-splitter | 大地割り | 候補 | [定義](skill-earth-splitter.md) |
| skill-embers | 火の粉 | 候補 | [定義](skill-embers.md) |
| skill-fire | ファイア | 候補 | [定義](skill-fire.md) |
| skill-fire-storm | ファイアストーム | 候補 | [定義](skill-fire-storm.md) |
| skill-flame-enchant | フレイムエンチャント | 候補 | [定義](skill-flame-enchant.md) |
| skill-flame-pillar | フレイムピラー | 候補 | [定義](skill-flame-pillar.md) |
| skill-gale | 疾風迅雷 | 候補 | [定義](skill-gale.md) |
| skill-giant-impact | ギガントインパクト | 候補 | [定義](skill-giant-impact.md) |
| skill-guard | ガード | 候補 | [定義](skill-guard.md) |
| skill-guardian-oath | 守護の誓い | 候補 | [定義](skill-guardian-oath.md) |
| skill-heal | ヒール | 候補 | [定義](skill-heal.md) |
| skill-holy-hammer | 聖なる鉄槌 | 候補 | [定義](skill-holy-hammer.md) |
| skill-holy-light | ホーリーライト | 候補 | [定義](skill-holy-light.md) |
| skill-hone | 研ぎ澄ます | 候補 | [定義](skill-hone.md) |
| skill-iai | 居合一閃 | 候補 | [定義](skill-iai.md) |
| skill-ice | アイスランス | 候補 | [定義](skill-ice.md) |
| skill-ice-mirror | アイスミラー | 候補 | [定義](skill-ice-mirror.md) |
| skill-icicles | つらら落とし | 候補 | [定義](skill-icicles.md) |
| skill-insight-arrow | 看破の矢 | 候補 | [定義](skill-insight-arrow.md) |
| skill-lightning-bolt | ライトニングボルト | 候補 | [定義](skill-lightning-bolt.md) |
| skill-mana-prayer | マナの祈り | 候補 | [定義](skill-mana-prayer.md) |
| skill-meteor | メテオ | 候補 | [定義](skill-meteor.md) |
| skill-poison-needle | 毒針 | 候補 | [定義](skill-poison-needle.md) |
| skill-protect | プロテクト | 候補 | [定義](skill-protect.md) |
| skill-purify | 浄化の光 | 候補 | [定義](skill-purify.md) |
| skill-quick-cast | 詠唱短縮 | 候補 | [定義](skill-quick-cast.md) |
| skill-regen | リジェネ | 候補 | [定義](skill-regen.md) |
| skill-resurrection | リザレクション | 候補 | [定義](skill-resurrection.md) |
| skill-revelation | 天啓 | 候補 | [定義](skill-revelation.md) |
| skill-scout | 偵察 | 候補 | [定義](skill-scout.md) |
| skill-shadow-snipe | シャドウスナイプ | 候補 | [定義](skill-shadow-snipe.md) |
| skill-shadow-stitch | 影縫い | 候補 | [定義](skill-shadow-stitch.md) |
| skill-shield-bash | シールドバッシュ | 候補 | [定義](skill-shield-bash.md) |
| skill-slash | 斬り払い | 候補 | [定義](skill-slash.md) |
| skill-step-prayer | 万歩の祈り | 候補 | [定義](skill-step-prayer.md) |
| skill-stride-strike | 健脚の一撃 | 候補 | [定義](skill-stride-strike.md) |
| skill-thunder | サンダー | 候補 | [定義](skill-thunder.md) |
| skill-thunder-spear | 雷神の槍 | 候補 | [定義](skill-thunder-spear.md) |
| skill-thundercloud | 雷雲 | 候補 | [定義](skill-thundercloud.md) |
| skill-vital-thrust | 急所突き | 候補 | [定義](skill-vital-thrust.md) |
| skill-war-cry | 鬨の声 | 候補 | [定義](skill-war-cry.md) |
| skill-whirlwind | 旋風斬 | 候補 | [定義](skill-whirlwind.md) |
<!-- entries:end -->

## 記入する項目

| 項目 | 初期の扱い |
|---|---|
| スキル・固有スキルの分類、所持者 | 個別設定は未決。構成は[キャラクターの構成](../../features/party.md#キャラクターの構成)に従う |
| カードの種類（攻撃・バフ・デバフ・回復・コスト軽減・持続攻撃など） | 個別設定は未決。種類は[カードの種類と流れ](../../features/combat.md#カードの種類と流れ)に従う |
| 対象（単体・全体）・発動条件 | 個別設定は未決 |
| 属性 | 個別設定は未決。種類は[属性と弱点](../../features/combat.md#属性と弱点)に従う |
| コスト | 個別値は未決。[エネルギーとコスト](../../features/combat.md#エネルギーとコスト)に従う |
| 威力・回復量（参照するステータスと割合）・効果ID | 固定値は持たない。個別値は未決。[威力の決め方](../../features/combat.md#威力の決め方)に従う |
| 持続ターン | 数ターン持続するカードで記入。個別値は未決 |
| クールターン | 固有スキルだけに記入。個別値は未決。[固有スキル](../../features/combat.md#固有スキル)に従う |
| UPTによる威力の変化 | UPTで威力が変わるカードだけに記入。参照するUPTと変え方は未決 |
| 動作・ポーズ・エフェクトの形・色・軌跡 | 未決 |
| アイコン・予告表示・音・振動の要否 | 未決 |

旧仕様のダウン値・キャストタイム・個別クールダウンは[戦闘仕様の変更記録](../../features/combat.md#変更と判断の記録)にあり、記入項目に含めない。

## 追加方法

[画像対象テンプレート](../templates/visual-entity.md)または[定義テンプレート](../templates/definition.md)をこの分類にコピーし、1対象1ファイルで記入する。
既存名・ID・画像の有無を確認し、未決欄を推測で埋めない。
追加後は[索引を再生成](../../tools/README.md)する。
