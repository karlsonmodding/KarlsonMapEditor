using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace KarlsonMapEditor
{
    [HarmonyPatch(typeof(PlayerMovement), "Pause")]
    public class PlayerMovement_Pause
    {
        public static bool Prefix() => !LevelEditor.editorMode;
    }
    [HarmonyPatch(typeof(PlayerMovement), "FixedUpdate")]
    public class PlayerMovement_FixedUpdate
    {
        public static bool Prefix() => !LevelEditor.editorMode;
    }

    [HarmonyPatch(typeof(PlayerMovement), "Update")]
    public class PlayerMovement_Update
    {
        public static bool Prefix(PlayerMovement __instance)
        {
            if (!LevelEditor.editorMode) return true;
            __instance.rb.velocity = Vector3.zero;
            if (Input.GetButton("Fire2"))
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;

                float x = Input.GetAxisRaw("Horizontal");
                float z = Input.GetAxisRaw("Vertical");
                float vertical = (Input.GetButton("Pickup") ? 0.1f : 0) - (Input.GetButton("Drop") ? 0.1f : 0);
                float scale = 20f;
                if (Input.GetKey(KeyCode.LeftShift)) scale *= 2;
                __instance.gameObject.transform.position += (Camera.main.transform.forward * z + Camera.main.transform.right * x + new Vector3(0, vertical * scale, 0)) * scale * Time.unscaledDeltaTime;
                typeof(PlayerMovement).GetMethod("Look", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(__instance, Array.Empty<object>());
            }
            else
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            if (UIManger.Instance.gameUI.activeSelf) UIManger.Instance.gameUI.SetActive(false);
            GameObject ps = GameObject.Find("Camera/Main Camera/Particle System"); // you little fucker
            if (ps.activeSelf) ps.SetActive(false);
            return false;
        }
    }
}
