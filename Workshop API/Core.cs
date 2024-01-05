using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace KarlsonMapEditor.Workshop_API
{
    public static class Core
    {
        public const string API_ENDPOINT = "https://karlsonlevelloader.000webhostapp.com"; // no trailing [slash]

        public static (string, int[]) Login(long id, string accessToken)
        {
            ServicePointManager.ServerCertificateValidationCallback = new RemoteCertificateValidationCallback( delegate { return true; } );
            using (WebClient wc = new WebClient())
            {
                wc.Headers[HttpRequestHeader.ContentType] = "application/x-www-form-urlencoded";
                try
                {
                    string result = wc.UploadString(API_ENDPOINT + $"/accounts/login.php", $"userid={id}&bearer={accessToken}");
                    JToken obj = JToken.Parse(result);
                    if (CheckError(obj)) return ("", new int[0]);
                    List<int> cast = new List<int>();
                    for (int i = 0; i < ((JArray)obj["likes"]).Count; i++)
                        cast.Add((int)((JArray)obj["likes"])[i]);
                    return ((string)obj["token"], cast.ToArray());
                }
                catch(Exception ex)
                {
                    Loadson.Console.Log(ex.ToString());
                }
                return ("", Array.Empty<int>());
            }
        }

        public static int[] GetMostLiked()
        {
            using (WebClient wc = new WebClient())
            {
                string result = wc.DownloadString(API_ENDPOINT + "/workshop/mostliked.php");
                JToken obj = JToken.Parse(result);
                if (CheckError(obj)) return Array.Empty<int>();
                JArray ar = (JArray)obj;
                List<int> top = new List<int>();
                for (int i = 0; i < ar.Count; i++)
                    top.Add((int)ar[i]);
                return top.ToArray();
            }
        }
        public static int[] GetMostDl()
        {
            using (WebClient wc = new WebClient())
            {
                string result = wc.DownloadString(API_ENDPOINT + "/workshop/mostdl.php");
                JToken obj = JToken.Parse(result);
                if (CheckError(obj)) return Array.Empty<int>();
                JArray ar = (JArray)obj;
                List<int> top = new List<int>();
                for (int i = 0; i < ar.Count; i++)
                    top.Add((int)ar[i]);
                return top.ToArray();
            }
        }
        public static int[] GetMostRecent()
        {
            using (WebClient wc = new WebClient())
            {
                string result = wc.DownloadString(API_ENDPOINT + "/workshop/mostrecent.php");
                JToken obj = JToken.Parse(result);
                if (CheckError(obj)) return Array.Empty<int>();
                JArray ar = (JArray)obj;
                List<int> top = new List<int>();
                for (int i = 0; i < ar.Count; i++)
                    top.Add((int)ar[i]);
                return top.ToArray();
            }
        }

        public static SmallLevelData GetLevelInfo(int id)
        {
            using (WebClient wc = new WebClient())
            {
                string result = wc.DownloadString(API_ENDPOINT + "/workshop/getlevelinfo.php?id=" + id);
                JToken obj = JToken.Parse(result);
                if (CheckError(obj)) return null;
                return new SmallLevelData(id, (long)obj["AuthorID"], (string)obj["Name"], Convert.FromBase64String((string)obj["Thumbnail"]), (int)obj["Downloads"], (int)obj["Likes"]);
            }
        }

        public static string GetUserName(long id)
        {
            using (WebClient wc = new WebClient())
            {
                string result = wc.DownloadString(API_ENDPOINT + "/accounts/username.php?id=" + id);
                JToken obj = JToken.Parse(result);
                if (CheckError(obj)) return "<Error>";
                return (string)obj["name"];
            }
        }

        public static void LikeLevel(int id)
        {
            using (WebClient wc = new WebClient())
            {
                wc.Headers[HttpRequestHeader.ContentType] = "application/x-www-form-urlencoded";
                string response = wc.UploadString(API_ENDPOINT + "/level/likelevel.php", "id=" + id + "&token=" + Main.workshopToken);
                JToken obj = JToken.Parse(response);
                CheckError(obj);
            }
        }
        public static void UnlikeLevel(int id)
        {
            using (WebClient wc = new WebClient())
            {
                wc.Headers[HttpRequestHeader.ContentType] = "application/x-www-form-urlencoded";
                string response = wc.UploadString(API_ENDPOINT + "/level/unlikelevel.php", "id=" + id + "&token=" + Main.workshopToken);
                JToken obj = JToken.Parse(response);
                CheckError(obj);
            }
        }

        public static void UploadLevel(KWM_Convert.KWM wml)
        {
            using (WebClient wc = new WebClient())
            {
                wc.Headers[HttpRequestHeader.ContentType] = "application/x-www-form-urlencoded";
                string response = wc.UploadString(API_ENDPOINT + "/level/uploadlevel.php", "POST", "token=" + Main.workshopToken + "&name=" + wml.Name + "&thumbnail=" + Convert.ToBase64String(wml.Thumbnail).Replace("+", "-").Replace("/", "_") + "&level=" + Convert.ToBase64String(KWM_Convert.Encode(wml)).Replace("+", "-").Replace("/", "_"));
                JToken obj = JToken.Parse(response);
                CheckError(obj);
            }
        }

        public class SmallLevelData
        {
            public SmallLevelData(int id, long author, string name, byte[] picture, int dl, int likes) {
                Id = id;
                Author = author;
                Name = name;
                Picture = picture;
                Dl = dl;
                Likes = likes;
            }

            public int Id;
            public long Author;
            public string Name;
            public byte[] Picture;
            public int Dl;
            public int Likes;
        }

        private static bool CheckError(JToken obj)
        {
            if(obj.Type == JTokenType.Object && ((JObject)obj).ContainsKey("error"))
            {
                Loadson.Console.Log("[WAPI ERROR] " + obj["error"].ToString());
                return true;
            }
            return false;
        }
    }
}
