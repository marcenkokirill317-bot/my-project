using System.Collections.Generic;
using UnityEngine;

namespace MSC.Multiplayer.Client
{
    // Единая точка отрисовки ников: мировая точка -> экран -> центрированный rect.
    public static class Nametag
    {
        public static float MaxDistance = 60f;
        private static GUIStyle style;
        private static readonly Dictionary<string, GUIContent> Cache = new Dictionary<string, GUIContent>();

        private static GUIStyle Style
        {
            get
            {
                if (style == null)
                {
                    style = new GUIStyle(GUI.skin.label);
                    style.alignment = TextAnchor.MiddleCenter;
                    style.fontSize = 14;
                    style.fontStyle = FontStyle.Bold;
                    style.wordWrap = false;
                    style.clipping = TextClipping.Overflow;
                    style.padding = new RectOffset(0, 0, 0, 0);
                    style.margin = new RectOffset(0, 0, 0, 0);
                    style.normal.textColor = Color.white;
                }
                return style;
            }
        }

        private static GUIContent Content(string text)
        {
            GUIContent c;
            if (Cache.TryGetValue(text, out c)) return c;
            if (Cache.Count > 64) Cache.Clear();
            c = new GUIContent(text);
            Cache[text] = c;
            return c;
        }

        // Вызывать только из OnGUI.
        public static void Draw(Camera cam, Vector3 worldAnchor, string nick)
        {
            if (cam == null || string.IsNullOrEmpty(nick)) return;
            Vector3 sp = cam.WorldToScreenPoint(worldAnchor);
            if (sp.z <= 0.05f || sp.z > MaxDistance) return;

            int pw = Mathf.Max(1, cam.pixelWidth);
            int ph = Mathf.Max(1, cam.pixelHeight);
            float gx = sp.x * Screen.width / pw;
            float gy = (ph - sp.y) * Screen.height / ph;

            GUIContent content = Content(nick);
            Vector2 size = Style.CalcSize(content);
            Rect rect = new Rect(gx - size.x * 0.5f, gy - size.y, size.x, size.y);
            if (rect.xMax < 0f || rect.x > Screen.width || rect.yMax < 0f || rect.y > Screen.height) return;

            float alpha = Mathf.Clamp01((MaxDistance - sp.z) / 10f);
            Matrix4x4 oldMatrix = GUI.matrix;
            Color oldColor = GUI.color;
            GUI.matrix = Matrix4x4.identity;
            GUI.color = new Color(0f, 0f, 0f, 0.7f * alpha);
            GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), content, Style);
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.Label(rect, content, Style);
            GUI.color = oldColor;
            GUI.matrix = oldMatrix;
        }
    }

    // Мировая точка над головой: кость головы либо измеренная высота модели.
    public sealed class NametagAnchor
    {
        private const float Offset = 0.22f;
        private Transform head, root;
        private Renderer[] renderers;
        private float height = 1.0f, nextMeasure;

        public void Bind(GameObject avatarRoot, Transform headBone)
        {
            root = avatarRoot != null ? avatarRoot.transform : null;
            head = headBone;
            renderers = avatarRoot != null ? avatarRoot.GetComponentsInChildren<Renderer>(true) : null;
            nextMeasure = 0f;
        }

        public Vector3 Position
        {
            get
            {
                if (head != null) return head.position + Vector3.up * Offset;
                if (root == null) return Vector3.zero;
                if (Time.realtimeSinceStartup >= nextMeasure)
                {
                    nextMeasure = Time.realtimeSinceStartup + 0.5f;
                    height = MeasureHeight();
                }
                return root.position + Vector3.up * (height + Offset);
            }
        }

        private float MeasureHeight()
        {
            if (renderers == null || renderers.Length == 0 || root == null) return 1.0f;
            bool has = false;
            Bounds b = new Bounds(root.position, Vector3.zero);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null) continue;
                if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
            }
            return has ? Mathf.Clamp(b.max.y - root.position.y, 0.3f, 4f) : 1.0f;
        }
    }
}