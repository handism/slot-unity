# copilot-instructions.md

## プロジェクト固有Copilot指示文

- 出力言語: 日本語 — すべての回答は日本語で行ってください（明示的指示がある場合を除く）。

- このプロジェクトはレトロクラシックテーマの5リール・25ライン・アーケード系スロットゲーム（Unity 6.3 LTS/PC向け）です。
- アーキテクチャやコーディング規約は `.claude/CLAUDE.md` を参照し、同等の厳格さで従ってください。
- Model/View/Presenterの3層構成。ModelはピュアC#、Viewは描画専用、PresenterはMonoBehaviourで制御。
- ゲームパラメータはScriptableObjectで管理し、コード直書き禁止。
- 非同期処理はUniTaskのみ。Coroutine禁止。
- 乱数生成はIRandomGeneratorインターフェース経由で依存注入。
- 保存はJSONで `Application.persistentDataPath/savedata.json` に行う。
- テストは必ずEditMode/PlayModeで実施し、再現性担保のためSeededRandomGeneratorを使う。
- コミット・PR運用は自動化せず、ユーザー承認後に手動で行う。
- ドキュメント・ナレッジは`.copilot/`または`.claude/`配下に保存。

## 禁止事項

- ModelにUnityEngine依存を持ち込むこと
- コードにゲームパラメータを直書きすること
- Coroutine/IEnumeratorの使用
- ScriptableObjectアセットの再作成
- 自動コミット・自動PR作成

---

> 詳細は `.claude/CLAUDE.md` および `docs/requirements.md`/`docs/design.md` を参照。
