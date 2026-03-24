using Cysharp.Threading.Tasks;
using DG.Tweening;
using SlotGame.Audio;
using SlotGame.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;

namespace SlotGame.View
{
    /// <summary>配当テーブルを ScrollView で動的生成して表示する View。</summary>
    public class PaytableView : MonoBehaviour
    {
        public const float SymbolColumnWidth = 120f;
        public const float ColumnWidth = 112f;
        public const float ColumnSpacing = 20f;
        public const float RowHeight = 60f;
        public const float RowSidePadding = 12f;
        public const float IconSize = 44f;

        [SerializeField] private Transform     contentRoot;
        [SerializeField] private GameObject    rowPrefab;   // Image + TMP_Text × 3（3/4/5 揃え）
        [SerializeField] private Button        closeButton;

        private CanvasGroup _canvasGroup;
        private AudioManager _audioManager;

        public event System.Action OnCloseRequested;

        private void Awake()
        {
            _audioManager = FindFirstObjectByType<AudioManager>();
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            _canvasGroup.alpha = 0;

            closeButton.onClick.AddListener(() =>
            {
                PlayButtonClickSe();
                OnCloseRequested?.Invoke();
            });
        }

        public void Populate(SymbolData[] symbols, PayoutTableData payoutData)
        {
            EnsureRowPrefab();
            if (rowPrefab == null || contentRoot == null) return;

            // RowTemplate: childControlWidth=true にして preferredWidth で列幅を制御
            var rowHlg = rowPrefab.GetComponent<HorizontalLayoutGroup>();
            if (rowHlg != null) rowHlg.childControlWidth = true;

            // RowTemplate のペイアウト列幅を ColumnWidth に統一（0番目はシンボル列なのでスキップ）
            int rowColIdx = 0;
            foreach (Transform child in rowPrefab.transform)
            {
                if (rowColIdx > 0)
                {
                    var le = child.GetComponent<LayoutElement>();
                    if (le != null) le.preferredWidth = ColumnWidth;
                }
                rowColIdx++;
            }

            // HeaderRow: childControlWidth=true にして同じ列幅を適用
            foreach (var hlg in GetComponentsInChildren<HorizontalLayoutGroup>(true))
            {
                if (hlg.gameObject.name != "HeaderRow") continue;
                hlg.childControlWidth = true;
                int headerColIdx = 0;
                foreach (Transform child in hlg.transform)
                {
                    if (headerColIdx > 0)
                    {
                        var le = child.GetComponent<LayoutElement>();
                        if (le != null) le.preferredWidth = ColumnWidth;
                        var txt = child.GetComponent<TMP_Text>();
                        if (txt != null) txt.alignment = TextAlignmentOptions.Right;
                    }
                    headerColIdx++;
                }
                break;
            }

            // 既存の行を削除
            var staleRows = new List<GameObject>();
            foreach (Transform child in contentRoot)
            {
                if (child == null) continue;
                if (rowPrefab != null && child.gameObject == rowPrefab) continue;
                staleRows.Add(child.gameObject);
            }

            foreach (var staleRow in staleRows)
                Destroy(staleRow);

            foreach (var sym in symbols)
            {
                if (sym.type != SymbolType.Normal && sym.type != SymbolType.Scatter && sym.type != SymbolType.Bonus) continue;
                
                var row = Instantiate(rowPrefab, contentRoot);
                row.SetActive(true);
                row.name = $"Row_{sym.symbolName}";

                var rowRect = row.GetComponent<RectTransform>();
                if (rowRect != null)
                {
                    rowRect.localScale = Vector3.one;
                    rowRect.anchoredPosition3D = Vector3.zero;
                }

                var texts = row.GetComponentsInChildren<TMP_Text>(true)
                    .Where(t => t.transform.parent == row.transform)
                    .OrderBy(t => t.transform.GetSiblingIndex())
                    .ToArray();
                var iconTransform = row.transform.Find("SymbolCell/Icon");
                var img = iconTransform != null ? iconTransform.GetComponent<Image>() : null;

                if (img != null)
                {
                    img.sprite = sym.sprite;
                    img.preserveAspect = true;
                    img.SetNativeSize();

                    var iconRect = img.rectTransform;
                    iconRect.sizeDelta = new Vector2(IconSize, IconSize);
                }

                if (sym.type == SymbolType.Normal)
                {
                    if (texts.Length > 0) texts[0].text = sym.payouts.Length > 0 ? sym.payouts[0].ToString("N0") : "-";
                    if (texts.Length > 1) texts[1].text = sym.payouts.Length > 1 ? sym.payouts[1].ToString("N0") : "-";
                    if (texts.Length > 2) texts[2].text = sym.payouts.Length > 2 ? sym.payouts[2].ToString("N0") : "-";
                }
                else if (sym.type == SymbolType.Scatter)
                {
                    // Scatter payouts from payoutData
                    if (payoutData != null && payoutData.scatterPayouts != null)
                    {
                        var p3 = payoutData.scatterPayouts.FirstOrDefault(p => p.scatterCount == 3);
                        var p4 = payoutData.scatterPayouts.FirstOrDefault(p => p.scatterCount == 4);
                        var p5 = payoutData.scatterPayouts.FirstOrDefault(p => p.scatterCount == 5);
                        
                        if (texts.Length > 0) texts[0].text = p3.multiplier > 0 ? p3.multiplier.ToString("N0") : "-";
                        if (texts.Length > 1) texts[1].text = p4.multiplier > 0 ? p4.multiplier.ToString("N0") : "-";
                        if (texts.Length > 2) texts[2].text = p5.multiplier > 0 ? p5.multiplier.ToString("N0") : "-";
                    }
                }
                else if (sym.type == SymbolType.Bonus)
                {
                    // Bonus symbol - just show info or leave blank
                    if (texts.Length > 0) texts[0].text = "Mini";
                    if (texts.Length > 1) texts[1].text = "Game";
                    if (texts.Length > 2) texts[2].text = "Trigger";
                }
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)contentRoot);
            if (contentRoot.parent is RectTransform parentRect)
                LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
            Canvas.ForceUpdateCanvases();
        }

        private void EnsureRowPrefab()
        {
            if (rowPrefab != null) return;

            rowPrefab = CreateFallbackRowPrefab();
        }

        private GameObject CreateFallbackRowPrefab()
        {
            var row = new GameObject("RuntimeRowPrefab", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            row.SetActive(false);
            row.hideFlags = HideFlags.HideAndDontSave;

            var rowImage = row.GetComponent<Image>();
            rowImage.color = new Color(1f, 1f, 1f, 0.08f);

            var rowLayout = row.GetComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = ColumnSpacing;
            rowLayout.padding = new RectOffset((int)RowSidePadding, (int)RowSidePadding, 6, 6);
            rowLayout.childAlignment = TextAnchor.MiddleCenter;
            rowLayout.childControlWidth = false;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;

            row.GetComponent<LayoutElement>().preferredHeight = RowHeight;

            CreateSymbolCell(row.transform);
            CreateValueText(row.transform, "Payout3");
            CreateValueText(row.transform, "Payout4");
            CreateValueText(row.transform, "Payout5");

            return row;
        }

        private static void CreateSymbolCell(Transform parent)
        {
            var cell = new GameObject("SymbolCell", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            cell.transform.SetParent(parent, false);
            cell.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.02f);
            cell.GetComponent<LayoutElement>().preferredWidth = SymbolColumnWidth;

            var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            icon.transform.SetParent(cell.transform, false);
            var iconRect = icon.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = Vector2.zero;
            iconRect.sizeDelta = new Vector2(IconSize, IconSize);
        }

        private static void CreateValueText(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredWidth = ColumnWidth;

            var text = go.AddComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.text = "-";
            text.fontSize = 24f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;

            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(ColumnWidth, 44f);
        }

        public async UniTask ShowAsync(System.Threading.CancellationToken ct = default)
        {
            gameObject.SetActive(true);
            transform.localScale = Vector3.one * 0.9f;
            _canvasGroup.alpha = 0f;

            await UniTask.WhenAll(
                DOTween.To(() => _canvasGroup.alpha, x => _canvasGroup.alpha = x, 1f, 0.2f).SetEase(Ease.OutQuad).ToUniTask(cancellationToken: ct),
                transform.DOScale(1f, 0.2f).SetEase(Ease.OutBack).ToUniTask(cancellationToken: ct)
            );
        }

        public async UniTask HideAsync(System.Threading.CancellationToken ct = default)
        {
            await UniTask.WhenAll(
                DOTween.To(() => _canvasGroup.alpha, x => _canvasGroup.alpha = x, 0f, 0.15f).SetEase(Ease.InQuad).ToUniTask(cancellationToken: ct),
                transform.DOScale(0.9f, 0.15f).SetEase(Ease.InBack).ToUniTask(cancellationToken: ct)
            );
            gameObject.SetActive(false);
        }

        private void PlayButtonClickSe()
        {
            _audioManager ??= FindFirstObjectByType<AudioManager>();
            _audioManager?.PlaySE(SEType.ButtonClick);
        }
    }
}
