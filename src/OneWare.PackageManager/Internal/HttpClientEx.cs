﻿using System.Net;

namespace OneWare.PackageManager.Internal
{
    internal static class HttpClientEx
    {
        private static HttpClient _singleton;

        public static HttpClient GetSingleton()
        {
            // Return cached singleton if already initialized
            if (_singleton != null)
                return _singleton;

            // Configure handler
            var handler = new HttpClientHandler();
            if (handler.SupportsAutomaticDecompression)
                handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            handler.UseCookies = false;

            // Configure client
            var client = new HttpClient(handler, true);
            client.DefaultRequestHeaders.Add("User-Agent", "VHDPlus Updater");

            return _singleton = client;
        }

        public static async Task<FiniteStream> ReadAsFiniteStreamAsync(this HttpContent content)
        {
            // Get content length
            var length = content.Headers.ContentLength ?? -1;
            if (length < 0)
                throw new InvalidOperationException("Response does not have 'Content-Length' header set.");

            // Read stream
            var stream = await content.ReadAsStreamAsync();

            return new FiniteStream(stream, length);
        }

        public static async Task<FiniteStream> GetFiniteStreamAsync(this HttpClient client, string requestUri)
        {
            var response = await client.GetAsync(requestUri, HttpCompletionOption.ResponseHeadersRead);

            return await response.Content.ReadAsFiniteStreamAsync();
        }
    }
}