---
name: Fantasy Slot プロジェクト概要
description: slot-unity リポジトリで開発中のファンタジーテーマ 5 リールスロットゲームの基本仕様と設計方針
type: project
---

Unity 6.3 LTS で 1 人開発中のアーケード系スロットゲーム（PC 向け）。

**基本仕様:**
- 5 リール × 3 行、25 固定ペイライン
- シンボル 11 種（高配当 4 / 低配当 4 / Wild / Scatter / **Bonus**）
  - Bonus (ID=10, SymbolType.Bonus): リール 0/2/4 全出現でボーナスラウンド発動（Scatter とは独立）
- フリースピン（Scatter 3個以上）+ ボーナスラウンド（宝箱選択ミニゲーム）
- 初期コイン 1000、ベット選択肢 10/20/50/100
- ローカル JSON 永続化（サーバー連携なし）

**実装進捗（2026-03-21 時点）:**
- フェーズ 0〜4: 完了（Model / Core / View / Editor ScriptableObject生成スクリプト）
- フェーズ 5: RtpCalculator.cs 完了。シミュレーション実行・リール調整は未実施
- フェーズ 6: 統合テスト未実施
- **次のアクション**: Unity Editor で `SlotGame/Create All ScriptableObject Assets` を実行してアセット生成 → RTP シミュレーション実施

**採用技術:**
- Unity 6.3 LTS
- アーキテクチャ: MVP パターン（Model はピュア C#、テスタブル）
- 非同期: UniTask
- データ: ScriptableObject（シンボル・リール・ペイライン・配当テーブル）
- 保存: Application.persistentDataPath/savedata.json

**ドキュメント（docs/）:**
- requirements.md: 要件定義書
- design.md: 設計書（クラス設計・ステートマシン・シーン構成）
- adr/: ADR-001〜006

**Why:** 最初からしっかり設計する方針。ゲームバランス調整はコード修正なし（ScriptableObject 編集のみ）で対応できる設計。
**How to apply:** 実装時は MVP の層分離を守ること。GameManager はステートマシンとしての遷移のみ、具体処理は各 Manager に委譲。
