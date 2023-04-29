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
    }
}
