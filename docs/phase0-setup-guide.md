# フェーズ0 セットアップ手順（Unity 初心者向け）

フェーズ0の残タスクは以下の4つです（asmdef・フォルダ構成は既に完了済み）：

1. Unity 6.3 LTS で新規プロジェクト作成
2. ProjectSettings で解像度設定
3. UPM パッケージ導入（UniTask・DOTween・TextMeshPro・New Input System）

---

## 1. Unity Hub でプロジェクト作成

1. **Unity Hub** を開く
2. 左上の **「New project」** をクリック
3. エディターバージョンで **Unity 6 (LTS)** を選択
4. テンプレートは **「Universal 2D」** を選択
5. プロジェクト名・保存場所を設定（保存場所をこのリポジトリ `/Users/mac/git/slot-unity` の親フォルダに指定し、プロジェクト名を `slot-unity` にすると既存フォルダを上書き生成できます）
   - ただし、既存ファイルがある場合は注意が必要です。**空フォルダを別の場所に作って、後でファイルを移動**する方が安全かもしれません

---

## 2. 解像度設定（ProjectSettings）

プロジェクトが開いたら：

1. メニュー **Edit → Project Settings** を開く
2. **Player** セクションを選択
3. **Resolution and Presentation** を展開
4. `Default Screen Width` → `1920`、`Default Screen Height` → `1080` に設定
5. `Fullscreen Mode` → `Windowed` に設定（開発中は見やすい）

---

## 3. UPM パッケージ導入

メニュー **Window → Package Manager** を開きます。

### TextMeshPro（組み込み済みの可能性あり）

1. Package Manager の検索欄に `TextMeshPro` と入力
2. **Install** → ポップアップが出たら **「Import TMP Essential Resources」** をクリック

### New Input System

1. Package Manager の **「Unity Registry」** から `Input System` を検索
2. **Install** → ポップアップ「Enable the new input backends?」→ **Yes**
3. プロジェクトが再起動します
4. 再起動後、**Edit → Project Settings → Player → Other Settings → Active Input Handling** が **`Both`** になっていることを確認

### UniTask（GitHub URL 経由）

1. Package Manager の左上 **「+」** ボタン → **「Add package from git URL...」** を選択
2. 以下の URL を入力して **Add**：
   ```
   https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask
   ```

### DOTween（Asset Store 経由）

1. ブラウザで `DOTween` を Asset Store で検索、または公式サイト `dotween.demigiant.com` からダウンロード
2. Unity に Import
3. **Import 後に必ず**：メニュー **Tools → Demigiant → DOTween Utility Panel** を開き、**「Setup DOTween...」** ボタンをクリック → **Apply** する（これをしないと動きません）

---

## 確認ポイント

- Package Manager でエラーがないか確認
- Console ウィンドウにエラーが出ていないか確認
