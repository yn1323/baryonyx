using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Baryonyx.Networking
{
    public sealed class ServerApiException : Exception
    {
        public long StatusCode { get; }

        public ServerApiException(long statusCode)
            : base("Server API request failed")
        {
            StatusCode = statusCode;
        }
    }

    // ゲームサーバーへのHTTP要求を送る。パスと入出力の型は各機能のクライアントが持つ。
    public sealed class ServerApi
    {
        private readonly string baseUrl;

        public ServerApi(string baseUrl)
        {
            var uri = new Uri(baseUrl);
            if (uri.Scheme != "https" && !(uri.Scheme == "http" && IsDevelopmentHost(uri)))
                throw new ArgumentException("HTTPS is required except for local development.");
            if (
                !string.IsNullOrEmpty(uri.UserInfo)
                || !string.IsNullOrEmpty(uri.Query)
                || !string.IsNullOrEmpty(uri.Fragment)
            )
                throw new ArgumentException(
                    "Use a server base URL without credentials, query or fragment."
                );
            this.baseUrl = baseUrl.TrimEnd('/');
        }

        // 10.0.2.2はAndroidエミュレーターから開発PCのループバックへ届く固定アドレス。
        private static bool IsDevelopmentHost(Uri uri) => uri.IsLoopback || uri.Host == "10.0.2.2";

        public async Task<T> SendAsync<T>(
            string path,
            string method,
            object body,
            string bearerToken,
            CancellationToken token
        )
            where T : new()
        {
            token.ThrowIfCancellationRequested();
            using var request = new UnityWebRequest(baseUrl + path, method)
            {
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = 30,
                redirectLimit = 0,
            };
            if (body != null)
            {
                request.uploadHandler = new UploadHandlerRaw(
                    Encoding.UTF8.GetBytes(JsonUtility.ToJson(body))
                );
                request.SetRequestHeader("Content-Type", "application/json");
            }
            if (!string.IsNullOrEmpty(bearerToken))
                request.SetRequestHeader("Authorization", "Bearer " + bearerToken);
            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                if (token.IsCancellationRequested)
                {
                    request.Abort();
                    token.ThrowIfCancellationRequested();
                }
                await Task.Yield();
            }
            token.ThrowIfCancellationRequested();
            if (request.result != UnityWebRequest.Result.Success)
                throw new ServerApiException(request.responseCode);
            return request.responseCode == 204
                ? new T()
                : JsonUtility.FromJson<T>(request.downloadHandler.text);
        }
    }
}
