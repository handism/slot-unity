# 実装とドキュメントの差異調査レポート

## 1. 未記載の機能 (Undocumented Features)
実装されているが、`requirements.md` や `design.md` に記載されていない、または詳細が不足している機能。

- **ターボモード (Turbo Mode)**:
  - `GameConfigData.cs` および `SlotConfig.cs` に `turboSpinDuration` (0.5s), `turboStopInterval` (0.1s) が実装されている。
  - `GameState.cs` で状態管理され、`MainHUDView.cs` に切り替えボタンが存在する。
- **キーボードショートカット**:
  - `SlotInputHandler.cs` により、Input System を介したキーボード操作（Spin, BetUp/Down, AutoSpin, Skip, Mute, Turbo, Paytable）が実装されている。
- **ミュート機能 (Toggle Mute)**:
  - `AudioManager.cs` に `ToggleMute()` が実装され、音量を保持したままミュート・解除が可能。
- **セッション統計 (Session Stats)**:
  - `GameState.cs` および `SessionStats.cs` で、セッションごとの当選率、最大獲得額、損益などが計算・管理されている。
  - `StatsView.cs` でこれらを表示する UI も存在する。
- **レターボックス対応 (ResolutionManager)**:
  - `ResolutionManager.cs` により、16:9 のアスペクト比を維持する機能が実装されている。

## 2. 仕様の不一致 (Specification Mismatches)
ドキュメントの記載と実際の実装が異なっている箇所。

- **ベット額の選択 UI**:
  - `requirements.md` 2.7節では「ボタン or スライダー」とあるが、`MainHUDView.cs` の実装は 10/20/50/100 の個別ボタン方式になっている。
- **ボーナスラウンドの発動リール**:
  - `requirements.md` 2.4.2節では「リール 1・3・5」とあるが、`SlotConfig.cs` や `GameConfig.asset` では `bonusTriggerReels` としてインデックス `0, 2, 4` が指定されている（実質同じだが、インデックス表記が混在）。
- **セーブデータのチェックサム**:
  - `design.md` 4.5節では SHA256 とあるが、実装（`SaveDataManager.cs`）ではソルトを用いた SHA256 ハッシュを Base64 エンコードして保存している。詳細な計算式がドキュメントに未記載。
- **UI グラデーション (UIGradient)**:
  - 設計書には「ブルー系グラデーション」などの記述はあるが、カスタムコンポーネント `UIGradient.cs` による実装の詳細は記載されていない。

## 3. 未実装または確認が必要な事項 (Missing / To be verified)
ドキュメントにあるが実装が見当たらない、または `TODO.md` に残っている事項。

- **オートスピン回数の詳細**:
  - `TODO.md` では解決済みとなっているが、`requirements.md` の「10 / 25 / 50 / 100 回」の選択 UI が `MainHUDView.cs` で動的に生成される点は特筆すべき仕様。
- **設定メニューの「ゲーム説明」**:
  - `TODO.md` で解決済みとされているが、`UIManager.cs` 内で `paytableView` をクローンして動的に生成する実装になっており、リソース管理上の注意点として設計書に記載すべき。
- **シンボルのアニメーション**:
  - `SymbolData.cs` に `winAnim` があるが、実際の `SymbolView.cs` ではパルスアニメーション (`PlayPulseAnimation`) が主に使用されており、Animator による個別アニメーションとの使い分けが不明確。
