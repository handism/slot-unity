using SlotGame.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SlotGame.View
{
    /// <summary>配当テーブルを ScrollView で動的生成して表示する View。</summary>
    public class PaytableView : MonoBehaviour
    {
        [SerializeField] private Transform     contentRoot;
        [SerializeField] private GameObject    rowPrefab;   // Image + TMP_Text × 3（3/4/5 揃え）
        [SerializeField] private Button        closeButton;

        public event System.Action OnCloseRequested;

        private void Awake()
        {
            closeButton.onClick.AddListener(() => OnCloseRequested?.Invoke());
        }

        public void Populate(SymbolData[] symbols)
        {
            // 既存の行を削除
            foreach (Transform child in contentRoot)
                Destroy(child.gameObject);

            foreach (var sym in symbols)
            {
                if (sym.type != SymbolType.Normal) continue;
                var row = Instantiate(rowPrefab, contentRoot);

                var texts = row.GetComponentsInChildren<TMP_Text>();
                var img   = row.GetComponentInChildren<Image>();

                if (img != null)   img.sprite    = sym.sprite;
                if (texts.Length > 0) texts[0].text = sym.payouts.Length > 0 ? sym.payouts[0].ToString() : "-";
                if (texts.Length > 1) texts[1].text = sym.payouts.Length > 1 ? sym.payouts[1].ToString() : "-";
                if (texts.Length > 2) texts[2].text = sym.payouts.Length > 2 ? sym.payouts[2].ToString() : "-";
            }
        }
    }
}
