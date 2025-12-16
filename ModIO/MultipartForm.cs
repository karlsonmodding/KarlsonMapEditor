/*
 * These classes are taken from
 * https://gist.github.com/ericvoid/568d733c90857f010fb860cb5e6aba43
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace KarlsonMapEditor.ModIO
{
    public class MultipartFormBuilder
    {
        const string MultipartContentType = "multipart/form-data; boundary=";
        const string FileHeaderTemplate = "Content-Disposition: form-data; name=\"{0}\"; filename=\"{1}\"\r\nContent-Type: application/{2}\r\n\r\n";
        const string FormDataTemplate = "\r\n--{0}\r\nContent-Disposition: form-data; name=\"{1}\";\r\n\r\n{2}";

        public string ContentType { get; private set; }

        readonly string Boundary;

        readonly Dictionary<(string, string), (byte[], string)> FilesToSend = new Dictionary<(string, string), (byte[], string)>();
        readonly Dictionary<string, string> FieldsToSend = new Dictionary<string, string>();

        public MultipartFormBuilder()
        {
            Boundary = "----------------------------" + DateTime.Now.Ticks.ToString("x");

            ContentType = MultipartContentType + Boundary;
        }

        public void AddField(string key, string value)
        {
            FieldsToSend.Add(key, value);
        }

        public void AddFile(string name, string filename, byte[] file, string file_type = "octet-stream")
        {
            FilesToSend.Add((name, filename), (file, file_type));
        }

        public MemoryStream GetStream()
        {
            var memStream = new MemoryStream();

            WriteFields(memStream);
            WriteStreams(memStream);
            WriteTrailer(memStream);

            memStream.Seek(0, SeekOrigin.Begin);

            return memStream;
        }

        void WriteFields(Stream stream)
        {
            if (FieldsToSend.Count == 0)
                return;

            foreach (var fieldEntry in FieldsToSend)
            {
                string content = string.Format(FormDataTemplate, Boundary, fieldEntry.Key, fieldEntry.Value);

                using (var fieldData = new MemoryStream(Encoding.UTF8.GetBytes(content)))
                {
                    fieldData.CopyTo(stream);
                }
            }
        }

        void WriteStreams(Stream stream)
        {
            if (FilesToSend.Count == 0)
                return;

            foreach (var fileEntry in FilesToSend)
            {
                WriteBoundary(stream);

                string header = string.Format(FileHeaderTemplate, fileEntry.Key.Item1, fileEntry.Key.Item2, fileEntry.Value.Item2);
                byte[] headerbytes = Encoding.UTF8.GetBytes(header);
                stream.Write(headerbytes, 0, headerbytes.Length);
                stream.Write(fileEntry.Value.Item1, 0, fileEntry.Value.Item1.Length);
            }
        }

        void WriteBoundary(Stream stream)
        {
            byte[] boundarybytes = Encoding.UTF8.GetBytes("\r\n--" + Boundary + "\r\n");
            stream.Write(boundarybytes, 0, boundarybytes.Length);
        }

        void WriteTrailer(Stream stream)
        {
            byte[] trailer = Encoding.UTF8.GetBytes("\r\n--" + Boundary + "--\r\n");
            stream.Write(trailer, 0, trailer.Length);
        }
    }

    public static class WebClientExtensionMethods
    {
        public static byte[] UploadMultipart(this WebClient client, string address, MultipartFormBuilder multipart)
        {
            client.Headers.Add(HttpRequestHeader.ContentType, multipart.ContentType);

            using (var stream = multipart.GetStream())
            {
                return client.UploadData(address, stream.ToArray());
            }
        }

        public static byte[] UploadMultipart(this WebClient client, Uri address, MultipartFormBuilder multipart)
        {
            client.Headers.Add(HttpRequestHeader.ContentType, multipart.ContentType);

            using (var stream = multipart.GetStream())
            {
                return client.UploadData(address, stream.ToArray());
            }
        }

        public static byte[] UploadMultipart(this WebClient client, string address, string method, MultipartFormBuilder multipart)
        {
            client.Headers.Add(HttpRequestHeader.ContentType, multipart.ContentType);

            using (var stream = multipart.GetStream())
            {
                return client.UploadData(address, method, stream.ToArray());
            }
        }

        public static byte[] UploadMultipart(this WebClient client, Uri address, string method, MultipartFormBuilder multipart)
        {
            client.Headers.Add(HttpRequestHeader.ContentType, multipart.ContentType);

            using (var stream = multipart.GetStream())
            {
                return client.UploadData(address, method, stream.ToArray());
            }
        }

        public static void UploadMultipartAsync(this WebClient client, Uri address, MultipartFormBuilder multipart)
        {
            client.Headers.Add(HttpRequestHeader.ContentType, multipart.ContentType);

            using (var stream = multipart.GetStream())
            {
                client.UploadDataAsync(address, stream.ToArray());
            }
        }

        public static void UploadMultipartAsync(this WebClient client, Uri address, string method, MultipartFormBuilder multipart)
        {
            client.Headers.Add(HttpRequestHeader.ContentType, multipart.ContentType);

            using (var stream = multipart.GetStream())
            {
                client.UploadDataAsync(address, method, stream.ToArray());
            }
        }

        public static void UploadMultipartAsync(this WebClient client, Uri address, string method, MultipartFormBuilder multipart, object userToken)
        {
            client.Headers.Add(HttpRequestHeader.ContentType, multipart.ContentType);

            using (var stream = multipart.GetStream())
            {
                client.UploadDataAsync(address, method, stream.ToArray(), userToken);
            }
        }

        public static async Task<byte[]> UploadMultipartTaskAsync(this WebClient client, string address, MultipartFormBuilder multipart)
        {
            client.Headers.Add(HttpRequestHeader.ContentType, multipart.ContentType);

            using (var stream = multipart.GetStream())
            {
                return await client.UploadDataTaskAsync(address, stream.ToArray());
            }
        }

        public static async Task<byte[]> UploadMultipartTaskAsync(this WebClient client, Uri address, MultipartFormBuilder multipart)
        {
            client.Headers.Add(HttpRequestHeader.ContentType, multipart.ContentType);

            using (var stream = multipart.GetStream())
            {
                return await client.UploadDataTaskAsync(address, stream.ToArray());
            }
        }

        public static async Task<byte[]> UploadMultipartTaskAsync(this WebClient client, string address, string method, MultipartFormBuilder multipart)
        {
            client.Headers.Add(HttpRequestHeader.ContentType, multipart.ContentType);

            using (var stream = multipart.GetStream())
            {
                return await client.UploadDataTaskAsync(address, method, stream.ToArray());
            }
        }

        public static async Task<byte[]> UploadMultipartTaskAsync(this WebClient client, Uri address, string method, MultipartFormBuilder multipart)
        {
            client.Headers.Add(HttpRequestHeader.ContentType, multipart.ContentType);

            using (var stream = multipart.GetStream())
            {
                return await client.UploadDataTaskAsync(address, method, stream.ToArray());
            }
        }
    }
}
