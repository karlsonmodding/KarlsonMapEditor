using HarmonyLib;
using KarlsonMapEditor.LevelLoader;
using KarlsonMapEditor.Scripting_API;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace KarlsonMapEditor
{
    public static class LevelPlayer
    {
        private static Automata.Backbone.FunctionRunner mainFunction = null;
        public static ScriptRunner currentScript { get; private set; } = null;

        public static void LoadLevel(string levelPath)
        {
            LoadLevel(Path.GetFileName(levelPath), File.ReadAllBytes(levelPath));
        }

        public static void LoadWorkshopLevel(int id)
        {
            if (!File.Exists(Path.Combine(Main.directory, "Levels", "Workshop", id + ".kmm")))
                return;
            // read kmm
            using (BinaryReader br = new BinaryReader(File.OpenRead(Path.Combine(Main.directory, "Levels", "Workshop", id + ".kmm"))))
                LoadLevel(id + ".kmm", br.ReadBytes((int)br.ReadInt64()));
        }

        public static void LoadLevel(string levelName, byte[] levelData)
        {
            LevelLoader.LevelPlayer.LoadLevel(levelName, levelData, () =>
            {
                if (LevelLoader.LevelPlayer.levelData.AutomataScript.Trim().Length > 0)
                { // load level script
                    var tokens = Automata.Parser.Tokenizer.Tokenize(Automata.Parser.ProgramCleaner.CleanProgram(LevelLoader.LevelPlayer.levelData.AutomataScript));
                    var program = new Automata.Parser.ProgramParser(tokens).ParseProgram();
                    mainFunction = new Automata.Backbone.FunctionRunner(new List<(Automata.Backbone.VarResolver, Automata.Backbone.BaseValue.ValueType)> { }, program);
                }
            }, () =>
            {
                if (mainFunction != null)
                    currentScript = new ScriptRunner(mainFunction);
                else
                    currentScript = null;
            });
        }
    }

    [HarmonyPatch(typeof(Game), "MainMenu")]
    class Hook_Game_MainMenu
    {
        public static void Postfix()
        {
            LevelLoader.LevelPlayer.ExitedLevel();
        }
    }

    [HarmonyPatch(typeof(Glass), "OnTriggerEnter")]
    class Hook_Glass_OnTriggerEnter
    {
        public static bool Prefix(Glass __instance, Collider other)
        {
            if (LevelLoader.LevelPlayer.currentLevel == "") return true;
            if (LevelPlayer.currentScript == null) return true;
            // determine break reason
            if (other.gameObject.layer == LayerMask.NameToLayer("Ground"))
                return false; // collide with ground, ignore
            int reason = 0;
            if (other.gameObject.layer == LayerMask.NameToLayer("Player"))
                reason = 1;
            if (other.gameObject.layer == LayerMask.NameToLayer("Bullet"))
            {
                if (other.gameObject.name == "Damage")
                    reason = 3;
                else
                    reason = 2;
            }
            var ret = LevelPlayer.currentScript.InvokeFunction("onbreak", __instance, other, reason);
            if (!ret.HoldsTrue() || ret.Type != Automata.Backbone.BaseValue.ValueType.Number) return true; // continue normal execution
            var retN = (double)ret.Value;
            if(retN > 0) // insta-break
                UnityEngine.Object.Destroy(__instance.gameObject);
            return false;
        }
    }
}
