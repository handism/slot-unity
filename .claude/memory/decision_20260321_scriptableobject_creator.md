---
name: ScriptableObject アセット生成方式
description: Unity .asset ファイルを Editor スクリプトで一括生成するアプローチの選択理由と実装詳細
type: project
---

Unity の ScriptableObject アセットは CLI から作成できないため、`Assets/Editor/ScriptableObjectCreator.cs` に `[MenuItem]` スクリプトを用意し、Unity Editor 上でメニューから一括生成する方式を採用した。

## 実装詳細

**生成コマンド**: Unity メニュー `SlotGame/Create All ScriptableObject Assets`

**生成アセット一覧** (`Assets/ScriptableObjects/` 以下):
| フォルダ | アセット | 内容 |
|---------|---------|------|
| Symbols/ | Dragon〜Bonus.asset × 11 | 配当倍率・SymbolType を設定済み。Sprite/WinAnim は Art 整備後に手動設定 |
| Reels/ | Reel0〜Reel4.asset × 5 | 各リール 60 シンボル |
| Paylines/ | PaylineData.asset | 25 ペイライン定義（requirements.md 準拠） |
| PayoutTable/ | PayoutTableData.asset | Scatter 配当 + ボーナス報酬重みテーブル |

**リールストリップ設計** (各リール 60 シンボル):
| シンボル | 枚数 | 確率 |
|---------|------|------|
| Jack | 10 | 16.7% |
| Queen | 10 | 16.7% |
| King | 10 | 16.7% |
| Ace | 10 | 16.7% |
| Sword | 5 | 8.3% |
| Crystal | 4 | 6.7% |
| Phoenix | 3 | 5.0% |
| Wild | 3 | 5.0% |
| Dragon | 2 | 3.3% |
| Scatter | 2 | 3.3% |
| Bonus | 1 | 1.7% |

**配置アルゴリズム**: 素数ステップ(7) を使ったインターリーブ。`gcd(7,60)=1` なので全60スロットを一巡し均等分散。リールごとに開始オフセットを変えてパターンの単調さを防ぐ。

## Why

- YAML 直書きは GUID 管理が複雑でエラーが起きやすい
- スクリプト生成なら型安全で、データ変更時も再実行で対応できる
- 既存アセットはスキップするため冪等に実行可能

## How to apply

- リールストリップを調整したい場合はスクリプト内の `baseCounts` テーブルの枚数を変更して再実行
- Art アセット（Sprite/AnimationClip）は Unity Editor で各 SymbolData に手動アサイン
- RTP シミュレーション後にストリップ枚数を調整し再実行するワークフロー
