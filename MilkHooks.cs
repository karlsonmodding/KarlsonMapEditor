using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.UI;

namespace KarlsonMapEditor
{
    [HarmonyPatch(typeof(Milk), "OnTriggerEnter")]
    class Milk_OnTriggerEnter
    {
        static bool Prefix()
        {
            return !LevelEditor.editorMode;
        }

        static void Postfix()
        {
            if (LevelEditor.editorMode) return;
            if (LevelPlayer.currentLevel != "") UIManger.Instance.winUI.transform.Find("NextBtn").gameObject.GetComponent<Button>().interactable = false;
            else UIManger.Instance.winUI.transform.Find("NextBtn").gameObject.GetComponent<Button>().interactable = true;
        }
    }
}
