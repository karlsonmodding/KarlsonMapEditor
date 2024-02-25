using LoadsonAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace KarlsonMapEditor
{
    public class ColorPicker
    {
        public static Texture2D imRight, imLeft, imCircle;
        int wid;
        Rect pos;
        public float wX { get => pos.x; }
        public float wY { get => pos.y; }
        public Color color;
        public void UpdateColor()
        {
            RGBToHSV(color, out h, out s, out v);
        }

        GUIStyle previewStyle;
        GUIStyle labelStyle;
        GUIStyle svStyle, hueStyle;
        const int kHSVPickerSize = 120, kHuePickerWidth = 16;

        Texture2D hueTexture, svTexture;
        float h = 0f, s = 1f, v = 1f;

        public ColorPicker(Color _color, int x, int y) {
            wid = ImGUI_WID.GetWindowId();
            pos = new Rect(x, y, 165, 215);
            color = _color;

            RGBToHSV(_color, out h, out s, out v);

            previewStyle = new GUIStyle();
            previewStyle.normal.background = Texture2D.whiteTexture;

            labelStyle = new GUIStyle();
            labelStyle.fontSize = 12;

            hueTexture = CreateHueTexture(20, kHSVPickerSize);
            hueStyle = new GUIStyle();
            hueStyle.normal.background = hueTexture;

            svTexture = CreateSVTexture(_color, kHSVPickerSize);
            svStyle = new GUIStyle();
            svStyle.normal.background = svTexture;
        }
        public void DrawWindow()
        {
            if(pos.x < 0 || pos.y < 0)
                pos = new Rect(Screen.width - 180, Screen.height - 20, 165, 215);

            pos = GUI.Window(wid, pos, (_) =>
            {
                using (new GUILayout.VerticalScope())
                {
                    GUILayout.Space(5f);
                    DrawPreview(color);

                    GUILayout.Space(5f);
                    DrawHSVPicker(ref color);

                    GUILayout.Space(5f);
                    DrawManualInput(ref color);

                    // update for custom color
                    float oldh = h, olds = s, oldv = v;
                    UpdateColor();
                    if(h != oldh || s != olds || v != oldv)
                        UpdateSVTexture(color, svTexture);
                }

                GUI.DragWindow(new Rect(0, 0, 165, 20));
            }, "Color Picker");
        }

        #region https://github.com/mattatz/unity-immediate-color-picker
        // Licensed under MIT license. Copyright (c) 2016 mattatz
        // Check their repo for full license and copyright notice
        public static Color HSVToRGB(float H, float S, float V, float keepAlpha = -1, bool hdr = false)
        {
            Color white = Color.white;
            if (S == 0f)
            {
                white.r = V;
                white.g = V;
                white.b = V;
            }
            else if (V == 0f)
            {
                white.r = 0f;
                white.g = 0f;
                white.b = 0f;
            }
            else
            {
                white.r = 0f;
                white.g = 0f;
                white.b = 0f;
                float num = H * 6f;
                int num2 = (int)Mathf.Floor(num);
                float num3 = num - (float)num2;
                float num4 = V * (1f - S);
                float num5 = V * (1f - S * num3);
                float num6 = V * (1f - S * (1f - num3));
                switch (num2 + 1)
                {
                    case 0:
                        white.r = V;
                        white.g = num4;
                        white.b = num5;
                        break;
                    case 1:
                        white.r = V;
                        white.g = num6;
                        white.b = num4;
                        break;
                    case 2:
                        white.r = num5;
                        white.g = V;
                        white.b = num4;
                        break;
                    case 3:
                        white.r = num4;
                        white.g = V;
                        white.b = num6;
                        break;
                    case 4:
                        white.r = num4;
                        white.g = num5;
                        white.b = V;
                        break;
                    case 5:
                        white.r = num6;
                        white.g = num4;
                        white.b = V;
                        break;
                    case 6:
                        white.r = V;
                        white.g = num4;
                        white.b = num5;
                        break;
                    case 7:
                        white.r = V;
                        white.g = num6;
                        white.b = num4;
                        break;
                }
                if (!hdr)
                {
                    white.r = Mathf.Clamp(white.r, 0f, 1f);
                    white.g = Mathf.Clamp(white.g, 0f, 1f);
                    white.b = Mathf.Clamp(white.b, 0f, 1f);
                }
            }
            if(keepAlpha != -1)
                white.a = Mathf.Clamp(keepAlpha, 0, 1);
            return white;
        }

        public static void RGBToHSV(Color color, out float H, out float S, out float V)
        {
            if (color.b > color.g && color.b > color.r)
            {
                RGBToHSVHelper(4f, color.b, color.r, color.g, out H, out S, out V);
            }
            else if (color.g > color.r)
            {
                RGBToHSVHelper(2f, color.g, color.b, color.r, out H, out S, out V);
            }
            else
            {
                RGBToHSVHelper(0f, color.r, color.g, color.b, out H, out S, out V);
            }
        }

        private static void RGBToHSVHelper(float offset, float dominantcolor, float colorone, float colortwo, out float H, out float S, out float V)
        {
            V = dominantcolor;
            if (V != 0f)
            {
                float num;
                if (colorone > colortwo)
                {
                    num = colortwo;
                }
                else
                {
                    num = colorone;
                }
                float num2 = V - num;
                if (num2 != 0f)
                {
                    S = num2 / V;
                    H = offset + (colorone - colortwo) / num2;
                }
                else
                {
                    S = 0f;
                    H = offset + (colorone - colortwo);
                }
                H /= 6f;
                if (H < 0f)
                {
                    H += 1f;
                }
            }
            else
            {
                S = 0f;
                H = 0f;
            }
        }
        Texture2D CreateHueTexture(int width, int height)
        {
            var tex = new Texture2D(width, height);
            for (int y = 0; y < height; y++)
            {
                var h = 1f * y / height;
                var color = HSVToRGB(h, 1f, 1f);
                for (int x = 0; x < width; x++)
                {
                    tex.SetPixel(x, y, color);
                }
            }
            tex.Apply();
            return tex;
        }

        Texture2D CreateSVTexture(Color c, int size)
        {
            var tex = new Texture2D(size, size);
            UpdateSVTexture(c, tex);
            return tex;
        }
        void DrawPreview(Color c)
        {
            using (new GUILayout.VerticalScope())
            {
                var tmp = GUI.backgroundColor;
                GUI.backgroundColor = new Color(c.r, c.g, c.b);
                GUILayout.Label("", previewStyle, GUILayout.Width(kHSVPickerSize + kHuePickerWidth + 10), GUILayout.Height(12f));

                GUILayout.Space(1f);

                var alpha = c.a;
                GUI.backgroundColor = new Color(alpha, alpha, alpha);
                GUILayout.Label("", previewStyle, GUILayout.Width(kHSVPickerSize + kHuePickerWidth + 10), GUILayout.Height(2f));

                GUI.backgroundColor = tmp;
            }
        }

        void DrawHSVPicker(ref Color c)
        {
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label("", svStyle, GUILayout.Width(kHSVPickerSize), GUILayout.Height(kHSVPickerSize));
                DrawSVHandler(GUILayoutUtility.GetLastRect(), ref c);

                GUILayout.Space(10f);

                GUILayout.Label("", hueStyle, GUILayout.Width(kHuePickerWidth), GUILayout.Height(kHSVPickerSize));
                DrawHueHandler(GUILayoutUtility.GetLastRect(), ref c);
            }
        }

        void DrawManualInput(ref Color c)
        {
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Space(-3);
                c.r = float.Parse(GUILayout.TextField(c.r.ToString("0.00"), GUILayout.Height(20), GUILayout.Width(35)));
                c.g = float.Parse(GUILayout.TextField(c.g.ToString("0.00"), GUILayout.Height(20), GUILayout.Width(35)));
                c.b = float.Parse(GUILayout.TextField(c.b.ToString("0.00"), GUILayout.Height(20), GUILayout.Width(35)));
                c.a = float.Parse(GUILayout.TextField(c.a.ToString("0.00"), GUILayout.Height(20), GUILayout.Width(35)));
            }
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Space(-3);
                c.a = GUILayout.HorizontalSlider(c.a, 0, 1, GUILayout.Height(20), GUILayout.Width(150));
            }

            // clamp values
            c.r = Mathf.Clamp(c.r, 0, 1);
            c.g = Mathf.Clamp(c.g, 0, 1);
            c.b = Mathf.Clamp(c.b, 0, 1);
            c.a = Mathf.Clamp(c.a, 0, 1);
        }

        void UpdateSVTexture(Color c, Texture2D tex)
        {
            float h, _s, _v;
            RGBToHSV(c, out h, out _s, out _v);

            var size = tex.width;
            for (int y = 0; y < size; y++)
            {
                var v = 1f * y / size;
                for (int x = 0; x < size; x++)
                {
                    var s = 1f * x / size;
                    var color = HSVToRGB(h, s, v);
                    tex.SetPixel(x, y, color);
                }
            }

            tex.Apply();
        }
        void DrawSVHandler(Rect rect, ref Color c)
        {
            const float size = 10f;
            const float offset = 5f;
            GUI.DrawTexture(new Rect(rect.x + s * rect.width - offset, rect.y + (1f - v) * rect.height - offset, size, size), imCircle);

            var e = Event.current;
            var p = e.mousePosition;
            if (e.button == 0 && (e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && rect.Contains(p))
            {
                s = (p.x - rect.x) / rect.width;
                v = 1f - (p.y - rect.y) / rect.height;
                c = HSVToRGB(h, s, v, c.a);

                e.Use();
            }
        }

        void DrawHueHandler(Rect rect, ref Color c)
        {
            const float size = 15f;
            GUI.DrawTexture(new Rect(rect.x + rect.width - size * 0.25f, rect.y + (1f - h) * rect.height - size * 0.5f, size, size), imLeft);
            GUI.DrawTexture(new Rect(rect.x - size * 0.75f, rect.y + (1f - h) * rect.height - size * 0.5f, size, size), imRight);

            var e = Event.current;
            var p = e.mousePosition;
            if (e.button == 0 && (e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && rect.Contains(p))
            {
                h = 1f - (p.y - rect.y) / rect.height;
                c = HSVToRGB(h, s, v, c.a);
                UpdateSVTexture(c, svTexture);

                e.Use();
            }
        }
        #endregion
    }
}
