# Fantasy Slot

ファンタジーテーマの 5 リール・25 ペイライン アーケードスロットゲーム（Unity 6.3 LTS / PC 向け）。

## 概要

| 項目 | 内容 |
|------|------|
| エンジン | Unity 6.3 LTS |
| 対象プラットフォーム | PC（Windows / macOS） |
| リール数 | 5 リール × 3 行表示 |
| ペイライン | 25 ライン（固定） |
| 初期コイン | 1,000 枚 |
| 目標 RTP | 約 94〜96% |

## ゲーム機能

- **スロット本体** — 5×3 グリッド、25 ペイライン、左→右一致判定（3 個以上）
- **Wild シンボル** — 特殊シンボルを除く任意シンボルに代替
- **Scatter シンボル** — 出現位置問わず判定。3 個以上でフリースピン発動
- **フリースピン** — コイン消費なし、配当×2。3/4/5 個で 10/15/20 回付与。再トリガーあり
- **ボーナスラウンド** — 宝箱選択ミニゲーム（9 個から 3 個選択）。専用シーンで進行
- **オートスピン** — 10/25/50/100 回設定。残高不足・ボーナス発動で自動停止
- **データ永続化** — コイン残高・ベット額・音量・統計を JSON で自動保存

## アーキテクチャ

MVP パターンを採用。

```
Model（ピュア C#・Unity 非依存）
  GameState        ← コイン・フリースピン残数などゲーム状態を保持
  SpinResult       ← 1スピンの結果（当選ライン・獲得コイン等）
  SaveData         ← JSON 永続化のデータ構造

Presenter（MonoBehaviour・フロー制御）
  GameManager      ← ステートマシン頂点。状態遷移のみ担当
  SpinManager      ← 5リール回転・停止の調整（0.3 秒ずらし停止）
  ReelController   ← 個別リールの回転・停止制御
  BonusManager     ← フリースピン・ボーナスラウンドのフロー
  AudioManager     ← BGM/SE 再生（プール方式・DOTween フェード）
  BootManager      ← 起動時の依存注入（Composition Root）
  GameContext      ← Presenter 間で共有するコンテキスト

View（MonoBehaviour・表示専用）
  UIManager        ← 各 View パネルの統括
  ReelView         ← シンボルスクロールアニメーション（循環バッファ）
  SymbolView       ← 個別シンボルのスプライト・アニメーション
  MainHUDView      ← コイン残高・ベット額・スピンボタン表示
  WinPopupView     ← 獲得コインポップアップ
  WinLevel         ← 配当額に応じた演出レベル定義（Small/Big/Mega）
  FreeSpinHUDView  ← フリースピン残数表示
  SettingsView     ← 音量設定パネル
  PaytableView     ← 配当表パネル
  BonusRoundView   ← 宝箱選択 UI
```

- **Model は View を参照しない**。View はデータを受け取って描画するのみ
- ゲームパラメータ（配当・確率・ライン定義）はすべて **ScriptableObject** で外出し。コード変更なしに調整可能
- RTP 調整・アセット一括生成のための **Editor ツール**（`RtpTuner`）を完備。シミュレーション結果は `RTP_Result.csv` に出力
- 非同期処理は **UniTask**（`async UniTask`）に統一。Coroutine は使わない

## シーン構成

| シーン | 役割 |
|-------|------|
| `Boot.unity` | 初期化・ロード画面 |
| `Main.unity` | スロット本体（メインシーン） |
| `BonusRound.unity` | ボーナスラウンド（Additive ロード） |

## プロジェクト構造

```
Assets/
  Scenes/
  Scripts/
    Core/       ← GameManager, SpinManager, ReelController,
                   BonusManager, AudioManager, BootManager, GameContext
    Model/      ← GameState, SpinResult, SaveData（ピュア C#）
    View/       ← UIManager, ReelView, SymbolView, MainHUDView,
                   WinPopupView, PaylineView, WinLevel, FreeSpinHUDView,
                   SettingsView, PaytableView, BonusRoundView
    Audio/      ← AudioManager
    Data/       ← ScriptableObject 定義クラス群
    Utility/    ← PaylineEvaluator（static）, SaveDataManager,
                   IRandomGenerator, SystemRandomGenerator,
                   SeededRandomGenerator
  Editor/
    Utility/    ← RtpCalculator.cs
    RtpTuner.cs, SceneBuilder.cs, ScriptableObjectCreator.cs,
    ViewSetupTool.cs
  ScriptableObjects/
    Symbols/    ← SymbolData × 11（Bonus シンボル含む）
    Reels/      ← ReelStripData × 5
    Paylines/   ← PaylineData
    PayoutTable/← PayoutTableData
  Art/
  Audio/
  Tests/        ← EditMode / PlayMode ユニットテスト
  Settings/     ← URP / InputSystem 設定アセット
docs/
  requirements.md   ← 要件定義書
  design.md         ← 設計書
  adr/              ← Architecture Decision Records（ADR-001〜006）
```

## 外部ライブラリ

| ライブラリ | 用途 | 導入方法 |
|-----------|------|---------|
| [UniTask](https://github.com/Cysharp/UniTask) | 非同期処理（async/await） | UPM |
| TextMeshPro | テキスト表示 | Unity 組み込み |
| DOTween（無料版） | UI / シンボルアニメーション補完 | Asset Store / UPM |
| Universal RP（URP） | 2D レンダリングパイプライン | UPM |
| New Input System | キーボード・マウス入力 | UPM |

## テストの実行

```bash
# Edit Mode テスト（PaylineEvaluator・GameState・SaveDataManager）
/Applications/Unity/Hub/Editor/<version>/Unity.app/Contents/MacOS/Unity \
  -batchmode -projectPath . -runTests -testPlatform EditMode \
  -testResults TestResults.xml -logFile test.log

# Play Mode テスト（スピンフロー全体）
/Applications/Unity/Hub/Editor/<version>/Unity.app/Contents/MacOS/Unity \
  -batchmode -projectPath . -runTests -testPlatform PlayMode \
  -testResults TestResults-PlayMode.xml -logFile test-playmode.log
```

## 実装状況

| フェーズ | 内容 | 状態 |
|---------|------|------|
| Phase 0 | 環境構築（アセンブリ定義・UPM パッケージ導入） | 完了 |
| Phase 1 | Model + ユニットテスト | 完了 |
| Phase 2 | ScriptableObject アセット作成 | 完了 |
| Phase 3 | Presenter / Core 実装 | 完了 |
| Phase 4 | View 実装・シーン構築 | 完了 |
| Phase 5 | RTP 検証・リールストリップ調整 | 完了 |
| Phase 6 | 統合テスト・最終ビルド確認 | 完了 |

## ドキュメント

| ドキュメント | 内容 |
|-------------|------|
| [要件定義書](docs/requirements.md) | 機能要件・非機能要件・シンボル仕様・ペイライン定義 |
| [設計書](docs/design.md) | アーキテクチャ・クラス設計・ステートマシン・UI レイアウト |
| [ADR 一覧](docs/adr/) | 技術選定・方式決定の記録 |
