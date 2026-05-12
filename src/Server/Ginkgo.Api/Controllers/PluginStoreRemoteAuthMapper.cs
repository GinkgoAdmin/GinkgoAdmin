using System.Text.Json;

namespace Ginkgo.Api.Controllers;

/// <summary>
/// 远程插件商城登录返回映射器。
/// </summary>
public static class PluginStoreRemoteAuthMapper
{
    public const int CaptchaRequiredCode = 449;

    public static PluginStoreRemoteLoginResult NormalizeLoginResponse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidOperationException("远程商城登录返回为空");

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var body = PickObject(root, "data", "Data", "result", "Result") ?? root;

        var token = PickString(body, "token", "accessToken", "authToken", "storeToken", "jwt")
            ?? PickString(root, "token", "accessToken", "authToken", "storeToken", "jwt");
        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("远程商城登录成功但未返回 Token");

        var userName = PickString(body, "userName", "username", "account", "email", "phone")
            ?? PickString(root, "userName", "username", "account", "email", "phone")
            ?? "store-user";
        var displayName = PickString(body, "displayName", "nickname", "name", "userName", "username")
            ?? PickString(root, "displayName", "nickname", "name", "userName", "username")
            ?? userName;

        return new PluginStoreRemoteLoginResult(
            Token: token,
            RefreshToken: PickString(body, "refreshToken") ?? PickString(root, "refreshToken"),
            ExpiresAt: PickDateTime(body, "expiresAt", "ExpiresAt") ?? PickDateTime(root, "expiresAt", "ExpiresAt"),
            UserName: userName,
            DisplayName: displayName,
            Avatar: PickString(body, "avatar") ?? PickString(root, "avatar"),
            Email: PickString(body, "email") ?? PickString(root, "email"),
            Phone: PickString(body, "phone", "mobile") ?? PickString(root, "phone", "mobile"),
            Roles: PickStringArray(body, "roles", "Roles")
        );
    }

    public static bool TryReadBusinessError(string json, out PluginStoreRemoteBusinessError? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(json)) return false;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return false;

            if (!TryPickInt(root, out var code, "code", "Code") || code == 0)
                return false;

            var message = PickString(root, "message", "Message", "title", "Title", "detail", "Detail")
                ?? "商城登录失败";
            JsonElement? data = null;
            if (root.TryGetProperty("data", out var dataEl) || root.TryGetProperty("Data", out dataEl))
                data = dataEl.Clone();

            error = new PluginStoreRemoteBusinessError(code, message, data);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static Dictionary<string, object?> CreateRemoteCaptchaChallenge(
        PluginStoreRemoteBusinessError error,
        string captchaApiBase)
    {
        var challenge = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (error.Data.HasValue && error.Data.Value.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in error.Data.Value.EnumerateObject())
            {
                challenge[property.Name] = ConvertJsonValue(property.Value);
            }
        }

        if (!challenge.ContainsKey("guardType"))
            challenge["guardType"] = "captcha";
        challenge["captchaApiBase"] = captchaApiBase;
        challenge["captchaRetryScope"] = "plugin-store-remote";
        return challenge;
    }

    private static JsonElement? PickObject(JsonElement element, params string[] names)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Object)
                return value;
        }
        return null;
    }

    private static bool TryPickInt(JsonElement element, out int value, params string[] names)
    {
        value = 0;
        if (element.ValueKind != JsonValueKind.Object) return false;
        foreach (var name in names)
        {
            if (!element.TryGetProperty(name, out var codeEl)) continue;
            if (codeEl.ValueKind == JsonValueKind.Number && codeEl.TryGetInt32(out value))
                return true;
            if (codeEl.ValueKind == JsonValueKind.String && int.TryParse(codeEl.GetString(), out value))
                return true;
        }
        return false;
    }

    private static string? PickString(JsonElement element, params string[] names)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;
        foreach (var name in names)
        {
            if (!element.TryGetProperty(name, out var value)) continue;
            if (value.ValueKind == JsonValueKind.String)
            {
                var text = value.GetString();
                if (!string.IsNullOrWhiteSpace(text)) return text;
            }
        }
        return null;
    }

    private static DateTime? PickDateTime(JsonElement element, params string[] names)
    {
        var text = PickString(element, names);
        return DateTime.TryParse(text, out var value) ? value : null;
    }

    private static string[] PickStringArray(JsonElement element, params string[] names)
    {
        if (element.ValueKind != JsonValueKind.Object) return Array.Empty<string>();
        foreach (var name in names)
        {
            if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array)
                continue;

            return value.EnumerateArray()
                .Where(x => x.ValueKind == JsonValueKind.String)
                .Select(x => x.GetString())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        return Array.Empty<string>();
    }

    private static object? ConvertJsonValue(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.TryGetInt64(out var longValue)
                ? longValue
                : value.TryGetDouble(out var doubleValue) ? doubleValue : value.GetRawText(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => value.Clone()
        };
    }
}

public sealed record PluginStoreRemoteLoginResult(
    string Token,
    string? RefreshToken,
    DateTime? ExpiresAt,
    string UserName,
    string DisplayName,
    string? Avatar,
    string? Email,
    string? Phone,
    string[] Roles);

public sealed record PluginStoreRemoteBusinessError(int Code, string Message, JsonElement? Data)
{
    public bool IsCaptchaChallenge => Code == PluginStoreRemoteAuthMapper.CaptchaRequiredCode;
}
