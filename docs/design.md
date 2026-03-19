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
  SymbolData × 10
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
    public SymbolType type;          // Normal / Wild / Scatter
    public int[]     payouts;        // [0]=3揃え倍率, [1]=4揃え, [2]=5揃え
    public AnimationClip winAnim;
}

public enum SymbolType { Normal, Wild, Scatter }

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
    public Vector2Int[] lines;       // [lineIndex][reelIndex] → 行インデックス
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

// 1スピンの結果
public class SpinResult
{
    public int[,]          StoppedSymbolIds;   // [reel, row]
    public List<LineWin>   LineWins;
    public bool            HasScatter;
    public int             ScatterCount;
    public bool            HasBonusCondition;
    public long            TotalWinAmount;
}

public class LineWin
{
    public int    LineIndex;
    public int    SymbolId;
    public int    MatchCount;      // 3/4/5
    public long   WinAmount;
}
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

    public SaveData Load();        // 破損時はデフォルト値を返す
    public void    Save(SaveData data);
    private bool   Validate(SaveData data);  // バージョン・範囲チェック
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

- `System.Random` を使用（ゲームプレイ用）
- `ReelStripData` のストリップは重み付き確率テーブルとして機能
  - 例: Dragon はストリップ 60 マス中 2 マス（出現率 3.3%）
- フリースピン中の乱数は通常と同じ系列（有利化なし、倍率のみ変化）
- ボーナスラウンドの宝箱報酬は `PayoutTableData` の重みに従い抽選

---

## 7. シーン構成

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
