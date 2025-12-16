using HarmonyLib;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Loadson;
using UnityEngine;

namespace KarlsonMapEditor
{
    public static class LevelTimeDB
    {
        private static List<(string, float)> db = new List<(string, float)>();
        public static void Load()
        {
            if (!File.Exists(Path.Combine(Main.directory, "timetable")))
            {
                Console.Log("Creating time table");
                File.WriteAllText(Path.Combine(Main.directory, "timetable"), "0");
            }
            string[] lines = File.ReadAllLines(Path.Combine(Main.directory, "timetable"));
            int count = int.Parse(lines[0]);
            for (int i = 1; i <= count; i++)
                db.Add((lines[i].Split('|')[0], float.Parse(lines[i].Split('|')[1])));
        }

        public static void Save()
        {
            string buf = db.Count + "\n";
            foreach (var e in db)
                buf += e.Item1 + "|" + e.Item2 + "\n";
            File.WriteAllText(Path.Combine(Main.directory, "timetable"), buf);
        }

        public static float getForLevel(string level)
        {
            var r = from x in db where x.Item1 == level select x;
            if (r.Count() == 0) return 0f;
            else return r.First().Item2;
        }

        public static void writeForLevel(string level, float time)
        {
            var r = from x in db where x.Item1 == level select x;
            if (r.Count() != 0) db.Remove(r.First());
            db.Add((level, time));
        }
    }

    [HarmonyPatch(typeof(Game), "Win")]
    public class Hook_Game_Win
    {
        public static bool Prefix(Game __instance)
        {
            if (LevelLoader.LevelPlayer.currentLevel == "") return true;
            if (LevelPlayer.currentScript != null)
            {
                var ret = LevelPlayer.currentScript.InvokeFunction("onwin");
                if (ret.HoldsTrue()) return false;
            }
            __instance.playing = false;
            Timer.Instance.Stop();
            Time.timeScale = 0.05f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            UIManger.Instance.WinUI(true);
            float timer = Timer.Instance.GetTimer();
            float num3 = LevelTimeDB.getForLevel(LevelLoader.LevelPlayer.currentLevel);
            if (timer < num3 || num3 == 0f)
            {
                LevelTimeDB.writeForLevel(LevelLoader.LevelPlayer.currentLevel, timer);
                LevelTimeDB.Save();
            }
            MonoBehaviour.print("time has been saved as: " + Timer.Instance.GetFormattedTime(timer) + " on timetable");
            __instance.done = true;
            return false;
        }
    }
}
