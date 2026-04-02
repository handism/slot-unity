---
name: save-knowledge
description: セッション中に得た知見をプロジェクトレベルの .copilot/memory/ に保存するスキル
---

# ナレッジ保存スキル

セッション中に得た学習内容・意思決定・ユーザーの好みなどをプロジェクトレベルの `.copilot/memory/` に保存します。

## 手順

1. **セッションを振り返り**、保存すべき知見を特定する
   - バグの原因・対処・再発防止策
   - 重要な意思決定とその理由
   - ユーザーのスタイル・好み・注意点
   - 確定した仕様変更

2. **ファイル名を決める**（命名規則を守る）

   | 内容             | 命名パターン                     |
   | ---------------- | -------------------------------- |
   | バグ修正         | `bugfix_YYYYMMDD_<keyword>.md`   |
   | 意思決定         | `decision_YYYYMMDD_<keyword>.md` |
   | フィードバック   | `feedback_<topic>.md`            |
   | ユーザー情報     | `user_profile.md`                |
   | プロジェクト概要 | `project_fantasy_slot.md` を更新 |

3. **ファイルを書く**（frontmatter 必須・日本語で記述）

   ```markdown
   ---
   name: 短いタイトル
   description: 一行説明（何の情報か）
   type: project | feedback | user | reference
   ---

   ## 概要

   （何が起きたか・何を決めたか）

   ## 原因 / 背景

   （経緯や理由など）
   ```

---
