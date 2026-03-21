using Cysharp.Threading.Tasks;
using DG.Tweening;
using SlotGame.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SlotGame.View
{
    /// <summary>配当テーブルを ScrollView で動的生成して表示する View。</summary>
    public class PaytableView : MonoBehaviour
    {
        public const float ColumnWidth = 180f;
        public const float ColumnSpacing = 20f;
        public const float RowHeight = 56f;
        public const float RowSidePadding = 12f;
        public const float IconSize = 44f;

        [SerializeField] private Transform     contentRoot;
        [SerializeField] private GameObject    rowPrefab;   // Image + TMP_Text × 3（3/4/5 揃え）
        [SerializeField] private Button        closeButton;

        private CanvasGroup _canvasGroup;

        public event System.Action OnCloseRequested;

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            _canvasGroup.alpha = 0;

            closeButton.onClick.AddListener(() => OnCloseRequested?.Invoke());
        }

        public void Populate(SymbolData[] symbols)
        {
            EnsureRowPrefab();
            if (rowPrefab == null || contentRoot == null) return;

            // 既存の行を削除
            foreach (Transform child in contentRoot)
                Destroy(child.gameObject);

            foreach (var sym in symbols)
            {
                if (sym.type != SymbolType.Normal) continue;
                var row = Instantiate(rowPrefab, contentRoot);
                row.SetActive(true);

                var texts = row.GetComponentsInChildren<TMP_Text>();
                var iconTransform = row.transform.Find("SymbolCell/Icon");
                var img = iconTransform != null ? iconTransform.GetComponent<Image>() : null;

                if (img != null)   img.sprite    = sym.sprite;
                if (texts.Length > 0) texts[0].text = sym.payouts.Length > 0 ? sym.payouts[0].ToString() : "-";
                if (texts.Length > 1) texts[1].text = sym.payouts.Length > 1 ? sym.payouts[1].ToString() : "-";
                if (texts.Length > 2) texts[2].text = sym.payouts.Length > 2 ? sym.payouts[2].ToString() : "-";
            }
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
            cell.GetComponent<LayoutElement>().preferredWidth = ColumnWidth;

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
            text.text = "-";
            text.fontSize = 24f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;

            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(ColumnWidth, 40f);
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
    }
}
