# 設計書 — Retro Slots

**バージョン**: 1.1
**作成日**: 2026-03-20
**最終更新日**: 2026-03-29（Issue #80: RetroColorTheme ScriptableObject 追加）

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
| Model | ゲーム状態・データ保持・ロジック | `GameState`, `SpinResult`, `PaylineEvaluator`, `SaveDataManager` |
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
      ┌───►│  Idle    │◄─────────────────────────────────┐
      │    └────┬─────┘                                  │
      │         │                                        │
      │   ┌─────┴──────────┐                             │
      │   ▼                ▼                             │
      │ Spinning        GameOver                         │
      │   │                ▲                             │
      │   ▼                │ (コイン不足)                │
      │ Evaluating         └─────────────────────────────┘
      │   │
      │   ├───────────────────────────────┐
      │   ▼ (当選/Scatter/Bonusあり)      │ (ハズレ)
      │ WinPresentation ──────────────────┤
      │   │                               │
      │   ├─────────────────────┐         │
      │   ▼ (ボーナス条件)      │         │
      │ BonusRound ─────────────┤         │
      │   │                     │         │
      │   ▼ (Scatter 3個以上)   │         │
      │ FreeSpin ───────────────┴─────────┘
      │
      └──────────────────────────────────────────────────
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
    Normal,   // 通常シンボル（Seven〜Lemon）
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

// レトロクラシックテーマのカラーパレット（Issue #80）
// UIManager / MainHUDView / WinPopupView が参照する。
// コードにカラーをハードコードせず、このアセットを Inspector で設定する。
[CreateAssetMenu(menuName = "SlotGame/RetroColorTheme")]
public class RetroColorTheme : ScriptableObject
{
    // カメラ背景色
    public Color normalCameraColor;      // 深いマホガニー
    public Color freeSpinCameraColor;    // 深いオリーブゴールド
    public Color bonusRoundCameraColor;  // カジノフェルト風の深緑

    // モードオーバーレイ Tint
    public Color normalTint;             // 透明（通常は無効）
    public Color freeSpinTint;           // アンバーゴールド半透明
    public Color bonusRoundTint;         // 深緑半透明

    // スピンボタン（UIGradient 上→下）
    public Color spinButtonTop;          // 赤メタル（上端）
    public Color spinButtonBottom;       // 深クリムゾン（下端）
    public Color spinStopButtonTop;      // オレンジレッド（上端）
    public Color spinStopButtonBottom;   // 深いオレンジ（下端）

    // オートスピンボタン（UIGradient 上→下）
    public Color autoSpinButtonTop;      // バーガンディ（上端）
    public Color autoSpinButtonBottom;   // 深い赤茶（下端）
    public Color autoSpinPopupBackground;// 回数選択ポップアップ背景（暗いマホガニー）

    // ベットボタン（未選択）
    public Color betUnselectedTop;
    public Color betUnselectedBottom;
    public Color betUnselectedHighlight;
    public Color betUnselectedPressed;
    public Color betUnselectedLabelColor;

    // ベットボタン（選択済み）
    public Color betSelectedTop;         // アンティークゴールド（上端）
    public Color betSelectedBottom;      // 深いゴールド（下端）
    public Color betSelectedHighlight;
    public Color betSelectedPressed;
    public Color betSelectedLabelColor;

    // WIN ポップアップ背景グラデーション
    public Color winPopupBackgroundTop;  // 深クリムゾン
    public Color winPopupBackgroundBottom; // 赤みを帯びたほぼ黒
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
    public ScatterPayout[]    scatterPayouts;    // Scatter個数→倍率
    public FreeSpinReward[]   freeSpinRewards;   // Scatter個数→フリースピン回数
    public int                freeSpinMultiplier; // フリースピン中の配当倍率
    public BonusRewardEntry[] bonusRewards;      // ボーナスラウンド報酬の重み付きテーブル
}

// ボーナス報酬エントリ（重み付き抽選テーブル）
// 宝箱選択時に weight に従って抽選し、multiplier × betAmount がプレイヤーに付与される
[Serializable]
public struct BonusRewardEntry
{
    public int multiplier;  // ベット額に掛ける倍率（例: ×5〜×100）
    public int weight;      // 抽選重み（合計に対する比率で確率が決まる）
}
// 例: { multiplier=5, weight=40 }, { multiplier=10, weight=25 }, ..., { multiplier=100, weight=3 }
// → ×5 が最も出やすく（40/100）、×100 は低確率（3/100）
```

---

### 4.2 Model 層（ピュア C#）

```csharp
// ゲーム全体の状態
public class GameState
{
    // 設定値（コンストラクタで固定）
    public long   InitialCoins          { get; }
    public long   MaxCoins              { get; }
    public int[]  ValidBetAmounts       { get; }

    // ゲーム状態
    public long   Coins                 { get; private set; }
    public int    BetAmount             { get; private set; }
    public int    FreeSpinsLeft         { get; private set; }
    public bool   IsFreeSpin            => FreeSpinsLeft > 0;
    public bool   HasCompletedTutorial  { get; private set; }
    public bool   IsTurbo               { get; private set; }

    // ライフタイム統計（永続化対象）
    public long   TotalSpins            { get; private set; }
    public long   TotalWins             { get; private set; }
    public long   MaxWin                { get; private set; }
    public int    TotalFreeSpinTriggers { get; private set; }

    // コイン操作
    public void SetCoins(long coins) { ... }
    public void AddCoins(long amount) { ... }
    public bool DeductBet() { ... }          // 残高不足で false
    public bool SetBetAmount(int bet) { ... } // 無効な値で false

    // フリースピン操作
    public void AddFreeSpins(int count) { ... }
    public void ConsumeFreeSpin() { ... }

    // ターボ・チュートリアル
    public void SetTurbo(bool enabled) { ... }
    public void CompleteTutorial() { ... }

    // 統計記録
    public void RecordSpin(long winAmount) { ... }           // ライフタイム + セッション統計を更新
    public void RecordFreeSpinTrigger() { ... }

    // 統計取得
    public SessionStats GetSessionStats() { ... }            // セッション統計スナップショット
    public SessionStats GetLifetimeStats() { ... }           // ライフタイム統計スナップショット

    // セーブデータ復元
    public void RestoreStats(
        long totalSpins, long totalWins, long maxWin, int totalFreeSpinTriggers) { ... }
}

// セッション統計スナップショット（インメモリのみ・永続化なし）
public readonly struct SessionStats
{
    public long  TotalSpins       { get; }   // スピン総数
    public long  Wins             { get; }   // 当選スピン数
    public float WinRate          { get; }   // 当選率（0〜100 %）
    public long  LargestWin       { get; }   // 最大獲得コイン数
    public int   FreeSpinTriggers { get; }   // フリースピン発動回数
    public long  NetProfit        { get; }   // 損益（負数 = 損失）
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
public class TitleManager : MonoBehaviour
{
    // タイトルシーンから Main シーンへの遷移を担当
    public void StartGame();
}

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
    public void ToggleMute();
    public void FadeOutBGM(float duration);
}

public enum BGMType  { Normal, FreeSpin, BonusRound }
public enum SEType   { SpinStart, ReelStop, SmallWin, BigWin, MegaWin, EpicWin,
                       ScatterAppear, FreeSpinStart, BonusStart,
                       ChestSelect, ChestOpen, ButtonClick }
```

---

### 4.4 View 層

```csharp
public class TitleEffects : MonoBehaviour
{
    // タイトル画面の演出（ブリージング、パルス、回転、浮遊）を担当
    public void StartAnimations();
    public async UniTask FadeOutAsync(CancellationToken ct);
}

// UI 統括
public class UIManager : MonoBehaviour
{
    // パネル参照
    [SerializeField] MainHUDView         mainHUD;
    [SerializeField] FreeSpinHUDView     freeSpinHUD;
    [SerializeField] BonusRoundView      bonusRoundView;
    [SerializeField] SettingsView        settingsView;
    [SerializeField] PaytableView        paytableView;
    [SerializeField] WinPopupView        winPopupView;
    [SerializeField] StatsView           statsView;
    // TutorialView / GameDescriptionView はランタイム動的生成（Prefab なし）

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
    // ベット選択ボタン（10 / 20 / 50 / 100 コインの 4 段階）
    [SerializeField] Button[]  betButtons;

    // オートスピン回数の選択肢（デフォルト: 10 / 25 / 50 / 100）
    // Inspector または直接編集で変更可能。変更時は Unity Editor 上で
    // MainHUDView コンポーネントの autoSpinCounts フィールドを編集する。
    // Awake() 内の BuildAutoSpinPopup() がこの配列をもとにボタンを動的生成する。
    [SerializeField] int[] autoSpinCounts = { 10, 25, 50, 100 };

    // autoSpinCounts の各値に対応するボタンを autoSpinButton の上部に
    // ポップアップとしてランタイム生成する。プレハブ不要で UI を構築する実装。
    private void BuildAutoSpinPopup() { /* ... */ }
}

// 個別リールの視覚表現
public class ReelView : MonoBehaviour
{
    // ReelController から参照され、シンボル画像のスクロールを担当。
    //
    //【循環バッファ方式】
    //   固定 5 スロット（上下バッファ 1 + 表示 3 + バッファ合計）の SymbolView を
    //   循環バッファとして使い回すことで、Instantiate/Destroy を排除し
    //   GC Allocation のスパイクを防止する。
    private SymbolView[] _symbolViews;

    public void Initialize(ReelStripData strip);
    public void StartScrolling();
    public async UniTask DecelerateAndStop(int targetStopIndex, CancellationToken ct);
    public void SnapToPosition(int targetStopIndex);
    public int[] GetVisibleSymbolIds();
    public async UniTask PlayWinAnimation(int row, CancellationToken ct);
    public void HighlightRows(IReadOnlyCollection<int> rows);
    public void ClearHighlights();
}

// 当選演出レベル（Scripts/View/WinLevel.cs）
public enum WinLevel { Small, Big, Mega, Epic }

// 5ステップのチュートリアルオーバーレイ（ランタイム UI 動的生成）
public class TutorialView : MonoBehaviour
{
    // Prefab 不要。Setup() を呼ぶと RectTransform・Image・CanvasGroup・Button を
    // Instantiate なしでその場生成する。
    public event System.Action OnComplete;

    public void Setup();                                       // UI 要素をランタイム生成
    public async UniTask ShowAsync(CancellationToken ct);     // フェードイン→ステップ表示
}

// セッション/ライフタイム統計パネル
public class StatsView : MonoBehaviour
{
    [SerializeField] TMP_Text totalSpinsText;
    [SerializeField] TMP_Text winsText;
    [SerializeField] TMP_Text winRateText;
    [SerializeField] TMP_Text largestWinText;
    [SerializeField] TMP_Text freeSpinTriggersText;
    [SerializeField] TMP_Text netProfitText;
    [SerializeField] Button   closeButton;

    public event System.Action OnCloseRequested;

    // SessionStats を受け取り各テキストを更新。NetProfit は正/負で緑/赤に色分け
    public void UpdateDisplay(in SessionStats stats);
    public async UniTask ShowAsync(CancellationToken ct = default);
    public async UniTask HideAsync(CancellationToken ct = default);
}

// ゲーム説明モーダル（PaytableView をクローンしてランタイム生成）
public class GameDescriptionView : MonoBehaviour
{
    [SerializeField] Button   closeButton;
    [SerializeField] TMP_Text descriptionText;

    public event System.Action OnCloseRequested;

    public void Setup();                                       // 動的生成後に初期化
    public void SetDescription(string text);
    public async UniTask ShowAsync(CancellationToken ct = default);
    public async UniTask HideAsync(CancellationToken ct = default);
}
```

---

### 4.5 データ永続化

```csharp
// 保存データ構造（JSON シリアライズ対象）
[Serializable]
public class SaveData
{
    public long   coins                   = 1000;
    public int    betAmount               = 10;
    public float  bgmVolume               = 0.8f;
    public float  seVolume                = 1.0f;
    public long   totalSpins              = 0;
    public long   totalWins               = 0;   // 勝利スピン総数
    public long   maxWin                  = 0;
    public int    totalFreeSpinTriggers   = 0;   // フリースピン発動回数
    public string saveVersion             = "1.0";
    public string checksum                = "";   // SHA256 チェックサム（JSON フィールドとして埋め込み）
    public bool   hasCompletedTutorial    = false;
    public bool   isTurbo                 = false;
}

public class SaveDataManager
{
    private readonly string     _savePath;
    private readonly SlotConfig _config;

    // コンストラクタでパスを差し替えることでテスト可能
    public SaveDataManager(SlotConfig config = null);
    public SaveDataManager(string savePath, SlotConfig config = null);

    // 読み込み: ファイル不存在→デフォルト、チェックサム不一致→破損扱い→.bak リネーム後デフォルト
    // 移行戦略: checksum フィールドが空の旧データはバリデーションのみで通す
    public SaveData Load();

    // 保存: アトミック書き込み（一時ファイルを経由してリネーム）
    //   1. checksum フィールドを計算して SaveData に設定する
    //   2. JSON を TempPath（savedata.json.tmp）に書き込む
    //   3. File.Replace / File.Move で SavePath にアトミックに昇格する
    //   書き込み途中でアプリが終了しても SavePath の元ファイルは破損しない
    public void Save(SaveData data);

    // バリデーション: 以下をすべて検証する
    //   ① saveVersion が既知のバージョンであること
    //   ② coins が 0 以上 MaxCoins（SlotConfig）以下であること
    //   ③ betAmount が ValidBetAmounts（SlotConfig）のいずれかであること
    //   ④ bgmVolume / seVolume が 0.0 〜 1.0 の範囲内であること
    //   ⑤ totalSpins / maxWin が 0 以上であること
    // 1つでも失敗した場合は false を返しデフォルト値へフォールバックする
    private static bool Validate(SaveData data, SlotConfig config);
}
```

#### チェックサム計算式

チェックサムは以下の手順で計算する。

1. **ソルト値の取得**  
   `SlotConfig.ChecksumSalt`（`GameConfigData.checksumSalt` フィールドから供給）を使用する。  
   `SlotConfig` が `null` の場合はフォールバック値 `"SALTY_SLOT_2026"` を使用する。

2. **ハッシュ入力文字列の構築**  
   以下のフィールドをコロン区切りで結合する（`bgmVolume` / `seVolume` は小数点以下 2 桁固定）。
   ```
   {coins}:{betAmount}:{bgmVolume:F2}:{seVolume:F2}:{totalSpins}:{totalWins}:{maxWin}:{totalFreeSpinTriggers}:{saveVersion}:{salt}
   ```

3. **SHA256 ハッシュの計算**  
   上記文字列を UTF-8 エンコードし、`SHA256.ComputeHash()` でハッシュバイト列を得る。

4. **Base64 エンコード**  
   ハッシュバイト列を `Convert.ToBase64String()` で Base64 文字列に変換し、`SaveData.checksum` に格納する。

```csharp
// 実装例（SaveDataManager.CalculateChecksum）
string salt = config != null ? config.ChecksumSalt : "SALTY_SLOT_2026";
string raw  = $"{data.coins}:{data.betAmount}:{data.bgmVolume:F2}:{data.seVolume:F2}"
            + $":{data.totalSpins}:{data.totalWins}:{data.maxWin}:{data.totalFreeSpinTriggers}"
            + $":{data.saveVersion}:{salt}";
using var sha256 = SHA256.Create();
byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(raw));
return Convert.ToBase64String(bytes);
```

---

### 4.6 UI 演出コンポーネント

#### UIGradient.cs

`UIGradient` は、UGUI の `Graphic`（Image, TMP_Text 等）のメッシュ頂点カラーを書き換えることでグラデーションを実現するカスタムコンポーネントである。

**特徴:**
- **カスタムシェーダー不要**: `BaseMeshEffect` を継承しており、標準の UGUI シェーダーで動作するため、マテリアルの追加管理が不要で軽量である。
- **頂点カラー乗算**: `Graphic.color`（または Image のカラー）とグラデーションカラーが乗算されるため、スクリプトからの動的な色変更（点滅演出やボタンの非活性化など）とグラデーションを共存させることができる。

**主要プロパティ:**
- `GradientDirection`: グラデーションの方向を指定する。
    - `TopToBottom`: 上から下への垂直グラデーション。
    - `LeftToRight`: 左から右への水平グラデーション。
    - `FourCorner`: 四隅の色を個別に設定可能なグラデーション。
- `Colors`: 方向に応じた色の設定（TopToBottom / LeftToRight の場合は 2色、FourCorner の場合は 4色）。

**使い分けの指針:**
- **UIGradient を使用する場合**: `Image`（ボタン、背景パネル）にグラデーションをかけたい場合や、テキストに対して動的な色変更とグラデーションを併用したい場合。
- **TMP 標準機能を使用する場合**: `TextMeshPro` の文字に対して静的な上下グラデーションをかけたい場合は、TMP の Inspector 上で `Color Gradient` -> `Enable Vertex Gradient` を使用する。

**使用例（コードからの動的適用）:**
```csharp
// ボタン画像にブルー系グラデーションを動的に適用する例
var gradient = buttonImage.gameObject.AddComponent<UIGradient>();
gradient.SetColors(new Color(0.3f, 0.5f, 0.9f), new Color(0.05f, 0.15f, 0.4f));
```

---

### 4.7 Utility / Core 層

```csharp
// 16:9 アスペクト比を維持（Scripts/Utility/ResolutionManager.cs）
// Camera にアタッチ。Awake() および Update() で Viewport Rect を再計算。
// ウィンドウが 16:9 より縦長 → ピラーボックス（左右黒帯）
// ウィンドウが 16:9 より横長 → レターボックス（上下黒帯）
[RequireComponent(typeof(Camera))]
public class ResolutionManager : MonoBehaviour { }

// キーボードショートカット処理（Scripts/Core/SlotInputHandler.cs）
// Unity Input System の PlayerInput と連携し、各アクションを GameManager に委譲する。
[RequireComponent(typeof(PlayerInput))]
public class SlotInputHandler : MonoBehaviour
{
    [SerializeField] GameManager gameManager;
    // Space/Enter → スピン, ↑/↓ → ベット増減, A → オートスピン,
    // S → スキップ, M → ミュート, T → ターボ, P → ペイテーブル
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

- 全 Wild ラインの配当ルール: ペイライン上の 5 シンボルがすべて Wild のとき、最高配当シンボル（Seven）の 5 揃え配当（×125）と同等の配当を与える
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
  - 例: Seven はストリップ 88 マス中 2 マス（出現率 2.3%）
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
    Boot.unity          ← 初期化・ロード画面
    Title.unity         ← タイトル画面（エントランス）
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
    Symbols/            ← SymbolData × 12
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

## 9. UI レイアウト（Main シーン）

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
解像度基準: 1920×1080 (16:9)

### 9.1 解像度管理 (Resolution Management)

**`ResolutionManager.cs`** は、異なるアスペクト比のディスプレイにおいて 16:9 のゲーム画面を正しく表示するためのレイアウト調整を行う。

- **メカニズム**: 実行時のウィンドウアスペクト比を計算し、ターゲットアスペクト比（16.0 / 9.0）と比較する。
- **ピラーボックス (Pillarbox)**: ウィンドウが 16:9 より縦長な場合、左右に黒帯を表示し、画面中央にゲームを表示するよう Camera の Viewport Rect (`x`, `y`, `width`, `height`) を調整する。
- **レターボックス (Letterbox)**: ウィンドウが 16:9 より横長な場合、上下に黒帯を表示するように Rect を調整する。
- **動的対応**: `Update()` 内でレイアウト更新を行うことで、実行中のウィンドウリサイズにも即座に対応する。
```

### Canvas 分割方針

| Canvas | 用途 | 更新頻度 |
|--------|------|---------|
| Main Canvas（Screen Space - Camera） | リールグリッド・ペイライン強調・ポップアップ | スピン開始/終了時のみ |
| HUD Canvas（Screen Space - Overlay） | コイン表示・WIN額・BETボタン | カウントアップ中は毎フレーム |

コイン/WIN表示を別 Canvas に分離することで、カウントアップ演出中の TMP_Text 更新が
リールグリッドの Canvas リビルドを誘発しない。DOTween の DOCounter で毎フレーム更新される
テキストノードのリビルドを HUD Canvas 内に閉じ込める。

### 9.2 UIカラーテーマ管理（RetroColorTheme）

UI カラーは `RetroColorTheme` ScriptableObject に一元集約し、View スクリプト内のハードコードを排除する。

**設計方針:**
- `UIManager`・`MainHUDView`・`WinPopupView` はそれぞれ `[SerializeField] private RetroColorTheme? colorTheme` フィールドを持つ
- `colorTheme` が null の場合（Inspector 未設定時）はコード内フォールバック値を使用する（デグレードしない）
- カラー調整はアセットファイル（`RetroColorTheme.asset`）の Inspector 編集のみで完結し、コードの再コンパイル不要

**アセット配置:**
```
Assets/
  Settings/
    RetroColorTheme.asset    ← Unity Editor で作成・Inspector から各 View に設定
```

**手動セットアップ手順（Unity Editor）:**
1. メニュー `Assets > Create > SlotGame > RetroColorTheme` でアセット作成
2. `Assets/Settings/RetroColorTheme.asset` に配置
3. 各 GameObject の Inspector で以下のフィールドにアセットを設定:
   - `UIManager` → `Color Theme`
   - `MainHUDView` → `Color Theme`
   - `WinPopupView` → `Color Theme`

---

## 10. 依存ライブラリ

| ライブラリ | 用途 | 導入方法 |
|-----------|------|---------|
| UniTask (Cysharp) | 非同期処理（async/await） | UPM |
| TextMeshPro | テキスト表示（Unity 標準） | Unity 組み込み |
| DOTween（無料版） | UI / シンボルアニメーション補完 | Asset Store / UPM |

---

## 11. テスト方針

| 対象 | 方式 |
|------|------|
| `PaylineEvaluator` | Unity Test Runner（Edit Mode）でユニットテスト |
| `GameState` | ピュア C# テスト（xUnit または NUnit） |
| `SaveDataManager` | Edit Mode テスト（仮想パスに書き込み） |
| スピンフロー全体 | Play Mode テスト（入力モック） |
| UI | 手動テスト + チェックリスト |

---

## 12. 追加機能仕様

### 12.1 ターボモード

ターボモードは、スピンにおけるリール回転の演出時間を短縮する機能である。関連するクラスおよび実装箇所は以下の通り:

- **`GameConfigData.cs`**: ターボモード時の各種パラメータを定義。`TurboSpinDuration` (0.5s) および `TurboStopInterval` (0.1s) が設定される。
- **`GameState.cs`**: 現在のターボ設定を `IsTurbo` フラグとして保持・管理する。
- **`MainHUDView.cs`**: ターボモードのUI表示およびボタン操作を通じた状態の制御を担当する。

### 12.2 キーボード入力制御

PC版向けに操作性を向上させるため、**`SlotInputHandler.cs`** にてキーボードショートカットを実装している。対応するキーバインドは以下の通り:

| アクション | キー |
|---|---|
| スピン | Space / Enter |
| ベット増加 | ↑ |
| ベット減少 | ↓ |
| オートスピン | A |
| スキップ | S |
| ミュート | M |
| ターボ | T |
| ペイテーブル | P |

### 12.3 セッション統計 (Session Statistics)

セッション統計機能は、ゲーム起動後のスピン結果をインメモリに蓄積し、プレイヤーが現在のセッションの成績を確認できるようにするものである。

- **`SessionStats` (Model)**: 1つのセッションスナップショットを保持する `readonly struct`。以下のフィールドを含む:
  - `TotalSpins`: スピン総数
  - `Wins`: 当選回数
  - `WinRate`: 当選率 (`Wins / TotalSpins * 100`)
  - `LargestWin`: 1スピンでの最大当選額
  - `FreeSpinTriggers`: フリースピンが発動した回数
  - `NetProfit`: セッション開始時からの累積損益
- **`GameState.cs` (Model)**: セッション固有のプライベート変数（`_sessionStartCoins` 等）で累積値を管理する。`RecordSpin()` および `RecordFreeSpinTrigger()` でこれらを更新し、`GetSessionStats()` で計算済みの構造体を返す。
- **`StatsView.cs` (View)**: 統計情報を UI に反映する。`CanvasGroup` を用いたフェードイン/アウトアニメーションおよび、`NetProfit` が正か負かによるテキストカラーの動的な変更（緑/赤）を担当する。

### 12.4 ミュート機能 (Mute Feature)

音声をワンボタンで切り替える機能であり、単なる音量ゼロ化ではなく、復元を考慮した実装となっている。

- **`AudioManager.cs`**: `_isMuted` フラグで状態を管理。
- **保存と復元**:
  - **ミュート時**: 現在の `bgmSource.volume` および `seSource.volume` の値を `_preMuteBgmVolume` / `_preMuteSeVolume` に保存してから 0 に設定する。
  - **解除時**: 保存しておいた `_preMute` 用変数から値を戻す。
- **音量設定との共存**: ミュート中に設定画面等でスライダーが操作された場合、`_preMute` 変数のみを更新し、解除時にその新しい値が適用されるように制御している。

### 12.5 UI 動的生成パターン (Dynamic UI Generation)

一部の補助的な UI パネルにおいて、Prefab の重複を避け、メンテナンス性を向上させるために、既存の UI パネルをテンプレートとしてクローン生成するパターンを採用している。

#### ゲーム説明画面 (GameDescriptionView) の生成

`GameDescriptionView` は、`PaytableView` と共通のレイアウト（背景パネル、スクロールビュー、閉じるボタン等）を持つため、独立した Prefab を作成せず、実行時に `paytableView` をクローンして生成される。

**実装フロー (`UIManager.cs`):**
1.  `ShowGameDescription()` が最初に呼ばれた際、`paytableView.gameObject` を `Instantiate` し、元の `PaytableView` コンポーネントを `Destroy` する。
2.  生成されたオブジェクトから `PaytableView` コンポーネントを `Destroy` し、代わりに `GameDescriptionView` コンポーネントを `AddComponent` する。
3.  イベントの購読 (`OnCloseRequested`) および初期テキストの設定を行う。

**設計上の考慮事項:**
-   **リソースの効率化**: 似た構造の Prefab を複数保持することを避け、デザイン変更（例：背景画像やボタンの差し替え）がすべてのパネルに自動的に波及するようにしている。
-   **遅延生成 (Lazy Initialization)**: 使用されるまでオブジェクトを生成しないことで、初期ロード時のメモリ負荷を抑えている。
-   **依存関係**: この生成方式により、`UIManager` は `paytableView`（Prefab またはシーン上のインスタンス）に強く依存する。
-   **将来の拡張**: `GameDescriptionView` のデザインが配当表から大きく乖離した場合や、独自の複雑なアニメーションが必要になった場合は、独立した Prefab へ分離することを検討すべきである。

