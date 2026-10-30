# 読み込みの待ち時間の隠し方の調査資料

調査日は2026-10-30。
[loading-concealment](../SKILL.md) の判断の根拠にする、Webとゲームの一般的な目安をまとめる。
このプロジェクトでの決まりは[読み込みの待ち時間を見せない](../../../../doc/rules/ui-design.md#読み込みの待ち時間を見せない)が正本であり、ここの数値は検討の出発点として使う。
UnityのAPIを実装へ使うときは、プロジェクトのUnity `6000.6.0f1` に対応する公式マニュアルを再確認する。

## 待ち時間の目安

- [Nielsen Norman Group「Response Times: The 3 Important Limits」](https://www.nngroup.com/articles/response-times-3-important-limits/)
  - 0.1秒以内なら、操作がそのまま効いたと感じ、結果を出すだけでよい。
  - 1秒以内なら、遅れに気づいても考えの流れは切れない。
  - 10秒を超えると注意がそれるため、進み具合と中断の手段を出す。
- [Nielsen Norman Group「Skeleton Screens 101」](https://www.nngroup.com/articles/skeleton-screens/)
  - 1秒未満の読み込みには、骨組み（スケルトン）も回る印も要らない。
  - 骨組みは画面全体の読み込みで10秒未満のときに使い、10秒を超えるなら進み具合のバーにする。
  - 枠だけで中身の形を示さない骨組みは、壊れて見えるため避ける。骨組みは速くする努力の代わりにならない。
  - このプロジェクトの[カードの裏面](../../../../doc/features/screens/guide.md#カードの裏面)は、一覧の形を保った骨組みに当たる。
- 印を出すのを遅らせる考え方（[Suspensive の Delay](https://suspensive.org/docs/react/Delay)、[Productboard「Spinners versus skeletons」](https://www.productboard.com/blog/spinners-versus-skeletons-in-the-battle-of-hasting/)）
  - 100〜300ms待ってから印を出し、それまでに読み終えたら何も出さない。出した印は短い時間で消さず、ちらつかせない。
  - 出典はライブラリと実務の記事であり、数値は計測した研究ではない。手元の待ち時間で決める。
- [Android Developers「Progress indicators」](https://developer.android.com/develop/ui/compose/components/progress)
  - 長さが分かる待ちは進み具合を示す印、分からない待ちは回り続ける印を使う。

## 待たずに出す

- 前に読んだ内容を先に出して裏で読み直す（stale-while-revalidate）
  - HTTPのキャッシュの指示と同じ考え方を、SWRやTanStack Queryなどのライブラリが画面に当てている。開き直した画面では印がほとんど出なくなる。
  - このプロジェクトでは[サーバーに置くときの作り方](../../../../doc/rules/data-storage.md#サーバーに置くときの作り方)の「前に読んだ内容」に当たる。
- 楽観的更新
  - 操作の結果を先に画面へ出し、要求はあとから送る。失敗したときに戻す道を必ず用意する。
- 押しそうな所を先に読む（[Chrome for Developers「Speculation Rules」の解説](https://developer.chrome.com/blog/devtools-tips-31)）
  - Webでは、リンクに200msほど指を置いた・押し始めた時点で次のページを読み始め、押したときには描き終えている。
  - 先読みは通信と記憶域を使うため、押す見込みの高い所に絞り、操作を伴うページ（購入など）は先に読まない。
  - ゲームでは、ホームの行き先カードのように次に開く画面が絞れる所で、その画面のサーバーの読み込みを先に始める形に置き換えられる。

## ゲームでの隠し方

- 移動や演出で隠す（[Giant Bomb「Elevators That Mask Loading Times」](https://www.giantbomb.com/wiki/Concepts/Elevators_That_Mask_Loading_Times)、[Tropedia「Dynamic Loading」](https://tropedia.fandom.com/wiki/Dynamic_Loading)）
  - エレベーター・細い通路・扉を開ける動きの間に次の場所を読む。読み込みが終わる前に通路の端へ着く場合を考え、通路を長くする・扉で止めるなどの手当てをする。
  - 隠す演出が終わっても読み終わらないと、止まったように見えて逆効果になる。演出の長さで隠しきれない待ちは、別の形と組み合わせる。
- 隠し方より回数（[GamesRadar「The secret art of the video game loading screen」](https://www.gamesradar.com/the-secret-art-of-the-video-game-loading-screen-and-why-they-wont-be-going-away-anytime-soon/)）
  - 1回ごとの短い暗転より、何度も続く暗転のほうが不満を生む。切り替えの回数を減らせないかを先に見る。
- 移動した感覚を出す切り替え
  - 地図の上の移動や、近づいて暗くなり次の地図が引きながら明るくなる演出は、読み込みを隠さなくても場所が変わったことを伝える。このプロジェクトでは[入れ子の地図の切り替えの演出](../../../../doc/features/world-atlas.md#切り替えの演出)に当たる。

## Unityの読み込み

- [AsyncOperation.allowSceneActivation](https://docs.unity3d.com/ScriptReference/AsyncOperation-allowSceneActivation.html)
  - `false` にすると、シーンの読み込みは進み具合0.9で止まり、切り替えを待つ。幕を閉じ終えてから切り替えたいときに使える。
  - 止めている間は、後に始めた非同期の読み込み（別のシーンの読み込み・破棄など）も待たされる。止めて待つ読み込みは1つにする。
  - Awakeなどのライフサイクルの中で値を変えず、コルーチンの中で扱う。
- シェーダーの初回の引っかかり（[Unity「Prewarm shaders」](https://docs.unity3d.com/Manual/shader-prewarm.html)、[PSOの記録と温め](https://docs.unity3d.com/Manual/shader-pso-trace.html)）
  - 描画ドライバーは、初めて使うシェーダーをその場で準備するため、初めて出る演出や画面で1コマ止まることがある。スマホで目立ちやすい。
  - Unity 6の `GraphicsStateCollection` は、開発用ビルドで使った描画の状態を記録し、読み込みの間に温める（`WarmUp`・`WarmUpProgressively`）。試験的なAPIで、Vulkanなど並列に作れる環境以外では古い `ShaderVariantCollection.WarmUp` に戻る。
  - 温めは幕で覆っている間（起動時やシーンの読み込み中）に行う。採用する前に、実機で引っかかりがあるかを確かめる。
