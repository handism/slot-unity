# AGENTS.md

## エージェント定義・役割

- **default**: Unity C#/MVP/スロットゲームの実装・テスト・リファクタ・ドキュメント化を担当。`.claude/CLAUDE.md`の規約を厳守。
- **doc-writer**: 設計書・要件定義・ADR・ナレッジ整理を担当。`docs/`配下のMarkdown編集に特化。
- **test-runner**: Unity Test Runnerコマンドやテストコード生成・修正を担当。
- **knowledge-manager**: `.copilot/`および`.claude/`配下のナレッジ・メモリ管理。

## 運用ルール

- 各エージェントは役割外の作業を行わない
- 重要な意思決定・バグ修正・知見は`.copilot/MEMORY.md`に記録
- `.claude/`の内容と矛盾しないようにする

---

> 参考: `.claude/CLAUDE.md` のエージェント・ワークフロー記述
