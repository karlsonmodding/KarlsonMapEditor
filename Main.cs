using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using KarlsonMapEditor.Workshop_API;
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
            if(gameTex.Length != 13) Loadson.Console.Log("<color=red>Invalid game texture array. Expected 13 items, got " + gameTex.Length + "</color>");

            MenuEntry.AddMenuEntry(new List<(string, System.Action)>
            {
                ("List", () => {
                    MenuCamera cam = UnityEngine.Object.FindObjectOfType<MenuCamera>();
                    typeof(MenuCamera).GetField("desiredPos", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(cam, new Vector3(-1f, 15.1f, 184.06f));
                    typeof(MenuCamera).GetField("desiredRot", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(cam, Quaternion.Euler(0f, -90f, 0f));

                    GameObject.Find("/UI/Menu").SetActive(false);
                    GameObject.Find("/UI").transform.Find("Custom").gameObject.SetActive(true);
                    Hook_Lobby_Start.RenderMenuPage(1);
                }),
                ("Workshop", () => {
                    if(DiscordAPI.HasDiscord && workshopToken == "") return; // wait for workshop login
                    MenuCamera cam = UnityEngine.Object.FindObjectOfType<MenuCamera>();
                    typeof(MenuCamera).GetField("desiredPos", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(cam, new Vector3(-1f, 15.1f, 184.06f));
                    typeof(MenuCamera).GetField("desiredRot", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(cam, Quaternion.Euler(0f, -90f, 0f));

                    GameObject.Find("/UI/Menu").SetActive(false);
                    WorkshopGUI.OpenWorkshop();
                }),
                ("Editor", () => LevelEditor.StartEdit()),
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
                Loadson.Console.Log("Discord not found. You will not be able to upload levels to the workshop");
            else
            {
                new Thread(() =>
                {
                    Loadson.Console.Log("Awaiting discord..");
                    while (DiscordAPI.User.Id == 0) Thread.Sleep(200);
                    Loadson.Console.Log("Got discord user id " + DiscordAPI.User.Id);
                    while (DiscordAPI.Bearer.Length < 2)
                    {
                        Loadson.Console.Log("Waiting for bearer (" + DiscordAPI.Bearer + ")");
                        Thread.Sleep(200);
                    }
                    Loadson.Console.Log("Got discord bearer " + DiscordAPI.Bearer);
                    Loadson.Console.Log("[WAPI] Logging in as " + DiscordAPI.User.Id + " " + DiscordAPI.Bearer);
                    int[] ta;
                    (workshopToken, ta) = Core.Login(DiscordAPI.User.Id, DiscordAPI.Bearer);
                    workshopLikes = ta.ToList();
                }).Start();
            }
            loginWid = ImGUI_WID.GetWindowId();
        }

        int loginWid;
        Rect loginRect = new Rect(Screen.width - 205, Screen.height - 50, 200, 45);

        public override void OnGUI()
        {
            LevelEditor._ongui();
            WorkshopGUI._OnGUI();
            if(loginWid != -1 && workshopToken == "")
                GUI.Window(loginWid, loginRect, (_) => {
                    if (!LoadsonAPI.DiscordAPI.HasDiscord)
                        GUI.Label(new Rect(5, 20, 200, 30), "Discord was not detected");
                    else if (LoadsonAPI.DiscordAPI.User.Id == 0)
                        GUI.Label(new Rect(5, 20, 200, 30), "Awaiting Discord User");
                    else if (DiscordAPI.Bearer.Length < 2)
                        GUI.Label(new Rect(5, 20, 200, 30), "Awaiting Discord Bearer");
                    else
                        GUI.Label(new Rect(5, 20, 200, 30), "Logging into KME Workshop");
                }, "KME Workshop Login");
        }

        public override void Update(float deltaTime)
        {
            LevelEditor._onupdate();
            if (runOnMain.Count > 0)
            { // once at a time, not to overload
                Action run = runOnMain[0];
                runOnMain.RemoveAt(0);
                run();
            }
        }

        public static Dictionary<string, string> prefs;
        public static string directory;
        public static Texture2D[] gameTex;
        public static string workshopToken = "";
        public static List<int> workshopLikes;
        public static List<Action> runOnMain = new List<Action>();
    }
}
