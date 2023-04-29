using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq.Expressions;
using Loadson;
using LoadsonAPI;
using UnityEngine;
using UnityEngine.UIElements;

namespace KarlsonMapEditor
{
    public class Main : Mod
    {
        public override void OnEnable()
        {
            prefs = Preferences.GetPreferences();
            if (!prefs.ContainsKey("edit_recent"))
                prefs.Add("edit_recent", "");

            directory = GetUserFilesFolder();
            Directory.CreateDirectory(Path.Combine(directory, "Levels", "Workshop"));

            LevelTimeDB.Load();

            HarmonyLib.Harmony harmony = new HarmonyLib.Harmony("KarlsonMapEditor");
            harmony.PatchAll();

            List<Texture2D> temp = new List<Texture2D>();
            foreach(var t in Resources.FindObjectsOfTypeAll<Texture2D>())
            {
                switch(t.name)
                {
                    default: break;
                    case "GridBox_Default":
                    case "prototype_512x512_grey3":
                    case "prototype_512x512_white":
                    case "prototype_512x512_yellow":
                    case "Floor":
                    case "Blue":
                    case "Red":
                    case "Barrel":
                    case "Orange":
                    case "Yellow":
                    case "UnityWhite":
                    case "UnityNormalMap":
                    case "Sunny_01B_down":
                        temp.Add(t);
                        break;
                }
            }
            gameTex = temp.ToArray();
            if(gameTex.Length != 13) Console.Log("<color=red>Invalid game texture array. Expected 13 items, got " + gameTex.Length + "</color>");

            MenuEntry.AddMenuEntry(new List<(string, System.Action)>
            {
                ("List", ()=> {
                    MenuCamera cam = UnityEngine.Object.FindObjectOfType<MenuCamera>();
                    typeof(MenuCamera).GetField("desiredPos", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(cam, new Vector3(-1f, 15.1f, 184.06f));
                    typeof(MenuCamera).GetField("desiredRot", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(cam, Quaternion.Euler(0f, -90f, 0f));

                    GameObject.Find("/UI/Menu").SetActive(false);
                    GameObject.Find("/UI").transform.Find("Custom").gameObject.SetActive(true);
                    Hook_Lobby_Start.RenderMenuPage(1);
                }),
                ("Workshop", ()=>{}),
                ("Editor", ()=> LevelEditor.StartEdit()),
            }, "Map Editor");

            AddAPIFunction("KarlsonMapEditor.PickFile", (args) =>
            {
                if (args.Length == 0)
                    return FilePicker.PickFile("Open a file..");
                if (args.Length == 1)
                    return FilePicker.PickFile((string)args[0]);
                return FilePicker.PickFile((string)args[0], (string)args[1]);
            });
            // load im assets
            ColorPicker.imRight = LoadAsset<Texture2D>("imRight");
            ColorPicker.imLeft = LoadAsset<Texture2D>("imLeft");
            ColorPicker.imCircle = LoadAsset<Texture2D>("imCircle");

            if (!DiscordAPI.HasDiscord)
                Console.Log("Discord not found. You will not be able to upload levels to the workshop");
            else
            {
                System.DateTime start = System.DateTime.Now;
                IEnumerator awaitDiscord()
                {
                    while (DiscordAPI.User.Id == 0) yield return new WaitForSeconds(0.2f);
                    while (DiscordAPI.Bearer == "") yield return new WaitForSeconds(0.2f);
                    Console.Log("Logged in discord as " + DiscordAPI.User.Username + "#" + DiscordAPI.User.Discriminator + " (" + DiscordAPI.User.Id + ")");
                    Console.Log("Bearer token for authentication is " + DiscordAPI.Bearer);
                    Console.Log("Took " + (System.DateTime.Now - start).TotalMilliseconds + "ms to initialize discord");
                }
                Coroutines.StartCoroutine(awaitDiscord());
            }
        }

        public override void OnGUI()
        {
            LevelEditor._ongui();
        }

        public override void Update(float deltaTime)
        {
            LevelEditor._onupdate();
        }

        public static Dictionary<string, string> prefs;
        public static string directory;
        public static Texture2D[] gameTex;
    }
}
