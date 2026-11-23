using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace TownOfHost.Modules;

internal static class BugReportSubmission
{
    internal const int MaxTitle = 80;
    internal const int MaxDescription = 1700;
    private static readonly HttpClient Client = new(new HttpClientHandler { AllowAutoRedirect = false })
    {
        Timeout = TimeSpan.FromSeconds(20),
        MaxResponseContentBufferSize = 4096
    };

    internal readonly record struct Result(string ThreadId, HttpStatusCode Status)
    {
        internal bool Success => ThreadId != null;
    }

    internal static bool TryConfigure(string url, string accessKey, out Uri endpoint)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out endpoint)
            && endpoint.Scheme == Uri.UriSchemeHttps
            && endpoint.AbsolutePath == "/v1/bug-reports"
            && endpoint.UserInfo.Length == 0 && endpoint.Query.Length == 0 && endpoint.Fragment.Length == 0
            && accessKey is { Length: >= 32 and <= 128 }
            && System.Linq.Enumerable.All(accessKey, c =>
                (c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '_'));
    }

    internal static async Task<Result> SendAsync(Uri endpoint, string accessKey, string title, string description,
        string version, string diagnostics, HttpClient client = null)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessKey);
            request.Content = new StringContent(JsonSerializer.Serialize(new { title, description, version, diagnostics }),
                Encoding.UTF8, "application/json");
            using var response = await (client ?? Client).SendAsync(request).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return new(null, response.StatusCode);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
            if (body.RootElement.TryGetProperty("threadId", out var value) && value.ValueKind == JsonValueKind.String
                && ulong.TryParse(value.GetString(), out var id) && id != 0)
                return new(value.GetString(), response.StatusCode);
        }
        catch (Exception e) when (e is HttpRequestException or OperationCanceledException or JsonException or InvalidOperationException)
        {
            // The server may already have created the post. Never retry automatically.
        }

        return default;
    }
}
