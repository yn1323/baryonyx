---
name: unity-csharp-differences
description: Unity 6のC#コードを実装・レビューするとき、標準C#とUnityEngine.Object、Unityのライフサイクル、シリアル化、Editor API、アナライザー規則の違いを確認し、安全な書き方と検証方法を選ぶ。
---

# Unity C#差分ガイド

Unityのコードは通常のC#としてコンパイルされるが、`UnityEngine.Object`のネイティブ実体、Unityが名前で呼ぶイベント、Inspectorのシリアライザー、Editor専用APIによって、標準C#と同じ書き方が同じ意味にならないことがある。このSkillは、Unity 6プロジェクトのコードを書く・直す・レビューする際に、実装前に差分を特定し、適切なUnity APIと検証を選ぶために使う。

## 発動したら最初に確認すること

1. `ProjectSettings/ProjectVersion.txt`、対象の`.asmdef`、`AGENTS.md`、既存のコーディング規約を確認する。`Assets/Analyzers/Microsoft.Unity.Analyzers/Microsoft.Unity.Analyzers.dll`と生成された`.csproj`のAnalyzer参照も確認し、実際に有効な診断を特定する。Unityのバージョンが変わっている場合は、記憶でAPIを断定せず、対象バージョンのUnity公式ドキュメントをWeb検索する。
2. 変数の型を分類する。`UnityEngine.Object`（`GameObject`、`Component`、`MonoBehaviour`、`ScriptableObject`、アセットなど）か、純粋なC#クラス・構造体かを分ける。null、生成、保存、破棄の規則はこの分類で変わる。
3. 必要な詳細だけを参照する。null・生成・破棄は[Unityオブジェクトの意味](references/unity-object-semantics.md)、アナライザーは[Unityアナライザー規則](references/unity-analyzers.md)、Inspector・ライフサイクル・Editor APIは[Unity実行規則](references/unity-runtime-rules.md)を読む。公式URLとバージョンの確認には[調査ソース](references/research-sources.md)を使う。
4. 変更後は、コンパイルまたはプロジェクトのアナライザー検査、対象のEdit Mode/Play Modeテスト、必要ならUnity Editorでのシーン動作を分けて確認する。コードがコンパイルできたことだけで、Inspector保存・ライフサイクル・シーン遷移が正しいとは判断しない。

## 実装時の必須判断

### `UnityEngine.Object`のnull

`UnityEngine.Object`はC#の管理対象ラッパーとUnityのネイティブ実体を持つ。ネイティブ実体が破棄されてもラッパーが残る「detached」状態があるため、Unityの存在判定は次の形を使う。

```csharp
if (component != null)
{
    component.DoSomething();
}
```

`UnityEngine.Object`では、`?.`、`?[]`、`??`、`??=`、`is null`、`is not null`、`?? throw`をnull判定やフォールバックに使わない。`ReferenceEquals`と型パターン`is Component`は管理対象の参照だけを見るため、Unity上の「破棄済み」を存在判定したい処理には使わない。`if (component)`はUnityのbool演算子を使うが、レビューで意図を明確にする必要があるコードでは`component != null`を優先する。純粋なC#型にはこの制限を適用しない。

### 生成・破棄

- `MonoBehaviour`や`Component`を`new`しない。`GameObject.AddComponent<T>()`、Prefabの`Instantiate`、またはシーン・PrefabからUnityに生成させる。
- `ScriptableObject`は`ScriptableObject.CreateInstance<T>()`を使う。保存するアセットはEditor APIで`AssetDatabase.CreateAsset`などを使う。
- 実行時の破棄は通常`Destroy`を使う。`DestroyImmediate`はEditor処理など明確な理由がある場合だけに限定する。
- `Transform`だけを破棄したいのか、GameObject全体を破棄したいのかを区別する。GameObject全体なら`Destroy(transform.gameObject)`を使う。
- `Destroy`は現在のUpdate処理の後に反映される。破棄要求後に同じフレームで参照するコードを作らず、状態フラグや`!= null`判定を設計する。

### Unityが呼び出すメソッドと属性

`Awake`、`OnEnable`、`Start`、`Update`、`FixedUpdate`、`LateUpdate`、`OnDisable`、`OnDestroy`、`OnValidate`などはUnityのイベント関数である。名前、大文字小文字、引数、戻り値をUnityのシグネチャに合わせる。通常のC#メソッドのように自由に改名したり、コンストラクターで`MonoBehaviour`を初期化したりしない。

- 同じオブジェクトでは`Awake`→`OnEnable`→`Start`の順を前提にできるが、別オブジェクト間の`Awake`/`OnEnable`の順は前提にしない。依存解決は`Start`、明示的な初期化、または設計した実行順に置く。
- `Update`はフレーム依存、`FixedUpdate`は固定時間間隔である。物理更新は`FixedUpdate`と`Time.fixedDeltaTime`、通常のフレーム処理は`Update`と`Time.deltaTime`を使う。
- `[RuntimeInitializeOnLoadMethod]`、`[InitializeOnLoad]`、`[InitializeOnLoadMethod]`は通常の呼び出しとは異なる。対象メソッドのstatic要件、Editor/Playerの境界、実行時点を確認する。
- `[MenuItem]`はEditorメニューから呼ばれるstaticメソッドとして定義する。Editor APIをRuntimeアセンブリへ混ぜない。

### Inspectorシリアル化

Unityのシリアライザーは通常プロパティではなくフィールドを保存する。Inspectorで設定する値は`public`または`[SerializeField] private`のフィールドにし、`static`、`const`、`readonly`、非対応コンテナを無意識に使わない。ポリモーフィズム、null、参照共有、循環グラフが必要な純粋C#クラスには`[SerializeReference]`を検討する。フィールド名変更時は既存データを守るため`[FormerlySerializedAs]`の要否を確認する。

### Unity APIの選び方

- コンポーネント取得は`GetComponent<T>()`を基本とし、存在確認と取得を一度に行うときは`TryGetComponent<T>(out T value)`を使う。文字列版・非ジェネリック版は特別な理由がない限り使わない。
- タグ比較は`CompareTag`を使う。`tag == "Player"`の文字列比較へ置き換えない。
- コルーチンは`IEnumerator`と`yield return`だけで完結させず、`StartCoroutine`/`StopCoroutine`の所有者、破棄・無効化時の停止、待機時間の再利用を設計する。
- 入力・UI・物理・レンダリングは、プロジェクトで有効なパッケージと既存方式を確認してからAPIを選ぶ。旧InputとInput Systemを混ぜない。

## コード作成時のチェックリスト

- [ ] Unity型か純粋C#型かを確認した。
- [ ] Unity型のnull判定に`?.`、`??`、`??=`、`is null`、`ReferenceEquals`を使っていない。
- [ ] 生成・破棄を`new`、`AddComponent`、`CreateInstance`、`Instantiate`、`Destroy`の責務に分けた。
- [ ] Unityイベントの名前・シグネチャ・実行順を確認した。
- [ ] Inspectorで保存するフィールド、`SerializeReference`、旧フィールド名の互換性を確認した。
- [ ] `GetComponent<T>`、`TryGetComponent<T>`、`CompareTag`、適切なdelta timeを選んだ。
- [ ] Unityコンパイル、アナライザー、必要なEdit/Play Modeテストを実行し、Editor未確認や端末未確認を明記した。

## 参照資料

- null、破棄済みオブジェクト、`==`/`!=`/`bool`、`?.`/`??`の違い：[references/unity-object-semantics.md](references/unity-object-semantics.md)
- Microsoft.Unity.Analyzersの診断IDと修正方針：[references/unity-analyzers.md](references/unity-analyzers.md)
- シリアル化、ライフサイクル、Editor/Runtime境界、APIパターン：[references/unity-runtime-rules.md](references/unity-runtime-rules.md)
- Unity 6とC#の公式一次資料：[references/research-sources.md](references/research-sources.md)
