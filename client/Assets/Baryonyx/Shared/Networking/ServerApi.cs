using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
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
            var watch = System.Diagnostics.Stopwatch.StartNew();
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
            // 開発ビルドとEditorでは、要求ごとの時間とサーバーの内訳をログに出し、遅い画面を切り分ける。
            // 完了はフレームごとに確かめるため、最大1フレーム分の待ちを含む。
            if (Debug.isDebugBuild)
                Debug.Log(
                    TimingLine(
                        method,
                        path,
                        request.responseCode,
                        watch.Elapsed.TotalMilliseconds,
                        request.GetResponseHeader("Server-Timing")
                    )
                );
            if (request.result != UnityWebRequest.Result.Success)
                throw new ServerApiException(request.responseCode);
            return request.responseCode == 204
                ? new T()
                : JsonUtility.FromJson<T>(request.downloadHandler.text);
        }

        // サーバーの Server-Timing のうち、処理の合計（total）と、DBとの往復の時間・回数（d1）。
        private static readonly Regex TotalMetric = new(@"(?:^|,)\s*total;dur=([\d.]+)");
        private static readonly Regex D1Metric = new(@"(?:^|,)\s*d1;dur=([\d.]+);desc=""(\d+) calls""");

        /// <summary>
        /// The log line of one request: the time until it finished, and the server's own time and
        /// its round trips to the database when it returned them (server/src/shared/server-timing.ts).
        /// </summary>
        internal static string TimingLine(
            string method,
            string path,
            long status,
            double milliseconds,
            string serverTiming
        )
        {
            var line = $"[計測] 通信 {method} {path} {status} {Ms(milliseconds)}";
            if (string.IsNullOrEmpty(serverTiming))
                return line;
            var total = TotalMetric.Match(serverTiming);
            var d1 = D1Metric.Match(serverTiming);
            if (!total.Success || !d1.Success)
                return $"{line}（サーバー {serverTiming}）";
            return $"{line}（サーバー {Ms(Parse(total))}、DB {d1.Groups[2].Value}回 {Ms(Parse(d1))}）";
        }

        private static double Parse(Match metric) =>
            double.Parse(metric.Groups[1].Value, CultureInfo.InvariantCulture);

        private static string Ms(double milliseconds) =>
            Math.Round(milliseconds).ToString("0", CultureInfo.InvariantCulture) + "ms";
    }
}
