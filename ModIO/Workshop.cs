using HarmonyLib;
using Loadson;
using LoadsonAPI;
using LoadsonExtensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Remoting.Metadata.W3cXsd2001;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace KarlsonMapEditor.ModIO
{
    public static class Workshop
    {
        static bool enabled = false, init = false, queryUser = false;
        static User currentUser = null;
        static Rect windowRect;
        static int wid;
        static string query = "";
        static GUIStyle queryBox, accountInfo, mapName;
        public static GUIStyle queryHint;
        static Texture2D icon_dl, icon_like, branding;

        static List<Mod> mods;
        static DateTime? lastUpdate = null;
        static GUIex.Dropdown sort_option;
        static int ModsToDownload = 0;

        static Vector2 scrollPos;

        static class DownloadProgress
        {
            static bool init = false;
            static int wid;
            static Rect wir;
            public static void DisplayProgress()
            {
                if (ModsToDownload == 0)
                    return;
                if(!init)
                {
                    init = true;
                    wid = ImGUI_WID.GetWindowId();
                    wir = new Rect(Screen.width - 205, Screen.height - 55, 200, 50);
                }
                GUI.Window(wid, wir, _ =>
                {
                    GUI.Label(new Rect(5, 25, 190, 20), $"Downloading {ModsToDownload} maps..");
                }, "Download progress");
            }
        }

        class DisplayedMod
        {
            private Mod Mod;
            List<Comment> comments;
            bool loading_mod = false;
            bool liked_mod = false;
            public DisplayedMod(Mod mod)
            {
                Mod = mod;
                scrollPos = new Vector2(0, 0);
                new Thread(() => {
                    loading_mod = true;
                    liked_mod = API.GetRating(Mod);
                    loading_mod = false;
                    comments = API.GetComments(mod);
                }).Start();
            }

            static bool init = false;
            static int wid;
            static Rect wir;
            static GUIStyle commentDate;
            Vector2 scrollPos;
            string comment = "";
            public bool DrawWindow()
            {
                if (Mod == null)
                    return false;
                if(!init)
                {
                    init = true;
                    wid = ImGUI_WID.GetWindowId();
                    if(Screen.width * 9f / 16f == Screen.height)
                        wir = new Rect(Screen.width * 2 / 10, Screen.height / 10, Screen.width * 6 / 10, Screen.height * 8 / 10);
                    else
                        wir = new Rect(Screen.width / 10, Screen.height / 10, Screen.width * 8 / 10, Screen.height * 8 / 10);
                    commentDate = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperRight };
                }
                wir = GUI.Window(wid, wir, _ =>
                {
                    if (GUI.Button(new Rect(wir.width - 50, 0, 50, 20), "Close"))
                    {
                        Mod = null;
                        return;
                    }
                    if(loading_mod)
                    {
                        GUI.Label(new Rect(5, 25, 600, 30), "<size=20>Loading map...</size>");
                        GUI.DragWindow();
                        return;
                    }
                    // calculate scroll height
                    var scrollHeight = 175 + (wir.width - 30) * 9 / 16;
                    if(comments != null)
                        scrollHeight += comments.Count * 60;
                    scrollPos = GUI.BeginScrollView(new Rect(5, 25, wir.width - 10, wir.height - 30), scrollPos, new Rect(0, 0, wir.width - 30, scrollHeight));
                    GUI.Label(new Rect(0, 0, 600, 30), "<size=20>" + Mod.name + "</size>");
                    GUI.DrawTexture(new Rect(0, 30, wir.width - 30, (wir.width - 30) * 9 / 16), Mod.image.GetTexture());
                    GUI.BeginGroup(new Rect(0, 35 + (wir.width - 30) * 9 / 16, int.MaxValue, int.MaxValue));
                    if (!Mod.downloaded && !Mod.downloading && GUI.Button(new Rect(wir.width - 130, 0, 100, 25), "Download"))
                        new Thread(() =>
                        {
                            Mod.downloading = true;
                            ModsToDownload++;
                            API.DownloadMod(Mod);
                            ModsToDownload--;
                            Mod.downloading = false;
                            Mod.downloaded = true;
                        }).Start();
                    if(Mod.downloaded && GUI.Button(new Rect(wir.width - 130, 0, 100, 25), "Play"))
                    {
                        LevelPlayer.LoadWorkshopLevel(Mod.id);
                        enabled = false;
                    }
                    if (GUI.Button(new Rect(wir.width - 210, 0, 75, 25), "Report"))
                        Process.Start($"https://mod.io/report/mods/{Mod.id}/widget");
                    GUI.DrawTexture(new Rect(0, 0, 30, 30), Mod.submitted_by.avatar.GetTexture(1));
                    var width = mapName.CalcSize(new GUIContent("<size=20>" + Mod.submitted_by.username + "</size>")).x;
                    GUI.Label(new Rect(35, 0, width, 30), "<size=20>" + Mod.submitted_by.username + "</size>", mapName);
                    if (GUI.Button(new Rect(40 + width, 5, 100, 20), "View Profile"))
                        displayUser = new DisplayUser(Mod.submitted_by);

                    GUI.BeginGroup(new Rect(145 + width, 5, 200, 20));
                    GUI.DrawTexture(new Rect(0, 0, 20, 20), icon_like);
                    width = mapName.CalcSize(new GUIContent(ShowBigNumber(Mod.ratings_total))).x;
                    GUI.Label(new Rect(25, 0, width, 20), ShowBigNumber(Mod.ratings_total));
                    if (liked_mod && GUI.Button(new Rect(30 + width, 0, 75, 20), "Unlike"))
                        new Thread(() =>
                        {
                            loading_mod = true;
                            if (API.RateMod(Mod, 0))
                            {
                                liked_mod = false;
                                Mod.ratings_total--;
                            }
                            loading_mod = false;
                        }).Start();
                    if (!liked_mod && GUI.Button(new Rect(30 + width, 0, 75, 20), "Like"))
                        new Thread(() =>
                        {
                            loading_mod = true;
                            if (API.RateMod(Mod, 1))
                            {
                                liked_mod = true;
                                Mod.ratings_total++;
                            }
                            loading_mod = false;
                        }).Start();
                    GUI.EndGroup();

                    GUI.Label(new Rect(0, 30, 600, 50), Mod.description);
                    if (comments == null)
                    {
                        GUI.Label(new Rect(0, 80, 600, 30), $"<size=20>Loading comments...</size>");
                        GUI.EndGroup();
                        GUI.EndScrollView();
                        GUI.DragWindow();
                        return;
                    }
                    GUI.Label(new Rect(0, 80, 600, 30), $"<size=20>Comments ({comments.Count})</size>");
                    if(!Mod.comments)
                    {
                        GUI.Box(new Rect(0, 110, wir.width - 30, 25), "Creator has disabled comments");
                    }
                    else
                    {
                        comment = GUI.TextField(new Rect(0, 110, wir.width - 135, 25), comment, queryBox);
                        if (comment == "")
                            GUI.Label(new Rect(0, 110, wir.width - 135, 25), " <color=grey>Write a comment</color>", mapName);
                        if (GUI.Button(new Rect(wir.width - 130, 110, 100, 25), "Comment") && comment.Trim().Length > 0)
                        {
                            comments = null;
                            new Thread(() =>
                            {
                                API.AddComment(Mod, comment);
                                comment = "";
                                comments = API.GetComments(Mod);
                            }).Start();
                        }
                    }

                    GUI.BeginGroup(new Rect(0, 140, int.MaxValue, int.MaxValue));
                    int k = 0;
                    foreach(var comment in comments)
                    {
                        GUI.BeginGroup(new Rect(0, 60 * k++, wir.width - 30, 55));
                        GUI.Box(new Rect(0, 0, wir.width - 30, 55), "");
                        GUI.DrawTexture(new Rect(5, 5, 20, 20), comment.user.avatar.GetTexture(1));
                        if(comment.user.id == Mod.submitted_by.id)
                            GUI.Label(new Rect(30, 5, 1000, 20), comment.user.username + " <color=cyan>(Creator)</color>");
                        else
                            GUI.Label(new Rect(30, 5, 1000, 20), comment.user.username);
                        GUI.Label(new Rect(0, 5, wir.width - 40, 20), comment.dateAdded.ToString(), commentDate);
                        GUI.Label(new Rect(5, 30, wir.width - 90, 25), comment.comment);
                        if(currentUser != null && comment.user.id == currentUser.id)
                        {
                            // if it's my comment, add delete button
                            if (GUI.Button(new Rect(wir.width - 85, 30, 50, 20), "Delete"))
                                ShowDialog("Confirm deletion", $"Are you sure you want to remove your comment \"{comment.comment}\"?", "Yes", "Cancel", () => new Thread(() =>
                                    {
                                        comments = null;
                                        API.DeleteComment(Mod, comment.id);
                                        comments = API.GetComments(Mod);
                                    }).Start());
                        }
                        GUI.EndGroup();
                    }
                    GUI.EndGroup();

                    GUI.EndGroup();
                    GUI.EndScrollView();
                    GUI.DragWindow();
                }, "mod.io - " + Mod.name);
                return true;
            }
        }
        static DisplayedMod displayedMod = null;

        public class EditMod
        {
            Mod Mod = null;
            string name, description;
            bool comments;
            bool upload_dialog;
            public EditMod(Mod mod)
            {
                Mod = mod;
                upload_dialog = false;

                name = mod.name;
                description = mod.description;
                comments = mod.comments;
            }
            Action<string, string, bool> OnConfirm = null;
            // same handler for uploading a level
            public EditMod(Action<string,string,bool> onConfirm)
            {
                OnConfirm = onConfirm;
                upload_dialog = true;

                name = "";
                description = "";
                comments = true; // enable comments by default
            }

            static int wid;
            static Rect wir;
            static bool init = false;

            bool uploading = false;
            public bool DrawWindow()
            {
                if (Mod == null && OnConfirm == null)
                    return false;
                if(!init)
                {
                    init = true;
                    wid = ImGUI_WID.GetWindowId();
                    wir = new Rect(Screen.width * 2 / 10, Screen.height * 2 / 10, Screen.width * 6 / 10, Screen.height * 6 / 10);
                }
                wir = GUI.Window(wid, wir, _ =>
                {
                    if(uploading)
                    {
                        GUI.Label(new Rect(5, 25, wir.width - 10, 25), "Please wait. Updating mod...");
                        GUI.DragWindow();
                        return;
                    }
                    if (!upload_dialog && GUI.Button(new Rect(wir.width - 105, 25, 100, 20), "Delete map"))
                        ShowDialog("Confirm deletion", $"Are you sure you want to delete {Mod.name}?", "Yes", "Cancel", () => new Thread(() =>
                        {
                            uploading = true;
                            API.DeleteMod(Mod);
                            Mod = null;
                            // invalidate workshop cache
                            lastUpdate = DateTime.Now.AddSeconds(-2);
                        }).Start());
                    GUI.Label(new Rect(5, 25, wir.width - 10, 25), "Name: ");
                    if(!upload_dialog)
                    {
                        name = GUI.TextField(new Rect(5, 50, wir.width - 10, 25), name);
                    }
                    else
                    {
                        GUI.DrawTexture(new Rect(wir.width - 167, 25, 162, 50), branding);
                        name = GUI.TextField(new Rect(5, 50, wir.width - 177, 25), name);
                    }
                    GUI.Label(new Rect(5, 80, wir.width - 10, 25), "Description: ");
                    description = GUI.TextArea(new Rect(5, 105, wir.width - 10, wir.height - 160), description);
                    comments = GUI.Toggle(new Rect(5, wir.height - 50, 200, 20), comments, "Enable comments");
                    if (name.Length < 1)
                        GUIex.DisabledButton(new Rect(5, wir.height - 25, wir.width / 2 - 7, 20), "Enter map's name");
                    else if (description.Trim().Length < 10)
                        GUIex.DisabledButton(new Rect(5, wir.height - 25, wir.width / 2 - 7, 20), "Description is too short");
                    else
                    {
                        if (!upload_dialog && GUI.Button(new Rect(5, wir.height - 25, wir.width / 2 - 7, 20), "Update"))
                        {
                            new Thread(() =>
                            {
                                uploading = true;
                                API.EditMod(Mod, name, description, comments);
                                Mod = null;
                                // invalidate workshop cache
                                lastUpdate = DateTime.Now.AddSeconds(-2);
                            }).Start();
                        }
                        if (upload_dialog && GUI.Button(new Rect(5, wir.height - 25, wir.width / 2 - 7, 20), "Upload"))
                        {
                            OnConfirm(name, description, comments);
                            OnConfirm = null;
                        }
                    }
                    if (GUI.Button(new Rect(wir.width / 2 + 2, wir.height - 25, wir.width / 2 - 7, 20), "Cancel"))
                    {
                        Mod = null;
                        OnConfirm = null;
                    }
                    GUI.DragWindow();
                }, upload_dialog ? "mod.io - Upload Map" : ("mod.io - Edit " + Mod.name));
                return true;
            }
        }
        static EditMod editMod = null;

        class DisplayUser
        {
            User User;
            List<Mod> UserMods;
            Vector2 scroll;
            public DisplayUser(User user)
            {
                User = user;
                scroll = new Vector2(0, 0);
                new Thread(() => UserMods = API.GetMods($"submitted_by={user.id}&_sort=-date_updated")).Start();
            }

            static bool init;
            static int wid;
            static Rect wir;
            public bool DrawWindow()
            {
                if (User == null)
                    return false;
                if (!init)
                {
                    init = true;
                    wid = ImGUI_WID.GetWindowId();
                    wir = new Rect(Screen.width * 2 / 10, Screen.height * 2 / 10, Screen.width * 6 / 10, Screen.height * 6 / 10);
                }
                if (!init)
                {
                    init = true;
                    wid = ImGUI_WID.GetWindowId();
                    wir = new Rect(Screen.width * 2 / 10, Screen.height * 2 / 10, Screen.width * 6 / 10, Screen.height * 6 / 10);
                }
                wir = GUI.Window(wid, wir, _ =>
                {
                    if(GUI.Button(new Rect(wir.width - 50, 0, 50, 20), "Close"))
                    {
                        User = null;
                        return;
                    }
                    GUI.DrawTexture(new Rect(5, 25, 50, 50), User.avatar.GetTexture(1));
                    GUI.Label(new Rect(60, 25, 500, 25), "<size=20>" + User.username + "</size>");
                    if(GUI.Button(new Rect(60, 55, 75, 20), "Report"))
                        Process.Start($"https://mod.io/report/users/{User.id}/widget");
                    if (UserMods == null)
                    {
                        GUI.Label(new Rect(5, 80, wir.width, 50), "<size=20>Loading maps ...</size>");
                        GUI.DragWindow();
                        return;
                    }

                    // draw mods
                    int mods_width = ((int)wir.width - 25) / 350;
                    // begin group
                    var max_width = mods_width * 355 - 5;
                    scroll = GUI.BeginScrollView(new Rect(((int)wir.width - max_width) / 2 - 12, 80, ((int)wir.width + max_width) / 2 + 7, wir.height - 85), scroll, new Rect(0, 0, max_width, (UserMods.Count + mods_width - 1) / mods_width * 255));
                    for (int k = 0; k < UserMods.Count; ++k)
                    {
                        int i = k / mods_width;
                        int j = k % mods_width;
                        RenderMod(UserMods[k], i, j);
                    }
                    GUI.EndScrollView();

                    GUI.DragWindow();
                }, "mod.io - " + User.username);
                return true;
            }
        }
        static DisplayUser displayUser = null;

        static class Dialog
        {
            public static bool Enabled;
            public static string Title, Caption, YesText, NoText;
            public static Action OnYes, OnNo;

            static bool init = false;
            static int wid;
            static Rect wir;
            public static void DrawDialog()
            {
                if (!Enabled)
                    return;
                if(!init)
                {
                    init = true;
                    wid = ImGUI_WID.GetWindowId();
                    wir = new Rect(Screen.width / 2 - 100, Screen.height / 2 - 70, 200, 140);
                }
                GUI.Box(wir, "");
                wir = GUI.ModalWindow(wid, wir, _ =>
                {
                    GUI.Label(new Rect(5, 25, 190, 85), Caption);
                    if(NoText == "")
                    {
                        if (GUI.Button(new Rect(5, 115, 190, 20), YesText))
                        {
                            OnYes?.Invoke();
                            Enabled = false;
                        }
                    }
                    else
                    {
                        if (GUI.Button(new Rect(5, 115, 93, 20), YesText))
                        {
                            OnYes?.Invoke();
                            Enabled = false;
                        }
                        if (GUI.Button(new Rect(102, 115, 93, 20), NoText))
                        {
                            OnNo?.Invoke();
                            Enabled = false;
                        }
                    }
                }, Title);
            }
        }
        public static void ShowDialog(string title, string caption, string yesText, string noText, Action onYes, Action onNo = null)
        {
            Dialog.Enabled = true;
            Dialog.Title = title;
            Dialog.Caption = caption;
            Dialog.YesText = yesText;
            Dialog.NoText = noText;
            Dialog.OnYes = onYes;
            Dialog.OnNo = onNo;
        }

        public static void _ongui()
        {
            Dialog.DrawDialog();
            if (!init)
            {
                init = true;
                windowRect = new Rect(0, 0, Screen.width, Screen.height);
                wid = ImGUI_WID.GetWindowId();
                queryBox = new GUIStyle(GUI.skin.textField);
                queryBox.alignment = TextAnchor.MiddleLeft;
                queryHint = new GUIStyle(GUI.skin.label);
                queryHint.alignment = TextAnchor.MiddleCenter;
                accountInfo = new GUIStyle(GUI.skin.label);
                accountInfo.alignment = TextAnchor.MiddleRight;
                mapName = new GUIStyle(GUI.skin.label);
                mapName.alignment = TextAnchor.MiddleLeft;
                mapName.wordWrap = false;
                sort_option = new GUIex.Dropdown(new string[] { "Most Popular", "Most Downloads", "Most Recent" }, 0);

                Color i = Color.clear, g = new Color(0.462f, 0.756f, 0.164f), r = new Color(0.737f, 0.086f, 0.086f);
                icon_dl = new Texture2D(15, 15);
                icon_dl.SetPixels(new Color[]
                {
                i,i,i,i,i,i,i,i,i,i,i,i,i,i,i,
                i,i,i,i,i,g,g,g,g,g,i,i,i,i,i,
                i,i,i,i,i,g,g,g,g,g,i,i,i,i,i,
                i,i,i,i,i,g,g,g,g,g,i,i,i,i,i,
                i,i,i,i,i,g,g,g,g,g,i,i,i,i,i,
                i,i,i,i,i,g,g,g,g,g,i,i,i,i,i,
                i,i,i,g,g,g,g,g,g,g,g,g,i,i,i,
                i,i,i,i,g,g,g,g,g,g,g,i,i,i,i,
                i,i,i,i,i,g,g,g,g,g,i,i,i,i,i,
                i,i,i,i,i,i,g,g,g,i,i,i,i,i,i,
                i,g,g,i,i,i,i,g,i,i,i,i,g,g,i,
                i,g,g,i,i,i,i,i,i,i,i,i,g,g,i,
                i,g,g,g,g,g,g,g,g,g,g,g,g,g,i,
                i,g,g,g,g,g,g,g,g,g,g,g,g,g,i,
                i,i,i,i,i,i,i,i,i,i,i,i,i,i,i,
                }.Reverse().ToArray());
                icon_dl.Apply();
                icon_like = new Texture2D(15, 15);
                icon_like.SetPixels(new Color[]
                {
                i,i,i,i,i,i,i,i,i,i,i,i,i,i,i,
                i,i,i,r,r,r,i,i,i,r,r,r,i,i,i,
                i,i,r,r,r,r,r,i,r,r,r,r,r,i,i,
                i,r,r,r,r,r,r,r,r,r,r,r,r,r,i,
                i,r,r,r,r,r,r,r,r,r,r,r,r,r,i,
                i,r,r,r,r,r,r,r,r,r,r,r,r,r,i,
                i,r,r,r,r,r,r,r,r,r,r,r,r,r,i,
                i,r,r,r,r,r,r,r,r,r,r,r,r,r,i,
                i,i,r,r,r,r,r,r,r,r,r,r,r,i,i,
                i,i,i,r,r,r,r,r,r,r,r,r,i,i,i,
                i,i,i,i,r,r,r,r,r,r,r,i,i,i,i,
                i,i,i,i,i,r,r,r,r,r,i,i,i,i,i,
                i,i,i,i,i,i,r,r,r,i,i,i,i,i,i,
                i,i,i,i,i,i,i,r,i,i,i,i,i,i,i,
                i,i,i,i,i,i,i,i,i,i,i,i,i,i,i,
                }.Reverse().ToArray());
                icon_like.Apply();
                branding = new Texture2D(0, 0);
                branding.LoadFromResources("KarlsonMapEditor.Assets.Branding.png");
            }
            if (!enabled)
                return;

            DownloadProgress.DisplayProgress();
            if (displayedMod != null && !displayedMod.DrawWindow())
                displayedMod = null;
            if (editMod != null && !editMod.DrawWindow())
                editMod = null;
            if (displayUser != null && !displayUser.DrawWindow())
                displayUser = null;

            GUI.Box(windowRect, "mod.io Workshop");
            if (GUI.Button(new Rect(Screen.width - 50, 0, 50, 20), "Close"))
            {
                enabled = false;
                GameObject.Find("/UI").transform.Find("Play").Find("Back").gameObject.GetComponent<Button>().onClick.Invoke();
            }

            // profile box
            GUI.BeginGroup(new Rect(Screen.width - 205, 25, 200, 60));
            GUI.Box(new Rect(0, 0, 200, 60), "");
            if (currentUser == null)
                GUI.Label(new Rect(5, 5, 190, 50), "Loading account . .");
            else
            {
                GUI.Label(new Rect(5, 5, 135, 50), "Logged into mod.io\n" + currentUser.username, accountInfo);
                GUI.DrawTexture(new Rect(145, 5, 50, 50), currentUser.avatar.GetTexture(1));
                if (GUI.Button(new Rect(145, 35, 50, 20), "View"))
                    displayUser = new DisplayUser(currentUser);
            }
            GUI.EndGroup();

            // query box
            GUI.BeginGroup(new Rect(250, 30, Screen.width - 500, 500));
            if (query != (query = GUI.TextField(new Rect(0, 0, Screen.width - 500, 25), query, queryBox)))
                lastUpdate = DateTime.Now;
            if(sort_option.Draw(new Rect(Screen.width - 700, 25, 200, 25)))
                lastUpdate = DateTime.Now.AddSeconds(-2); // instant update when changing sort
            if (query == "")
                GUI.Label(new Rect(0, 0, Screen.width - 500, 25), "<b>Search</b>", queryHint);
            GUI.EndGroup();

            GUI.DrawTexture(new Rect(15, 15, 195, 60), branding);
            if (mods == null)
            {
                GUI.Label(new Rect(5, 90, Screen.width, 200), "<size=24>Loading maps ..</size>");
                return;
            }
            if(mods.Count == 0)
            {
                GUI.Label(new Rect(5, 90, Screen.width, 200), "<size=24>There are no maps matching your query</size>");
                return;
            }
            // draw mods
            int mods_width = (Screen.width - 25) / 350;
            // begin group
            var max_width = mods_width * 355 - 5;
            scrollPos = GUI.BeginScrollView(new Rect((Screen.width - max_width) / 2 - 12, 100, (Screen.width + max_width) / 2 + 7, Screen.height - 105), scrollPos, new Rect(0, 0, max_width, (mods.Count + mods_width - 1) / mods_width * 255));
            for(int k = 0; k < mods.Count; ++k)
            {
                int i = k / mods_width;
                int j = k % mods_width;
                RenderMod(mods[k], i, j);
            }
            GUI.EndScrollView();

            sort_option.Draw(new Rect(Screen.width - 450, 55, 200, 25));
        }
        static void RenderMod(Mod mod, int i, int j)
        {
            GUI.BeginGroup(new Rect(j * 355, i * 255, 350, 250));
            GUI.Box(new Rect(0, 0, 350, 250), "");
            GUI.DrawTexture(new Rect(5, 5, 340, 191), mod.image_thumbnail.GetTexture());
            GUI.Label(new Rect(5, 196, 340, 29), "<size=20>" + mod.name + "</size>", mapName);
            GUI.DrawTexture(new Rect(5, 225, 20, 20), mod.submitted_by.avatar.GetTexture(1));
            GUI.Label(new Rect(30, 225, 310, 20), "<color=grey>" + mod.submitted_by.username + "</color>", mapName);
            // draw like and download counter
            var width = GUI.skin.label.CalcSize(new GUIContent(ShowBigNumber(mod.ratings_total))).x;
            GUI.Label(new Rect(345 - width, 225, width, 20), ShowBigNumber(mod.ratings_total), mapName);
            GUI.DrawTexture(new Rect(325 - width, 227, 20, 20), icon_like);
            var width2 = GUI.skin.label.CalcSize(new GUIContent(ShowBigNumber(mod.downloads_total))).x;
            GUI.Label(new Rect(320 - width - width2, 225, width, 20), ShowBigNumber(mod.downloads_total), mapName);
            GUI.DrawTexture(new Rect(300 - width - width2, 227, 20, 20), icon_dl);
            if (currentUser != null && currentUser.id == mod.submitted_by.id && GUI.Button(new Rect(293, 149, 50, 20), "Edit"))
                editMod = new EditMod(mod);
            if (GUI.Button(new Rect(293, 174, 50, 20), "View"))
                displayedMod = new DisplayedMod(mod);
            if (mod.downloaded && GUI.Button(new Rect(7, 174, 50, 20), "Play"))
            {
                LevelPlayer.LoadWorkshopLevel(mod.id);
                enabled = false;
            }
            GUI.EndGroup();
        }

        public static void _onupdate()
        {
            if (!lastUpdate.HasValue)
                return;
            if ((DateTime.Now - lastUpdate.Value).TotalSeconds < 2)
                return;
            if (mods == null)
                return; // there is another query already running
            lastUpdate = null;
            mods = null;
            scrollPos = new Vector2(0, 0);
            new Thread(() =>
            {
                string queryString = "";
                if (query != "")
                    queryString = $"_q={Uri.EscapeDataString(query)}&";
                switch(sort_option.Index)
                {
                    case 0: queryString += "_sort=-popular"; break;
                    case 1: queryString += "_sort=-downloads_total"; break;
                    case 2: queryString += "_sort=-date_updated"; break;
                }
                mods = API.GetMods(queryString);
            }).Start();
        }

        static bool workshop_fetched = false;
        public static void Open()
        {
            scrollPos = new Vector2(0, 0);
            enabled = true;
            if (mods == null && !workshop_fetched)
            {
                workshop_fetched = true;
                new Thread(() => mods = API.GetMods("_sort=-popular")).Start();
            }
            if (Auth.ModioBearer != "" && currentUser == null && !queryUser)
            {
                queryUser = true;
                new Thread(() => currentUser = Auth.GetCurrentUser()).Start();
            }
        }

        private static string ShowBigNumber(float number)
        {
            string[] suffix = { "", " k", " M", " B" };
            int suffixIndex;
            for (suffixIndex = 0; number >= 1000; suffixIndex++)
                number /= 1000;
            return number.ToString("0.##") + suffix[suffixIndex];
        }
    }
}
