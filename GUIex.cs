using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace KarlsonMapEditor
{
    internal class GUIex
    {
        public static bool Toggle(Rect pos, ref bool toggle, string onText="On", string offText="Off")
        {
            if (GUI.Button(pos, toggle ? onText : offText))
            {
                toggle = !toggle;
                return true;
            }
            return false;
        }

        public class Dropdown
        {

            private static readonly Texture2D BackgroundTex = new Texture2D(1, 1, TextureFormat.RGBAFloat, false);

            public Dropdown(string[] options, int defaultIdx)
            {
                Options = options;
                Index = defaultIdx;
                BackgroundTex.SetPixel(0, 0, Color.black);
                BackgroundTex.Apply();
                buttonText = new GUIStyle();
                buttonText.normal.background = BackgroundTex;
                buttonText.alignment = TextAnchor.MiddleCenter;
            }
            public string[] Options;
            public int Index;
            private bool dropped = false;
            GUIStyle buttonText;
            public void Draw(Rect pos)
            {
                if (!dropped)
                {
                    if (GUI.Button(pos, Options[Index])) dropped = true;
                    GUI.Label(new Rect(pos.x + pos.width - pos.height, pos.y, pos.height, pos.height), "▼");
                }
                else
                {
                    if (GUI.Button(pos, Options[Index])) dropped = false;
                    GUI.Label(new Rect(pos.x + pos.width - pos.height, pos.y, pos.height, pos.height), "▲");
                    GUI.Box(new Rect(pos.x, pos.y + pos.height, pos.width, pos.height * Options.Length), "");
                    for (int i = 0; i < Options.Length; i++)
                    {
                        string color = "<color=white>";
                        if (i == Index) color = "<color=green>";
                        if (GUI.Button(new Rect(pos.x, pos.y + pos.height * (i + 1), pos.width, pos.height), color + Options[i] + "</color>", buttonText))
                        {
                            Index = i;
                            dropped = false;
                        }
                    }
                }
            }
        }
    }
}
