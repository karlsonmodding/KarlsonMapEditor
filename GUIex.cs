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

        public static void DisabledButton(Rect pos, string text)
        {
            GUI.Box(pos, "");
            GUI.Label(pos, "<color=grey>" + text + "</color>", ModIO.Workshop.queryHint);
        }

        public class Dropdown
        {
            private static readonly Texture2D BackgroundTex = new Texture2D(1, 1, TextureFormat.RGBAFloat, false);

            public Dropdown(string[] options, int defaultIdx)
            {
                Options = options;
                Index = defaultIdx;
            }
            public string[] Options;
            public int Index;
            private bool dropped = false;
            public static GUIStyle dropdownButton = null;
            static GUIStyle buttonText = null, dropdownText = null;
            public bool Draw(Rect pos)
            {
                if (dropdownButton == null)
                {
                    BackgroundTex.SetPixel(0, 0, Color.clear);
                    BackgroundTex.Apply();
                    dropdownButton = new GUIStyle(GUI.skin.button);
                    dropdownButton.normal.background = BackgroundTex;
                    dropdownButton.alignment = TextAnchor.MiddleLeft;
                }
                if (buttonText == null)
                {
                    buttonText = new GUIStyle(GUI.skin.button);
                    buttonText.alignment = TextAnchor.MiddleLeft;
                }
                if (dropdownText == null)
                {
                    dropdownText = new GUIStyle(GUI.skin.label);
                    dropdownText.alignment = TextAnchor.MiddleCenter;
                }

                if (!dropped)
                {
                    if (GUI.Button(pos, Options[Index], buttonText)) dropped = true;
                    GUI.Label(new Rect(pos.x + pos.width - pos.height, pos.y, pos.height, pos.height), "▼", dropdownText);
                }
                else
                {
                    if (GUI.Button(pos, Options[Index], buttonText)) dropped = false;
                    GUI.Label(new Rect(pos.x + pos.width - pos.height, pos.y, pos.height, pos.height), "▲", dropdownText);
                    GUI.Box(new Rect(pos.x, pos.y + pos.height, pos.width, pos.height * Options.Length), "");
                    for (int i = 0; i < Options.Length; i++)
                    {
                        string color = "<color=white>";
                        if (i == Index) color = "<color=#00FF00>";
                        if (GUI.Button(new Rect(pos.x, pos.y + pos.height * (i + 1), pos.width, pos.height), color + Options[i] + "</color>", dropdownButton))
                        {
                            Index = i;
                            dropped = false;
                            return true;
                        }
                    }
                }
                return false;
            }
        }
    }
}
