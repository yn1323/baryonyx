# Unityの実行・保存・API規則

ここでは、C#の文法は正しくてもUnityの実行モデルで意味が変わる規則をまとめる。

## 1. イベント関数と実行順

Unityは特定の名前のメソッドをメッセージとして呼び出す。名前、大文字小文字、引数、戻り値が違うと、通常のメソッドとしてはコンパイルできてもUnityから呼ばれない、またはアナライザーが指摘する。

| 段階 | 主なメソッド | 判断 |
|---|---|---|
| 生成・有効化 | `Awake`、`OnEnable` | 自身の初期化と購読。別オブジェクトの`Awake`順を前提にしない |
| 初回開始 | `Start` | シーン内の依存関係が揃ってからの初期化 |
| 毎フレーム | `Update`、`LateUpdate` | 入力、通常のゲームロジック、カメラ追従 |
| 固定時間 | `FixedUpdate` | Rigidbodyなどの物理更新。`Time.fixedDeltaTime` |
| 無効化・破棄 | `OnDisable`、`OnDestroy` | イベント購読解除、リソース解放 |
| Editor | `Reset`、`OnValidate` | Inspector変更や編集時の検証。Runtime副作用を起こさない |

同一オブジェクトでは`Awake`、`OnEnable`、`Start`の順を使えるが、異なるGameObject間での呼び出し順は前提にしない。`Script Execution Order`を使う場合は、依存関係と対象アセンブリを明記する。

### 属性によるコールバック

- `[RuntimeInitializeOnLoadMethod]`はPlayer起動時の指定段階に呼ばれるstaticメソッド。`BeforeSceneLoad`、`AfterSceneLoad`などで、シーンオブジェクトが存在する時点が異なる。
- `[InitializeOnLoad]`はEditorロード時にstaticコンストラクターを呼ばせる属性。staticコンストラクターの副作用と再コンパイルを考慮する。
- `[InitializeOnLoadMethod]`はEditorロード時のstaticメソッド。Runtimeアセンブリへ置かない。
- `[MenuItem]`はEditorメニューのstaticメソッドに付ける。引数・戻り値・検証用のvalidate関数の形を確認する。

## 2. Inspectorとシリアル化

Unityの標準シリアライザーは、通常プロパティではなくフィールドへ直接作用する。

```csharp
[SerializeField] private string nextSceneName = "Home";
public string NextSceneName => nextSceneName;
```

この形なら、Inspectorに保存するフィールドとコードから公開する読み取り専用プロパティを分けられる。

通常シリアル化される主な条件は次のとおり。

- `public`、または`[SerializeField]`が付いた非staticフィールド。
- `const`、`readonly`ではない。
- primitive、enum、Unity組み込み型、`[Serializable]`なstruct、`UnityEngine.Object`参照、配列、`List<T>`など対応型。

通常対応しないもの、または追加設計が必要なものは次のとおり。

- プロパティ、辞書、多次元配列、ジャグ配列、入れ子のコンテナ。
- 純粋C#クラスのnull、共有参照、循環参照、ポリモーフィズム。必要なら`[SerializeReference]`を使い、データの安定性とInspector表示を確認する。
- フィールド名変更。既存シーン・Prefab・アセットを守る場合は`[FormerlySerializedAs("OldName")]`を検討する。

シリアル化された値を`Awake`で必ず上書きしない。Inspector設定、Prefab差分、アセット参照を保つ責務を明確にする。

## 3. コンポーネント・タグ・検索

```csharp
var image = GetComponent<Image>();
if (!TryGetComponent<Button>(out var button))
{
    return;
}

if (other.CompareTag("Player"))
{
    // タグ判定
}
```

- `GetComponent<T>()`は型安全なジェネリック版を基本にする。無い場合は`null`なのでUnityの`!= null`で判定する。
- `TryGetComponent<T>`は取得と存在確認を一度に行う。Editorで見つからない場合の不要な割り当ても避けやすい。
- タグは`CompareTag`を使う。文字列プロパティを毎回比較しない。
- `Find`系APIを毎フレーム呼ばない。所有者が変わらない参照は初期化時に取得して保持する。
- `[RequireComponent(typeof(Button))]`などで自分が必要とするコンポーネントを宣言し、`GetComponent`だけに依存しない。

## 4. 時間・物理・コルーチン

- `Update`はフレームごとに呼ばれ、フレーム間隔は一定ではない。移動・補間などの経過時間には`Time.deltaTime`を使う。
- `FixedUpdate`は固定時間間隔で呼ばれ、物理計算に使う。`Time.fixedDeltaTime`を使い、`Update`からRigidbodyを直接更新する設計を避ける。
- `LateUpdate`は`Update`後。追従カメラなど、対象の更新を待ってから行う処理に使う。
- コルーチンはUnityが`IEnumerator`を進める仕組み。`yield return null`、`WaitForSeconds`、`WaitForFixedUpdate`の時点を確認し、`StartCoroutine`した所有者が無効・破棄された場合のキャンセルを設計する。
- ループ内で同じ`WaitForSeconds`を大量に生成しない。待機値が固定なら再利用を検討する。

## 5. EditorとRuntimeの境界

Runtimeスクリプトから`UnityEditor`、`AssetDatabase`、`Selection`、`MenuItem`、`Undo`を参照しない。Editor専用処理は`Editor`フォルダー、Editor asmdef、または適切な条件付きコンパイルで分離する。

`OnValidate`、`Reset`、`InitializeOnLoadMethod`、`MenuItem`はEditorで何度も呼ばれ得る。アセットを書き換える、Prefabを生成する、外部サービスを呼ぶ処理は冪等性とUndo・保存状態を設計する。

## 6. 生成アセット・シーンの注意

- 新しいシーンやPrefabを追加したら、Build Settings、参照GUID、展示室やカタログなどプロジェクト固有の登録も確認する。
- シーン遷移は`SceneManager.LoadScene`だけで完了とせず、Build Settingsの登録、遷移先のEventSystem/Input、戻る・連打・未登録シーンのエラーを検証する。
- `DontDestroyOnLoad`を使う場合は、重複生成、シーン再入場、破棄順を設計する。

## 参照

- Unity 6イベント関数の実行順序：<https://docs.unity3d.com/ja/6000.0/Manual/execution-order.html>
- Unity 6シリアル化ルール：<https://docs.unity3d.com/ja/6000.0/Manual/script-serialization-rules.html>
- Unity 6 `MonoBehaviour`：<https://docs.unity3d.com/jp/current/Manual/class-MonoBehaviour.html>
- `TryGetComponent<T>`：<https://docs.unity3d.com/ja/current/ScriptReference/Component.TryGetComponent.html>
- `CompareTag`：<https://docs.unity3d.com/ja/current/ScriptReference/Component.CompareTag.html>
- `RuntimeInitializeOnLoadMethod`：<https://docs.unity3d.com/ja/6000.0/ScriptReference/RuntimeInitializeOnLoadMethodAttribute.html>
- `InitializeOnLoadMethod`：<https://docs.unity3d.com/jp/current/ScriptReference/InitializeOnLoadMethodAttribute.html>
