using LoadsonExtensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace KarlsonMapEditor.ModIO
{
    public class CachedImage
    {
        private CachedImage(string url)
        {
            Url = url;
            imageRequested = false;
        }
        readonly string Url;
        static readonly Dictionary<string, CachedImage> Cache = new Dictionary<string, CachedImage>();
        public static CachedImage GetImage(string url)
        {
            if (!Cache.ContainsKey(url))
                Cache[url] = new CachedImage(url);
            return Cache[url];
        }
        Texture2D image = null;
        bool imageRequested = false;
        void RequestImage()
        {
            if(imageRequested)
                return;
            imageRequested = true;
            new Thread(() =>
            {
                using (WebClient wc = new WebClient())
                {
                    var data = wc.DownloadData(Url);
                    Main.runOnMain.Add(() =>
                    {
                        image = new Texture2D(0, 0);
                        image.LoadImage(data);
                    });
                }
            }).Start();
        }
        static Texture2D placeholder = null;
        static Dictionary<float, Texture2D> placeholder_scaled = new Dictionary<float, Texture2D>();
        static Texture2D GetPlaceholder(float aspectRatio)
        {
            if(placeholder == null)
            {
                placeholder = new Texture2D(0, 0);
                placeholder.LoadFromResources("KarlsonMapEditor.Assets.Placeholder.png");
            }
            if(!placeholder_scaled.ContainsKey(aspectRatio))
            {
                var width = placeholder.width;
                var height = Mathf.RoundToInt(placeholder.width / aspectRatio);
                if(height > placeholder.height)
                {
                    width = Mathf.RoundToInt(placeholder.height * aspectRatio);
                    height = placeholder.height;
                }
                var tx = new Texture2D(width, height);
                tx.SetPixels(placeholder.GetPixels((placeholder.width - width) / 2, (placeholder.height - height) / 2, width, height));
                tx.Apply();
                placeholder_scaled[aspectRatio] = tx;
            }
            return placeholder_scaled[aspectRatio];
        }
        public Texture2D GetTexture(float aspectRatio = 16f / 9f)
        {
            if(image == null)
            {
                RequestImage();
                return GetPlaceholder(aspectRatio);
            }
            return image;
        }
        public Texture2D WaitForTexture()
        {
            if (image != null)
                return image;
            RequestImage();
            while (image == null) Thread.Sleep(100);
            return image;
        }
    }
}
