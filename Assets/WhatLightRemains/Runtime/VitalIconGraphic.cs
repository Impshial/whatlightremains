using UnityEngine;
using UnityEngine.UI;

namespace WhatLightRemains.Runtime
{
    /// <summary>Small resolution-independent line icons; no font glyph or external art dependency.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class VitalIconGraphic : MaskableGraphic
    {
        public enum Symbol { Heart, Food, Air }
        [SerializeField] private Symbol icon;
        public Symbol Icon { get => icon; set { icon = value; SetVerticesDirty(); } }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (icon == Symbol.Heart)
            {
                Vector2[] points = new Vector2[65];
                for (int i = 0; i < points.Length; i++)
                {
                    float t = i * Mathf.PI * 2f / (points.Length - 1);
                    points[i] = new Vector2(16f * Mathf.Pow(Mathf.Sin(t), 3f) / 38f,
                        (13f * Mathf.Cos(t) - 5f * Mathf.Cos(2f * t)
                         - 2f * Mathf.Cos(3f * t) - Mathf.Cos(4f * t) + 2f) / 36f);
                }
                Stroke(vh, points);
            }
            else if (icon == Symbol.Food)
            {
                Stroke(vh, new Vector2(-0.24f, -0.43f), new Vector2(-0.24f, 0.13f));
                Stroke(vh, new Vector2(-0.39f, 0.42f), new Vector2(-0.39f, 0.15f),
                    new Vector2(-0.32f, 0.06f), new Vector2(-0.16f, 0.06f),
                    new Vector2(-0.09f, 0.15f), new Vector2(-0.09f, 0.42f));
                Stroke(vh, new Vector2(-0.24f, 0.42f), new Vector2(-0.24f, 0.18f));
                Stroke(vh, new Vector2(0.28f, -0.43f), new Vector2(0.28f, 0.43f),
                    new Vector2(0.16f, 0.29f), new Vector2(0.12f, 0.08f),
                    new Vector2(0.12f, -0.03f), new Vector2(0.28f, -0.03f));
            }
            else
            {
                // Shared-air/wind symbol, deliberately not lungs or a personal tank.
                Stroke(vh, new Vector2(-0.43f, 0.20f), new Vector2(0.02f, 0.20f),
                    new Vector2(0.12f, 0.24f), new Vector2(0.16f, 0.33f),
                    new Vector2(0.12f, 0.42f), new Vector2(0.02f, 0.45f), new Vector2(-0.06f, 0.39f));
                Stroke(vh, new Vector2(-0.34f, 0f), new Vector2(0.27f, 0f),
                    new Vector2(0.40f, 0.04f), new Vector2(0.45f, 0.15f),
                    new Vector2(0.40f, 0.26f), new Vector2(0.29f, 0.29f));
                Stroke(vh, new Vector2(-0.43f, -0.20f), new Vector2(0.13f, -0.20f),
                    new Vector2(0.24f, -0.24f), new Vector2(0.28f, -0.34f),
                    new Vector2(0.23f, -0.43f), new Vector2(0.12f, -0.45f), new Vector2(0.05f, -0.39f));
            }
        }

        private void Stroke(VertexHelper vh, params Vector2[] points)
        {
            Rect rect = GetPixelAdjustedRect();
            float scale = Mathf.Min(rect.width, rect.height);
            float halfWidth = scale * 0.031f;
            for (int i = 0; i < points.Length; i++)
            {
                Vector2 point = rect.center + points[i] * scale;
                // Round joins and end caps also make the tiny utensils readable at 720p.
                int center = vh.currentVertCount;
                vh.AddVert(point, color, Vector2.zero);
                for (int j = 0; j < 8; j++)
                {
                    float angle = j * Mathf.PI / 4f;
                    vh.AddVert(point + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * halfWidth, color, Vector2.zero);
                }
                for (int j = 0; j < 8; j++) vh.AddTriangle(center, center + 1 + j, center + 1 + (j + 1) % 8);
                if (i == 0) continue;
                Vector2 previous = rect.center + points[i - 1] * scale;
                Vector2 delta = (point - previous).normalized;
                Vector2 normal = new Vector2(-delta.y, delta.x) * halfWidth;
                int start = vh.currentVertCount;
                vh.AddVert(previous - normal, color, Vector2.zero);
                vh.AddVert(previous + normal, color, Vector2.zero);
                vh.AddVert(point + normal, color, Vector2.zero);
                vh.AddVert(point - normal, color, Vector2.zero);
                vh.AddTriangle(start, start + 1, start + 2);
                vh.AddTriangle(start, start + 2, start + 3);
            }
        }
    }
}
