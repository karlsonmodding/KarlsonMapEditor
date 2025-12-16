using ICSharpCode.SharpZipLib.Zip;
using KarlsonMapEditor.Automata.Parser;
using Loadson;
using LoadsonAPI;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace KarlsonMapEditor.ModIO
{
    public static class ApiData
    {
        public const string GameId = "11560";
        public const string URL = "https://g-11560.modapi.io/v1";
        public const string AuthProxy = "https://kme-auth.devilexe1337.workers.dev";
    }

    public static class Auth
    {
        static string promptString = "";
        static List<(string, string)> promptLinks = new List<(string, string)>();
        static bool promptTerms = false;
        static int promptWid = 0;
        static Rect promptRect;

        public static string ModioBearer = "";

        public static void _ongui()
        {
            if (!promptTerms)
                return;
            if (promptWid == 0)
            {
                promptWid = ImGUI_WID.GetWindowId();
                promptRect = new Rect(Screen.width / 2 - 200, Screen.height / 2 - 150, 400, 300);
            }
            GUI.Box(promptRect, "");
            GUI.ModalWindow(promptWid, promptRect, _ =>
            {
                GUI.Label(new Rect(5, 20, 390, 230), promptString);
                int i = 0;
                GUI.Label(new Rect(5, 225, 100, 20), "Links:");
                foreach (var link in promptLinks)
                {
                    if (GUI.Button(new Rect(5 + 131 * i, 245, 126, 20), link.Item1))
                        Process.Start(link.Item2);
                    i++;
                }
                if (GUI.Button(new Rect(5, 270, 192, 25), "I Agree"))
                {
                    new Thread(() => Login(true)).Start();
                    promptTerms = false;
                    GameObject.Find("/UI").transform.Find("Play").Find("Back").gameObject.GetComponent<Button>().onClick.Invoke();
                }
                if (GUI.Button(new Rect(203, 270, 192, 25), "No, Thanks"))
                {
                    Main.noDiscordAck = true; // disable the bottom right login status
                    promptTerms = false;
                    GameObject.Find("/UI").transform.Find("Play").Find("Back").gameObject.GetComponent<Button>().onClick.Invoke();
                }
            }, "mod.io Workshop Terms & Conditions");
        }

        public static void Login(bool accepted_terms = false)
        {
            using (WebClient wc = new WebClient())
            {
                wc.Headers[HttpRequestHeader.ContentType] = "application/x-www-form-urlencoded";
                try
                {
                    string result = wc.UploadString($"{ApiData.AuthProxy}/auth", $"discord_token={DiscordAPI.Bearer}&terms_agreed={(accepted_terms ? "true" : "false")}");
                    JToken obj = JToken.Parse(result);
                    if ((int)obj["code"] != 200)
                        return;
                    ModioBearer = (string)obj["access_token"];
                }
                catch (WebException wex)
                {
                    using (StreamReader sr = new StreamReader(wex.Response.GetResponseStream()))
                    {
                        JToken obj = JToken.Parse(sr.ReadToEnd());
                        int err_ref = (int)obj["error"]["error_ref"];
                        if (err_ref == 11074)
                        {
                            // collect terms
                            JToken tnc = JToken.Parse(wc.DownloadString(ApiData.AuthProxy + $"/terms"));
                            promptString = "<size=18>" + (string)tnc["plaintext"] + $"</size>\n\nYour mod.io account will be crated from the Discord account linked with Loadson (@{DiscordAPI.User.Username}).";
                            foreach (var link in tnc["links"])
                            {
                                if ((bool)link.First["required"])
                                    promptLinks.Add(((string)link.First["text"], (string)link.First["url"]));
                            }
                            // set user on main screen and disable buttons
                            try
                            {
                                GameObject.Find("/UI").transform.Find("Play").Find("Back").gameObject.GetComponent<Button>().onClick.Invoke();
                                GameObject.Find("/UI/Menu").SetActive(false);
                            }
                            catch { }

                            promptTerms = true;
                        }
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Loadson.Console.Log(ex.ToString());
                }
            }
        }

        public static User GetCurrentUser()
        {
            using (WebClient wc = new WebClient())
            {
                wc.Headers[HttpRequestHeader.Authorization] = "Bearer " + ModioBearer;
                try
                {
                    string result = wc.DownloadString(ApiData.URL + $"/me");
                    JToken obj = JToken.Parse(result);
                    return new User
                    {
                        id = (int)obj["id"],
                        name_id = (string)obj["name_id"],
                        username = (string)obj["username"],
                        avatar = CachedImage.GetImage((string)obj["avatar"]["thumb_50x50"]),
                    };
                }
                catch (Exception ex)
                {
                    Loadson.Console.Log(ex.ToString());
                }
                return null;
            }
        }
    }

    public static class API
    {
        public static List<Mod> GetMods(string query_param = "")
        {
            using (WebClient wc = new WebClient())
            {
                wc.Headers[HttpRequestHeader.Authorization] = "Bearer " + Auth.ModioBearer;
                var mod_list = (JArray)JToken.Parse(wc.DownloadString($"{ApiData.URL}/games/{ApiData.GameId}/mods" + (query_param != "" ? $"?{query_param}" : "")))["data"];
                List<Mod> mods = new List<Mod>();
                foreach (var mod in mod_list)
                {
                    mods.Add(new Mod((int)mod["id"])
                    {
                        submitted_by = new User
                        {
                            id = (int)mod["submitted_by"]["id"],
                            name_id = (string)mod["submitted_by"]["name_id"],
                            username = (string)mod["submitted_by"]["username"],
                            avatar = CachedImage.GetImage((string)mod["submitted_by"]["avatar"]["thumb_50x50"])
                        },
                        description = WebUtility.HtmlDecode((string)mod["summary"]),
                        name = WebUtility.HtmlDecode((string)mod["name"]),
                        image_thumbnail = CachedImage.GetImage((string)mod["logo"]["thumb_320x180"]),
                        image = CachedImage.GetImage((string)mod["logo"]["original"]),
                        comments = (((int)mod["community_options"]) & 1) == 1,
                        modfile_id = (int)mod["modfile"]["id"],
                        downloads_total = (int)mod["stats"]["downloads_total"],
                        ratings_total = (int)mod["stats"]["ratings_total"],
                    });
                }
                return mods;
            }
        }

        public static void SubscribeMod(int id)
        {
            using (WebClient wc = new WebClient())
            {
                wc.Headers[HttpRequestHeader.Authorization] = "Bearer " + Auth.ModioBearer;
                wc.Headers[HttpRequestHeader.ContentType] = "application/x-www-form-urlencoded";
                wc.UploadString($"{ApiData.URL}/games/{ApiData.GameId}/mods/{id}/subscribe", "");
            }
        }

        public static void DownloadMod(Mod mod, Action<int> onProgress = null, Action onDone = null)
        {
            using (WebClient wc = new WebClient())
            {
                // get download url
                wc.Headers[HttpRequestHeader.Authorization] = "Bearer " + Auth.ModioBearer;
                string download_url = (string)JToken.Parse(wc.DownloadString($"{ApiData.URL}/games/{ApiData.GameId}/mods/{mod.id}/files/{mod.modfile_id}"))["download"]["binary_url"];
                Loadson.Console.Log("Download url: " + download_url);
                // download file
                if (onProgress != null)
                    wc.DownloadProgressChanged += (a, b) => onProgress(b.ProgressPercentage);
                using (BinaryWriter bw = new BinaryWriter(File.OpenWrite(Path.Combine(Main.directory, "Levels", "Workshop", mod.id + ".kmm"))))
                {
                    var level = wc.DownloadData(download_url);
                    using (var decompressed = new MemoryStream())
                    {
                        using (var ms = new MemoryStream(level))
                        using (var zf = new ZipInputStream(ms))
                        {
                            var entry = zf.GetNextEntry();
                            zf.CopyTo(decompressed);
                        }

                        bw.Write(decompressed.Length);
                        bw.Write(decompressed.ToArray());
                    }
                    var thumb = mod.image_thumbnail.WaitForTexture().EncodeToPNG();
                    bw.Write(thumb.Length);
                    bw.Write(thumb);
                    bw.Write(mod.name);
                    bw.Write(mod.submitted_by.username);
                }
            }
        }

        public static void AddMod(string name, string summary, byte[] logo, byte[] map_data, bool comments)
        {
            using (WebClient wc = new WebClient())
            {
                wc.Headers[HttpRequestHeader.Authorization] = "Bearer " + Auth.ModioBearer;
                var multipart = new MultipartFormBuilder();
                multipart.AddField("name", name);
                multipart.AddField("summary", summary);
                multipart.AddField("community_options", comments ? "1" : "0");
                multipart.AddFile("logo", "logo.png", logo);
                int modid = (int)JToken.Parse(Encoding.ASCII.GetString(wc.UploadMultipart($"{ApiData.URL}/games/{ApiData.GameId}/mods", multipart)))["id"];
                multipart = new MultipartFormBuilder();
                // modio is a jackass and actually wants the map to be a zip file.
                using (var ms = new MemoryStream())
                using (var zf = new ZipOutputStream(ms))
                {
                    var entry = new ZipEntry("map.kme");
                    zf.PutNextEntry(entry);
                    zf.Write(map_data, 0, map_data.Length);
                    zf.Finish();

                    multipart.AddFile("filedata", "map.kme.zip", ms.ToArray(), "zip");
                    wc.UploadMultipart($"{ApiData.URL}/games/{ApiData.GameId}/mods/{modid}/files", multipart);
                }
            }
        }

        public static List<Comment> GetComments(Mod mod)
        {
            using (WebClient wc = new WebClient())
            {
                wc.Headers[HttpRequestHeader.Authorization] = "Bearer " + Auth.ModioBearer;
                var data = (JArray)JToken.Parse(wc.DownloadString($"{ApiData.URL}/games/{ApiData.GameId}/mods/{mod.id}/comments"))["data"];
                List<Comment> comments = new List<Comment>();
                foreach (var comment in data)
                {
                    comments.Add(new Comment
                    {
                        id = (int)comment["id"],
                        user = new User
                        {
                            id = (int)comment["user"]["id"],
                            name_id = (string)comment["user"]["name_id"],
                            username = (string)comment["user"]["username"],
                            avatar = CachedImage.GetImage((string)comment["user"]["avatar"]["thumb_50x50"])
                        },
                        comment = WebUtility.HtmlDecode((string)comment["content"]),
                        dateAdded = UnixTimeStampToDateTime((int)comment["date_added"]),
                    });
                }
                return comments;
            }
        }

        public static void AddComment(Mod mod, string comment)
        {
            using (WebClient wc = new WebClient())
            {
                wc.Headers[HttpRequestHeader.Authorization] = "Bearer " + Auth.ModioBearer;
                wc.Headers[HttpRequestHeader.ContentType] = "application/x-www-form-urlencoded";
                wc.UploadString($"{ApiData.URL}/games/{ApiData.GameId}/mods/{mod.id}/comments", "content=" + Uri.EscapeDataString(comment));
            }
        }

        public static void DeleteComment(Mod mod, int id)
        {
            using (WebClient wc = new WebClient())
            {
                wc.Headers[HttpRequestHeader.Authorization] = "Bearer " + Auth.ModioBearer;
                wc.Headers[HttpRequestHeader.ContentType] = "application/x-www-form-urlencoded";
                wc.UploadString($"{ApiData.URL}/games/{ApiData.GameId}/mods/{mod.id}/comments/{id}", "DELETE", string.Empty);
            }
        }

        public static DateTime UnixTimeStampToDateTime(double unixTimeStamp)
        {
            // Unix timestamp is seconds past epoch
            DateTime dateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            dateTime = dateTime.AddSeconds(unixTimeStamp).ToLocalTime();
            return dateTime;
        }

        public static void EditMod(Mod mod, string name, string description, bool enableComments)
        {
            using (WebClient wc = new WebClient())
            {
                wc.Headers[HttpRequestHeader.Authorization] = "Bearer " + Auth.ModioBearer;
                var multipart = new MultipartFormBuilder();
                multipart.AddField("name", name);
                multipart.AddField("summary", description);
                multipart.AddField("community_options", enableComments ? "1" : "0");
                wc.UploadMultipart($"{ApiData.URL}/games/{ApiData.GameId}/mods/{mod.id}", multipart);
            }
        }

        public static void DeleteMod(Mod mod)
        {
            using (WebClient wc = new WebClient())
            {
                wc.Headers[HttpRequestHeader.Authorization] = "Bearer " + Auth.ModioBearer;
                wc.Headers[HttpRequestHeader.ContentType] = "application/x-www-form-urlencoded";
                wc.UploadString($"{ApiData.URL}/games/{ApiData.GameId}/mods/{mod.id}", "DELETE", string.Empty);
            }
        }

        public static bool GetRating(Mod mod)
        {
            using (WebClient wc = new WebClient())
            {
                wc.Headers[HttpRequestHeader.Authorization] = "Bearer " + Auth.ModioBearer;
                var data = (JArray)JToken.Parse(wc.DownloadString($"{ApiData.URL}/me/ratings?mod_id={mod.id}"))["data"];
                if (data.Count == 0)
                    return false; // idk if it's needed btw
                return (int)data[0]["rating"] == 1;
            }
        }

        public static bool RateMod(Mod mod, int rating)
        {
            try
            {
                using (WebClient wc = new WebClient())
                {
                    wc.Headers[HttpRequestHeader.Authorization] = "Bearer " + Auth.ModioBearer;
                    wc.Headers[HttpRequestHeader.ContentType] = "application/x-www-form-urlencoded";
                    // API says rating field is deprecated. but .. like wdym
                    wc.UploadString($"{ApiData.URL}/games/{ApiData.GameId}/mods/{mod.id}/ratings", $"rating={rating}");
                }
            }
            catch
            {
                return false;
            }
            return true;
        }
    }

    public class Mod
    {
        public readonly int id;
        public User submitted_by;
        public CachedImage image, image_thumbnail;
        public string name, description;
        public int modfile_id;
        public int downloads_total, ratings_total;
        public bool comments;

        public bool downloaded, downloading;
        public Mod(int id)
        {
            this.id = id;
            downloaded = File.Exists(Path.Combine(Main.directory, "Levels", "Workshop", id + ".kmm"));
            downloading = false;
        }
    }

    public class User
    {
        public int id;
        public string username;
        public string name_id;
        public CachedImage avatar;
    }

    public class Comment
    {
        public int id;
        public User user;
        public string comment;
        public DateTime dateAdded;
    }
}
