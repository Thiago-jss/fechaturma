using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FechaTurma.Desktop.Api
{
    internal sealed class ClosureHttpException : Exception
    {
        public ClosureHttpException(HttpStatusCode status) : base("HTTP " + (int)status)
        {
            Status = status;
        }

        public HttpStatusCode Status { get; }
    }

    internal sealed class ClosureClient : IDisposable
    {
        public const string DefaultOrigin = "http://localhost:5184";
        private const string Collection = "/api/v1/closures";
        private readonly HttpClient http;

        public ClosureClient(string origin)
        {
            if (origin == null || !Regex.IsMatch(origin, @"\Ahttps?://(?:localhost|127\.0\.0\.1|\[::1\])(?::[0-9]{1,5})?/?\z", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
                || !Uri.TryCreate(origin, UriKind.Absolute, out var address) || address.Port == 0)
                throw new ArgumentException("A URL deve apontar para o servidor local.", nameof(origin));

            http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false, UseCookies = false })
            {
                BaseAddress = address,
                Timeout = TimeSpan.FromSeconds(8),
                MaxResponseContentBufferSize = 16 * 1024 * 1024
            };
        }

        public async Task<ClosureSummary[]> ListAsync(string key)
        {
            var summaries = await SendAsync<ClosureSummary[]>(HttpMethod.Get, Collection, key).ConfigureAwait(false);
            foreach (var item in summaries) ValidateSummary(item);
            return summaries;
        }

        public Task<ClosureDetail> DetailAsync(Guid id, string key) =>
            FetchDetailAsync(HttpMethod.Get, Collection + "/" + id.ToString("D"), id, key);

        public Task<ClosureDetail> CloseAsync(Guid id, int version, string key) =>
            FetchDetailAsync(HttpMethod.Post, Collection + "/" + id.ToString("D") + "/close", id, key, version);

        private async Task<ClosureDetail> FetchDetailAsync(HttpMethod method, string path, Guid id, string key, int? version = null)
        {
            var detail = await SendAsync<ClosureDetail>(method, path, key, version).ConfigureAwait(false);
            ValidateSummary(detail);
            if (Guid.Parse(detail.Id) != id || detail.Rows == null || detail.Audit == null ||
                Array.Exists(detail.Rows, row => row == null) || Array.Exists(detail.Audit, entry => entry == null) ||
                (version.HasValue && detail.Status != "closed"))
                throw new SerializationException();
            return detail;
        }

        private static void ValidateSummary(ClosureSummary item)
        {
            if (item == null || !Guid.TryParseExact(item.Id, "D", out _) || (item.Status != "pending" && item.Status != "closed"))
                throw new SerializationException();
        }

        private async Task<T> SendAsync<T>(HttpMethod method, string path, string key, int? version = null) where T : class
        {
            using (var request = new HttpRequestMessage(method, path))
            {
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                if (!string.IsNullOrWhiteSpace(key))
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
                if (version.HasValue)
                {
                    using (var body = new MemoryStream())
                    {
                        new DataContractJsonSerializer(typeof(CloseRequest)).WriteObject(body, new CloseRequest { Version = version.Value });
                        request.Content = new StringContent(Encoding.UTF8.GetString(body.ToArray()), Encoding.UTF8, "application/json");
                    }
                }
                using (var response = await http.SendAsync(request).ConfigureAwait(false))
                {
                    if (response.StatusCode != HttpStatusCode.OK)
                        throw new ClosureHttpException(response.StatusCode);
                    using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                        return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream) ?? throw new SerializationException();
                }
            }
        }

        public void Dispose() => http.Dispose();
    }
}
