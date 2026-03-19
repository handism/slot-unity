# ADR-002: アーキテクチャパターンとして MVP を採用

**ステータス**: 承認済み
**日付**: 2026-03-20

---

## コンテキスト

スロットゲームのコードアーキテクチャを決定する。
ゲームの状態（コイン残高、スピン結果、ボーナス状態）と UI 表示が密結合になりやすく、テスト困難・スパゲッティ化のリスクがある。
1人開発で規模は中程度（スクリプト数 20〜40 本程度を想定）。

## 選択肢

| パターン | 概要 |
|---------|------|
| **MVP** | Model（状態・ロジック）/ View（UI） / Presenter（橋渡し）に分離 |
| MVC | Controller が View を直接更新。Unity では View と Controller の責務が曖昧になりやすい |
| ECS (DOTS) | 高パフォーマンスだが学習コストが高く、2D スロットには過剰 |
| 無設計（MonoBehaviour 直書き） | 短期は速いが、ボーナスロジック追加時に破綻しやすい |

## 決定

**MVP パターンを採用する。**

具体的には:
- **Model**: ピュア C# クラス（`GameState`, `SpinResult`, `SaveData`）。Unity 非依存でユニットテスト可能
- **View**: `MonoBehaviour` ベースの純粋な表示担当（データを受け取って描画するのみ）
- **Presenter**: `GameManager` を頂点としたステートマシンが各 Manager を調整

## 理由

- Model がピュア C# なので Unity Test Runner での自動テストが容易
- `PaylineEvaluator`（配当計算）を static クラスとして切り出せるため、確率・倍率の検証が独立してテストできる
- 1人開発でも責務が明確なため、数週間後に自分のコードを読み返せる
- Unity の `MonoBehaviour` のライフサイクル（Awake/Start/Update）と Model ロジックが混在しない

## トレードオフ

- 純粋な MVP より Presenter が複数（`SpinManager`, `BonusManager` 等）に分散するため、GameManager の責務が大きくなりがち
  → 対策: GameManager はステートマシンとしての遷移のみを担い、各 Manager に具体的な処理を委譲する
- View の更新をすべて Presenter 経由にすると、細粒度の UI イベント（ホバー演出など）でも Presenter を経由する必要がある
  → 対策: 純粋な視覚フィードバック（ホバー等）は View 内で完結させ、データ変更を伴う操作のみ Presenter に通知する
