using LoadsonAPI;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UI;

namespace KarlsonMapEditor.Workshop_API
{
    public static class WorkshopGUI
    {
        public static void OpenWorkshop()
        {
            if (windowId == -1) windowId = ImGUI_WID.GetWindowId();
            if (windowIdDL == -1) windowIdDL = ImGUI_WID.GetWindowId();
            workshopOpen = true;
            WorkshopCache.MakeCache();
            if (initTex) return;
            initTex = true;
            icon_dl = new Texture2D(15, 15);
            Color i = Color.clear, g = new Color(0.462f, 0.756f, 0.164f), r = new Color(0.737f, 0.086f, 0.086f);
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
            icon_unlike = new Texture2D(15, 15);
            icon_unlike.SetPixels(new Color[]
            {
                i,i,i,i,i,i,i,i,i,i,i,i,i,i,i,
                i,i,i,r,r,r,i,i,i,r,r,r,i,i,i,
                i,i,r,i,i,i,r,i,r,i,i,i,r,i,i,
                i,r,i,i,i,i,i,r,i,i,i,i,i,r,i,
                i,r,i,i,i,i,i,i,i,i,i,i,i,r,i,
                i,r,i,i,i,i,i,i,i,i,i,i,i,r,i,
                i,r,i,i,i,i,i,i,i,i,i,i,i,r,i,
                i,r,i,i,i,i,i,i,i,i,i,i,i,r,i,
                i,i,r,i,i,i,i,i,i,i,i,i,r,i,i,
                i,i,i,r,i,i,i,i,i,i,i,r,i,i,i,
                i,i,i,i,r,i,i,i,i,i,r,i,i,i,i,
                i,i,i,i,i,r,i,i,i,r,i,i,i,i,i,
                i,i,i,i,i,i,r,i,r,i,i,i,i,i,i,
                i,i,i,i,i,i,i,r,i,i,i,i,i,i,i,
                i,i,i,i,i,i,i,i,i,i,i,i,i,i,i,
            }.Reverse().ToArray());
            icon_unlike.Apply();
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
            dl_back = new Texture2D(1, 1);
            dl_back.SetPixel(0, 0, new Color(0.5f, 0.5f, 0.5f));
            dl_back.Apply();
            dl_fore = new Texture2D(1, 1);
            dl_fore.SetPixel(0, 0, new Color(1f, 1f, 1f));
            dl_fore.Apply();
            leftAlign = new GUIStyle();
            leftAlign.alignment = TextAnchor.UpperRight;
            emptyButton = new GUIStyle();
            emptyButton.normal.background = Texture2D.blackTexture;
        }
        private static bool workshopOpen = false;

        private static int windowId = -1, windowIdDL = -1;
        private static Rect windowRect = new Rect((Screen.width - 1150) / 2, (Screen.height - 800) / 2, 1150, 800);
        private static Vector2 scrollLiked = new Vector2(0, 0), scrollDl = new Vector2(0, 0), scrollRecent = new Vector2(0, 0);

        private static bool initTex = false;
        private static Texture2D icon_dl, icon_like, icon_unlike, dl_back, dl_fore;
        private static GUIStyle leftAlign, emptyButton;

        public static void _OnGUI()
        {
            if(WorkshopCache.QueuedFiles() > 0)
            {
                GUI.Window(windowIdDL, new Rect(Screen.width - 205, 5, 200, 50), (_) =>
                {
                    GUI.Label(new Rect(5, 20, 190, 30), "Current file: " + WorkshopCache.downloadCurrent);
                    GUI.DrawTexture(new Rect(5, 40, 190, 5), dl_back);
                    GUI.DrawTexture(new Rect(5, 40, 190 * (WorkshopCache.downloadProgress / 100f), 5), dl_fore);
                }, "Downloading " + WorkshopCache.QueuedFiles() + " files..");
            }
            else if(WorkshopCache.WorkshopLoadingLevels() > 0)
            {
                GUI.Window(windowIdDL, new Rect(Screen.width - 205, 5, 200, 45), (_) =>
                {
                    GUI.Label(new Rect(5, 20, 190, 30), "Loading " + WorkshopCache.WorkshopLoadingLevels() + " levels..");
                    /*GUI.DrawTexture(new Rect(5, 40, 190, 5), dl_back);
                    GUI.DrawTexture(new Rect(5, 40, 190 * (WorkshopCache.downloadProgress / 100f), 5), dl_fore);*/
                }, "Loading workshop levels..");
            }
            if(!workshopOpen) return;
            GUI.Box(windowRect, "");
            windowRect = GUI.Window(windowId, windowRect, (_) =>
            {
                GUI.DragWindow(new Rect(0, 0, 1030, 20));
                if (GUI.Button(new Rect(1100, 0, 50, 20), "Close"))
                {
                    workshopOpen = false;
                    GameObject.Find("/UI").transform.Find("Play").Find("Back").gameObject.GetComponent<Button>().onClick.Invoke();
                }
                if (GUI.Button(new Rect(1030, 0, 70, 20), "Refresh"))
                {
                    WorkshopCache.ClearCache();
                    WorkshopCache.MakeCache();
                }
                GUI.Label(new Rect(5, 20, 1000, 100), "<size=20>Most Liked Levels:</size>");
                scrollLiked = GUI.BeginScrollView(new Rect(5, 40, 1140, 230), scrollLiked, new Rect(0, 0, Math.Max(281 * WorkshopCache.mostLikedCache.Length, 200), 200));
                if (WorkshopCache.mostLikedCacheGen)
                    GUI.Label(new Rect(50, 100, 200, 200), "Loading . . .");
                else if (WorkshopCache.mostLikedCache.Length == 0)
                    GUI.Label(new Rect(50, 100, 200, 200), "This list is empty :(");
                else
                {
                    for (int i = 0; i < WorkshopCache.mostLikedCache.Length; i++)
                        RenderLevel(281 * i + 5, 10, WorkshopCache.levelCache[WorkshopCache.mostLikedCache[i]]);
                }
                GUI.EndScrollView();
                GUI.Label(new Rect(5, 280, 1000, 100), "<size=20>Most Downloaded Levels:</size>");
                scrollDl = GUI.BeginScrollView(new Rect(5, 300, 1140, 230), scrollDl, new Rect(0, 0, Math.Max(281 * WorkshopCache.mostDlCache.Length, 200), 200));
                if (WorkshopCache.mostDlCacheGen)
                    GUI.Label(new Rect(50, 100, 200, 200), "Loading . . .");
                else if (WorkshopCache.mostDlCache.Length == 0)
                    GUI.Label(new Rect(50, 100, 200, 200), "This list is empty :(");
                else
                {
                    for (int i = 0; i < WorkshopCache.mostDlCache.Length; i++)
                        RenderLevel(281 * i + 5, 10, WorkshopCache.levelCache[WorkshopCache.mostDlCache[i]]);
                }
                GUI.EndScrollView();
                GUI.Label(new Rect(5, 540, 1000, 100), "<size=20>Most Recent Levels:</size>");
                scrollRecent = GUI.BeginScrollView(new Rect(5, 560, 1140, 230), scrollRecent, new Rect(0, 0, Math.Max(281 * WorkshopCache.mostRecCache.Length, 200), 200));
                if (WorkshopCache.mostRecCacheGen)
                    GUI.Label(new Rect(50, 100, 200, 200), "Loading . . .");
                else if(WorkshopCache.mostRecCache.Length == 0)
                    GUI.Label(new Rect(50, 100, 200, 200), "This list is empty :(");
                else
                {
                    for (int i = 0; i < WorkshopCache.mostRecCache.Length; i++)
                        RenderLevel(281 * i + 5, 10, WorkshopCache.levelCache[WorkshopCache.mostRecCache[i]]);
                }
                GUI.EndScrollView();
            }, "Karlson Map Editor Workshop");
        }

        private static void RenderLevel(int x, int y, WorkshopLevel level) => _RenderLevel(x, y, level.Thumbnail, level.Name, level.Author, level.Id, level.Dl, level.Likes, level.Liked, !level.Downloaded, level.ToggleLike, level.DownloadLevel);

        private static void _RenderLevel(int x, int y, Texture2D image, string name, string author, int levelid, int dl, int like, bool isLiked, bool canDl, Action onLike, Action onDl)
        {
            GUI.BeginGroup(new Rect(x, y, 276, 200));
            GUI.Box(new Rect(0, 0, 276, 200), "");
            GUI.DrawTexture(new Rect(5, 5, 266, 150), image);
            GUI.Label(new Rect(5, 155, 266, 25), "<size=15>" + name + "</size>");
            GUI.Label(new Rect(5, 177, 266, 20), "<size=13><color=grey>by " + author + " [ID " + levelid + "]</color></size>");
            leftAlign.normal.textColor = new Color(0.462f, 0.756f, 0.164f);
            GUI.Label(new Rect(206, 160, 46, 15), ShowBigNumber(dl), leftAlign);
            leftAlign.normal.textColor = new Color(0.737f, 0.086f, 0.086f);
            GUI.Label(new Rect(206, 180, 46, 15), ShowBigNumber(like), leftAlign);
            GUI.DrawTexture(new Rect(256, 160, 15, 15), icon_dl);
            if (GUI.Button(new Rect(256, 180, 15, 15), isLiked ? icon_like : icon_unlike, emptyButton)) onLike();
            if(canDl)
            {
                if (GUI.Button(new Rect(246, 130, 20, 20), "<size=20><color=green><b>+</b></color></size>")) onDl();
            }
            else
            {
                GUI.Box(new Rect(246, 130, 20, 20), "<size=10><color=green><b>✓</b></color></size>");
            }
            GUI.EndGroup();
        }

        private static string ShowBigNumber(float number)
        {
            string[] suffix = { "", " k", " M", " B" };
            int suffixIndex;
            for (suffixIndex = 0; number >= 1100; suffixIndex++)
                number /= 1000;
            return number.ToString("0.##") + suffix[suffixIndex];
        }
    }
}
