# PR #6 レビュー

**日付:** 2026-03-23
**対象ブランチ:** `feature/code-optimization-and-security`, `feature/title-scene`
**レビュアー:** Claude

---

## 重大度：HIGH（マージ前に要修正）

### 1. アーキテクチャ違反 — `GameConfigData`（ScriptableObject）がModel/Utility層に侵入
**対象:** `Assets/Scripts/Utility/SaveDataManager.cs`, `Assets/Scripts/Utility/PaylineEvaluator.cs`

`GameConfigData` は `UnityEngine.ScriptableObject` を継承しており、CLAUDE.mdの「Model はピュア C# にする（`UnityEngine` への依存を持たせない）」ルールに違反しています。
`SaveDataManager` と `PaylineEvaluator`（静的クラス）への引数注入により、Unity依存がない純粋C#層に Unity 型が持ち込まれています。

**修正方法:** `GameConfigData` から必要な値を抽出してプリミティブ型または plain-C# の struct/record で渡す。

---

### 2. NullReferenceException リスク — `gameConfig` のnullガード漏れ
**対象:** `Assets/Scripts/Core/GameManager.cs` (`Initialize` メソッド)

InspectorでSerializeFieldの`gameConfig`が未設定の場合、`gameConfig.defaultBgmVolume` 等へのアクセスで実行時クラッシュします。デバッグパス（`else`ブランチ）も同様。

---

### 9. タイトル画面がComposition Root（DI）をスキップ
**対象:** `Assets/Scripts/Core/TitleManager.cs`

```csharp
public void StartGame()
{
    SceneManager.LoadScene("Main"); // 同期ロード
}
```

`BootManager` が行うDI設定（`GameContext.GameState`、`SaveDataManager` 等）を完全にバイパスしています。`GameManager.Awake()` 実行時に `GameContext.GameState == null` となり、開発用フォールバックパスに入ります。
`TitleManager` から `Boot` シーンを経由するか、非同期ロード＋DI設定を再現する必要があります。

---

## 重大度：MEDIUM（要対応）

### 3. チェックサムsaltがInspectorに平文露出
**対象:** `Assets/Scripts/Data/GameConfigData.cs`

```csharp
public string checksumSalt = "SALTY_SLOT_2026";
```

ScriptableObjectアセットはYAMLでシリアライズされるため、saltが容易に読み取れます。ユーザーがsaltを取得してSHA-256を自前計算することで、任意のコイン値に有効なチェックサムを生成できます。「セキュリティ向上」の効果が実質無効です。

---

### 4. 「新規ゲーム判定」のロジックバグ
**対象:** `Assets/Scripts/Core/GameManager.cs` (`Initialize` メソッド)

```csharp
if (save.totalSpins == 0) // Assume new game
```

セーブデータが破損して `RecoverFromCorruption()` → `new SaveData()` に戻った場合も `totalSpins == 0` になり、ユーザーが設定した音量設定が上書きされます。旧コードはsaveから直接読み込む設計であり、そちらが正しい挙動です。

---

### 5. テスト回帰 — ベット額バリデーションテストの削除
**対象:** `Assets/Tests/EditMode/SaveDataManagerTests.cs`

`Load_InvalidBetAmount_ReturnsDefault` テストが削除され、代替テストは `config == null` で呼び出すため、ベット額バリデーションのパスを全くテストしていません。`validBetAmounts` 付きの `GameConfigData` を渡すテストケースを追加してください。

---

### 10. Boot→Title→Main のDI連携が暗黙的で脆弱
**対象:** `Assets/Scripts/Core/BootManager.cs`（タイトルブランチ）

静的な `GameContext` がシーン遷移を超えて生き残ることに暗黙的に依存しており、実行順序が変わると壊れます。明示的にDIデータを受け渡す設計にしてください。

---

## 重大度：LOW（改善推奨）

| # | ファイル | 内容 |
|---|---------|------|
| 6 | `SpinManager.cs` | `Initialize()` 再呼び出し時に `_cachedSymbolDefs` がクリアされず古い辞書が残る |
| 7 | `PaylineEvaluator.cs` | `reelCount` は設定可能だがボーナストリガー位置（0, 2, 4）が依然ハードコード。部分的な設定可能性が誤解を招く |
| 8 | `UIManager.cs` | `PaylineView` プールに上限なし。長時間プレイでメモリが増加し続ける |
| 11 | `TitleManager.cs` | テストカバレッジなし |

---

## 総評

コードの最適化（辞書ルックアップ、オブジェクトプール）の方向性は良いですが、**アーキテクチャ違反（#1）とDIスキップ（#9）** は設計の根幹に関わるため、マージ前の修正が必須です。
特に `GameConfigData` をModel/Utility層に渡す設計は、Edit Mode テスタビリティを損なう重大な問題です。
