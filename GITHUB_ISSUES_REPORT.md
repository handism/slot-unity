# GitHub Issues 提案レポート

以下の差異に基づき、GitHub Issue として登録すべき項目を提案します。

## Issue 1: [DOCS] ターボモードとキーボードショートカットの仕様追加
**タイトル**: [DOCS] Update requirements.md and design.md for Turbo Mode and Keyboard Shortcuts
**ラベル**: documentation
**本文**:
実装されているがドキュメントに記載がない以下の機能について、`requirements.md` および `design.md` を更新してください。

- **ターボモード**:
  - `GameConfigData.cs` で定義されている `TurboSpinDuration` (0.5s), `TurboStopInterval` (0.1s) の値。
  - `GameState.cs` の `IsTurbo` フラグによる状態管理。
  - `MainHUDView.cs` での UI 制御。
- **キーボードショートカット**:
  - `SlotInputHandler.cs` に実装されているキーバインド一覧。
    - Spin: Space / Enter
    - Bet Up/Down: Up / Down Arrows
    - Auto-Spin: A
    - Skip: S
    - Mute: M
    - Turbo: T
    - Paytable: P

## Issue 2: [DOCS] セッション統計とミュート機能の設計詳細の追記
**タイトル**: [DOCS] Add SessionStats and Mute logic to design.md
**ラベル**: documentation
**本文**:
実装済みの以下の設計詳細を `design.md` に追記してください。

- **セッション統計**:
  - `SessionStats` 構造体の定義と、`GameState.cs` での算出ロジック。
  - `StatsView.cs` による表示。
- **ミュート機能**:
  - `AudioManager.cs` の `ToggleMute()` ロジック（前回音量の保存と復元）。
- **解像度管理**:
  - `ResolutionManager.cs` による 16:9 アスペクト比維持の仕組み。

## Issue 3: [SPEC] ベット額選択 UI の仕様と実装の不一致の解消
**タイトル**: [SPEC] Align documentation with implementated Bet Button UI
**ラベル**: enhancement, documentation
**本文**:
`requirements.md` 2.7 節に「ボタン or スライダー」と記載がありますが、現在は 10/20/50/100 の個別ボタン方式 (`MainHUDView.cs`) で実装されています。ドキュメントを現状に合わせて更新するか、スライダー方式の検討が必要か判断してください。

## Issue 4: [DOCS] セーブデータのチェックサムとソルト計算式の明文化
**タイトル**: [DOCS] Document Checksum/Salt calculation in design.md
**ラベル**: security, documentation
**本文**:
`SaveDataManager.cs` で実装されているセーブデータの整合性検証について、`design.md` 4.5 節に詳細なハッシュ計算式と `SlotConfig` (または `GameConfigData`) から供給されるソルト値の扱いを明文化してください。

## Issue 5: [FIX] シンボルアニメーションの仕様統一
**タイトル**: [FIX] Clarify/Unify usage of symbol win animations vs pulse effect
**ラベル**: enhancement
**本文**:
`SymbolData.cs` の `winAnim` フィールドと `SymbolView.cs` の `PlayPulseAnimation` / `PlayWinAnim` の使い分けが不明確です。現在はパルス演出が主に使用されていますが、Animator によるアニメーション演出の有無や、実装方針についてドキュメントを更新し、必要であれば実装を整理してください。
