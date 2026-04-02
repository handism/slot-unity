# .copilot/README.md

このディレクトリはGitHub CopilotおよびCopilot Chat向けのカスタマイズ・ガイドライン・プロンプト・エージェント設定ファイルを格納します。

## 推奨ファイル構成

- `copilot-instructions.md` : プロジェクト固有のCopilot指示文
- `AGENTS.md` : エージェント定義・役割説明
- `SKILL-*.md` : スキル定義（必要に応じて追加）
- `MEMORY.md` : Copilot用ナレッジインデックス

## 運用ルール

- `.claude/`と同様、プロジェクト固有の知識・規約・ワークフローを記載
- 変更時は必ず内容・意図を明記すること
- Copilot Chatの指示文は日本語で記述可

---

> 参考: `.claude/CLAUDE.md` を参照し、Copilot用に最適化してください。
