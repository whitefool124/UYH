using UnityEngine;

namespace SpellGuard.UI
{
    public static class GestureSkeletonDrawer
    {
        private static readonly (int from, int to)[] HandConnections =
        {
            (0, 1), (1, 2), (2, 3), (3, 4),
            (0, 5), (5, 6), (6, 7), (7, 8),
            (5, 9), (9, 10), (10, 11), (11, 12),
            (9, 13), (13, 14), (14, 15), (15, 16),
            (13, 17), (17, 18), (18, 19), (19, 20),
            (0, 17)
        };

        public static void DrawHand(Rect rect, Vector2[] landmarks, Color lineColor, Color pointColor)
        {
            if (landmarks == null || landmarks.Length < 21)
            {
                DrawEmpty(rect);
                return;
            }

            var points = new Vector2[landmarks.Length];
            for (var i = 0; i < landmarks.Length; i++)
            {
                var p = landmarks[i];
                points[i] = new Vector2(
                    Mathf.Lerp(rect.x + 16f, rect.xMax - 16f, Mathf.Clamp01(p.x)),
                    Mathf.Lerp(rect.yMax - 16f, rect.y + 16f, Mathf.Clamp01(p.y)));
            }

            foreach (var (from, to) in HandConnections)
            {
                if (from >= points.Length || to >= points.Length)
                {
                    continue;
                }

                DrawLine(points[from], points[to], lineColor, 2f);
            }

            for (var i = 0; i < points.Length; i++)
            {
                GUI.color = pointColor;
                GUI.DrawTexture(new Rect(points[i].x - 3f, points[i].y - 3f, 6f, 6f), Texture2D.whiteTexture);
            }

            GUI.color = Color.white;
        }

        public static void DrawTrajectory(Rect rect, Vector2[] points, Color color)
        {
            if (points == null || points.Length < 2)
            {
                return;
            }

            for (var i = 1; i < points.Length; i++)
            {
                DrawLine(MapPoint(rect, points[i - 1]), MapPoint(rect, points[i]), color, 2f);
            }
        }

        public static void DrawTargetHint(Rect rect, string label, Color accent)
        {
            GUI.color = new Color(accent.r, accent.g, accent.b, 0.16f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = accent;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 2f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(rect.x + 8f, rect.y + 6f, rect.width - 16f, 20f), label);
        }

        private static Vector2 MapPoint(Rect rect, Vector2 p)
        {
            return new Vector2(
                Mathf.Lerp(rect.x + 16f, rect.xMax - 16f, Mathf.Clamp01(p.x)),
                Mathf.Lerp(rect.yMax - 16f, rect.y + 16f, Mathf.Clamp01(p.y)));
        }

        private static void DrawEmpty(Rect rect)
        {
            GUI.color = new Color(1f, 1f, 1f, 0.04f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private static void DrawLine(Vector2 start, Vector2 end, Color color, float width)
        {
            var oldColor = GUI.color;
            var matrix = GUI.matrix;
            var delta = end - start;
            var angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            GUI.color = color;
            GUIUtility.RotateAroundPivot(angle, start);
            GUI.DrawTexture(new Rect(start.x, start.y - width * 0.5f, delta.magnitude, width), Texture2D.whiteTexture);
            GUI.matrix = matrix;
            GUI.color = oldColor;
        }
    }
}
