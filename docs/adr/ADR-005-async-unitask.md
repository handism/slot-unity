# ADR-005: 非同期処理に UniTask を採用

**ステータス**: 承認済み
**日付**: 2026-03-20

---

## コンテキスト

スロットゲームではリールアニメーション・当選演出・ボーナスラウンド遷移など、時間を伴う非同期処理が多数存在する。
これらを適切に制御する方法を選定する。

## 選択肢

| 方式 | 概要 |
|------|------|
| **UniTask** | Cysharp 製の Unity 向け async/await ライブラリ |
| Coroutine | Unity 標準の `IEnumerator` ベース |
| `async Task` (BCL) | .NET 標準。スレッドプール使用でメインスレッド制約あり |
| Unity 6 `Awaitable` | Unity 6 標準の軽量 async 対応 |
| DOTween Sequence | アニメーション特化。汎用非同期には不向き |

## 決定

**UniTask を採用する。**

- UPM 経由でインストール: `https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask`

## 理由

- `async UniTask` / `async UniTaskVoid` でメインスレッド上の非同期処理を安全に記述できる
- Coroutine に比べて戻り値を直接扱える（`SpinResult` を返すなど）
- `CancellationToken` によるオートスピン中断・シーン遷移時のキャンセルが容易
- Unity 6 の `Awaitable` より成熟しており、`WhenAll` / `WhenAny` 等のユーティリティが充実
- `.WithCancellation(this.GetCancellationTokenOnDestroy())` でメモリリークを防げる

## トレードオフ

- 外部ライブラリへの依存が発生する
  → UniTask は MIT ライセンスで広く利用されており、アーケードゲーム規模では依存リスクは低い
- Coroutine より学習コストがわずかに高い
  → `async/await` パターンに慣れている開発者であれば問題なし
- Unity 6 の `Awaitable` で代替可能なケースもあるが、`WhenAll` 等の欠如から UniTask が上位互換
