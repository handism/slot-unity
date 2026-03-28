---
name: issue
description: GitHub Issue の内容に沿って実装・改善を行い、ブランチを切って PR を作成する
---

# Issue 実装スキル

引数で渡された Issue 番号（`$ARGUMENTS`）の内容を読み込み、ブランチ作成 → 実装 → PR 作成までを一貫して行います。

## 手順

### STEP 1 — Issue の内容を取得・分析する

```bash
GODEBUG=x509usefallbackroots=1 gh issue view $ARGUMENTS --json number,title,body,labels,assignees,milestone,comments
```

> **TLS エラーが出た場合**: macOS キーチェーンと Go の証明書検証が競合することがある。`GODEBUG=x509usefallbackroots=1` を付けることで Go 組み込みの CA バンドルを使い回避できる。それでも失敗する場合は REST API で代替する：
> ```bash
> GODEBUG=x509usefallbackroots=1 gh api repos/:owner/:repo/issues/$ARGUMENTS
> ```

取得した内容をもとに以下を整理する：

- **目的**: Issue が解決しようとしている問題・要求
- **受け入れ条件**: 完了と見なせる基準（記載がなければ Issue 本文から推定する）
- **影響範囲**: 変更が及ぶファイル・コンポーネント（CLAUDE.md のアーキテクチャを参照）
- **懸念点**: 実装前に確認が必要な点

分析結果をユーザーに提示し、**着手前に承認を得る**。

---

### STEP 2 — ブランチを作成する

ブランチ名は `feature/issue-<番号>-<キーワード>` の形式で作成する（キーワードは Issue タイトルから英数字・ハイフンで3〜5語）。

```bash
git checkout main && git pull origin main
git checkout -b feature/issue-$ARGUMENTS-<keyword>
```

---

### STEP 3 — 実装計画を立てる（CLAUDE.md の開発ルールを厳守）

1. 仕様が不明瞭な点はユーザーに質問して固める
2. 必要に応じて `docs/requirements.md` / `docs/design.md` / `docs/adr/` を更新する
3. 実装ステップをリストアップしてユーザーに提示する
4. 承認を得てから実装に入る

> **注意**: 実装前にドキュメントを飛ばさない（`feedback_workflow.md` 参照）

---

### STEP 4 — 実装する（マイクロインクリメンタル方式）

- 実装は小さな単位に分割して進める
- 複数ファイルにまたがる変更・複雑な変更は、ステップごとにユーザーへ確認を取る
- コンパイルエラーや不具合が発生したら **次のステップへ進まず停止して報告する**
- バグ修正時は同様のパターンがコードベース全体に存在しないか検索する

---

### STEP 5 — PR を作成する

実装完了後、以下の手順で PR を作成する。

```bash
# リモートへプッシュ
git push -u origin HEAD

# PR 作成
gh pr create \
  --title "<Issue タイトルを簡潔に>" \
  --body "$(cat <<'EOF'
## 概要

- <変更内容を箇条書きで>

## 対応 Issue

Closes #$ARGUMENTS

## 変更ファイル

- <主要な変更ファイルを列挙>

## テスト・検証

- [ ] <手動確認が必要な項目>
- [ ] デバイス・Play Mode での動作確認（該当する場合）

## 備考

<実装上の判断・制約・注意点があれば記載>

🤖 Generated with [Claude Code](https://claude.ai/claude-code)
EOF
)"
```

---

### STEP 6 — セッション知見を保存する（オプション）

実装中に得たノウハウ・落とし穴・意思決定があれば `/save-knowledge` で保存する。

---

## 注意事項

- **手動検証が必要なタスク**（Unity Play Mode・デバイス動作）は完了とマークしない
- ScriptableObject を再作成しない・asmdef の依存を確認する（`feedback_diagnose_before_fix.md` 参照）
