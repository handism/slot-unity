# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 実装進行ルール（厳守）

1. **PLAN.md を上から順番に** マイクロインクリメンタル手法で実装する
2. **1タスク完了したら必ず停止**し、ユーザーに完了報告をして次のタスクへの進行確認を取る
3. **完了したタスクは即座にチェック** — `- [ ]` → `- [x]` に更新する
4. **コミットは絶対に自動で行わない** — ユーザーが確認・承認してからユーザー自身がコミットする

## プロジェクト概要

ファンタジーテーマの 5 リール・25 ペイライン・アーケード系スロットゲーム（Unity 6.3 LTS / PC 向け）。
詳細仕様は `docs/requirements.md`、設計は `docs/design.md`、技術選定の背景は `docs/adr/` を参照。

## よく使うコマンド

Unity プロジェクトはエディタ操作が中心だが、CLI からも以下を実行できる。

```bash
# Unity Test Runner をバッチモードで実行（Edit Mode テスト）
/Applications/Unity/Hub/Editor/6000.3.11f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -projectPath . -runTests -testPlatform EditMode \
  -testResults TestResults.xml -logFile test.log

# Play Mode テスト
/Applications/Unity/Hub/Editor/6000.3.11f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -projectPath . -runTests -testPlatform PlayMode \
  -testResults TestResults-PlayMode.xml -logFile test-playmode.log
```

## アーキテクチャ

### レイヤー構成（MVP）

```
Model（ピュア C#・Unity 非依存）
  GameState        ← コイン・フリースピン残数などゲーム状態を保持
  SpinResult       ← 1スピンの結果（当選ライン・獲得コイン等）
  SaveData         ← JSON 永続化のデータ構造
  SlotConfig       ← GameConfigData から変換されたランタイム設定（record）
  SessionStats     ← セッション統計（スピン数・勝率・最大勝利額など）

Presenter（MonoBehaviour・フロー制御）
  GameManager      ← ステートマシン頂点。状態遷移のみ担当
  SpinManager      ← 5リール回転・停止の調整（0.3 秒ずらし停止）
  ReelController   ← 個別リールの回転・停止制御
  BonusManager     ← フリースピン・ボーナスラウンドのフロー
  AudioManager     ← BGM/SE 再生（プール方式・DOTween フェード）
  BootManager      ← 起動時の依存注入（Composition Root）
  GameContext      ← Presenter 間で共有するコンテキスト

View（MonoBehaviour・表示専用）
  UIManager        ← 各 View パネルの統括（PaylineView プールも管理）
  ReelView         ← シンボルスクロールアニメーション（循環バッファ）
  SymbolView       ← 個別シンボルのスプライト・アニメーション
  MainHUDView      ← コイン残高・ベット額・スピンボタン表示
  WinPopupView     ← 獲得コインポップアップ
  FreeSpinHUDView  ← フリースピン残数表示
  SettingsView     ← 音量設定パネル
  PaytableView     ← 配当表パネル
  BonusRoundView   ← 宝箱選択 UI
  PaylineView      ← 当選ラインを LineRenderer で描画（グロウアニメ付き）
  StatsView        ← セッション統計パネル
```

- **Model は View を参照しない**。View はデータを受け取って描画するのみ
- `GameManager` の `GamePhase` enum がステートマシンの状態を管理する
- ホバー等の純粋な視覚フィードバックは View 内で完結させてよい

### ScriptableObject によるデータ管理

ゲームパラメータはすべて ScriptableObject に外出しされており、**コード変更なしに調整可能**。

| アセット            | 内容                                                                  |
| ------------------- | --------------------------------------------------------------------- |
| `SymbolData` × 12   | 各シンボルの配当倍率・スプライト・アニメ（Bonus・Blank シンボル含む） |
| `ReelStripData` × 5 | リールの出目テーブル（重み付き確率、1リール 88 スロット）             |
| `PaylineData`       | 25 ペイラインの定義（行インデックス配列）                             |
| `PayoutTableData`   | Scatter 配当・ボーナス報酬の重み                                      |
| `GameConfigData`    | 初期コイン・ベット額・リール数・音量デフォルト値など全体設定          |

`GameConfigData.ToModelConfig()` が `SlotConfig`（純粋 C# record）に変換され、Runtime に渡される。ScriptableObject を直接 Model に持ち込まない。

### 非同期処理

UniTask（`async UniTask`）を使用。Coroutine は使わない。
キャンセルは `this.GetCancellationTokenOnDestroy()` で取得した `CancellationToken` を渡す。

### シーン間データ引き継ぎ

`GameContext`（`Scripts/Core/GameContext.cs`）が Boot シーンから Main シーンへ Model インスタンスを渡す静的コンテナ。`GameManager.Awake()` で `GameContext.GameState != null` をチェックし、null の場合はデバッグ用フォールバックで自前初期化する。

### ランダム生成の依存注入

`IRandomGenerator` インターフェース（`Scripts/Utility/`）を介して乱数生成器を渡す。テストでは `SeededRandomGenerator` を使い再現性を担保、本番では `SystemRandomGenerator`。`BootManager` が生成して `GameContext.Random` にセットする。

### Canvas アーキテクチャ

デュアルキャンバス構成を採用。コイン残高カウントアップアニメーション中に HUD 再構築が走らないよう分離。

| キャンバス  | 用途                                                 |
| ----------- | ---------------------------------------------------- |
| Main Canvas | リールグリッド・ポップアップ                         |
| HUD Canvas  | コイン残高・WIN 表示（カウンターアニメーション対象） |

### 配当判定

`PaylineEvaluator`（`Scripts/Utility/` の static クラス）が Unity 非依存の純粋関数として実装されており、Edit Mode でユニットテストできる。

### 保存

`Application.persistentDataPath/savedata.json` に `SaveData` クラスを JSON シリアライズして保存。
破損時はデフォルト値でフォールバックし、破損ファイルは `.bak` にリネームする。

## シーン構成

| シーン             | 役割                                |
| ------------------ | ----------------------------------- |
| `Boot.unity`       | 初期化・ロード画面                  |
| `Title.unity`      | タイトル画面                        |
| `Main.unity`       | スロット本体（メインシーン）        |
| `BonusRound.unity` | ボーナスラウンド（Additive ロード） |

## 外部ライブラリ

- **UniTask**: UPM で管理。`Cysharp/UniTask` のパッケージ参照
- **DOTween（無料版）**: UI / シンボルアニメーション補完
- **TextMeshPro**: Unity 組み込み

## テスト対象と方法

| 対象               | テスト種別                                         |
| ------------------ | -------------------------------------------------- |
| `PaylineEvaluator` | Edit Mode ユニットテスト（必須）                   |
| `GameState`        | Edit Mode ユニットテスト（必須）                   |
| `SaveDataManager`  | Edit Mode テスト（仮想パス使用）                   |
| `RtpCalculator`    | Edit Mode（エディタ専用、10 万回シミュレーション） |
| スピンフロー       | Play Mode テスト（`SpinFlowTests.cs`）             |

スピンフローテストでは `SeededRandomGenerator` を注入して再現性を担保する。

---

## 開発ルール

### 作業着手順序

新機能・新システムの実装を始める前に、必ず以下の順序を守る。

1. **仕様確認** — 不明点をユーザーに質問して固める。詳細は「お任せ」でよい（ベストプラクティスで決定し、成果物内に決定理由を明示する）
2. **要件定義書** — `docs/requirements.md` を作成・更新
3. **設計書** — `docs/design.md` を作成・更新
4. **ADR** — `docs/adr/ADR-NNN-*.md` を追加（技術選定・方式決定のたびに）
5. **CLAUDE.md 更新** — アーキテクチャや規約に変化があれば反映
6. **実装**

ドキュメントを飛ばして実装を提案しない。

### コーディング規約

- **非同期処理は UniTask のみ**。`IEnumerator` / Coroutine は使わない
- **Model はピュア C#** にする（`UnityEngine` への依存を持たせない）
- **View はデータを受け取って描画するだけ**。ロジックを書かない
- **ゲームパラメータ（配当・確率・ライン定義）はコードに直書きしない**。ScriptableObject に外出しする
- `GameManager` はステートマシンとしての**遷移制御のみ**を担う。具体的な処理は各 Manager に委譲する

### Unity C# ガイドライン

- **asmdef の依存確認** — Unity スクリプトを別フォルダに移動する前に、Assembly Definition（`.asmdef`）の参照関係を必ず確認する
- **DOTween は `DOTween.To()` パターンを優先** — `DOFade()` / `DOAnchorPosY()` 等の拡張メソッドはモジュール構成によって使えない場合があるため、`DOTween.To()` を使う
- **ScriptableObject アセットを再作成しない** — 既存アセットを編集して使う。再作成するとシーン参照（GUID）が壊れる
- **コンパイルエラー修正後は連鎖エラーを確認** — 1 件修正しただけで完了と報告せず、残存エラーを確認してから報告する

### テスト・検証の限界

デバイス・シミュレーター・Unity Play Mode が必要なタスクについては、「実施したこと」と「手動確認が必要なこと」を明示する。手動検証が未実施のタスクを完了とマークしない。

### デバッグ・バグ修正

バグを修正した際は、**同じパターンがコードベース全体に存在しないか検索**して確認する。最初に見つけた箇所だけ修正して完了とせず、類似箇所に同じ対処が必要かどうかを確認してから報告する。

### ADR の追加基準

以下の判断をした場合は ADR を新規作成する。

- 外部ライブラリの採用・変更
- アーキテクチャパターンの採用・変更
- データ永続化方式の変更
- 複数の実装方式で迷い、一方を選んだ場合

### ナレッジの保存（Knowledge Persistence）

学習内容・ノウハウ・メモリの保存を求められた場合、**必ずプロジェクトレベルの `.claude/memory/` ディレクトリに保存する**（ユーザーレベルの `~/.claude/` ではない）。ドキュメントは特に指示がない限り日本語で記述する。

以下のタイミングで `.claude/memory/` にログを保存する。

| タイミング             | 保存内容                                                   | ファイル例                          |
| ---------------------- | ---------------------------------------------------------- | ----------------------------------- |
| 重要な意思決定をした時 | 何を・なぜ決めたか（ADR に書かないレベルの細かい判断含む） | `decision_YYYYMMDD_*.md`            |
| バグを修正した時       | 原因・再現条件・対処内容                                   | `bugfix_YYYYMMDD_*.md`              |
| 新たな知見を得た時     | ユーザーのスタイル・好み・注意点など                       | `feedback_*.md` / `user_profile.md` |
| 仕様が変更・確定した時 | `project_fantasy_slot.md` を更新                           | —                                   |

保存後は必ず `.claude/memory/MEMORY.md` のインデックスも更新する。

**フォーマット（frontmatter 必須）:**

```markdown
---
name: 短いタイトル
description: 一行説明（何の情報か）
type: project | feedback | user | reference
---

内容...
```
