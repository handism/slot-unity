#nullable enable
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SlotGame.View
{
    /// <summary>
    /// UGUI の Graphic メッシュ頂点カラーを書き換えてグラデーションを実現するコンポーネント。
    /// BaseMeshEffect を継承しカスタムシェーダー不要でコードのみで動作する。
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    public class UIGradient : BaseMeshEffect
    {
        public enum GradientDirection
        {
            TopToBottom,
            LeftToRight,
            FourCorner,
        }

        [SerializeField] private GradientDirection _direction = GradientDirection.TopToBottom;
        [SerializeField] private Color _topColor    = Color.white;
        [SerializeField] private Color _bottomColor = new(0.6f, 0.6f, 0.6f, 1f);

        // FourCorner 用（TopToBottom / LeftToRight の場合は _topColor / _bottomColor を使用）
        [SerializeField] private Color _topLeftColor     = Color.white;
        [SerializeField] private Color _topRightColor    = Color.white;
        [SerializeField] private Color _bottomLeftColor  = new(0.6f, 0.6f, 0.6f, 1f);
        [SerializeField] private Color _bottomRightColor = new(0.6f, 0.6f, 0.6f, 1f);

        private readonly List<UIVertex> _tempVerts = new();

        /// <summary>TopToBottom または LeftToRight グラデーションの色を設定してメッシュを更新する。</summary>
        public void SetColors(Color top, Color bottom)
        {
            _topColor    = top;
            _bottomColor = bottom;
            if (graphic != null)
                graphic.SetVerticesDirty();
        }

        /// <summary>FourCorner グラデーションの色を設定してメッシュを更新する。</summary>
        public void SetColors(Color topLeft, Color topRight, Color bottomLeft, Color bottomRight)
        {
            _direction        = GradientDirection.FourCorner;
            _topLeftColor     = topLeft;
            _topRightColor    = topRight;
            _bottomLeftColor  = bottomLeft;
            _bottomRightColor = bottomRight;
            if (graphic != null)
                graphic.SetVerticesDirty();
        }

        /// <summary>グラデーション方向を変更してメッシュを更新する。</summary>
        public void SetDirection(GradientDirection direction)
        {
            _direction = direction;
            if (graphic != null)
                graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive()) return;

            _tempVerts.Clear();
            vh.GetUIVertexStream(_tempVerts);

            if (_tempVerts.Count == 0) return;

            // 頂点の Y/X 範囲を求める
            float yMin = float.MaxValue, yMax = float.MinValue;
            float xMin = float.MaxValue, xMax = float.MinValue;

            foreach (var v in _tempVerts)
            {
                if (v.position.y < yMin) yMin = v.position.y;
                if (v.position.y > yMax) yMax = v.position.y;
                if (v.position.x < xMin) xMin = v.position.x;
                if (v.position.x > xMax) xMax = v.position.x;
            }

            float yRange = yMax - yMin;
            float xRange = xMax - xMin;

            for (int i = 0; i < _tempVerts.Count; i++)
            {
                var vertex = _tempVerts[i];
                Color gradColor;

                switch (_direction)
                {
                    case GradientDirection.LeftToRight:
                    {
                        float tx = xRange > 0f ? (vertex.position.x - xMin) / xRange : 0f;
                        gradColor = Color.Lerp(_topColor, _bottomColor, tx);
                        break;
                    }
                    case GradientDirection.FourCorner:
                    {
                        float tx = xRange > 0f ? (vertex.position.x - xMin) / xRange : 0f;
                        float ty = yRange > 0f ? (vertex.position.y - yMin) / yRange : 0f;
                        Color bottom = Color.Lerp(_bottomLeftColor, _bottomRightColor, tx);
                        Color top    = Color.Lerp(_topLeftColor,    _topRightColor,    tx);
                        gradColor = Color.Lerp(bottom, top, ty);
                        break;
                    }
                    default: // TopToBottom
                    {
                        float ty = yRange > 0f ? (vertex.position.y - yMin) / yRange : 0f;
                        gradColor = Color.Lerp(_bottomColor, _topColor, ty);
                        break;
                    }
                }

                // 既存の頂点カラー（Image.color 等）と乗算合成する
                vertex.color = Multiply(vertex.color, (Color32)gradColor);
                _tempVerts[i] = vertex;
            }

            vh.Clear();
            vh.AddUIVertexTriangleStream(_tempVerts);
        }

        private static Color32 Multiply(Color32 a, Color32 b)
        {
            return new Color32(
                (byte)(a.r * b.r / 255),
                (byte)(a.g * b.g / 255),
                (byte)(a.b * b.b / 255),
                (byte)(a.a * b.a / 255)
            );
        }
    }
}
