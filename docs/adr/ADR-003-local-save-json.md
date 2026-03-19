# ADR-003: ローカル保存に JSON ファイルを使用する

**ステータス**: 承認済み
**日付**: 2026-03-20

---

## コンテキスト

プレイヤーのコイン残高・設定値をローカルに永続化する必要がある。
サーバー連携は現時点で対象外。

## 選択肢

| 方式 | 概要 |
|------|------|
| **JSON ファイル** | `Application.persistentDataPath` に `savedata.json` として保存 |
| PlayerPrefs | Unity 標準。キー・バリュー形式。レジストリ / plist に保存 |
| SQLite | リレーショナル DB。プラグイン必要 |
| Binary シリアライズ | `BinaryFormatter` は非推奨（.NET 5 以降） |

## 決定

**JSON ファイル（`Application.persistentDataPath/savedata.json`）を採用する。**

- `JsonUtility` または `System.Text.Json` でシリアライズ
- 保存タイミング: スピン結果確定後・設定変更後の都度保存（クラッシュ時のデータロスト最小化）
- 破損時: デフォルト値でフォールバック後、破損ファイルを `.bak` にリネームして新規作成

## 理由

- PlayerPrefs はキー管理が煩雑で、セーブデータ構造の変更（フィールド追加）に弱い
- JSON はデバッグ時に直接ファイルを確認・編集できる
- `SaveData` クラスに `saveVersion` フィールドを持たせることで、将来のマイグレーションに対応しやすい
- サーバー連携に切り替える場合も JSON 構造をそのまま API ペイロードに流用できる

## トレードオフ

- PlayerPrefs より若干実装量が多い
- JSON は人間が読めるためチートが容易 → アーケード向けの今回は許容範囲（実害なし）
- 将来的にチート対策が必要な場合は AES 暗号化ラッパーを `SaveDataManager` に追加する
