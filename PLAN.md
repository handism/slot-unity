# Fantasy Slot 実装計画（PLAN.md）

**作成日**: 2026-03-20
**対象**: Unity 6.3 LTS / PC (Windows/macOS) / 1人開発

## Context

ドキュメント（要件定義書・設計書・ADR × 6）が完全に整備された状態から実装を開始する。
Assets/ ディレクトリは存在しないため、Unity プロジェクトのゼロから構築が必要。
MVP アーキテクチャ・UniTask・ScriptableObject によるパラメータ管理が確定済み。

---

## フォルダ構成（Assets/ 以下）

```
Assets/
├── Scenes/                    # Boot.unity / Main.unity / BonusRound.unity
├── Scripts/
│   ├── Core/                  # GameManager, SpinManager, BonusManager, ReelController
│   ├── Model/                 # GameState, SpinResult, SaveData（ピュア C#）
│   ├── View/                  # UIManager, ReelView, BonusRoundView, 各 HUD
│   ├── Audio/                 # AudioManager
│   ├── Data/                  # ScriptableObject クラス定義
│   └── Utility/               # PaylineEvaluator, SaveDataManager, IRandomGenerator
├── ScriptableObjects/
│   ├── Symbols/               # Dragon〜Scatter の .asset × 10
│   ├── Reels/                 # Reel0〜Reel4.asset × 5
│   ├── Paylines/              # PaylineData.asset
│   └── PayoutTable/           # PayoutTableData.asset
├── Art/Sprites/ / Art/Animations/ / Audio/BGM/ / Audio/SE/
└── Tests/EditMode/ / Tests/PlayMode/
```

## 技術選定補足（Unity 6.3 ベストプラクティス）

| 技術              | 採用   | 理由                                                                       |
| ----------------- | ------ | -------------------------------------------------------------------------- |
| URP 2D            | 採用   | Unity 6.3 デフォルト。新規プロジェクトは `Universal 2D` テンプレートで作成 |
| New Input System  | 採用   | Space=Spin 等のショートカット管理に使用。UGUI の Button は OnClick() 継続  |
| Addressables      | 不採用 | BonusRound.unity の Additive ロードのみ。`LoadSceneAsync` で十分           |
| UI Toolkit        | 不採用 | UGUI + DOTween + TextMeshPro の組み合わせで実績あり                        |
| Unity 6 Awaitable | 不採用 | `WhenAll` 等が不足。UniTask が上位互換（ADR-005）                          |

---

## フェーズ 0: プロジェクト初期設定

- [x] Unity 6.3 LTS で新規プロジェクト作成（テンプレート: `Universal 2D`）
- [x] ProjectSettings: 解像度 1920×1080、16:9 固定
- [x] UPM パッケージ導入
  - [x] UniTask（GitHub URL 経由）
  - [x] DOTween（Asset Store）+ Setup 実行
  - [x] TextMeshPro Essential Resources インポート
  - [x] New Input System（`Both` モードで互換性確保）
- [x] Assembly Definition（`.asmdef`）を各フォルダに作成
  - `SlotGame.Model`（Unity 参照なし）/ `SlotGame.Data` / `SlotGame.Core` / `SlotGame.View` / `SlotGame.Utility` / `SlotGame.Tests.EditMode` / `SlotGame.Tests.PlayMode`
- [x] フォルダ構成を作成

---

## フェーズ 1: Model 層（ピュア C#）＋ ユニットテスト

**依存フェーズなし。最初に実装してロジックの正確性を保証する。**

### 1-1. ScriptableObject データ定義クラス（`Scripts/Data/`）

- [x] `SymbolData.cs`（`symbolId`, `symbolName`, `SymbolType enum`, `payouts[3]`, `sprite`, `winAnim`）
- [x] `ReelStripData.cs`（`reelIndex`, `List<SymbolData> strip`）
- [x] `PaylineData.cs` — **注意**: 設計書の `Vector2Int[] lines` は 5 列に対応不可。以下の構造を採用
  ```csharp
  [Serializable] public struct PaylineEntry { public int[] rows; } // 要素数 5
  public PaylineEntry[] lines; // 要素数 25
  ```
- [x] `PayoutTableData.cs`（`ScatterPayout[]`, `BonusRewardEntry[]`）

### 1-2. Model クラス（`Scripts/Model/`）

- [x] `GameState.cs`
  - 全プロパティ `get; private set;`
  - コイン内部型は `long`（ボーナス重畳時の中間値オーバーフロー防止）、外部クランプ上限 9,999,999
  - コイン変化を通知する `event Action<long> OnCoinsChanged`（View 側がポーリング不要に）
  - フリースピン残数変化を通知する `event Action<int> OnFreeSpinsChanged`
- [x] `SpinResult.cs`（`sealed record`、`IReadOnlyList<LineWin>` で保護）
- [x] `SaveData.cs`（`[Serializable]`、デフォルト値明記。coins は `long` で保存）
- [x] `IRandomGenerator.cs` インターフェース
- [x] `SystemRandomGenerator.cs`（`System.Random` ラップ）
- [x] `SeededRandomGenerator.cs`（テスト用固定シード）

### 1-3. PaylineEvaluator（`Scripts/Utility/`）

- [x] `PaylineEvaluator.cs`（static クラス）
  - `Evaluate(int[,] grid, SymbolData[] defs, PaylineData paylines, PayoutTableData payouts, int bet) : SpinResult`
  - Wild 置換: 左端から連続一致カウント時に Wild を現在シンボルとして扱う
  - 全 Wild ライン → 最高配当シンボル相当とする（仕様外のため設計書に追記）
  - Scatter はグリッド全体スキャン（ペイライン判定と独立）
  - ボーナス条件: `SymbolType.Bonus` のシンボルがリール 0/2/4 全てに出現で発動
    - `SymbolType` enum に `Bonus` を追加して `Scatter` と明確に分離

### 1-4. SaveDataManager（`Scripts/Utility/`）

- [x] `SaveDataManager.cs`（コンストラクタで保存パスを DI、テスト可能にする）
  - `Load()`: ファイル不存在→デフォルト、JSON パース失敗→`.bak` リネーム後デフォルト
    - 読み込み時に整合性ハッシュ（SHA256）を検証。不一致は破損扱い
  - `SaveAsync(CancellationToken ct) : UniTask`: **アトミックな書き込み**
    - `savedata.json.tmp` に書き込み → 成功したら `File.Move(overwrite:true)` でリネーム
    - 書き込み途中でアプリが落ちても元ファイルを破損させない
    - ハッシュ（JSON文字列の SHA256 hex）を `savedata.json.hash` に同時保存
  - ダーティフラグ `_isDirty` を持ち、変更がない場合は Save をスキップ
  - `Validate()`: コイン範囲・ベット値・バージョン等 5 条件チェック

### 1-5. Edit Mode ユニットテスト（**必須**、`Tests/EditMode/`）

- [x] `PaylineEvaluatorTests.cs`
  - [x] 3/4/5 揃えの配当計算
  - [x] Wild 置換（Wild+2Normal=3揃え、Wild+Wild+Normal=3揃え）
  - [x] 全 Wild ライン判定
  - [x] Scatter 3/4/5 個判定
  - [x] ボーナス条件発動・非発動
  - [x] 複数ライン同時当選（配当合算）
  - [x] ハズレ（配当 0）
- [x] `GameStateTests.cs`
  - [x] `DeductBet()` が残高不足で `false` を返す
  - [x] コイン上限クランプ
  - [x] `FreeSpinsLeft` が 0 未満にならない
  - [x] `TotalSpins` インクリメント / `MaxWin` 更新
- [x] `SaveDataManagerTests.cs`
  - [x] 正常な JSON 読み込み
  - [x] ファイル不存在でデフォルト
  - [x] 破損 JSON で `.bak` 生成 + デフォルト
  - [x] 保存 → 読み込みのラウンドトリップ

---

## フェーズ 2: ScriptableObject アセット作成

**フェーズ 1 完了後に実施（データ定義クラスが確定してから値を入力）**

- [x] `SymbolData` アセット × 11（Dragon〜Bonus）
  - 配当倍率を `docs/requirements.md` の値に従って設定
  - `SymbolType.Bonus` シンボル（ID=10、宝箱用）を追加済み
  - **生成方法**: Unity Editor で `SlotGame/Create All ScriptableObject Assets` を実行
- [x] `ReelStripData` アセット × 5（各リール 88 スロット）
  - Dragon×2, Phoenix×3, Crystal×4, Sword×5, Ace×10, King×10, Queen×10, Jack×9, Wild×3, Scatter×3, Bonus×3, Blank×26
  - 素数ステップ（7）インターリーブで均等分散、リールごとにオフセット
- [x] `PaylineData` アセット（25 ライン定義を `requirements.md` から転記済み）
- [x] `PayoutTableData` アセット
  - Scatter 配当: 3個→×2, 4個→×10, 5個→×50
  - ボーナス報酬重み: ×5(w40), ×10(w25), ×20(w15), ×30(w10), ×50(w7), ×100(w3)（暫定）
  - **生成方法**: Unity Editor で `SlotGame/Create All ScriptableObject Assets` を実行

---

## フェーズ 3: Core / Presenter 層実装

**フェーズ 1 + 2 完了後に実施**

### 3-1. ReelController（`Scripts/Core/`）

- [x] `ReelController.cs`
  - `StartSpin()`: UniTask ループでスクロール開始
  - `StopSpin(int stopIndex, CancellationToken ct)`: 減速 → DOTween `Ease.OutBounce` でスナップ
  - `GetVisibleSymbolIds()`: 中段の行を基準に上段・下段 ID を返す
  - ストリップ循環: `(index + length) % length` でラップ

### 3-2. SpinManager（`Scripts/Core/`）

- [x] `SpinManager.cs`
  - `ExecuteSpin(CancellationToken ct) : UniTask<SpinResult>`
  - 乱数で全リール停止位置を決定
  - 全リール同時 `StartSpin()` → 最低 2 秒後に 0.3 秒間隔で順次 `StopSpin()`
  - 早期停止フラグ `_skipRequested`（スピン中のボタン押下で全リール即スナップ）
  - `UniTask.WhenAll` で全リール停止を待機 → `PaylineEvaluator.Evaluate()` を呼び結果を返す
  - `IRandomGenerator` を `BootManager`（コンポジションルート）から受け取る（DI）

### 3-3. BonusManager（`Scripts/Core/`）

- [x] `BonusManager.cs`
  - `RunFreeSpins(GameState state, int count, Func<SpinResult, UniTask> onSpin, CancellationToken ct)`
    - `state.AddFreeSpins(count)` → ループ、再トリガー時は `+count`（上限 +20）
  - `RunBonusRound(int betAmount, PayoutTableData payouts, CancellationToken ct) : UniTask<long>`
    - `LoadSceneAsync("BonusRound", Additive)` → `BonusRoundView` で選択待機 → アンロード

### 3-4. AudioManager（`Scripts/Audio/`）

- [x] `AudioManager.cs`
  - BGM 用 `AudioSource` × 1、SE 用 `AudioSource` × 最大 4（`AudioSource[]` プール）
  - BGM フェード: `DOTween.To` で volume を補間 → `UniTask` で完了待機
  - SE 再生制限: 同一クリップの同時発音を最大 3 に制限、0.05 秒以内の連続要求は間引き（DSP スパイク防止）
  - ボリューム設定は `SaveData` 経由（PlayerPrefs 不使用）

### 3-5. GameManager（`Scripts/Core/`）

- [x] `GameManager.cs`（ステートマシン。インスタンス参照は最小限に保ち、具体処理は各 Manager に委譲）
  - `GamePhase` enum: `Idle, Spinning, Evaluating, WinPresentation, BonusRound, FreeSpin, GameOver`
  - 各フェーズは `private async UniTask XxxPhase(CancellationToken ct)` に分離
  - `TransitionTo(GamePhase next)` でログ付き遷移
  - **遷移ガード**: `CanTransitionTo(GamePhase next) : bool` を持ち、無効な遷移を拒否する
    - `Spinning` 中は `Idle/Spinning/BonusRound` への遷移を拒否（設定パネル表示は View レベルで制限）
    - `TransitionTo()` 冒頭でガードを呼び出し、失敗時は `Debug.LogWarning` のみで処理継続しない
  - `SpinManager`, `BonusManager`, `AudioManager`, `UIManager`, `GameState`, `SaveDataManager` への参照は `BootManager` から注入される（GameManager 自身では `new` しない）
  - `OnApplicationPause/Focus` で `SaveDataManager.SaveAsync()` を呼び出し（`async void` ラッパー経由）
  - オートスピン: `CancellationTokenSource _autoSpinCts` で管理（`Dispose() → 再生成`）
  - ボーナスラウンド + フリースピン同時発動: ボーナスラウンド優先、終了後にフリースピン移行
  - `OperationCanceledException` は最上位（`GameManager` のフローメソッド）のみでキャッチ → `Idle` へ遷移
  - **キャンセル時クリーンアップ方針**: 各 `XxxPhase` メソッド内の DOTween Tween は `ct.Register(() => tween.Kill())` または `using` スコープで必ず終了させる。View の表示ステートは `TransitionTo(Idle)` 内でリセットする

---

## フェーズ 4: View 層実装

**フェーズ 3 の Presenter インターフェースが確定後に実施**

### 4-1. シーン構築（Main.unity）

- [x] Canvas（メイン）: `Screen Space - Camera`、Reference Resolution 1920×1080、`Scale With Screen Size`（Width/Height 0.5）
- [x] Canvas（HUD専用、別 Canvas）: `Screen Space - Overlay`
  - コイン・WIN表示の TMP_Text を独立 Canvas に分離
  - **理由**: スピン中のカウントアップ演出で毎フレーム更新されるテキストが、リールグリッドの Canvas リビルドを誘発しないようにする
- [x] 5×3 リールグリッド: 各リールは `RectMask2D` 付き Panel、内部に `SymbolView` × 5（バッファ込み）

### 4-2. ReelView（`Scripts/View/`）

- [x] `SymbolView.cs`（`Image` でスプライト表示、`PlayWinAnim()` で Animator 再生）
- [x] `ReelView.cs`
  - **循環バッファ方式**（5 シンボル固定、Instantiate/Destroy なし）
  - `Update()` で `localPosition.y` を加算、上端超え → 下端に再配置
  - `SnapToPosition(int stopIndex)`: DOTween `Ease.OutBounce`
  - `PlayWinAnimation(int row, AnimationClip clip) : UniTask`

### 4-3. 各 View パネル（`Scripts/View/`）

- [x] `MainHUDView.cs`
  - `Awake()` で `GameState.OnCoinsChanged` を購読 → DOTween `DOCounter` でカウントアップ表示
  - `GameState.OnFreeSpinsChanged` を購読 → フリースピンHUD の残数を更新
  - ベット選択ボタン
- [x] `WinPopupView.cs`（当選額 + DOTween スケールアニメ）
- [x] `FreeSpinHUDView.cs`（残り回数・累計コイン表示、`SetActive()` で切替）
- [x] `SettingsView.cs`（BGM/SE スライダー、コインリセットボタン）
- [x] `PaytableView.cs`（`SymbolData[]` から配当テーブルを動的生成）
- [x] `UIManager.cs`（上記 View のコーディネーター）

### 4-4. BonusRound シーン（BonusRound.unity）

- [x] `BonusRoundView.cs`
  - 9 箱を `GridLayout` で配置、選択後は非インタラクティブ化
  - `UniTaskCompletionSource<int[]>` で選択結果を返す
  - 開封アニメーション（DOTween スケール + 回転）後に報酬額表示

### 4-5. Boot シーン（Boot.unity）

- [x] `BootManager.cs`（**コンポジションルート**。全 Manager のインスタンス生成と依存注入を担う）
  1. `DOTween.Init()` 初期化
  2. `SaveDataManager` をインスタンス化し `LoadAsync()` でデータ読み込み → `GameState` を復元
  3. `IRandomGenerator`（`SystemRandomGenerator`）をインスタンス化
  4. `SpinManager`, `BonusManager`, `AudioManager` に必要な依存を注入
  5. `GameManager` に各 Manager・GameState・SaveDataManager を渡す
  6. `LoadSceneAsync("Main", Single)` で遷移

---

## フェーズ 5: RTP 検証・リールストリップ最終調整

**全システムが動いてからシミュレーション実施**

- [x] `RtpCalculator.cs`（Editor Only、`[MenuItem]`）を作成
  - 10 万回スピンシミュレーション + 期待値計算
  - 各シンボル出現確率・ライン当選確率・期待配当を CSV 出力
- [x] シミュレーション実行（目標: 通常スピン RTP ≈ 88〜90%）
- [x] リールストリップ調整 → 再シミュレーション
- [x] フリースピン込み RTP ≈ 94〜96% を確認
- [x] `ReelStripData` アセットを確定値に更新

---

## フェーズ 6: 統合テスト・最終調整

- [x] Play Mode テスト（`SpinFlowTests.cs`）
  - [x] 通常スピン 1 回のエンドツーエンドフロー
  - [x] フリースピン発動〜完走フロー
  - [x] オートスピン中断テスト
  - [x] ゲームオーバー → リセットフロー
- [x] 手動テストチェックリスト
  - [x] スピン中に早期停止ボタンが機能する
  - [x] フリースピン中にオートスピンが自動停止する
  - [x] ボーナスラウンドとフリースピン同時発動でボーナス優先
  - [x] コイン 0 でスピンボタンが無効化される
  - [x] BGM/SE ボリュームが再起動後も保存されている
  - [x] ウィンドウリサイズ・フルスクリーン切り替えでレイアウトが崩れない
  - [x] ペイテーブルの配当値が requirements.md の値と一致する
  - [x] セーブデータ破損時にデフォルト値でフォールバックする
- [x] Profiler で 60FPS 維持・GC Alloc スパイクなしを確認
- [x] Windows / macOS ビルド確認

---

## 設計書への修正事項

実装前に `docs/design.md` への追記が必要な点:

1. **PaylineData の型定義を修正**: `Vector2Int[] lines` → `PaylineEntry[] lines`（5 列 × 25 ライン対応）
2. **全 Wild ライン の配当ルールを明記**: 最高配当シンボル（Dragon）と同等扱い
3. **ボーナスシンボルの定義を追記**: `SymbolType.Bonus` を新設、Scatter とは独立したシンボルとする
4. **GameState の coins 型を `long` に変更**: ボーナス重畳時の中間値オーバーフロー防止
5. **イベント通知パターンを追記**: `GameState` が `OnCoinsChanged`/`OnFreeSpinsChanged` を持ち View が購読する一方向データフロー
6. **BootManager をコンポジションルートとして追記**: 全依存注入の起点
7. **SaveDataManager の非同期 I/O を明記**: `SaveAsync(UniTask)` で同期ブロッキングを排除

---

## 依存関係（実装順序の根拠）

```
フェーズ 0（環境構築）
    ↓
フェーズ 1（Model + Tests）← ロジックの正確性を最初に保証
    ↓
フェーズ 2（ScriptableObject）← Model 確定後に値を入力
    ↓
フェーズ 3（Presenter）← Model + Data が揃ってから
    ↓
フェーズ 4（View）← Presenter の I/F 確定後
    ↓
フェーズ 5（RTP 検証）← 全システム動作後
    ↓
フェーズ 6（統合テスト）← 最終確認
```

## 参照ドキュメント

- `docs/requirements.md` — シンボル配当・ペイライン定義・ボーナス条件の確定値
- `docs/design.md` — クラス設計・シグネチャ定義（上記修正事項を反映後に参照）
- `docs/adr/ADR-005-async-unitask.md` — キャンセレーション戦略
- `docs/adr/ADR-004-scriptableobject-config.md` — パラメータ外出し方針
- `docs/adr/ADR-006-25-paylines.md` — ペイライン定義
