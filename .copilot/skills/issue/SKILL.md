---
name: issue
description: GitHub Issue の内容に沿って実装・改善を行い、ブランチを切って PR を作成する
---

# Issue 実装スキル

引数で渡された Issue 番号（`$ARGUMENTS`）の内容を読み込み、ブランチ作成 → 実装 → PR 作成までを一貫して行います。

## 手順

### STEP 1 — Issue の内容を取得・分析する

以下のコマンドを **順に試し**、最初に成功したものを使う。
macOS では `gh` コマンドが TLS 証明書チェーンの問題で失敗することがある（`x509: OSStatus -26276`）ため、`curl` を最終手段として用意する。

```bash
# リモートから owner/repo を自動取得（SSH・HTTPS どちらの URL にも対応）
REPO=$(git remote get-url origin | sed 's/.*github\.com[:/]\(.*\)/\1/' | sed 's/\.git$//')

# 1. gh（GraphQL）
GODEBUG=x509usefallbackroots=1 gh issue view $ARGUMENTS --json number,title,body,labels,assignees,milestone,comments 2>/dev/null \
  || \
  # 2. gh（REST API）
  GODEBUG=x509usefallbackroots=1 gh api "repos/$REPO/issues/$ARGUMENTS" 2>/dev/null \
  || \
  # 3. curl（TLS 検証スキップ）— 公開リポジトリのみ。内容の盗聴リスクは許容範囲
  curl -s --insecure "https://api.github.com/repos/$REPO/issues/$ARGUMENTS"
```

取得した内容をもとに以下を整理する：

- **目的**: Issue が解決しようとしている問題・要求
- **受け入れ条件**: 完了と見なせる基準（記載がなければ Issue 本文から推定する）
- **影響範囲**: 変更が及ぶファイル・コンポーネント（CLAUDE.md のアーキテクチャを参照）
- **懸念点**: 実装前に確認が必要な点

分析結果をユーザーに提示し、**着手前に承認を得る**。

---
