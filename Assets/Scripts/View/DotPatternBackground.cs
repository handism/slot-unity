#nullable enable
using UnityEngine;
using UnityEngine.UI;

namespace SlotGame.View
{
    /// <summary>
    /// 手続き生成した Texture2D のタイリングで全画面ドットパターン背景を表示するコンポーネント。
    /// RawImage を使い uvRect を Canvas サイズに合わせることで物理的なドットサイズを一定に保つ。
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public class DotPatternBackground : MonoBehaviour
    {
        [SerializeField] private int   _tileSize  = 32;   // タイルテクスチャのピクセルサイズ
        [SerializeField] private float _dotRadius = 3.5f; // ドットの半径（ピクセル）

        private static readonly Color NormalDot     = new(0.4f, 0.6f, 1f,  0.10f);
        private static readonly Color FreeSpinDot   = new(0.2f, 0.9f, 1f,  0.12f);
        private static readonly Color BonusRoundDot = new(1f,   0.6f, 0.2f, 0.12f);

        private RawImage   _rawImage   = null!;
        private Texture2D? _dotTexture;

        private void Awake()
        {
            _rawImage = GetComponent<RawImage>();
            _rawImage.raycastTarget = false;

            // fullscreen stretch
            var rt = GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private void Start()
        {
            GenerateDotTexture(NormalDot);
        }

        private void OnDestroy()
        {
            DestroyTexture();
        }

        protected void OnRectTransformDimensionsChange()
        {
            if (_rawImage != null && _dotTexture != null)
                UpdateUVRect();
        }

        /// <summary>ゲームモードに応じてドットの色を切り替える。</summary>
        public void SetMode(ModeVisualType mode)
        {
            Color dotColor = mode switch
            {
                ModeVisualType.FreeSpin   => FreeSpinDot,
                ModeVisualType.BonusRound => BonusRoundDot,
                _                         => NormalDot,
            };
            GenerateDotTexture(dotColor);
        }

        private void GenerateDotTexture(Color dotColor)
        {
            DestroyTexture();

            int size = Mathf.Max(4, _tileSize);
            _dotTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode   = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                name       = "DotPatternTile",
            };

            Color[] pixels = new Color[size * size];
            Vector2 center = new(size * 0.5f, size * 0.5f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                    // SDF 的アンチエイリアシング（境界を 1px でぼかす）
                    float t = Mathf.Clamp01(_dotRadius - dist + 1f);
                    pixels[y * size + x] = new Color(dotColor.r, dotColor.g, dotColor.b, dotColor.a * t);
                }
            }

            _dotTexture.SetPixels(pixels);
            _dotTexture.Apply();

            _rawImage.texture = _dotTexture;
            _rawImage.color   = Color.white;
            UpdateUVRect();
        }

        private void UpdateUVRect()
        {
            if (_dotTexture == null) return;
            var rect = GetComponent<RectTransform>().rect;
            float tilesX = rect.width  / _tileSize;
            float tilesY = rect.height / _tileSize;
            _rawImage.uvRect = new Rect(0f, 0f, tilesX, tilesY);
        }

        private void DestroyTexture()
        {
            if (_dotTexture != null)
            {
                Destroy(_dotTexture);
                _dotTexture = null;
            }
        }
    }
}
