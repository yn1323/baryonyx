# Unityオブジェクトの意味

この資料は、`UnityEngine.Object`を純粋なC#参照として扱えない場面を整理する。対象はUnity 6（プロジェクトの実バージョンが優先）である。

## 1. 管理対象とネイティブ実体

`GameObject`、`Component`、`MonoBehaviour`、`ScriptableObject`、Prefabや各種アセットは、C#の管理対象オブジェクトからUnityのネイティブ実体へつながっている。`Destroy`などでネイティブ実体が破棄されても、C#のラッパーがすぐGCされるとは限らない。この状態はUnityのドキュメントでdetached stateと説明されている。

Unityは`UnityEngine.Object`の`==`、`!=`、`bool`を上書きし、管理対象が残っていてもネイティブ実体がない場合を存在しないものとして扱う。

```csharp
if (component == null)
{
    return;
}

if (component != null)
{
    component.DoSomething();
}

if (component)
{
    component.DoSomething();
}
```

`ReferenceEquals(component, null)`はC#の管理対象参照だけを確認するため、Unityの破棄済み判定にはならない。`is null`も演算子オーバーロードを使わないため同じ注意が必要である。

## 2. null構文の判断表

| 書き方 | Unity型の存在判定 | 方針 |
|---|---|---|
| `obj == null` / `obj != null` | Unityの`==`/`!=`を使う | 使用する |
| `if (obj)` | Unityの`bool`を使う | 短い条件では可。意味を明確にするコードは`!= null` |
| `obj?.Member` | C#の参照nullだけを確認し、Unityのdetached状態を扱えない | 使用しない |
| `obj?[index]` | null条件インデックスアクセスも同じ | 使用しない |
| `obj ?? fallback` | C#のnull合体で、Unityの`==`を使わない | 使用しない |
| `obj ??= fallback` | C#のnull合体代入で、Unityの`==`を使わない | 使用しない |
| `obj is null` / `obj is not null` | nullパターンで、Unityの`==`を使わない | 使用しない |
| `obj ?? throw ...` | null合体とthrow式の組み合わせ | 明示的な`if`へ分解 |
| `ReferenceEquals(obj, null)` | 管理対象参照だけを確認する | Unityの存在確認には使用しない |
| `obj is Component` / `obj as Component` | 型の一致・キャストであり、実体が生きている保証ではない | 続けて`obj != null`を確認 |

純粋なC#クラス、`string`、`List<T>`などにはこのUnity固有の制限はない。ただし、フィールドが`UnityEngine.Object`の基底型として宣言されている場合は、実際の代入型に関係なくUnity型として扱う。

## 3. 生成

### `MonoBehaviour`・`Component`

`MonoBehaviour`や`Component`は、通常のC#クラスのように`new`して使わない。GameObjectにアタッチされてUnityがライフサイクルを管理するため、次を使う。

```csharp
var component = gameObject.AddComponent<MyComponent>();
var clone = UnityEngine.Object.Instantiate(prefab);
```

文字列を受け取る古い`AddComponent`は非推奨。ジェネリック版か`Type`版を使う。

### `ScriptableObject`

```csharp
var instance = ScriptableObject.CreateInstance<MyData>();
```

保存対象のアセットはEditor側でアセット作成APIを使う。Runtimeアセンブリから`AssetDatabase`を参照しない。

## 4. 破棄

```csharp
Destroy(component);          // 通常のRuntime破棄。現在のUpdate処理後に反映
Destroy(gameObject);         // GameObjectと子階層を破棄
DestroyImmediate(asset);     // Editor専用。Runtimeでは原則使わない
```

`Destroy(component)`はコンポーネントだけを取り除き、GameObjectは残す。GameObject全体を消す場合は`Destroy(component.gameObject)`を使う。`Destroy`を呼んだ直後の同一フレームでは、処理の流れを状態フラグで止めるか、Unityの`!= null`で確認する。

`DestroyImmediate`はEditor処理など明確な理由がある場合だけにし、アセットを破壊する引数は特に慎重に扱う。Editorの選択オブジェクトを削除する処理ではUndo対応の要否も確認する。

## 5. 典型的な修正

```csharp
// 避ける
button?.onClick.AddListener(HandleClick);
target ??= FindTarget();
if (target is not null) { }

// Unityの存在判定を使う
if (button != null)
{
    button.onClick.AddListener(HandleClick);
}

if (target == null)
{
    target = FindTarget();
}

if (target != null)
{
    HandleTarget(target);
}
```

## 参照

- Unity 6 `UnityEngine.Object`：<https://docs.unity3d.com/ja/6000.0/ScriptReference/Object.html>
- Unity 6 `Object.operator ==`：<https://docs.unity3d.com/ja/current/ScriptReference/Object-operator_eq.html>
- Unity 6 `Object.bool`：<https://docs.unity3d.com/ja/6000.0/ScriptReference/Object-operator_Object.html>
- `GameObject.AddComponent`：<https://docs.unity3d.com/ja/current/ScriptReference/GameObject.AddComponent.html>
- `Object.Destroy`：<https://docs.unity3d.com/ja/current/ScriptReference/Object.Destroy.html>
- `Object.DestroyImmediate`：<https://docs.unity3d.com/ja/6000.0/ScriptReference/Object.DestroyImmediate.html>
