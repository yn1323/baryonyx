# 調査ソースとバージョン方針

Unity APIやC#の挙動を断定する前に、プロジェクトのUnityバージョンと対象パッケージに合う一次資料を確認する。検索結果のスニペット、古いブログ、フォーラム回答だけを根拠にしない。

## このSkillの主な一次資料

### Unity 6 Scripting API / Manual

- [`UnityEngine.Object`](https://docs.unity3d.com/ja/6000.0/ScriptReference/Object.html)：detached state、`==`、`!=`、`bool`、`?.`、`??`の制約。
- [`Object.operator ==`](https://docs.unity3d.com/ja/current/ScriptReference/Object-operator_eq.html)：managed参照とnative pointerの両方を確認するUnityの等価演算子。
- [`Object.bool`](https://docs.unity3d.com/ja/6000.0/ScriptReference/Object-operator_Object.html)：`if (obj)`と`obj != null`が同じ存在判定になること。
- [`Object.Destroy`](https://docs.unity3d.com/ja/current/ScriptReference/Object.Destroy.html) / [`DestroyImmediate`](https://docs.unity3d.com/ja/6000.0/ScriptReference/Object.DestroyImmediate.html)：破棄タイミングとEditor/Runtime境界。
- [`GameObject.AddComponent`](https://docs.unity3d.com/ja/current/ScriptReference/GameObject.AddComponent.html)：コンポーネント生成とジェネリックAPI。
- [`ScriptableObject.CreateInstance`](https://docs.unity3d.com/ja/current/ScriptReference/ScriptableObject.CreateInstance.html)：ScriptableObject生成。
- [`Component.TryGetComponent`](https://docs.unity3d.com/ja/current/ScriptReference/Component.TryGetComponent.html) / [`GetComponent`](https://docs.unity3d.com/ja/current/ScriptReference/GameObject.GetComponent.html)：取得・null・allocation。
- [`CompareTag`](https://docs.unity3d.com/ja/current/ScriptReference/Component.CompareTag.html)：タグ判定。
- [`イベント関数の実行順序`](https://docs.unity3d.com/ja/6000.0/Manual/execution-order.html)：Awake、OnEnable、Start、Update、FixedUpdate、OnDestroyなど。
- [`シリアル化のルール`](https://docs.unity3d.com/ja/6000.0/Manual/script-serialization-rules.html)：フィールド、`SerializeField`、`SerializeReference`、対応型。
- [`MonoBehaviour`](https://docs.unity3d.com/jp/current/Manual/class-MonoBehaviour.html)：コンストラクターを使わない理由、アタッチ、Start/Update、コルーチン。
- [`RuntimeInitializeOnLoadMethod`](https://docs.unity3d.com/ja/6000.0/ScriptReference/RuntimeInitializeOnLoadMethodAttribute.html)：Player起動時のstaticコールバックと実行段階。
- [`InitializeOnLoadMethod`](https://docs.unity3d.com/jp/current/ScriptReference/InitializeOnLoadMethodAttribute.html)：Editorロード時のstaticコールバック。

### Microsoft / C# / Analyzer

- [Microsoft.Unity.Analyzers診断一覧](https://github.com/microsoft/Microsoft.Unity.Analyzers/blob/main/doc/index.md)：`UNT0001`〜`UNT0046`の現行一覧とカテゴリ。
- [`UNT0007` null coalescing](https://github.com/microsoft/Microsoft.Unity.Analyzers/blob/main/doc/UNT0007.md)：`??`。
- [`UNT0008` null propagation](https://github.com/microsoft/Microsoft.Unity.Analyzers/blob/main/doc/UNT0008.md)：`?.`と`?[]`。
- [`UNT0023` coalescing assignment](https://github.com/microsoft/Microsoft.Unity.Analyzers/blob/main/doc/UNT0023.md)：`??=`。
- [`UNT0029` pattern matching](https://github.com/microsoft/Microsoft.Unity.Analyzers/blob/main/doc/UNT0029.md)：`is null` / `is not null`。
- [C# null operators](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/null-safety/null-operators)：C#標準のnull演算子の意味。Unityの挙動と混同しない。
- [C# equality operators](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/equality-operators)：`==`のオーバーロードと参照等価の違い。

## 調査の手順

1. `client/ProjectSettings/ProjectVersion.txt`からUnityの実バージョンを読む。
2. Unity公式URLのバージョンを合わせる。Unity 6.0（6000.0）の資料しかない場合は、プロジェクト版（例: 6000.6.0f1）との差分がないかリリースノート・API変更履歴を確認する。
3. C#言語仕様の意味とUnity APIの実装・運用規則を分ける。`?.`はC#構文、`UnityEngine.Object`の`==`はUnity APIの演算子である。
4. Analyzerの診断ID、プロジェクトのAnalyzerパッケージ版、CIの重大度を確認する。
   このプロジェクトでは`client/Assets/Analyzers/Microsoft.Unity.Analyzers/Microsoft.Unity.Analyzers.dll`が生成`.csproj`から参照されているため、Unity公式の挙動とローカルAnalyzerの有効ルールを分けて確認する。
5. 公式資料にない判断は「推奨」や「このプロジェクトの方針」として明示し、Unityの仕様と断定しない。

## Web検索の検索語

- `site:docs.unity3d.com/ja/6000.0/ScriptReference UnityEngine.Object operator bool null`
- `site:docs.unity3d.com/ja/6000.0/Manual script serialization rules execution order`
- `site:github.com/microsoft/Microsoft.Unity.Analyzers/doc UNT00 null Unity objects`
- `site:learn.microsoft.com C# null conditional operator overloaded equality`
