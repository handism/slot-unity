using UnityEngine;

namespace SlotGame.Data
{
    /// <summary>
    /// レトロクラシックアーケード筐体テーマのカラーパレットを一元管理する ScriptableObject。
    /// UIManager・MainHUDView・WinPopupView から参照され、ハードコードカラーの代替として機能する。
    /// </summary>
    [CreateAssetMenu(fileName = "RetroColorTheme", menuName = "SlotGame/RetroColorTheme")]
    public class RetroColorTheme : ScriptableObject
    {
        // ── カメラ背景色 ──────────────────────────────────────────────
        [Header("カメラ背景色")]
        [Tooltip("通常モードのカメラ背景色（深いマホガニー）")]
        public Color normalCameraColor = new(0.10f, 0.05f, 0.01f, 1f);

        [Tooltip("フリースピンモードのカメラ背景色（深いオリーブゴールド）")]
        public Color freeSpinCameraColor = new(0.08f, 0.12f, 0.02f, 1f);

        [Tooltip("ボーナスラウンドのカメラ背景色（カジノフェルト風の深緑）")]
        public Color bonusRoundCameraColor = new(0.02f, 0.08f, 0.03f, 1f);

        // ── モードオーバーレイ Tint ────────────────────────────────────
        [Header("モードオーバーレイ Tint（画面全体に乗算するカラー）")]
        [Tooltip("通常モードは透明（オーバーレイなし）")]
        public Color normalTint = new(0.10f, 0.05f, 0.01f, 0f);

        [Tooltip("フリースピン時のアンバーゴールド Tint")]
        public Color freeSpinTint = new(0.60f, 0.40f, 0.00f, 0.25f);

        [Tooltip("ボーナスラウンド時の深緑 Tint")]
        public Color bonusRoundTint = new(0.00f, 0.30f, 0.05f, 0.25f);

        // ── スピンボタン ──────────────────────────────────────────────
        [Header("スピンボタン（UIGradient 上→下）")]
        [Tooltip("SPINモード: 上端の赤メタル")]
        public Color spinButtonTop = new(0.80f, 0.15f, 0.10f, 1f);

        [Tooltip("SPINモード: 下端の深クリムゾン")]
        public Color spinButtonBottom = new(0.45f, 0.04f, 0.02f, 1f);

        [Tooltip("STOPモード: 上端のビビッドオレンジレッド")]
        public Color spinStopButtonTop = new(1.00f, 0.40f, 0.30f, 1f);

        [Tooltip("STOPモード: 下端の深いオレンジ")]
        public Color spinStopButtonBottom = new(0.60f, 0.10f, 0.05f, 1f);

        // ── オートスピンボタン ────────────────────────────────────────
        [Header("オートスピンボタン（UIGradient 上→下）")]
        [Tooltip("上端のバーガンディ")]
        public Color autoSpinButtonTop = new(0.65f, 0.12f, 0.08f, 1f);

        [Tooltip("下端の深い赤茶")]
        public Color autoSpinButtonBottom = new(0.35f, 0.04f, 0.02f, 1f);

        [Tooltip("オートスピン回数選択ポップアップの背景色（暗いマホガニー）")]
        public Color autoSpinPopupBackground = new(0.12f, 0.06f, 0.02f, 0.92f);

        // ── ベットボタン（未選択） ─────────────────────────────────────
        [Header("ベットボタン（未選択状態）")]
        public Color betUnselectedTop = new(0.22f, 0.14f, 0.06f, 0.92f);
        public Color betUnselectedBottom = new(0.12f, 0.07f, 0.03f, 0.92f);
        public Color betUnselectedHighlight = new(0.40f, 0.25f, 0.10f, 1f);
        public Color betUnselectedPressed = new(0.08f, 0.04f, 0.02f, 1f);
        public Color betUnselectedLabelColor = new(0.95f, 0.88f, 0.75f, 0.96f);

        // ── ベットボタン（選択済み） ───────────────────────────────────
        [Header("ベットボタン（選択済み状態）")]
        public Color betSelectedTop = new(1.00f, 0.85f, 0.40f, 0.96f);
        public Color betSelectedBottom = new(0.70f, 0.45f, 0.10f, 0.96f);
        public Color betSelectedHighlight = new(1.00f, 0.92f, 0.55f, 1f);
        public Color betSelectedPressed = new(0.85f, 0.60f, 0.20f, 1f);
        public Color betSelectedLabelColor = new(0.11f, 0.09f, 0.06f, 1f);

        // ── WIN ポップアップ背景 ──────────────────────────────────────
        [Header("WIN ポップアップ背景グラデーション（UIGradient 上→下）")]
        [Tooltip("上端: 深いクリムゾンレッド")]
        public Color winPopupBackgroundTop = new(0.20f, 0.04f, 0.02f, 0.95f);

        [Tooltip("下端: 赤みを帯びたほぼ黒")]
        public Color winPopupBackgroundBottom = new(0.08f, 0.01f, 0.01f, 0.98f);
    }
}
