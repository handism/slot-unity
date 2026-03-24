# 設計書 — Fantasy Slot

**バージョン**: 1.0
**作成日**: 2026-03-20

---

## 1. システム全体構成

```
┌─────────────────────────────────────────────────────┐
│                    Unity Scene                      │
│                                                     │
│  ┌─────────────┐    ┌──────────────────────────┐   │
│  │  GameManager │◄──►│      UIManager           │   │
│  │ (State M/C) │    │  (View 統括)              │   │
│  └──────┬──────┘    └──────────────────────────┘   │
│         │                                           │
│    ┌────▼─────┐  ┌────────────┐  ┌──────────────┐  │
│    │  Spin    │  │  Bonus     │  │  Audio       │  │
│    │  Manager │  │  Manager   │  │  Manager     │  │
│    └────┬─────┘  └────┬───────┘  └──────────────┘  │
│         │             │                             │
│  ┌──────▼──────┐  ┌───▼────────┐                   │
│  │ReelController│  │BonusRound  │                   │
│  │  × 5        │  │Controller  │                   │
│  └──────┬──────┘  └────────────┘                   │
│         │                                           │
│  ┌──────▼──────────────────┐                       │
│  │   PaylineEvaluator      │                       │
│  └─────────────────────────┘                       │
│                                                     │
│  ┌─────────────────────────┐                       │
│  │   SaveDataManager       │  ← JSON永続化         │
│  └─────────────────────────┘                       │
└─────────────────────────────────────────────────────┘

[ScriptableObject 群]
  ReelStripData × 5
  SymbolData × 12
  PaylineData
  PayoutTableData
```

---

## 2. アーキテクチャ

### 2.1 採用パターン: MVP（Model-View-Presenter）

| 層 | 責務 | 主なクラス |
|----|------|-----------|
| Model | ゲーム状態・データ保持・ロジック | `GameState`, `ReelResult`, `PaylineEvaluator`, `SaveDataManager` |
| View | UI 表示・アニメーション再生のみ | `UIManager`, `ReelView`, `BonusRoundView` |
| Presenter | Model と View を繋ぐ・イベント処理 | `GameManager`, `SpinManager`, `BonusManager` |

**原則:**
- View は Model を直接参照しない
- Model は Unity の MonoBehaviour に依存しない（ピュア C#）
- GameManager がステートマシンとして全体フローを管理

---

## 3. ゲームステートマシン

```
         ┌──────────┐
    ┌───►│  Idle    │◄────────────────────┐
    │    └────┬─────┘                     │
    │         │ SpinStart                 │
    │    ┌────▼──────┐                    │
    │    │ Spinning  │                    │
    │    └────┬──────┘                    │
    │         │ AllReelsStopped           │
    │    ┌────▼──────────┐               │
    │    │  Evaluating   │               │
    │    └────┬──────────┘               │
    │         │                          │
    │   ┌─────┴──────┐                  │
    │   ▼            ▼                  │
    │  Win          NoWin               │
    │   │            │                  │
    │   │       ┌────▼──────┐           │
    │   │       │ CheckBonus│           │
    │   │       └────┬──────┘           │
    │   │            │                  │
    │   │    ┌───────┴──────────┐       │
    │   │    ▼                  ▼       │
    │   │  BonusRound      FreeSpin     │
    │   │    │                  │       │
    │   │    │ Complete    AllFree       │
    │   │    │             SpinDone     │
    │   └────┴──────────────────┴───────┘
    │
    │ GameOver (コイン=0)
    └──────────────────────────────────
```

---

## 4. クラス設計

### 4.1 ScriptableObject

```csharp
// シンボル定義
[CreateAssetMenu]
public class SymbolData : ScriptableObject
{
    public int       symbolId;
    public string    symbolName;
    public Sprite    sprite;
    public SymbolType type;          // Normal / Wild / Scatter / Bonus / Filler
    public int[]     payouts;        // [0]=3揃え倍率, [1]=4揃え, [2]=5揃え（Normal のみ）
    public AnimationClip winAnim;
}

public enum SymbolType
{
    Normal,   // 通常シンボル（Dragon〜Jack）
    Wild,     // ワイルド（魔法使い）: 通常シンボルの代替として機能
    Scatter,  // スキャター（魔法陣）: ペイラインに依存しない全体判定
    Bonus,    // ボーナストリガー（宝箱）: リール 0/2/4 全てに出現でボーナスラウンド発動
    Filler    // 空白シンボル: ペイラインを遮断するだけで配当なし（RTP 調整用）
}

// リールストリップ（各リールの出目テーブル）
[CreateAssetMenu]
public class ReelStripData : ScriptableObject
{
    public int reelIndex;
    public List<SymbolData> strip;   // 出目順に並んだシンボルリスト（重複あり）
}

// ペイライン定義
[CreateAssetMenu]
public class PaylineData : ScriptableObject
{
    // 各ラインは 5要素の int 配列（0=Top, 1=Mid, 2=Bot）
    // ※ Vector2Int は 2 要素しか持てないため、5 リール対応の専用構造体を使用する
    public PaylineEntry[] lines;     // 要素数 25（各要素が 5 列分の行インデックスを保持）
}

// ペイライン 1 本分の定義（5 リール × 行インデックス）
[Serializable]
public struct PaylineEntry
{
    public int[] rows;   // 要素数 5（rows[reelIndex] = 行インデックス: 0=Top, 1=Mid, 2=Bot）
}

// 配当テーブル
[CreateAssetMenu]
public class PayoutTableData : ScriptableObject
{
    public ScatterPayout[] scatterPayouts;  // Scatter個数→倍率
    public BonusRewardRange bonusRange;     // ×5 〜 ×100
}
```

---

### 4.2 Model 層（ピュア C#）

```csharp
// ゲーム全体の状態
public class GameState
{
    public long   Coins         { get; private set; }
    public int    BetAmount     { get; private set; }
    public int    FreeSpinsLeft { get; private set; }
    public bool   IsFreeSpin    => FreeSpinsLeft > 0;
    public long   TotalSpins    { get; private set; }
    public long   MaxWin        { get; private set; }

    public void SetCoins(long coins) { ... }
    public void AddCoins(long amount) { ... }
    public bool DeductBet() { ... }         // 残高不足で false
    public void AddFreeSpins(int count) { ... }
    public void ConsumeFreeSpin() { ... }
    public void RecordSpin(long winAmount) { ... }
}

// 1スピンの結果（イミュータブル）
// 一度生成された結果は変更不可。DTOとして値の安全性を保証する。
public sealed record SpinResult(
    int[,]                    StoppedSymbolIds,   // [reel, row] ※配列は参照型のため変更注意
    IReadOnlyList<LineWin>    LineWins,
    bool                      HasScatter,
    int                       ScatterCount,
    bool                      HasBonusCondition,
    long                      TotalWinAmount
);

public sealed record LineWin(
    int  LineIndex,
    int  SymbolId,
    int  MatchCount,    // 3/4/5
    long WinAmount
);
```

---

### 4.3 Presenter / Manager 層

```csharp
// ゲーム全体フロー管理（MonoBehaviour + ステートマシン）
public class GameManager : MonoBehaviour
{
    [SerializeField] SpinManager      spinManager;
    [SerializeField] BonusManager     bonusManager;
    [SerializeField] UIManager        uiManager;
    [SerializeField] AudioManager     audioManager;
    [SerializeField] SaveDataManager  saveDataManager;

    private GameState     gameState;
    private GamePhase     currentPhase;

    // 状態遷移メソッド
    private async UniTask StartSpin();
    private async UniTask EvaluateSpin(SpinResult result);
    private async UniTask PlayWinPresentation(SpinResult result);
    private async UniTask HandleBonus(SpinResult result);
    private void          CheckGameOver();

    // 遷移ガード: 現フェーズから next へ遷移可能かを判定する
    // Spinning 中は Idle/Spinning/BonusRound への再遷移を拒否する（割り込み対策）
    // TransitionTo() 冒頭で必ず呼び出し、false の場合は Debug.LogWarning を出して即リターン
    private bool          CanTransitionTo(GamePhase next);
    private void          TransitionTo(GamePhase next);   // CanTransitionTo のチェック込み
}

public enum GamePhase
{
    Idle, Spinning, Evaluating, WinPresentation,
    BonusRound, FreeSpin, GameOver
}

// スピン制御
public class SpinManager : MonoBehaviour
{
    [SerializeField] ReelController[] reels;   // 5個

    // 全リール回転開始 → 順次停止 → SpinResult を返す
    public async UniTask<SpinResult> ExecuteSpin(
        ReelStripData[] strips, PaylineData paylines, PayoutTableData payouts, int betAmount);
}

// 個別リール制御
public class ReelController : MonoBehaviour
{
    public int ReelIndex { get; }

    public void StartSpin();
    public async UniTask StopSpin(int targetStopIndex);  // ストリップ上の停止位置
    public int[] GetVisibleSymbolIds();                  // 表示中の3シンボルIDを返す
}

// ペイライン判定（ピュア C# / テスタブル）
public static class PaylineEvaluator
{
    public static SpinResult Evaluate(
        int[,] symbolGrid,          // [reel, row]
        SymbolData[] symbolDefs,
        PaylineData paylines,
        PayoutTableData payouts,
        int betAmount);
}

// ボーナス管理
public class BonusManager : MonoBehaviour
{
    // フリースピン
    public async UniTask RunFreeSpins(GameState state, int count);

    // ボーナスラウンド（宝箱ミニゲーム）
    public async UniTask<long> RunBonusRound(int betAmount, PayoutTableData payouts);
}

// サウンド管理
public class AudioManager : MonoBehaviour
{
    public void PlayBGM(BGMType type);
    public void PlaySE(SEType type);
    public void SetBGMVolume(float volume);
    public void SetSEVolume(float volume);
    public void FadeOutBGM(float duration);
}

public enum BGMType  { Normal, FreeSpin, BonusRound }
public enum SEType   { SpinStart, ReelStop, SmallWin, BigWin, MegaWin,
                       ScatterAppear, FreeSpinStart, BonusStart,
                       ChestSelect, ChestOpen, ButtonClick }
```

---

### 4.4 View 層

```csharp
// UI 統括
public class UIManager : MonoBehaviour
{
    // パネル参照
    [SerializeField] MainHUDView      mainHUD;
    [SerializeField] FreeSpinHUDView  freeSpinHUD;
    [SerializeField] BonusRoundView   bonusRoundView;
    [SerializeField] SettingsView     settingsView;
    [SerializeField] PaytableView     paytableView;
    [SerializeField] WinPopupView     winPopupView;

    // Presenter から呼ばれるメソッド群
    public void UpdateCoins(long coins);
    public void UpdateBet(int bet);
    public void SetSpinButtonInteractable(bool interactable);
    public async UniTask ShowWinAmount(long amount, WinLevel level);
    public void HighlightWinLines(List<LineWin> wins);
    public void ClearLineHighlights();
    public void ShowFreeSpinHUD(int remaining, long totalWin);
    public void HideFreeSpinHUD();
}

// メインHUD（コイン、ベット、スピンボタン）
public class MainHUDView : MonoBehaviour
{
    [SerializeField] TMP_Text  coinText;
    [SerializeField] TMP_Text  betText;
    [SerializeField] Button    spinButton;
    [SerializeField] Button    autoSpinButton;
    [SerializeField] Slider    betSlider;
}

// 個別リールの視覚表現
public class ReelView : MonoBehaviour
{
    // ReelController から参照され、シンボル画像のスクロールを担当
    //
    //【オブジェクトプーリング】
    //   スピン中に画面外へ出たシンボル GameObject は Destroy せずプールに返却し、
    //   新たに必要なシンボルはプールから取得することで GC Allocation のスパイクを防ぐ。
    //   Unity 組み込みの UnityEngine.Pool.ObjectPool<SymbolView> を使用する。
    private ObjectPool<SymbolView> _symbolPool;

    public void ScrollSymbols(float normalizedSpeed);
    public void SnapToPosition(int stopIndex);
    public async UniTask PlayWinAnimation(int row, AnimationClip clip);
}
```

---

### 4.5 データ永続化

```csharp
// 保存データ構造（JSON シリアライズ対象）
[Serializable]
public class SaveData
{
    public long   coins         = 1000;
    public int    betAmount     = 10;
    public float  bgmVolume     = 0.8f;
    public float  seVolume      = 1.0f;
    public long   totalSpins    = 0;
    public long   maxWin        = 0;
    public string saveVersion   = "1.0";
}

public class SaveDataManager : MonoBehaviour
{
    private static readonly string SavePath =
        Path.Combine(Application.persistentDataPath, "savedata.json");
    // アトミック書き込み用の一時ファイルパス
    private static readonly string TempPath =
        Path.Combine(Application.persistentDataPath, "savedata.json.tmp");
    // 整合性ハッシュファイルパス（JSON文字列の SHA256 hex を格納）
    private static readonly string HashPath =
        Path.Combine(Application.persistentDataPath, "savedata.json.hash");

    // 読み込み: ファイル不存在→デフォルト、ハッシュ不一致→破損扱い→.bak リネーム後デフォルト
    public SaveData Load();

    // 保存: アトミック書き込み（temp ファイルに書き込んでからリネーム）
    //   1. JSON を TempPath に書き込む
    //   2. SHA256 ハッシュを HashPath に書き込む
    //   3. File.Move(TempPath → SavePath, overwrite:true)
    //   書き込み途中でアプリが終了しても SavePath の元ファイルは破損しない
    public async UniTask SaveAsync(CancellationToken ct);

    // バリデーション: 以下をすべて検証する
    //   ① saveVersion が既知のバージョンであること
    //   ② coins が 0 以上 9,999,999 以下であること
    //   ③ betAmount が選択肢 (10/20/50/100) のいずれかであること
    //   ④ bgmVolume / seVolume が 0.0 〜 1.0 の範囲内であること
    //   ⑤ totalSpins / maxWin が 0 以上であること
    // 1つでも失敗した場合は false を返しデフォルト値へフォールバックする
    private bool   Validate(SaveData data);
}
```

---

## 5. リール制御フロー

```
1. SpinManager.ExecuteSpin() 呼び出し
2. 乱数でリールごとの「停止位置（ストリップインデックス）」を決定
3. 全 ReelController に StartSpin() → 高速スクロール開始
4. 停止順（Reel 0 → 1 → 2 → 3 → 4）で 0.3 秒間隔で StopSpin() 呼び出し
5. 各リール停止アニメーション（バウンス）再生
6. 全リール停止後、表示中シンボルを取得
7. PaylineEvaluator.Evaluate() で当選判定
8. SpinResult を返す

早期停止（スキップ）:
- スピン中にスピンボタンを押すと全リールが即座に停止位置へスナップ
```

---

## 6. 乱数生成

```csharp
// 乱数生成をインターフェースでラップし、テスト時に決定論的な実装を差し込めるようにする
public interface IRandomGenerator
{
    int   Next(int minValue, int maxValue);   // minValue 以上 maxValue 未満
    float NextFloat();                         // 0.0f 以上 1.0f 未満
}

// プロダクション実装: System.Random を使用（ゲームプレイ用で十分）
public class SystemRandomGenerator : IRandomGenerator { ... }

// テスト用実装: 固定シードで決定論的に動作する
public class SeededRandomGenerator : IRandomGenerator { ... }
```

**方針:**

- 全 Wild ラインの配当ルール: ペイライン上の 5 シンボルがすべて Wild のとき、最高配当シンボル（Dragon）の 5 揃え配当（×125）と同等の配当を与える
- 乱数の予測可能性はローカルオフラインのアーケードゲームにおいてセキュリティリスクではないため、`System.Security.Cryptography.RandomNumberGenerator`（暗号論的乱数）は使用しない
- `IRandomGenerator` でラップする主目的は**テスト容易性（決定論的な再現）**
- `SpinManager` は DI（SerializeField または コンストラクタ）で `IRandomGenerator` を受け取る
- **`SystemRandomGenerator` は `BootManager` が 1 インスタンスだけ生成し、使い回す**
  - `new Random()` を毎スピン呼び出すと短い間隔で同じ系列が発生するリスクがある
  - インスタンスは `BootManager` → `SpinManager` → `ReelController` に DI で伝達する
- 配当計算のパイプラインに float を介在させない
  - `SymbolData.payouts` は `int[]`（倍率を整数で保持）
  - 最終配当は `betAmount * payouts[matchCount - 3]`（整数演算のみ）で算出する
- `ReelStripData` のストリップは重み付き確率テーブルとして機能
  - 例: Dragon はストリップ 60 マス中 2 マス（出現率 3.3%）
- フリースピン中の乱数は通常と同じ系列（有利化なし、倍率のみ変化）
- ボーナスラウンドの宝箱報酬は `PayoutTableData` の重みに従い抽選

---

## 7. UniTask キャンセレーション戦略

スピン中のシーン遷移・オートスピン中断・アプリ終了時に `OperationCanceledException` が発生する。
これを適切に処理しないと Console エラーが出続けたり、状態が不整合になる。

### キャンセルトークンの管理

| キャンセル源 | トークン取得方法 |
|-------------|----------------|
| シーン破棄（GameObject Destroy） | `this.GetCancellationTokenOnDestroy()` |
| オートスピン中断 | `CancellationTokenSource` を `GameManager` で管理し `Cancel()` を呼ぶ |

### ハンドリング方針

```csharp
// GameManager でオートスピンを制御する例
private CancellationTokenSource _autoSpinCts;

private async UniTask RunAutoSpin(int count, CancellationToken destroyToken)
{
    _autoSpinCts = CancellationTokenSource.CreateLinkedTokenSource(destroyToken);
    var token = _autoSpinCts.Token;

    try
    {
        for (int i = 0; i < count; i++)
        {
            await spinManager.ExecuteSpin(..., token);
        }
    }
    catch (OperationCanceledException)
    {
        // 正常なキャンセル — ログなしで安全に終了
        // GameManager はここで状態を Idle に戻す
        TransitionTo(GamePhase.Idle);
    }
}
```

**ルール:**
- `OperationCanceledException` は最上位の呼び出し元（`GameManager` のフローメソッド）でキャッチし、`Idle` 状態に遷移させる
- キャッチ後にログを吐かない（`UniTask` の仕様上 `OperationCanceledException` は正常終了扱い）
- `SpinManager` / `BonusManager` の内部では例外を catch せずに上位に伝播させる

---

## 8. シーン構成

```
Assets/
  Scenes/
    Boot.unity          ← 初期化・ロード画面（Additive でMain をロード）
    Main.unity          ← ゲームメイン（スロット本体）
    BonusRound.unity    ← ボーナスラウンド専用シーン（Additive）

  Scripts/
    Core/               ← GameManager, SpinManager, BonusManager
    Model/              ← GameState, SpinResult, SaveData（ピュア C#）
    View/               ← UIManager, ReelView, BonusRoundView 等
    Audio/              ← AudioManager
    Data/               ← ScriptableObject 定義クラス群
    Utility/            ← PaylineEvaluator（static）, SaveDataManager

  ScriptableObjects/
    Symbols/            ← SymbolData × 10
    Reels/              ← ReelStripData × 5
    Paylines/           ← PaylineData（1ファイル）
    PayoutTable/        ← PayoutTableData（1ファイル）

  Art/
    Sprites/Symbols/
    Sprites/UI/
    Animations/
    Fonts/

  Audio/
    BGM/
    SE/
```

---

## 8. UI レイアウト（Main シーン）

```
┌──────────────────────────────────────────────────┐
│  [タイトルロゴ]        [設定ボタン] [ペイテーブル] │
│                                                  │
│  ┌─────┬─────┬─────┬─────┬─────┐                │
│  │ R1  │ R2  │ R3  │ R4  │ R5  │  ← ペイライン  │
│  │[sym]│[sym]│[sym]│[sym]│[sym]│    強調オーバ  │
│  │[sym]│[sym]│[sym]│[sym]│[sym]│    レイ        │
│  │[sym]│[sym]│[sym]│[sym]│[sym]│                │
│  └─────┴─────┴─────┴─────┴─────┘                │
│                                                  │
│  コイン: [999,999]     WIN: [------]             │
│                                                  │
│  BET: [10▼] [20] [50] [100]   [AUTO] [SPIN]     │
└──────────────────────────────────────────────────┘
解像度基準: 1920×1080 (16:9)、レターボックスでリサイズ対応
```

### Canvas 分割方針

| Canvas | 用途 | 更新頻度 |
|--------|------|---------|
| Main Canvas（Screen Space - Camera） | リールグリッド・ペイライン強調・ポップアップ | スピン開始/終了時のみ |
| HUD Canvas（Screen Space - Overlay） | コイン表示・WIN額・BETボタン | カウントアップ中は毎フレーム |

コイン/WIN表示を別 Canvas に分離することで、カウントアップ演出中の TMP_Text 更新が
リールグリッドの Canvas リビルドを誘発しない。DOTween の DOCounter で毎フレーム更新される
テキストノードのリビルドを HUD Canvas 内に閉じ込める。

---

## 9. 依存ライブラリ

| ライブラリ | 用途 | 導入方法 |
|-----------|------|---------|
| UniTask (Cysharp) | 非同期処理（async/await） | UPM |
| TextMeshPro | テキスト表示（Unity 標準） | Unity 組み込み |
| DOTween（無料版） | UI / シンボルアニメーション補完 | Asset Store / UPM |

---

## 10. テスト方針

| 対象 | 方式 |
|------|------|
| `PaylineEvaluator` | Unity Test Runner（Edit Mode）でユニットテスト |
| `GameState` | ピュア C# テスト（xUnit または NUnit） |
| `SaveDataManager` | Edit Mode テスト（仮想パスに書き込み） |
| スピンフロー全体 | Play Mode テスト（入力モック） |
| UI | 手動テスト + チェックリスト |
