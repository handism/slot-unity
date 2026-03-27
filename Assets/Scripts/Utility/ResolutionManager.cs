using UnityEngine;

namespace SlotGame.Utility
{
    /// <summary>
    /// 16:9 アスペクト比を維持し、必要に応じてレターボックス（黒帯）を表示する。
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class ResolutionManager : MonoBehaviour
    {
        private const float TargetAspect = 16f / 9f;
        private Camera _camera;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            UpdateLayout();
        }

        private void Update()
        {
            // 実行中のリサイズに対応
            UpdateLayout();
        }

        private void UpdateLayout()
        {
            float windowAspect = (float)Screen.width / Screen.height;
            float scale = windowAspect / TargetAspect;

            Rect rect = _camera.rect;

            if (scale < 1.0f)
            {
                // 縦長すぎる場合（ピラーボックス）
                rect.width  = 1.0f;
                rect.height = scale;
                rect.x      = 0;
                rect.y      = (1.0f - scale) / 2.0f;
            }
            else
            {
                // 横長すぎる場合（レターボックス）
                float invScale = 1.0f / scale;
                rect.width  = invScale;
                rect.height = 1.0f;
                rect.x      = (1.0f - invScale) / 2.0f;
                rect.y      = 0;
            }

            _camera.rect = rect;
        }
    }
}
