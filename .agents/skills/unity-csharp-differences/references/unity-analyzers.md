# Microsoft.Unity.Analyzers規則

この資料はMicrosoft.Unity.Analyzersの公式診断一覧を、コードを書くときの判断に使える形へ圧縮したもの。実際に有効な規則、重大度、バージョンはプロジェクトのAnalyzer設定とCIの結果を優先する。ルールIDや名称はパッケージ更新で増減するため、更新時は公式一覧を再確認する。

## 今回のnull系で必ず覚える規則

| ID | 対象 | 修正 |
|---|---|---|
| `UNT0007` | Unityオブジェクトへの`??` | `a != null ? a : b`または明示的な`if` |
| `UNT0008` | Unityオブジェクトへの`?.`、`?[]` | `if (a != null)`へ分解 |
| `UNT0023` | Unityオブジェクトへの`??=` | `if (a == null) a = b;` |
| `UNT0029` | Unityオブジェクトへの`is null`/`is not null` | `== null`/`!= null` |
| `USP0018`/`USP0022` | Unityオブジェクトに対する一般C#のthrow/null合体簡略化 | Unityの明示的なnull判定へ戻す |

`?.`だけでなく、null合体、null合体代入、nullパターンも同じ根本原因で検査される。Unity公式は、detached状態を`?.`や`??`で再現できないため、Unityオブジェクトでは使用しないよう説明している。

## 全診断の実装時クイックリファレンス

### Correctness・Type Safety

| ID | 要点 | コードを書くときの判断 |
|---|---|---|
| `UNT0001` | 空のUnityメッセージ | 空の`Update`などを置かない。必要な理由がなければ削除 |
| `UNT0002` | 非効率なタグ比較 | `CompareTag`を使う |
| `UNT0003` | 非ジェネリック`GetComponent` | `GetComponent<T>`を使う |
| `UNT0004` | `Update`で`fixedDeltaTime` | 通常のフレーム処理は`deltaTime` |
| `UNT0005` | `FixedUpdate`で`deltaTime`（retired） | 既存コードの指摘は対象Analyzer版を確認 |
| `UNT0006` | Unityメッセージのシグネチャ不正 | 名前、大文字小文字、引数、戻り値を公式シグネチャに合わせる |
| `UNT0007` | null合体 | 上記null系ルール |
| `UNT0008` | null伝播 | 上記null系ルール |
| `UNT0009` | `InitializeOnLoad`にstaticコンストラクターがない | Editorロード時のstatic初期化を定義 |
| `UNT0010` | `MonoBehaviour`をインスタンス化 | `AddComponent`またはPrefabを使う |
| `UNT0011` | `ScriptableObject`を直接インスタンス化 | `CreateInstance`を使う |
| `UNT0012` | コルーチン戻り値を未使用 | `StartCoroutine`、停止、完了を設計 |
| `UNT0013` | 不正・冗長な`SerializeField` | フィールドの公開性、型、属性を確認 |
| `UNT0014` | `GetComponent`の対象型不正 | ComponentまたはInterfaceの型を指定 |
| `UNT0015` | 初期化属性メソッドのシグネチャ不正 | static、引数、戻り値を公式に合わせる |
| `UNT0016` | メソッド名の安全でない取得 | `nameof`など、名前変更に強い方法を使う |
| `UNT0020` | 非staticメソッドへの`MenuItem` | Editorメニュー関数をstaticにする |
| `UNT0021` | Unityメッセージのprotected化（opt-in） | プロジェクト方針を確認。Unityに呼ばせるメソッドを勝手に改変しない |
| `UNT0023` | null合体代入 | 上記null系ルール |
| `UNT0025` | `Input.GetKey`の`KeyCode`引数 | 推奨オーバーロードと現在のInput方式を確認 |
| `UNT0027` | `PropertyDrawer.OnGUI`の直接呼び出し | Unityの描画フローから呼ばせる |
| `UNT0029` | nullパターン | 上記null系ルール |
| `UNT0030` | `Transform`を直接Destroy | TransformだけかGameObject全体かを明示 |
| `UNT0031` | `LoadAttribute`内のアセット操作 | ロード属性の実行時点でAsset操作をしない |
| `UNT0033` | Unityメッセージの大文字小文字不正 | `OnEnable`など公式名をそのまま使う |
| `UNT0039` | 自身の`GetComponent`と`RequireComponent` | 必須コンポーネントを属性で宣言 |
| `UNT0040` | `GameObject.isStatic`のRuntime利用 | Editor専用プロパティをRuntimeへ持ち込まない |
| `UNT0043` | 条件コンパイル記号の typo | `#if`の定義名をProject Settingsと照合 |

### Performance・Allocation

| ID | 要点 | コードを書くときの判断 |
|---|---|---|
| `UNT0017` | `SetPixels`呼び出しが遅い | 大量更新は対象APIと更新頻度を再設計 |
| `UNT0018` | 性能が重要なUnityメッセージ内のReflection | `Update`等のループでReflectionを避ける |
| `UNT0019` | `GameObject.gameObject`の不要な間接参照 | 既にGameObjectならそのまま使う |
| `UNT0022` | positionとrotationの個別設定 | 可能なら一度に設定するAPIを使う |
| `UNT0024` | Vector演算よりscalar計算を優先 | スカラーで済む計算をVector化しない |
| `UNT0026` | `GetComponent`の常時allocation | 毎フレーム取得せず初期化時にキャッシュ |
| `UNT0028` | allocationする物理API | NonAlloc版や再利用バッファを検討 |
| `UNT0032` | localPositionとlocalRotationの個別設定 | 一度に設定するAPIを使う |
| `UNT0036` | positionとrotationの非効率な取得 | 必要な値だけ取得し、ループ内取得を減らす |
| `UNT0037` | localPositionとlocalRotationの非効率な取得 | 同上 |
| `UNT0038` | `WaitForSeconds`の毎回生成 | 固定待機時間は再利用を検討 |
| `UNT0041` | Animatorメソッドへ文字列を反復投入 | `Animator.StringToHash`をキャッシュ |
| `UNT0042` | Mesh配列プロパティのループ内アクセス | 配列を一度取得して使う |
| `UNT0044` | TextMeshPro設定時の一時文字列 | `SetText`や既存の文字列APIを確認 |
| `UNT0045` | allocationする配列アクセス | NonAlloc APIや再利用配列を検討 |
| `UNT0046` | `Shader.PropertyToID`の反復呼び出し | IDをstatic readonly等へキャッシュ |

### Readability・型変換

| ID | 要点 | コードを書くときの判断 |
|---|---|---|
| `UNT0034` | `Vector3`から`Vector2`へ暗黙変換可能 | 不要な変換や意図しない軸欠落を確認 |
| `UNT0035` | `Vector2`から`Vector3`へ暗黙変換可能 | z値の補完が意図どおりか確認 |

## Unityが一般C#アナライザーを抑制する例

UnityのライフサイクルやInspectorで使われるコードは、通常のC#アナライザーだけでは未使用・未代入と誤判定される。Microsoft.Unity.Analyzersは次のような抑制も提供する。

- `[SerializeField]`フィールドをreadonly・未代入・未使用と誤判定しない。
- `Awake`、`Start`、`Update`、`MenuItem`、`RuntimeInitializeOnLoadMethod`などUnityが呼ぶメソッドを未使用と誤判定しない。
- `?.`、`??`、`??=`、throw式について、Unity型に一般C#の簡略化を適用しない。
- Unityの命名規則や暗黙使用属性を、一般C#の命名・未使用ルールと混同しない。

## エラーを受けたときの調査順

1. IDを公式のAnalyzerドキュメントで確認する。
2. 指摘された式の型が`UnityEngine.Object`か、純粋なC#型かを確認する。
3. Unityの推奨置換（`!= null`、`CompareTag`、ジェネリックAPI、Unityの生成API）へ修正する。
4. AnalyzerだけでなくUnityコンパイルと対象動作を検証する。Analyzerの警告が消えても、シーン、Inspector、ライフサイクルの動作確認にはならない。

## 参照

- Microsoft.Unity.Analyzers診断一覧：<https://github.com/microsoft/Microsoft.Unity.Analyzers/blob/main/doc/index.md>
- `UNT0007`：<https://github.com/microsoft/Microsoft.Unity.Analyzers/blob/main/doc/UNT0007.md>
- `UNT0008`：<https://github.com/microsoft/Microsoft.Unity.Analyzers/blob/main/doc/UNT0008.md>
- `UNT0023`：<https://github.com/microsoft/Microsoft.Unity.Analyzers/blob/main/doc/UNT0023.md>
- `UNT0029`：<https://github.com/microsoft/Microsoft.Unity.Analyzers/blob/main/doc/UNT0029.md>
- Visual Studio Tools for Unityの診断説明：<https://github.com/MicrosoftDocs/visualstudio-docs/blob/main/gamedev/unity/change-log-visual-studio-tools-for-unity.md>
