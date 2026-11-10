using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace FCG.Users.IntegrationTests.Infrastructure;

public static class ApiClientExtensions
{
    public const string AdminEmail = "fcg@admin.com";
    public const string AdminPassword = "Fcg@Admin2026!";
    public const string ValidPassword = "Abcdef1!";

    public static async Task<JsonElement> ReadValueAsync(this HttpResponseMessage response, CancellationToken cancellationToken)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return document.RootElement.GetProperty("value").Clone();
    }

    public static Task<HttpResponseMessage> RegisterAsync(this HttpClient client, string email, CancellationToken cancellationToken, string password = ValidPassword)
    {
        return client.PostAsJsonAsync("/api/v1/users/register", new { name = "Player", email, password }, cancellationToken);
    }

    public static async Task<string> LoginAsync(this HttpClient client, string email, string password, CancellationToken cancellationToken)
    {
        var response = await client.PostAsJsonAsync("/api/v1/users/logins", new { email, password }, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.ReadValueAsync(cancellationToken)).GetProperty("token").GetString()!;
    }

    public static HttpClient WithToken(this HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public static string NewEmail() => $"player-{Guid.NewGuid():N}@test.com";
}
