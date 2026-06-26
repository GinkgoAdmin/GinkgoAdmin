using Ginkgo.Api.Modules;
using Ginkgo.Plugin.Abstractions.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SqlSugar;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Ginkgo.Api.Controllers;

/// <summary>
/// 插件商店 API（框架内置功能）。
/// 根据 appsettings.json 中 PluginStore:ServerUrl 配置决定是否启用。
/// </summary>
[ApiController]
[Route("api/system/plugin-store")]
[Authorize(Policy = "Permission")]
public sealed class PluginStoreController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ModuleUploadService _uploadService;
    private readonly ILogger<PluginStoreController> _logger;
    private readonly ISqlSugarClient _db;
    private readonly IHostEnvironment _env;
    private readonly LicenseFileVerifier _licenseVerifier;

    public PluginStoreController(
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ModuleUploadService uploadService,
        ILogger<PluginStoreController> logger,
        ISqlSugarClient db,
        IHostEnvironment env,
        LicenseFileVerifier licenseVerifier)
    {
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _uploadService = uploadService;
        _logger = logger;
        _db = db;
        _env = env;
        _licenseVerifier = licenseVerifier;
    }

    /// <summary>
    /// 判断是否为开发环境
    /// </summary>
    private bool IsDevelopmentEnvironment() => string.Equals(_env.EnvironmentName, "Development", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 获取插件商店配置（是否启用、服务地址、远端登录页路径）。
    /// loginPath 返回远端站点上的“可嵌入式登录页”路径，前端在打开弹窗时会拼接上 serverUrl 与 origin/state 参数，
    /// 让远端用户以任意方式（密码/手机验证码/邮箱验证码/三方等）在远端原生页面完成登录，再通过 postMessage 回传 token。
    /// 本地不再代理任何远端登录/验证码业务，实现与远端登录方式的彻底解耦。
    /// </summary>
    [HttpGet("config")]
    [AllowAnonymous]
    public IActionResult GetConfig()
    {
        var serverUrl = _configuration["PluginStore:ServerUrl"] ?? "";
        var enabled = !string.IsNullOrWhiteSpace(serverUrl);
        var isDev = IsDevelopmentEnvironment();
        // 远端登录页默认路径，允许通过配置覆盖以适配不同部署。
        var loginPath = _configuration["PluginStore:LoginPath"];
        if (string.IsNullOrWhiteSpace(loginPath))
            loginPath = "/zh/web/store/login";
        return Ok(new { serverUrl, enabled, canInstall = isDev, loginPath });
    }

    /// <summary>
    /// 获取已上架商品列表（前台展示，无需登录，直接查本地数据库）。
    /// 支持按分类和关键词过滤。
    /// </summary>
    [HttpGet("local-items")]
    [AllowAnonymous]
    public async Task<IActionResult> GetLocalPublishedItems([FromQuery] string? category, [FromQuery] string? keyword)
    {
        try
        {
            // 使用参数化查询防止 SQL 注入
            var query = _db.Queryable<dynamic>()
                .AS("ginkgo_PS_StoreItem")
                .Where($"Status = @status AND {_db.NotDeletedSql()} AND {_db.QuoteCol("IsVisible")} = {_db.BoolSql(true)}", new { status = "published" });

            // 按分类筛选（参数化）
            if (!string.IsNullOrWhiteSpace(category))
                query = query.Where("Category = @cat", new { cat = category.Trim() });

            // 按关键词搜索（参数化，模糊匹配名称和描述）
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var kw = $"%{keyword.Trim()}%";
                query = query.Where("(Name LIKE @kw OR Description LIKE @kw)", new { kw });
            }

            var items = await query
                .OrderBy("SortOrder ASC, CreatedAt DESC")
                .ToListAsync();

            // 获取每个商品的版本列表
            var result = new List<object>();
            foreach (var item in items)
            {
                var dict = (IDictionary<string, object>)item;
                var itemId = dict["Id"];
                // 使用参数化查询获取版本列表
                var editions = await _db.Queryable<dynamic>()
                    .AS("ginkgo_PS_ItemEdition")
                    .Where("ItemId = @itemId", new { itemId })
                    .OrderBy("SortOrder ASC")
                    .ToListAsync();

                result.Add(new
                {
                    id = itemId?.ToString(),
                    name = dict.TryGetValue("Name", out var n) ? n : "",
                    category = dict.TryGetValue("Category", out var c) ? c : "",
                    description = dict.TryGetValue("Description", out var d) ? d : "",
                    status = dict.TryGetValue("Status", out var s) ? s : "",
                    imageUrl = dict.TryGetValue("ImageUrl", out var img) ? img : null,
                    sortOrder = dict.TryGetValue("SortOrder", out var so) ? so : 0,
                    createdAt = dict.TryGetValue("CreatedAt", out var ca) ? ca : null,
                    updatedAt = dict.TryGetValue("UpdatedAt", out var ua) ? ua : null,
                    developer = dict.TryGetValue("Developer", out var dev) ? dev : null,
                    officialWebsite = dict.TryGetValue("OfficialWebsite", out var ow) ? ow : null,
                    serviceEmail = dict.TryGetValue("ServiceEmail", out var se) ? se : null,
                    serviceQQ = dict.TryGetValue("ServiceQQ", out var sq) ? sq : null,
                    imagesJson = dict.TryGetValue("ImagesJson", out var ij) ? ij : null,
                    bannerUrl = dict.TryGetValue("BannerUrl", out var bu) ? bu : null,
                    bannerTextColor = dict.TryGetValue("BannerTextColor", out var btc) ? btc : null,
                    isVisible = dict.TryGetValue("IsVisible", out var isVis) ? Convert.ToBoolean(isVis) : true,
                    editions = editions.Select(e =>
                    {
                        var ed = (IDictionary<string, object>)e;
                        return new
                        {
                            id = ed.TryGetValue("Id", out var eid) ? eid?.ToString() : "",
                            name = ed.TryGetValue("Name", out var en) ? en : "",
                            price = ed.TryGetValue("Price", out var ep) ? ep : 0,
                            isFree = ed.TryGetValue("Price", out var fp) && Convert.ToDecimal(fp) == 0,
                            packageType = ed.TryGetValue("PackageType", out var pt) ? pt : "compiled",
                            recommendTag = ed.TryGetValue("RecommendTag", out var rt) ? rt : null,
                            featuresJson = ed.TryGetValue("FeaturesJson", out var fj) ? fj : null,
                            licenseMonths = ed.TryGetValue("LicenseMonths", out var lm) ? lm : 12,
                            downloadPath = ed.TryGetValue("DownloadPath", out var dp) ? dp : null,
                            downloadType = ed.TryGetValue("DownloadType", out var dt) ? dt : "file",
                            sortOrder = ed.TryGetValue("SortOrder", out var eso) ? eso : 0,
                            updateLog = ed.TryGetValue("UpdateLog", out var ul) ? ul : null,
                        };
                    }).ToList()
                });
            }

            return Ok(new { code = 0, data = result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取本地商品列表失败");
            return StatusCode(500, new { code = 500, message = "获取商品列表失败" });
        }
    }

    /// <summary>
    /// 获取商品详情（前台展示，无需登录，直接查本地数据库）。
    /// 对 ID 进行格式校验以防止 SQL 注入。
    /// </summary>
    [HttpGet("local-items/{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetLocalItemById(string id)
    {
        // 安全校验：ID 只允许数字（雪花ID格式）
        if (string.IsNullOrWhiteSpace(id) || !id.All(char.IsDigit))
            return BadRequest(new { code = 400, message = "无效的商品ID" });

        try
        {
            // 使用参数化查询防止 SQL 注入
            var items = await _db.Queryable<dynamic>()
                .AS("ginkgo_PS_StoreItem")
                .Where($"Id = @id AND {_db.NotDeletedSql()}", new { id = long.Parse(id) })
                .ToListAsync();

            if (items.Count == 0)
                return NotFound(new { code = 404, message = "商品不存在" });

            var dict = (IDictionary<string, object>)items[0];
            var itemId = dict["Id"];

            // 使用参数化查询获取版本列表
            var editions = await _db.Queryable<dynamic>()
                .AS("ginkgo_PS_ItemEdition")
                .Where("ItemId = @itemId", new { itemId })
                .OrderBy("SortOrder ASC")
                .ToListAsync();

            var result = new
            {
                id = itemId?.ToString(),
                name = dict.TryGetValue("Name", out var n) ? n : "",
                category = dict.TryGetValue("Category", out var c) ? c : "",
                description = dict.TryGetValue("Description", out var d) ? d : "",
                status = dict.TryGetValue("Status", out var s) ? s : "",
                imageUrl = dict.TryGetValue("ImageUrl", out var img) ? img : null,
                sortOrder = dict.TryGetValue("SortOrder", out var so) ? so : 0,
                createdAt = dict.TryGetValue("CreatedAt", out var ca) ? ca : null,
                updatedAt = dict.TryGetValue("UpdatedAt", out var ua) ? ua : null,
                developer = dict.TryGetValue("Developer", out var dev) ? dev : null,
                officialWebsite = dict.TryGetValue("OfficialWebsite", out var ow) ? ow : null,
                serviceEmail = dict.TryGetValue("ServiceEmail", out var se) ? se : null,
                serviceQQ = dict.TryGetValue("ServiceQQ", out var sq) ? sq : null,
                imagesJson = dict.TryGetValue("ImagesJson", out var ij) ? ij : null,
                bannerUrl = dict.TryGetValue("BannerUrl", out var bu) ? bu : null,
                bannerTextColor = dict.TryGetValue("BannerTextColor", out var btc) ? btc : null,
                isVisible = dict.TryGetValue("IsVisible", out var isVis) ? Convert.ToBoolean(isVis) : true,
                editions = editions.Select(e =>
                {
                    var ed = (IDictionary<string, object>)e;
                    return new
                    {
                        id = ed.TryGetValue("Id", out var eid) ? eid?.ToString() : "",
                        name = ed.TryGetValue("Name", out var en) ? en : "",
                        price = ed.TryGetValue("Price", out var ep) ? ep : 0,
                        isFree = ed.TryGetValue("Price", out var fp) && Convert.ToDecimal(fp) == 0,
                        packageType = ed.TryGetValue("PackageType", out var pt) ? pt : "compiled",
                        recommendTag = ed.TryGetValue("RecommendTag", out var rt) ? rt : null,
                        featuresJson = ed.TryGetValue("FeaturesJson", out var fj) ? fj : null,
                        licenseMonths = ed.TryGetValue("LicenseMonths", out var lm) ? lm : 12,
                        downloadPath = ed.TryGetValue("DownloadPath", out var dp) ? dp : null,
                        downloadType = ed.TryGetValue("DownloadType", out var dt) ? dt : "file",
                        sortOrder = ed.TryGetValue("SortOrder", out var eso) ? eso : 0,
                        updateLog = ed.TryGetValue("UpdateLog", out var ul) ? ul : null,
                    };
                }).ToList()
            };

            return Ok(new { code = 0, data = result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取商品详情失败: Id={Id}", id);
            return StatusCode(500, new { code = 500, message = "获取商品详情失败" });
        }
    }

    /// <summary>
    /// 代理登录远程插件商城。
    /// </summary>
    /// <remarks>
    /// 主路径用于账号密码登录，避免浏览器跨域弹窗、COOP、postMessage 竞态导致本地拿不到商城 token。
    /// 第三方登录仍可通过远端弹窗备用入口完成。
    /// </remarks>
    [HttpPost("login")]
    public async Task<IActionResult> LoginStore([FromBody] StoreLoginInput input)
    {
        var serverUrl = _configuration["PluginStore:ServerUrl"];
        if (string.IsNullOrWhiteSpace(serverUrl))
            return BadRequest(new { ok = false, message = "插件商店服务未配置" });
        if (string.IsNullOrWhiteSpace(input.UserName) || string.IsNullOrWhiteSpace(input.Password))
            return BadRequest(new { ok = false, message = "请输入商城账号和密码" });

        try
        {
            var client = _httpClientFactory.CreateClient("PluginStoreRemote");
            client.Timeout = TimeSpan.FromSeconds(20);

            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["userName"] = input.UserName.Trim(),
                ["password"] = input.Password,
                ["clientType"] = string.IsNullOrWhiteSpace(input.ClientType) ? "WEB_PORTAL" : input.ClientType.Trim()
            });
            var captchaToken = Request.Headers["X-Captcha-Token"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(captchaToken))
                client.DefaultRequestHeaders.TryAddWithoutValidation("X-Captcha-Token", captchaToken);

            var response = await client.PostAsync($"{serverUrl.TrimEnd('/')}/api/auth/login", content);
            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                var message = ExtractRemoteErrorMessage(body, "商城登录失败");
                // 远端 401/403（账号密码错误/风控/封禁）不能透传给前端，否则会被 http.ts 当成 admin 会话失效
                return MapRemoteStoreFailure(response.StatusCode, message);
            }

            if (PluginStoreRemoteAuthMapper.TryReadBusinessError(body, out var businessError))
            {
                if (businessError!.IsCaptchaChallenge)
                {
                    var challenge = PluginStoreRemoteAuthMapper.CreateRemoteCaptchaChallenge(
                        businessError,
                        "/system/plugin-store/remote-captcha");
                    return Ok(new { code = businessError.Code, message = businessError.Message, data = challenge });
                }

                return BadRequest(new { code = businessError.Code, message = businessError.Message });
            }

            var result = PluginStoreRemoteAuthMapper.NormalizeLoginResponse(body);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "远程商城登录返回格式异常");
            return StatusCode(502, new { ok = false, message = ex.Message });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "无法连接远程商城登录接口");
            return StatusCode(503, new { ok = false, message = "无法连接到远程商城登录接口" });
        }
        catch (TaskCanceledException)
        {
            return StatusCode(503, new { ok = false, message = "连接远程商城登录接口超时" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "远程商城登录失败");
            return StatusCode(500, new { ok = false, message = "远程商城登录失败" });
        }
    }

    /// <summary>
    /// 代理获取远端商城验证码配置。
    /// </summary>
    [HttpGet("remote-captcha/config")]
    public Task<IActionResult> GetRemoteCaptchaConfig(CancellationToken ct)
    {
        return ProxyRemoteStoreGetAsync("/api/verify/captcha/config", ct);
    }

    /// <summary>
    /// 代理生成远端商城验证码挑战。
    /// </summary>
    [HttpPost("remote-captcha/generate")]
    public Task<IActionResult> GenerateRemoteCaptcha([FromBody] StoreCaptchaGenerateInput? input, CancellationToken ct)
    {
        return ProxyRemoteStorePostAsync("/api/verify/captcha/generate", new { type = input?.Type }, ct);
    }

    /// <summary>
    /// 代理校验远端商城验证码，返回远端可识别的验证令牌。
    /// </summary>
    [HttpPost("remote-captcha/validate")]
    public Task<IActionResult> ValidateRemoteCaptcha([FromBody] StoreCaptchaValidateInput input, CancellationToken ct)
    {
        return ProxyRemoteStorePostAsync("/api/verify/captcha/validate", new
        {
            input.ChallengeId,
            input.Payload
        }, ct);
    }

    private async Task<IActionResult> ProxyRemoteStoreGetAsync(string remotePath, CancellationToken ct)
    {
        var serverUrl = _configuration["PluginStore:ServerUrl"];
        if (string.IsNullOrWhiteSpace(serverUrl))
            return BadRequest(new { ok = false, message = "插件商店服务未配置" });

        try
        {
            var client = _httpClientFactory.CreateClient("PluginStoreRemote");
            client.Timeout = TimeSpan.FromSeconds(20);
            var response = await client.GetAsync($"{serverUrl.TrimEnd('/')}{remotePath}", ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            if (response.IsSuccessStatusCode)
                return Content(body, "application/json");

            return MapRemoteStoreFailure(response.StatusCode,
                ExtractRemoteErrorMessage(body, "远端商城验证码接口调用失败"));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogError(ex, "远端商城验证码 GET 代理失败: {Path}", remotePath);
            return StatusCode(503, new { ok = false, message = "无法连接远端商城验证码接口" });
        }
    }

    private async Task<IActionResult> ProxyRemoteStorePostAsync<TBody>(string remotePath, TBody payload, CancellationToken ct)
    {
        var serverUrl = _configuration["PluginStore:ServerUrl"];
        if (string.IsNullOrWhiteSpace(serverUrl))
            return BadRequest(new { ok = false, message = "插件商店服务未配置" });

        try
        {
            var client = _httpClientFactory.CreateClient("PluginStoreRemote");
            client.Timeout = TimeSpan.FromSeconds(20);
            var response = await client.PostAsJsonAsync($"{serverUrl.TrimEnd('/')}{remotePath}", payload, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            if (response.IsSuccessStatusCode)
                return Content(body, "application/json");

            return MapRemoteStoreFailure(response.StatusCode,
                ExtractRemoteErrorMessage(body, "远端商城验证码接口调用失败"));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogError(ex, "远端商城验证码 POST 代理失败: {Path}", remotePath);
            return StatusCode(503, new { ok = false, message = "无法连接远端商城验证码接口" });
        }
    }

    private static string ExtractRemoteErrorMessage(string errorBody, string fallbackMessage)
    {
        if (string.IsNullOrWhiteSpace(errorBody))
            return fallbackMessage;

        try
        {
            var errorJson = JsonSerializer.Deserialize<JsonElement>(errorBody);

            if (errorJson.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String)
                return message.GetString() ?? fallbackMessage;

            if (errorJson.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String)
            {
                var titleText = title.GetString();
                var validationText = ExtractValidationErrors(errorJson);
                if (!string.IsNullOrWhiteSpace(validationText))
                    return $"{titleText}: {validationText}";
                return titleText ?? fallbackMessage;
            }

            var errorsText = ExtractValidationErrors(errorJson);
            if (!string.IsNullOrWhiteSpace(errorsText))
                return errorsText;
        }
        catch
        {
            // ignore parse failure and fall back to raw body
        }

        return string.IsNullOrWhiteSpace(errorBody) ? fallbackMessage : errorBody;
    }

    /// <summary>
    /// 将"远端商城"返回的非 2xx 响应统一映射为前端可安全处理的 HTTP 状态码。
    /// <para>
    /// 关键原则：远端商城的 401/403 表示 <b>store 维度</b> 的业务问题（未登录商城、license 过期、
    /// 商城权限不足、付费过期等），与当前 admin 会话无关；如果把远端 401/403 原样透传给前端，
    /// 前端 <c>web/src/api/http.ts</c> 的全局拦截器会把它当作 admin 会话失效，执行清 token +
    /// 跳登录页流程，造成"下载免费插件却被踢出系统"这类事故。
    /// </para>
    /// <para>
    /// 映射规则：
    /// <list type="bullet">
    ///   <item>远端 401 / 403 → 本地 400（业务错误，前端仅提示不登出）；</item>
    ///   <item>远端 404 → 本地 404（语义不冲突）；</item>
    ///   <item>远端 5xx → 本地 502（上游故障）；</item>
    ///   <item>其它非 2xx → 本地 400。</item>
    /// </list>
    /// </para>
    /// </summary>
    private IActionResult MapRemoteStoreFailure(System.Net.HttpStatusCode remoteStatus, string message)
    {
        int localStatus = (int)remoteStatus switch
        {
            401 or 403 => StatusCodes.Status400BadRequest,
            404 => StatusCodes.Status404NotFound,
            >= 500 => StatusCodes.Status502BadGateway,
            _ => StatusCodes.Status400BadRequest
        };
        return StatusCode(localStatus, new { ok = false, message });
    }

    private static string ExtractValidationErrors(JsonElement errorJson)
    {
        if (!errorJson.TryGetProperty("errors", out var errors) || errors.ValueKind != JsonValueKind.Object)
            return string.Empty;

        var messages = new List<string>();
        foreach (var property in errors.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var item in property.Value.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String)
                    continue;

                var value = item.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                    messages.Add(value!);
            }
        }

        return string.Join("；", messages.Distinct(StringComparer.Ordinal));
    }

    /// <summary>
    /// 宽松读取 JsonElement 中的整数值。
    /// <para>
    /// 远端商城的 long 字段（雪花 ID、Total 等）通常被全局 <c>LongAsString</c> 转换器序列化成字符串，
    /// 直接 <c>GetInt64()</c> 会抛 <c>InvalidOperationException</c>。这里同时兼容：
    /// Number / String("123") / 其他 → 返回 fallback。
    /// </para>
    /// </summary>
    private static long ReadInt64Loose(JsonElement el, long fallback)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.Number:
                return el.TryGetInt64(out var n) ? n : fallback;
            case JsonValueKind.String:
                var s = el.GetString();
                return long.TryParse(s, out var v) ? v : fallback;
            default:
                return fallback;
        }
    }

    /// <summary>
    /// 宽松读取 JsonElement 中的 decimal 值。Number / String 均可，其他类型回退。
    /// 用于 price 字段——若远端被全局转换器序列化成字符串，避免 <c>GetDecimal()</c> 抛异常。
    /// </summary>
    private static decimal ReadDecimalLoose(JsonElement el, decimal fallback)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.Number:
                return el.TryGetDecimal(out var n) ? n : fallback;
            case JsonValueKind.String:
                var s = el.GetString();
                return decimal.TryParse(s, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : fallback;
            default:
                return fallback;
        }
    }

    /// <summary>
    /// 拉取远端商城当前生效的 <c>publicBaseUrl</c>（云存储/CDN 公共访问域名前缀）。
    /// <para>
    /// 顺次尝试两个源（取第一个非空值即返回）：
    /// <list type="number">
    ///   <item><description><b>云存储模块</b>：<c>GET /api/v1/oss/resource-config</c>，
    ///   返回 <c>{ ossEnabled, publicBaseUrl }</c>。这是商城真正用于上传/直链的 OSS 配置（如
    ///   <c>https://image1.ginkgoadmin.com</c>），优先级最高。</description></item>
    ///   <item><description><b>PluginStore 模块自身的 storage-info</b>：
    ///   <c>GET /api/plugin-store/portal/storage-info</c>，作为兼容兜底（仅当 PluginStore
    ///   独立存储配置时才有意义）。</description></item>
    /// </list>
    /// 任何异常都返回空字符串，不允许阻断主列表加载；外层调用 <see cref="ResolveRemoteResourceUrl"/>
    /// 时若拿到空字符串，会按商城自身域名 <paramref name="serverUrl"/> 兜底拼接。
    /// </para>
    /// </summary>
    private async Task<string> FetchStoragePublicBaseUrlAsync(HttpClient client, string serverUrl)
    {
        // 1) 云存储模块（优先）
        var fromOss = await TryGetPublicBaseUrlAsync(client,
            $"{serverUrl.TrimEnd('/')}/api/v1/oss/resource-config",
            "云存储模块 resource-config");
        if (!string.IsNullOrWhiteSpace(fromOss)) return fromOss;

        // 2) PluginStore 自身（兜底兼容）
        var fromPluginStore = await TryGetPublicBaseUrlAsync(client,
            $"{serverUrl.TrimEnd('/')}/api/plugin-store/portal/storage-info",
            "PluginStore portal storage-info");
        return fromPluginStore;
    }

    /// <summary>
    /// 尝试从给定 URL 拉取 publicBaseUrl，兼容两种响应形态：
    /// <c>{ publicBaseUrl: "..." }</c>（云存储模块）与 <c>{ data: { publicBaseUrl: "..." } }</c>
    /// （PluginStore Result 包装）。失败一律返回空字符串。
    /// </summary>
    private async Task<string> TryGetPublicBaseUrlAsync(HttpClient client, string url, string sourceLabel)
    {
        try
        {
            using var resp = await client.GetAsync(url);
            if (!resp.IsSuccessStatusCode) return string.Empty;
            var contentType = resp.Content.Headers.ContentType?.MediaType ?? "";
            if (!contentType.Contains("json", StringComparison.OrdinalIgnoreCase)) return string.Empty;

            var body = await resp.Content.ReadAsStringAsync();
            var doc = JsonSerializer.Deserialize<JsonElement>(body);
            JsonElement target = doc.TryGetProperty("data", out var d) && d.ValueKind == JsonValueKind.Object ? d : doc;
            if (target.ValueKind != JsonValueKind.Object) return string.Empty;
            var pub = target.GetProp("publicBaseUrl");
            return string.IsNullOrWhiteSpace(pub) ? string.Empty : pub!.TrimEnd('/');
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "拉取 {Source} 失败（旧版商城无此端点或暂不可用时正常）", sourceLabel);
            return string.Empty;
        }
    }

    /// <summary>
    /// 把远端图片字段解析成可直链访问的完整 URL，支持以下输入：
    /// <list type="bullet">
    ///   <item><description><c>data:</c> / <c>blob:</c> → 原样返回</description></item>
    ///   <item><description>绝对 <c>http(s)://</c> URL：若 host 与 <paramref name="publicBaseUrl"/>
    ///   不一致但属同一根域（如 DB 残留的 <c>www.ginkgoadmin.com</c> 与当前 OSS 配置的
    ///   <c>image1.ginkgoadmin.com</c>），则把 host 重写为 publicBaseUrl 的 host，
    ///   保留原始 PathAndQuery；其他情况下原样返回</description></item>
    ///   <item><description>相对路径：用 <paramref name="publicBaseUrl"/> 拼接；为空时用
    ///   <paramref name="serverUrl"/> 兜底（适用于商城本地 /uploads/ 镜像）</description></item>
    /// </list>
    /// 「同一根域」校验避免把第三方图床（如 <c>https://otherhost.com/x.jpg</c>）也错误重写。
    /// </summary>
    private static string? ResolveRemoteResourceUrl(string? raw, string? publicBaseUrl, string serverUrl)
    {
        if (string.IsNullOrWhiteSpace(raw)) return raw;
        var path = raw.Trim();

        if (path.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("blob:", StringComparison.OrdinalIgnoreCase))
        {
            return path;
        }

        // 绝对 URL：尝试把过时的官网域名重写到当前 publicBaseUrl 的 host
        if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(publicBaseUrl)
                && Uri.TryCreate(path, UriKind.Absolute, out var rawUri)
                && Uri.TryCreate(publicBaseUrl, UriKind.Absolute, out var pubUri))
            {
                if (!string.Equals(rawUri.Host, pubUri.Host, StringComparison.OrdinalIgnoreCase))
                {
                    var rawRoot = ExtractRootDomain(rawUri.Host.ToLowerInvariant());
                    var pubRoot = ExtractRootDomain(pubUri.Host.ToLowerInvariant());
                    if (!string.IsNullOrEmpty(rawRoot) && string.Equals(rawRoot, pubRoot, StringComparison.OrdinalIgnoreCase))
                    {
                        return $"{pubUri.Scheme}://{pubUri.Authority}{rawUri.PathAndQuery}";
                    }
                }
            }
            return path;
        }

        // 相对路径
        var rel = path.TrimStart('/');
        if (!string.IsNullOrWhiteSpace(publicBaseUrl))
            return $"{publicBaseUrl!.TrimEnd('/')}/{rel}";
        return $"{serverUrl.TrimEnd('/')}/{rel}";
    }

    /// <summary>
    /// 从 imagesJson（图集 JSON）字符串中提取首张图片 URL。
    /// <para>
    /// 远端 ImagesJson 通常是 <c>["url1","url2"]</c> 或 <c>[{"url":"..."},{"url":"..."}]</c> 形态，
    /// 也可能是逗号分隔的纯文本。解析失败时统一返回 null，由调用方继续走占位图逻辑。
    /// </para>
    /// </summary>
    private static string? ExtractFirstImageFromJson(string? imagesJson)
    {
        if (string.IsNullOrWhiteSpace(imagesJson)) return null;
        var raw = imagesJson.Trim();

        // 非 JSON：按逗号分隔取首段
        if (!raw.StartsWith('[') && !raw.StartsWith('{'))
        {
            var parts = raw.Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return parts.Length > 0 ? parts[0] : null;
        }

        try
        {
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in root.EnumerateArray())
                {
                    if (el.ValueKind == JsonValueKind.String)
                    {
                        var s = el.GetString();
                        if (!string.IsNullOrWhiteSpace(s)) return s;
                    }
                    else if (el.ValueKind == JsonValueKind.Object)
                    {
                        // 兼容 {"url":"..."} / {"src":"..."} / {"path":"..."}
                        foreach (var key in new[] { "url", "src", "path", "image", "imageUrl" })
                        {
                            if (el.TryGetProperty(key, out var vEl) && vEl.ValueKind == JsonValueKind.String)
                            {
                                var s = vEl.GetString();
                                if (!string.IsNullOrWhiteSpace(s)) return s;
                            }
                        }
                    }
                }
            }
            else if (root.ValueKind == JsonValueKind.Object)
            {
                foreach (var key in new[] { "url", "src", "path", "image", "imageUrl" })
                {
                    if (root.TryGetProperty(key, out var vEl) && vEl.ValueKind == JsonValueKind.String)
                    {
                        var s = vEl.GetString();
                        if (!string.IsNullOrWhiteSpace(s)) return s;
                    }
                }
            }
        }
        catch
        {
            // ImagesJson 不合法 JSON 时静默忽略，回退到占位图
        }
        return null;
    }

    /// <summary>
    /// 从 edition 的 updateLog 字段中提取最新版本号。
    /// updateLog 可能是 JSON 数组 [{"version":"V0.1",...}] 或纯文本。
    /// </summary>
    private static string ExtractVersion(JsonElement edition)
    {
        var raw = edition.GetProp("updateLog");
        if (string.IsNullOrWhiteSpace(raw))
            return "1.0.0";

        try
        {
            var arr = JsonSerializer.Deserialize<JsonElement>(raw);
            if (arr.ValueKind == JsonValueKind.Array && arr.GetArrayLength() > 0)
            {
                // 取最后一条（最新版本）
                var last = arr[arr.GetArrayLength() - 1];
                return last.GetProp("version") ?? "1.0.0";
            }
        }
        catch { /* 非 JSON，当作纯文本 */ }

        return raw.Length > 20 ? "1.0.0" : raw;
    }

    /// <summary>
    /// 获取可购买插件列表（支持分类筛选和关键词搜索）。
    /// 代理到远程商城 /api/plugin-store/items/published，将分类和搜索参数同时传递给远程 API，
    /// 并将 StoreItem 格式转换为前端所需的扁平 AvailablePlugin 格式。
    /// </summary>
    [HttpGet("available-plugins")]
    [AllowAnonymous]
    public async Task<IActionResult> GetAvailablePlugins(
        [FromQuery] string? category,
        [FromQuery] string? keyword,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var serverUrl = _configuration["PluginStore:ServerUrl"];
        if (string.IsNullOrWhiteSpace(serverUrl))
            return BadRequest(new { ok = false, message = "插件商店服务未配置" });

        // 参数纠偏：避免 0/负数/过大值被转发给远端
        if (page <= 0) page = 1;
        if (pageSize <= 0) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        try
        {
            var client = _httpClientFactory.CreateClient("PluginStoreRemote");
            client.Timeout = TimeSpan.FromSeconds(15);

            // 转发商城 Token（已登录用户可获取购买状态）
            var storeToken = Request.Headers["X-Store-Token"].FirstOrDefault();
            if (!string.IsNullOrEmpty(storeToken))
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", storeToken);

            // 并发拉远端「公开存储信息」：用于把 ImageUrl/BannerUrl/ImagesJson 中的相对路径
            // 拼成完整的 CDN/对象存储直链，否则前端拿到的是裸路径无法显示图片。
            // 拉不到时（旧版商城没有这个端点）publicBaseUrl 为空字符串，此时按商城自身域名兜底。
            var storageInfoTask = FetchStoragePublicBaseUrlAsync(client, serverUrl);

            // 构建远程 API URL：优先走「分页门户接口」/api/plugin-store/portal/items（新版商城）。
            // 远端返回 Result<PagedResult<StoreItemDetailDto>>，天然带 total/page/pageSize。
            // 若远端是旧版（仅有 /api/plugin-store/items/published 不带分页），下面会按 404 回退兼容。
            var queryCommon = new List<string>();
            if (!string.IsNullOrWhiteSpace(category))
                queryCommon.Add($"category={Uri.EscapeDataString(category.Trim())}");
            if (!string.IsNullOrWhiteSpace(keyword))
                queryCommon.Add($"keyword={Uri.EscapeDataString(keyword.Trim())}");

            var portalQuery = new List<string>(queryCommon) { $"page={page}", $"pageSize={pageSize}" };
            var apiUrl = $"{serverUrl.TrimEnd('/')}/api/plugin-store/portal/items?{string.Join("&", portalQuery)}";

            var response = await client.GetAsync(apiUrl);
            // 兼容旧版商城：portal/items 不存在时回退到 items/published（无分页，整批拿回，本地兜底分页）
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogInformation("远端不支持 /portal/items，回退到 /items/published");
                var fallbackUrl = $"{serverUrl.TrimEnd('/')}/api/plugin-store/items/published"
                    + (queryCommon.Count > 0 ? "?" + string.Join("&", queryCommon) : string.Empty);
                response.Dispose();
                response = await client.GetAsync(fallbackUrl);
                apiUrl = fallbackUrl;
            }
            if (!response.IsSuccessStatusCode)
            {
                var errBody = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("远端商城列表返回非 2xx：{Status} URL={Url} Body={Body}",
                    (int)response.StatusCode, apiUrl, errBody);
                var msg = ExtractRemoteErrorMessage(errBody, "获取插件列表失败");
                return MapRemoteStoreFailure(response.StatusCode, msg);
            }

            // 检查响应内容类型，避免将 HTML/重定向页面当成 JSON 解析
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
            if (!contentType.Contains("json", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError("远程商城 API 返回了非 JSON 响应（ContentType={ContentType}），" +
                    "请检查 PluginStore:ServerUrl 配置是否指向正确的 API 后端地址（当前地址：{ApiUrl}）",
                    contentType, apiUrl);
                return StatusCode(503, new { ok = false, message = $"插件商店地址配置有误：{serverUrl} 返回了 HTML 而非 API 数据，请检查 PluginStore:ServerUrl 是否指向正确的后端地址" });
            }

            // 处理远程返回
            var jsonTask = response.Content.ReadAsStringAsync();
            var licTask = Task.FromResult("[]");
            if (!string.IsNullOrEmpty(storeToken))
            {
                // 并发获取当前用户授权列表
                // TODO: 远程由于没有提供 Items 自带购买状态，所以我们在此发起二次查询补充此状态
                licTask = client.GetStringAsync($"{serverUrl.TrimEnd('/')}/api/plugin-store/portal/user-licenses").ContinueWith(t => t.IsCompletedSuccessfully ? t.Result : "[]");
            }

            var json = await jsonTask;
            var doc = JsonSerializer.Deserialize<JsonElement>(json);

            // 兼容三种响应格式：
            //   A) 分页门户接口（新）：{ code: 0, data: { total, page, pageSize, items: [...] } }
            //   B) 旧 published 接口：{ code: 0, data: [ StoreItem, ... ] }
            //   C) 裸数组：[ StoreItem, ... ]
            // 同时把远端分页元信息（total/page/pageSize）透传给前端，用于 el-pagination。
            // 注意：远端可能用 long-as-string 全局转换器把 Total 序列化成字符串（避免 JS 精度丢失），
            // 所以 total/page/pageSize 字段需要同时兼容 Number 与 String 两种 JsonValueKind。
            JsonElement itemsElement;
            long remoteTotal = 0;
            int remotePage = page;
            int remotePageSize = pageSize;
            if (doc.TryGetProperty("data", out var dataEl))
            {
                if (dataEl.ValueKind == JsonValueKind.Object
                    && dataEl.TryGetProperty("items", out var itemsProp) && itemsProp.ValueKind == JsonValueKind.Array)
                {
                    itemsElement = itemsProp;
                    if (dataEl.TryGetProperty("total", out var tEl)) remoteTotal = ReadInt64Loose(tEl, 0);
                    if (dataEl.TryGetProperty("page", out var pEl)) remotePage = (int)ReadInt64Loose(pEl, page);
                    if (dataEl.TryGetProperty("pageSize", out var psEl)) remotePageSize = (int)ReadInt64Loose(psEl, pageSize);
                }
                else if (dataEl.ValueKind == JsonValueKind.Array)
                {
                    itemsElement = dataEl;
                    remoteTotal = dataEl.GetArrayLength();
                }
                else
                {
                    return Ok(new { items = Array.Empty<object>(), total = 0L, page, pageSize });
                }
            }
            else if (doc.ValueKind == JsonValueKind.Array)
            {
                itemsElement = doc;
                remoteTotal = doc.GetArrayLength();
            }
            else
            {
                return Ok(new { items = Array.Empty<object>(), total = 0L, page, pageSize });
            }

            // 解析已拥有的授权
            var purchasedEditions = new HashSet<string>();
            var purchasedItems = new HashSet<string>();
            if (!string.IsNullOrEmpty(storeToken))
            {
                try
                {
                    var licJson = await licTask;
                    var licDoc = JsonSerializer.Deserialize<JsonElement>(licJson);
                    JsonElement licData;
                    if (licDoc.TryGetProperty("data", out var ldataEl) && ldataEl.ValueKind == JsonValueKind.Array)
                        licData = ldataEl;
                    else if (licDoc.ValueKind == JsonValueKind.Array)
                        licData = licDoc;
                    else
                        licData = default;

                    if (licData.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var lic in licData.EnumerateArray())
                        {
                            var edId = lic.GetProp("editionId") ?? lic.GetProp("EditionId");
                            var itemId = lic.GetProp("itemId") ?? lic.GetProp("ItemId");
                            var status = lic.GetProp("status") ?? lic.GetProp("Status");
                            if (status == "active" || status == "Active")
                            {
                                if (!string.IsNullOrEmpty(edId))
                                    purchasedEditions.Add(edId);
                                if (!string.IsNullOrEmpty(itemId))
                                    purchasedItems.Add(itemId);
                            }
                        }
                    }
                }
                catch { }
            }

            // 本地二次过滤（如果远程 API 不支持这些参数，则在本地兜底过滤）
            var categoryFilter = category?.Trim();
            var keywordFilter = keyword?.Trim();

            // 取远端公开存储信息（已并发触发）：拼接 CDN/对象存储直链时使用
            var publicBaseUrl = await storageInfoTask;

            // 转换为前端所需的扁平 AvailablePlugin 格式
            var result = new List<object>();
            foreach (var item in itemsElement.EnumerateArray())
            {
                var name = item.GetProp("name");
                var itemCategory = item.GetProp("category");
                var description = item.GetProp("description");
                // 封面顺次回退：imageUrl → bannerUrl → imagesJson 数组首张。
                // StoreItemDetailDto 的主图字段是 ImageUrl，但部分商品后台可能只上传了 Banner 或图集，
                // 不做回退会导致前端只能显示拼图占位图，体验上"远端明明有图但本地不显示"。
                var imageUrl = item.GetProp("imageUrl");
                if (string.IsNullOrWhiteSpace(imageUrl))
                    imageUrl = item.GetProp("bannerUrl");
                if (string.IsNullOrWhiteSpace(imageUrl))
                    imageUrl = ExtractFirstImageFromJson(item.GetProp("imagesJson"));
                // 把相对路径补全为完整可访问 URL（CDN 直链 / 商城本地 /uploads/）
                imageUrl = ResolveRemoteResourceUrl(imageUrl, publicBaseUrl, serverUrl);
                var developer = item.GetProp("developer");
                var itemId = item.GetProp("id");

                // 分类本地兜底过滤（远程可能不支持该参数）
                if (!string.IsNullOrEmpty(categoryFilter) && !string.Equals(itemCategory, categoryFilter, StringComparison.OrdinalIgnoreCase))
                    continue;

                // 关键词本地兜底过滤（远程可能不支持该参数）
                if (!string.IsNullOrEmpty(keywordFilter))
                {
                    var nameMatch = name?.Contains(keywordFilter, StringComparison.OrdinalIgnoreCase) == true;
                    var descMatch = description?.Contains(keywordFilter, StringComparison.OrdinalIgnoreCase) == true;
                    var devMatch = developer?.Contains(keywordFilter, StringComparison.OrdinalIgnoreCase) == true;
                    if (!nameMatch && !descMatch && !devMatch)
                        continue;
                }

                // 每个 edition 展开为一条 AvailablePlugin
                if (item.TryGetProperty("editions", out var editions) && editions.ValueKind == JsonValueKind.Array)
                {
                    foreach (var ed in editions.EnumerateArray())
                    {
                        var edName = ed.GetProp("name");
                        var price = ed.TryGetProperty("price", out var p) ? ReadDecimalLoose(p, 0m) : 0m;
                        var packageType = ed.GetProp("packageType") ?? "compiled";
                        var editionId = ed.GetProp("id");
                        var isFree = price == 0m;
                        var version = ExtractVersion(ed);
                        // 本地解析并覆盖 purchased
                        var purchased = !string.IsNullOrEmpty(editionId) && purchasedEditions.Contains(editionId);

                        result.Add(new
                        {
                            id = itemId,
                            name = $"{name}",
                            editionName = edName,
                            version,
                            description,
                            price,
                            purchased,
                            installed = false, // 由前端根据本地模块列表判断
                            category = itemCategory,
                            coverUrl = imageUrl,
                            author = developer,
                            imageUrl,
                            editionId,
                            packageType,
                            isFree,
                        });
                    }
                }
                else
                {
                    // 无 edition 的商品作为单条记录
                    result.Add(new
                    {
                        id = itemId,
                        name,
                        editionName = (string?)null,
                        version = "1.0.0",
                        description,
                        price = 0m,
                        purchased = !string.IsNullOrEmpty(itemId) && purchasedItems.Contains(itemId),
                        installed = false,
                        category = itemCategory,
                        coverUrl = imageUrl,
                        author = developer,
                        imageUrl,
                        editionId = (string?)null,
                        packageType = "compiled",
                        isFree = true,
                    });
                }
            }

            // 返回结构：items 仍是 flatten 后的 edition 级列表（前端 groupedPlugins 负责回聚），
            // total/page/pageSize 按远端分页元信息透传，前端据此渲染 el-pagination。
            return Ok(new
            {
                items = result,
                total = remoteTotal,
                page = remotePage,
                pageSize = remotePageSize
            });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "无法连接远程商城服务");
            return StatusCode(503, new { ok = false, message = "无法连接到远程商城服务" });
        }
        catch (TaskCanceledException)
        {
            return StatusCode(503, new { ok = false, message = "连接远程商城服务超时" });
        }
        catch (Exception ex)
        {
            // 把异常类型与首层 message 透出到响应，便于前端控制台直接看到根因（远端 5xx / JSON 解析失败 / 等）
            _logger.LogError(ex, "获取可购买插件列表失败");
            var detail = $"{ex.GetType().Name}: {ex.Message}";
            if (ex.InnerException != null)
                detail += $" ← {ex.InnerException.GetType().Name}: {ex.InnerException.Message}";
            return StatusCode(503, new { ok = false, message = "插件商店服务不可用", detail });
        }
    }

    /// <summary>
    /// 代理远端商城已启用分类列表（公开接口，用于商店前台筛选 & 分类标签中文展示）。
    /// <para>
    /// 远端：<c>GET /api/plugin-store/categories/enabled</c>，返回 <c>Result&lt;List&lt;ItemCategoryDto&gt;&gt;</c>，
    /// 每条包含 <c>Code</c>（英文代码，用于查询过滤）与 <c>Name</c>（中文名称，用于展示）。
    /// 前端据此构建分类单选器，并把 plugin.category(=code) 渲染为对应中文名，而不是硬编码映射。
    /// </para>
    /// </summary>
    [HttpGet("categories")]
    [AllowAnonymous]
    public async Task<IActionResult> GetStoreCategories(CancellationToken ct)
    {
        var serverUrl = _configuration["PluginStore:ServerUrl"];
        if (string.IsNullOrWhiteSpace(serverUrl))
            return Ok(new { code = 0, data = Array.Empty<object>() });

        try
        {
            var client = _httpClientFactory.CreateClient("PluginStoreRemote");
            client.Timeout = TimeSpan.FromSeconds(10);
            // 商城登录 token 可有可无：enabled 分类是 AllowAnonymous 的公开接口
            var storeToken = Request.Headers["X-Store-Token"].FirstOrDefault();
            if (!string.IsNullOrEmpty(storeToken))
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", storeToken);

            var resp = await client.GetAsync($"{serverUrl.TrimEnd('/')}/api/plugin-store/categories/enabled", ct);
            if (resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync(ct);
                return Content(body, "application/json");
            }

            var errBody = await resp.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("获取远端分类失败: {Status} {Body}", resp.StatusCode, errBody);
            var msg = ExtractRemoteErrorMessage(errBody, "获取分类失败");
            return MapRemoteStoreFailure(resp.StatusCode, msg);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogError(ex, "代理获取远端分类异常");
            // 分类不是关键路径：异常时返回空列表而不是 503，前端仍可按原始 code 展示
            return Ok(new { code = 0, data = Array.Empty<object>() });
        }
    }

    /// <summary>
    /// 远程商城静态资源代理。
    /// 适用场景：远程商城返回的封面图 URL 可能是相对路径、商城自身域名、或者 OSS/CDN 域名；
    /// 浏览器直接加载这些 URL 时容易遇到跨域、SSL 不可信、混合内容等问题。
    /// 这里统一通过后端代理（复用 PluginStoreRemote HttpClient，自动跳过 SSL）避免上述问题。
    /// </summary>
    /// <param name="url">远程返回的资源原始 URL/相对路径，由前端 encodeURIComponent 后传入。</param>
    [HttpGet("asset")]
    [AllowAnonymous]
    public async Task<IActionResult> GetStoreAsset([FromQuery] string url, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(url))
            return BadRequest();

        var serverUrl = _configuration["PluginStore:ServerUrl"];
        if (string.IsNullOrWhiteSpace(serverUrl))
            return BadRequest();

        // 1. 解析为绝对 URL（绝对原样使用；相对路径基于 PluginStore:ServerUrl 拼接）
        Uri targetUri;
        try
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var absUri)
                && (absUri.Scheme == Uri.UriSchemeHttp || absUri.Scheme == Uri.UriSchemeHttps))
            {
                targetUri = absUri;
            }
            else
            {
                var baseUri = new Uri(serverUrl.EndsWith('/') ? serverUrl : serverUrl + "/");
                targetUri = new Uri(baseUri, url.TrimStart('/'));
            }
        }
        catch
        {
            return BadRequest();
        }

        // 2. SSRF 防护：只允许商城域名/子域名以及常见对象存储 / CDN 域名
        if (!IsAllowedAssetHost(targetUri, serverUrl))
        {
            _logger.LogWarning("[PluginStore] 拒绝代理非白名单资源: {Host}", targetUri.Host);
            return BadRequest();
        }

        try
        {
            var client = _httpClientFactory.CreateClient("PluginStoreRemote");
            client.Timeout = TimeSpan.FromSeconds(30);
            var resp = await client.GetAsync(targetUri, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogInformation("[PluginStore] 远程资源返回非 2xx: {Status} {Url}", (int)resp.StatusCode, targetUri);
                return StatusCode((int)resp.StatusCode);
            }

            var contentType = resp.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
            var stream = await resp.Content.ReadAsStreamAsync(ct);
            // 浏览器层面缓存 1 天，避免每次进入商店都重新拉远端
            Response.Headers["Cache-Control"] = "public, max-age=86400";
            return File(stream, contentType);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[PluginStore] 资源代理失败: {Url}", targetUri);
            return StatusCode(503);
        }
    }

    /// <summary>
    /// 校验目标资源 Host 是否在允许列表内：商城自身域名/子域名 + 常见对象存储/CDN 域名。
    /// </summary>
    private static bool IsAllowedAssetHost(Uri uri, string serverUrl)
    {
        if (!Uri.TryCreate(serverUrl, UriKind.Absolute, out var baseUri))
            return false;

        var host = uri.Host.ToLowerInvariant();
        var baseHost = baseUri.Host.ToLowerInvariant();

        // 同域或商城主域名的子域名
        if (host == baseHost || host.EndsWith("." + baseHost))
            return true;

        // 提取商城主域名（去掉最左侧子域）作为更宽松的匹配（如 api.x.com → x.com）
        var baseRoot = ExtractRootDomain(baseHost);
        if (!string.IsNullOrEmpty(baseRoot) && (host == baseRoot || host.EndsWith("." + baseRoot)))
            return true;

        // 常见对象存储 / CDN 域名后缀
        string[] allowedSuffixes =
        {
            ".aliyuncs.com",
            ".alicdn.com",
            ".myqcloud.com",
            ".qcloud.com",
            ".cos.ap-",
            ".upyun.com",
            ".upaiyun.com",
            ".b0.upaiyun.com",
            ".upcdn.net", // 又拍云 CDN 自定义域名（如 xxx.test.upcdn.net）
            ".upyuncdn.net",
            ".qiniucdn.com",
            ".clouddn.com",
            ".bkt.clouddn.com",
            ".bcebos.com",
            ".amazonaws.com",
            ".cloudfront.net",
            ".obs.cn-",
            ".obs.myhuaweicloud.com",
            ".huaweicloud.com",
            ".ksyun.com",
            ".ks3-cn-",
            ".jdcloud.com",
            ".jcloudcs.com"
        };
        return allowedSuffixes.Any(s => host.Contains(s));
    }

    /// <summary>
    /// 提取根域名：xx.example.com → example.com；example.com → example.com。
    /// 简化处理，不解析公共后缀列表（PSL），对国别域名（co.uk 等）会偏宽松，但作为白名单能接受。
    /// </summary>
    private static string ExtractRootDomain(string host)
    {
        var parts = host.Split('.');
        if (parts.Length < 2) return host;
        return $"{parts[^2]}.{parts[^1]}";
    }

    /// <summary>
    /// 获取商城用户信息（从 JWT token 中解码）。
    /// </summary>
    [HttpGet("user-info")]
    public IActionResult GetUserInfo()
    {
        var storeToken = Request.Headers["X-Store-Token"].FirstOrDefault();
        if (string.IsNullOrEmpty(storeToken))
            return BadRequest(new { ok = false, message = "未提供商城 Token" });

        try
        {
            // JWT 由三段组成：header.payload.signature，解码 payload 即可
            var parts = storeToken.Split('.');
            if (parts.Length < 2)
                return BadRequest(new { ok = false, message = "Token 格式无效" });

            var payload = parts[1];
            // 补齐 Base64 填充
            switch (payload.Length % 4)
            {
                case 2: payload += "=="; break;
                case 3: payload += "="; break;
            }
            var bytes = Convert.FromBase64String(payload.Replace('-', '+').Replace('_', '/'));
            var json = System.Text.Encoding.UTF8.GetString(bytes);
            var claims = JsonSerializer.Deserialize<JsonElement>(json);

            var username = claims.GetProp("name") 
                ?? claims.GetProp("unique_name") 
                ?? claims.GetProp("preferred_username") 
                ?? claims.GetProp("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name") 
                ?? claims.GetProp("nickname") 
                ?? "Ginkgo用户";

            var nickname = claims.GetProp("nickname") 
                ?? claims.GetProp("given_name") 
                ?? username;

            return Ok(new { username, nickname });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "解析商城 Token 失败");
            return BadRequest(new { ok = false, message = "Token 解析失败" });
        }
    }

    /// <summary>
    /// 购买插件（代理到远程商城完成支付）。
    /// </summary>
    [HttpPost("purchase")]
    public async Task<IActionResult> PurchasePlugin([FromBody] PurchasePluginInput input)
    {
        var serverUrl = _configuration["PluginStore:ServerUrl"];
        if (string.IsNullOrWhiteSpace(serverUrl))
            return BadRequest(new { ok = false, message = "插件商店服务未配置" });

        try
        {
            var client = _httpClientFactory.CreateClient("PluginStoreRemote");
            client.Timeout = TimeSpan.FromMinutes(2);

            var storeToken = Request.Headers["X-Store-Token"].FirstOrDefault();
            if (!string.IsNullOrEmpty(storeToken))
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", storeToken);

            var response = await client.PostAsJsonAsync($"{serverUrl.TrimEnd('/')}/api/plugin-store/orders", new
            {
                EditionId = input.EditionId,
                ChannelType = input.ChannelType ?? "wechat_native"
            });

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return Content(content, "application/json");
            }

            var errorBody = await response.Content.ReadAsStringAsync();
            var orderMsg = ExtractRemoteErrorMessage(errorBody, "创建订单失败");
            return MapRemoteStoreFailure(response.StatusCode, orderMsg);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "购买插件失败: {PluginId}", input.PluginId);
            return StatusCode(500, new { ok = false, message = $"购买失败: {ex.Message}" });
        }
    }

    /// <summary>
    /// 代理远程商城的"已启用支付渠道"列表（购买对话框据此动态展示支付方式）。
    /// 远端：GET /api/plugin-store/portal/payment-channels（公开接口，无需 Token）。
    /// 远端只会返回真正 Enabled=true 的渠道（如 wechat / alipay / unionpay），
    /// 远端 Payment 模块未安装或未配置渠道时会返回空数组。
    /// </summary>
    [HttpGet("payment-channels")]
    public async Task<IActionResult> GetPaymentChannels()
    {
        var serverUrl = _configuration["PluginStore:ServerUrl"];
        if (string.IsNullOrWhiteSpace(serverUrl))
            return Ok(new { code = 0, data = Array.Empty<string>() });

        try
        {
            var client = _httpClientFactory.CreateClient("PluginStoreRemote");
            client.Timeout = TimeSpan.FromSeconds(10);
            var response = await client.GetAsync($"{serverUrl.TrimEnd('/')}/api/plugin-store/portal/payment-channels");
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return Content(content, "application/json");
            }
            _logger.LogWarning("获取远端启用支付渠道失败: {Status}", response.StatusCode);
            return Ok(new { code = 0, data = Array.Empty<string>() });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "代理获取支付渠道异常");
            return Ok(new { code = 0, data = Array.Empty<string>() });
        }
    }

    /// <summary>
    /// 查询订单状态（用于购买完成后的扫码轮询）。
    /// </summary>
    [HttpGet("orders/{orderNo}")]
    public async Task<IActionResult> GetStoreOrder(string orderNo)
    {
        if (!ValidateOrderNo(orderNo))
            return BadRequest(new { code = 400, message = "订单号格式无效" });

        var serverUrl = _configuration["PluginStore:ServerUrl"];
        if (string.IsNullOrWhiteSpace(serverUrl))
            return BadRequest(new { code = 500, message = "未配置" });

        try
        {
            var client = _httpClientFactory.CreateClient("PluginStoreRemote");
            client.Timeout = TimeSpan.FromSeconds(10);
            var storeToken = Request.Headers["X-Store-Token"].FirstOrDefault();
            if (!string.IsNullOrEmpty(storeToken))
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", storeToken);

            var response = await client.GetAsync($"{serverUrl.TrimEnd('/')}/api/plugin-store/orders/by-no/{orderNo}");
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return Content(content, "application/json");
            }

            var errorBody = await response.Content.ReadAsStringAsync();
            var msg = ExtractRemoteErrorMessage(errorBody, "查询订单失败");
            return MapRemoteStoreFailure(response.StatusCode, msg);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查询订单状态代理失败: OrderNo={OrderNo}", orderNo);
            return StatusCode(500, new { code = 500, message = "查询订单失败" });
        }
    }

    /// <summary>
    /// 透明代理远端支付订单详情（按支付订单号），用于本地 admin「下单后 payParams 缺失」的 fallback 拉取。
    /// <para>
    /// 远端商城 <c>POST /api/plugin-store/orders</c> 返回的 <c>StoreOrderDto</c> 在创建链路上有几种情况会出现
    /// <c>payParams</c> 字段为空但 <c>paymentOrderNo</c> 已写入的现象（异步派发支付订单创建 / 渠道适配器晚返参等）。
    /// 远端门户 <c>CheckoutPage.vue</c> 的实现是：发现 payParams 为空时立刻再去查一次支付订单详情拿到 payParams 兜底，
    /// 本接口为 admin 客户端复用同一思路提供透明代理：转发到远端
    /// <c>GET /api/payment/orders?OrderNo={paymentOrderNo}&amp;PageSize=1</c> 取首条记录返回。
    /// </para>
    /// <para>
    /// 仅做透传：响应 JSON 直接 passthrough，前端取 <c>data.items[0].payParams</c> 即可。
    /// </para>
    /// </summary>
    [HttpGet("payment-orders/by-no/{paymentOrderNo}")]
    public async Task<IActionResult> GetPaymentOrderByNo(string paymentOrderNo)
    {
        if (!ValidateOrderNo(paymentOrderNo))
            return BadRequest(new { code = 400, message = "支付订单号格式无效" });

        var serverUrl = _configuration["PluginStore:ServerUrl"];
        if (string.IsNullOrWhiteSpace(serverUrl))
            return BadRequest(new { code = 500, message = "未配置" });

        try
        {
            var client = _httpClientFactory.CreateClient("PluginStoreRemote");
            client.Timeout = TimeSpan.FromSeconds(10);
            var storeToken = Request.Headers["X-Store-Token"].FirstOrDefault();
            if (!string.IsNullOrEmpty(storeToken))
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", storeToken);

            var response = await client.GetAsync(
                $"{serverUrl.TrimEnd('/')}/api/payment/orders?OrderNo={Uri.EscapeDataString(paymentOrderNo)}&PageSize=1");
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return Content(content, "application/json");
            }

            var errorBody = await response.Content.ReadAsStringAsync();
            var msg = ExtractRemoteErrorMessage(errorBody, "查询支付订单失败");
            return MapRemoteStoreFailure(response.StatusCode, msg);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查询支付订单代理失败: PaymentOrderNo={No}", paymentOrderNo);
            return StatusCode(500, new { code = 500, message = "查询支付订单失败" });
        }
    }

    /// <summary>
    /// 主动查询支付网关并同步订单状态（前端「我已完成支付」按钮 / 周期性兜底）。
    /// <para>
    /// 当支付回调因网络、防火墙或开发环境等原因未到达远端商城时，前端可通过此接口触发
    /// 远端主动向支付网关查询真实支付状态；若已支付则远端会自动完成订单确认与授权生成。
    /// </para>
    /// <para>
    /// 服务端限流策略 <c>payment-check</c>（60 次/分钟/IP）已就位，避免恶意客户端高频
    /// 调用第三方支付网关导致商户号风控。
    /// </para>
    /// </summary>
    [HttpPost("orders/{orderNo}/check-payment")]
    [EnableRateLimiting("payment-check")]
    public async Task<IActionResult> CheckPaymentStatus(string orderNo)
    {
        if (!ValidateOrderNo(orderNo))
            return BadRequest(new { code = 400, message = "订单号格式无效" });

        var serverUrl = _configuration["PluginStore:ServerUrl"];
        if (string.IsNullOrWhiteSpace(serverUrl))
            return BadRequest(new { code = 500, message = "未配置" });

        try
        {
            var client = _httpClientFactory.CreateClient("PluginStoreRemote");
            client.Timeout = TimeSpan.FromSeconds(15);
            var storeToken = Request.Headers["X-Store-Token"].FirstOrDefault();
            if (!string.IsNullOrEmpty(storeToken))
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", storeToken);

            var response = await client.PostAsync(
                $"{serverUrl.TrimEnd('/')}/api/plugin-store/orders/by-no/{orderNo}/check-payment", null);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return Content(content, "application/json");
            }

            var errorBody = await response.Content.ReadAsStringAsync();
            var msg = ExtractRemoteErrorMessage(errorBody, "查询支付状态失败");
            return MapRemoteStoreFailure(response.StatusCode, msg);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "主动查询支付状态代理失败: OrderNo={OrderNo}", orderNo);
            return StatusCode(500, new { code = 500, message = "查询支付状态失败" });
        }
    }

    /// <summary>
    /// 列出当前用户对某档位下「license 视角」的全部可见发版及其可下载状态。
    /// <para>
    /// 仅做透明代理：直接转发到远端商城 <c>GET /api/plugin-store/downloads/editions/{editionId}/available-releases</c>，
    /// 由远端按当前 license 的升级窗口与关键安全版本规则给出 <c>available</c>/<c>inUpgradeWindow</c>/<c>isLatest</c> 标记。
    /// 前端用于「版本选择对话框」：超出升级窗口的版本置灰提示，关键安全版本可直接下载。
    /// </para>
    /// </summary>
    [HttpGet("editions/{editionId}/available-releases")]
    public async Task<IActionResult> ListAvailableReleases(string editionId, CancellationToken ct)
    {
        var serverUrl = _configuration["PluginStore:ServerUrl"];
        if (string.IsNullOrWhiteSpace(serverUrl))
            return BadRequest(new { ok = false, message = "插件商店服务未配置" });

        try
        {
            var client = _httpClientFactory.CreateClient("PluginStoreRemote");
            client.Timeout = TimeSpan.FromSeconds(30);
            // 与下载令牌一致使用 WPF 客户端通道，跳过 Referer 校验
            client.DefaultRequestHeaders.TryAddWithoutValidation("X-Ginkgo-Client", "WPF");
            var storeToken = Request.Headers["X-Store-Token"].FirstOrDefault();
            if (!string.IsNullOrEmpty(storeToken))
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", storeToken);

            var url = $"{serverUrl.TrimEnd('/')}/api/plugin-store/downloads/editions/{editionId}/available-releases";
            var resp = await client.GetAsync(url, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("获取可下载版本列表失败: {StatusCode} {Body}", resp.StatusCode, body);
                var msg = ExtractRemoteErrorMessage(body, "获取可下载版本列表失败");
                // 不透传远端 401/403：那是远端商城 license 维度的业务错误，
                // 直接回给前端会被 http.ts 的拦截器当成 admin 会话失效而清 token/跳登录页。
                // 统一收敛为 400 业务错误，由前端 ElMessage 弹出提示即可，不影响当前登录态。
                return MapRemoteStoreFailure(resp.StatusCode, msg);
            }

            // 透传远端 Result<List<AvailableReleaseDto>>
            return Content(body, "application/json");
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(503, new { ok = false, message = $"无法连接远程商城: {ex.Message}" });
        }
        catch (TaskCanceledException)
        {
            return StatusCode(503, new { ok = false, message = "连接远程商城超时" });
        }
    }

    /// <summary>
    /// 下载并安装插件。
    /// 远程商城使用两步式防盗链下载：
    /// 1. POST /api/plugin-store/downloads/token 获取一次性下载令牌
    /// 2. GET /api/plugin-store/downloads/{token} 使用令牌下载 ZIP 文件流
    /// </summary>
    [HttpPost("install")]
    [EnableRateLimiting("install")]
    public async Task<IActionResult> InstallPlugin([FromBody] InstallPluginInput input, CancellationToken ct)
    {
        // 生产环境禁止通过商店安装插件（无源码工作区，前端文件也无处存放）
        if (!IsDevelopmentEnvironment())
            return BadRequest(new { ok = false, message = "生产环境不支持在线安装插件。请在开发环境中安装后重新部署。" });

        var serverUrl = _configuration["PluginStore:ServerUrl"];
        if (string.IsNullOrWhiteSpace(serverUrl))
            return BadRequest(new { ok = false, message = "插件商店服务未配置" });

        try
        {
            var client = _httpClientFactory.CreateClient("PluginStoreRemote");
            client.Timeout = TimeSpan.FromMinutes(5);

            // 标记为客户端通道：远端商城会按 60min/10次 颁发令牌，并跳过 Referer 校验
            client.DefaultRequestHeaders.TryAddWithoutValidation("X-Ginkgo-Client", "WPF");

            var storeToken = Request.Headers["X-Store-Token"].FirstOrDefault();
            if (!string.IsNullOrEmpty(storeToken))
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", storeToken);

            // ========== 第1步：从远程商城获取下载令牌 ==========
            // 当前端传入了具体 ReleaseId（版本选择对话框场景）→ 走 token-for-release，按指定版本下发；
            // 否则走默认 token，远端会自动选取「license 升级窗口内的最新可用版本」。
            HttpResponseMessage tokenResponse;
            string tokenUrl;
            if (!string.IsNullOrWhiteSpace(input.ReleaseId))
            {
                tokenUrl = $"{serverUrl.TrimEnd('/')}/api/plugin-store/downloads/token-for-release";
                _logger.LogInformation("请求下载令牌(指定版本): URL={Url}, EditionId={EditionId}, ReleaseId={ReleaseId}",
                    tokenUrl, input.EditionId, input.ReleaseId);
                tokenResponse = await client.PostAsJsonAsync(tokenUrl, new { releaseId = input.ReleaseId });
            }
            else
            {
                tokenUrl = $"{serverUrl.TrimEnd('/')}/api/plugin-store/downloads/token";
                _logger.LogInformation("请求下载令牌: URL={Url}, EditionId={EditionId}", tokenUrl, input.EditionId);
                tokenResponse = await client.PostAsJsonAsync(tokenUrl, new { editionId = input.EditionId });
            }
            if (!tokenResponse.IsSuccessStatusCode)
            {
                var tokenErrorBody = await tokenResponse.Content.ReadAsStringAsync();
                _logger.LogWarning("获取下载令牌失败: {StatusCode} {Body}", tokenResponse.StatusCode, tokenErrorBody);
                
                var friendlyMsg = ExtractRemoteErrorMessage(tokenErrorBody, "获取下载令牌失败");
                return BadRequest(new { ok = false, message = friendlyMsg });
            }

            var tokenResultJson = await tokenResponse.Content.ReadAsStringAsync();
            _logger.LogInformation("下载令牌响应: {Body}", tokenResultJson);

            // 解析令牌响应，兼容 Result<T> 包装格式和裸对象格式
            string? downloadToken = null;
            string? downloadPath = null;
            try
            {
                var tokenDoc = JsonSerializer.Deserialize<JsonElement>(tokenResultJson);
                
                // 优先从 data.token / data.downloadUrl 提取（Result<T> 包装格式）
                if (tokenDoc.TryGetProperty("data", out var dataProp) && dataProp.ValueKind == JsonValueKind.Object)
                {
                    downloadToken = dataProp.GetProp("token");
                    downloadPath = dataProp.GetProp("downloadUrl");
                }
                // 兜底：直接从根对象提取
                if (string.IsNullOrEmpty(downloadToken))
                {
                    downloadToken = tokenDoc.GetProp("token");
                    downloadPath = tokenDoc.GetProp("downloadUrl");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "解析下载令牌响应失败: {Body}", tokenResultJson);
                return BadRequest(new { ok = false, message = "解析下载令牌失败" });
            }

            if (string.IsNullOrEmpty(downloadToken))
            {
                return BadRequest(new { ok = false, message = "远程商城未返回有效的下载令牌" });
            }

            // ========== 第2步：使用令牌下载 ZIP 文件 ==========
            // 优先使用返回的 downloadUrl，否则自行拼接
            var fileDownloadUrl = !string.IsNullOrEmpty(downloadPath)
                ? $"{serverUrl.TrimEnd('/')}{downloadPath}"
                : $"{serverUrl.TrimEnd('/')}/api/plugin-store/downloads/{downloadToken}";

            _logger.LogInformation("开始下载插件: URL={Url}", fileDownloadUrl);

            // 手动控制重定向：远端商城 ObjectStorage 模式会 302 跳转到对象存储域名（OSS/S3/COS 等），
            // 默认 AllowAutoRedirect=true 时若目标域 SSL 证书在本机不被信任，会抛 SSL 错且拿不到目标地址，
            // 不便排查。这里先关闭自动重定向，拿到 Location 后再单独发起请求，并独立报错。
            HttpResponseMessage downloadResponse;
            try
            {
                var skipSsl = string.Equals(_configuration["PluginStore:SkipSslValidation"], "true", StringComparison.OrdinalIgnoreCase);
                using var noRedirectHandler = new HttpClientHandler
                {
                    AllowAutoRedirect = false,
                    SslProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13,
                    AutomaticDecompression = System.Net.DecompressionMethods.All,
                    ServerCertificateCustomValidationCallback = skipSsl
                        ? HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                        : null
                };
                _logger.LogInformation("[PluginStore] InstallPlugin 重定向探测 handler: SkipSslValidation 配置=\"{Raw}\", 生效={Effective}",
                    _configuration["PluginStore:SkipSslValidation"] ?? "(null)", skipSsl);
                using var probeClient = new HttpClient(noRedirectHandler) { Timeout = TimeSpan.FromMinutes(5) };
                probeClient.DefaultRequestHeaders.TryAddWithoutValidation("X-Ginkgo-Client", "WPF");
                if (!string.IsNullOrEmpty(storeToken))
                    probeClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", storeToken);

                var probeResp = await probeClient.GetAsync(fileDownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
                if ((int)probeResp.StatusCode >= 300 && (int)probeResp.StatusCode < 400 && probeResp.Headers.Location != null)
                {
                    var redirectUri = probeResp.Headers.Location.IsAbsoluteUri
                        ? probeResp.Headers.Location
                        : new Uri(new Uri(serverUrl), probeResp.Headers.Location);
                    _logger.LogInformation("商城 302 重定向到对象存储: {Url}", redirectUri);
                    probeResp.Dispose();

                    try
                    {
                        downloadResponse = await client.GetAsync(redirectUri, HttpCompletionOption.ResponseHeadersRead, ct);
                    }
                    catch (HttpRequestException dlEx)
                    {
                        var inner = dlEx.InnerException?.Message ?? dlEx.Message;
                        _logger.LogError(dlEx, "对象存储下载链接 SSL/网络异常: {Url}", redirectUri);
                        return StatusCode(503, new
                        {
                            ok = false,
                            message = $"无法从对象存储下载插件包（{redirectUri.Host}）：{inner}。" +
                                      "通常原因：本机未信任目标域的 HTTPS 证书 / 网络出口无法访问该域名 / TLS 版本不兼容。" +
                                      "请将该域名的证书加入本机受信任根，或在远端商城将存储模式改为 Local 直传。"
                        });
                    }
                }
                else
                {
                    // 非 3xx，由商城直接返回内容，复用主 client
                    downloadResponse = probeResp;
                }
            }
            catch (HttpRequestException probeEx)
            {
                var inner = probeEx.InnerException?.Message ?? probeEx.Message;
                _logger.LogError(probeEx, "请求商城下载链接异常: {Url}", fileDownloadUrl);
                return StatusCode(503, new { ok = false, message = $"请求商城下载链接失败：{inner}" });
            }

            if (!downloadResponse.IsSuccessStatusCode)
            {
                var dlErrorBody = await downloadResponse.Content.ReadAsStringAsync();
                _logger.LogWarning("下载插件文件失败: {StatusCode} {Body}", downloadResponse.StatusCode, dlErrorBody);
                return BadRequest(new { ok = false, message = $"下载插件文件失败: HTTP {(int)downloadResponse.StatusCode}" });
            }

            // ========== 第3步：使用 ModuleUploadService 验证并安装（与本地安装流程完全一致） ==========
            using var stream = await downloadResponse.Content.ReadAsStreamAsync();
            var fileName = $"{input.PluginId}.gmod.zip";

            _logger.LogInformation("开始验证插件包: FileName={FileName}", fileName);
            var validation = await _uploadService.UploadAndValidateAsync(stream, fileName, ct);
            if (!validation.IsValid)
            {
                return BadRequest(new { ok = false, message = $"插件包验证失败: {validation.ErrorMessage}" });
            }

            // ========== 第3.5步：拉取并校验 license.lic（阶段 C，仅对需要授权的插件）==========
            // 注意：免费插件通常没有授权流程，远端 /downloads/license 端点对此返回 404（nginx 直接拦掉），
            // 此时按"无授权要求"处理并继续安装；仅在远端返回 2xx 时才严格校验签名。
            // 非 404 的失败（401/403/5xx 等）仍视为致命错误，避免漏掉真实配置/网络问题。
            try
            {
                var licenseUrl = $"{serverUrl.TrimEnd('/')}/api/plugin-store/downloads/license";
                var fingerprint = MachineFingerprintProvider.Get(_env.ContentRootPath);
                var domain = Request.Host.Host?.ToLowerInvariant();
                var displayName = Environment.MachineName;
                var licenseReqBody = new
                {
                    token = downloadToken,
                    domain,
                    machineFingerprint = fingerprint,
                    displayName
                };
                var licenseResp = await client.PostAsJsonAsync(licenseUrl, licenseReqBody, ct);
                if (licenseResp.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    // 远端未部署该端点 / 该插件不需要授权（免费插件常见路径）
                    _logger.LogInformation("远端未提供该插件的授权文件（404），按免费/无授权要求继续安装: PluginId={PluginId}", input.PluginId);
                    validation.SecurityWarnings.Add("此插件未携带 license.lic 授权文件（远端返回 404，通常为免费插件）");
                }
                else if (!licenseResp.IsSuccessStatusCode)
                {
                    var errBody = await licenseResp.Content.ReadAsStringAsync(ct);
                    var msg = ExtractRemoteErrorMessage(errBody, "获取授权文件失败");
                    _logger.LogWarning("获取 license.lic 失败: {Status} {Body}", licenseResp.StatusCode, errBody);
                    return BadRequest(new { ok = false, message = $"授权获取失败: {msg}" });
                }
                else
                {
                    var licenseBytes = await licenseResp.Content.ReadAsByteArrayAsync(ct);
                    var verify = _licenseVerifier.Verify(
                        licenseBytes,
                        expectedModuleId: validation.Manifest?.Id ?? input.PluginId,
                        currentDomain: domain,
                        currentMachineFingerprint: fingerprint);

                    if (!verify.IsValid)
                    {
                        _logger.LogWarning("license.lic 校验失败: {Err}", verify.ErrorMessage);
                        return BadRequest(new { ok = false, message = $"授权校验失败: {verify.ErrorMessage}" });
                    }
                    validation.LicenseFileBytes = licenseBytes;
                    validation.LicenseValidation = verify;
                    if (verify.Warnings.Count > 0)
                        validation.SecurityWarnings.AddRange(verify.Warnings);

                    _logger.LogInformation("license.lic 校验通过: LicenseKey={Key}, Activation={Aid}",
                        verify.File?.Payload.LicenseKey, verify.File?.Payload.ActivationId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "拉取/校验 license.lic 异常");
                return BadRequest(new { ok = false, message = $"授权链路异常: {ex.Message}" });
            }

            // 记录安全审计（签名/哈希警告）
            try
            {
                var auditSvc = HttpContext.RequestServices.GetService<ModuleSecurityAuditService>();
                auditSvc?.AuditUploadValidation(validation.Manifest?.Id ?? input.PluginId, validation);
            }
            catch { }

            _logger.LogInformation("开始安装插件: ModuleId={ModuleId}, Version={Version}", 
                validation.Manifest?.Id, validation.Manifest?.Version);
            var installResult = await _uploadService.InstallModuleAsync(validation, ct);
            if (installResult.Ok)
            {
                return Ok(new
                {
                    ok = true,
                    message = "插件安装完成，请重启系统以加载新模块",
                    moduleId = installResult.ModuleId,
                    version = installResult.Version,
                    steps = installResult.ExecutedSteps,
                    // 安全信息（供前端展示能力告警）
                    security = new
                    {
                        hashValid = validation.HashValidation?.IsValid ?? true,
                        signatureValid = validation.SignatureValidation?.IsValid ?? true,
                        signaturePublisher = validation.SignatureValidation?.MatchedPublisher,
                        capabilities = validation.Manifest?.Capabilities,
                        warnings = validation.SecurityWarnings
                    }
                });
            }

            return BadRequest(new { ok = false, message = installResult.Message });
        }
        catch (HttpRequestException ex)
        {
            var detail = BuildExceptionChainDetail(ex);
            _logger.LogError(ex, "安装插件网络异常: PluginId={PluginId}, ExceptionChain={Detail}", input.PluginId, detail);
            return StatusCode(503, new { ok = false, message = $"无法连接远程商城: {detail}" });
        }
        catch (TaskCanceledException)
        {
            return StatusCode(503, new { ok = false, message = "连接远程商城超时" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "安装插件失败: {PluginId}", input.PluginId);
            return StatusCode(500, new { ok = false, message = $"安装失败: {ex.Message}" });
        }
    }

    /// <summary>
    /// 递归收集异常链上每一层的 GetType().Name 与 Message，便于排查 SSL/网络底层失败原因。
    /// </summary>
    private static string BuildExceptionChainDetail(Exception ex)
    {
        var parts = new List<string>();
        Exception? current = ex;
        var depth = 0;
        while (current != null && depth < 6)
        {
            parts.Add($"{current.GetType().Name}: {current.Message}");
            current = current.InnerException;
            depth++;
        }
        return string.Join(" -> ", parts);
    }

    /// <summary>
    /// 检查指定模块是否存在可升级版本（代理远程商城 /api/plugin-store/upgrade/check）。
    /// 需要携带 X-Store-Token 以便商城识别当前账号的 License。
    /// </summary>
    [HttpGet("upgrade-check")]
    public async Task<IActionResult> CheckUpgrade([FromQuery] string moduleId, [FromQuery] string? currentVersion, CancellationToken ct)
    {
        var serverUrl = _configuration["PluginStore:ServerUrl"];
        if (string.IsNullOrWhiteSpace(serverUrl))
            return BadRequest(new { ok = false, message = "插件商店服务未配置" });
        if (string.IsNullOrWhiteSpace(moduleId))
            return BadRequest(new { ok = false, message = "moduleId 不能为空" });

        try
        {
            var client = _httpClientFactory.CreateClient("PluginStoreRemote");
            client.Timeout = TimeSpan.FromSeconds(30);
            var storeToken = Request.Headers["X-Store-Token"].FirstOrDefault();
            if (!string.IsNullOrEmpty(storeToken))
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", storeToken);

            var url = $"{serverUrl.TrimEnd('/')}/api/plugin-store/upgrade/check?moduleId={Uri.EscapeDataString(moduleId)}";
            if (!string.IsNullOrWhiteSpace(currentVersion))
                url += $"&currentVersion={Uri.EscapeDataString(currentVersion)}";
            var resp = await client.GetAsync(url, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("升级检查失败: {Status} {Body}", resp.StatusCode, body);
                return BadRequest(new { ok = false, message = ExtractRemoteErrorMessage(body, "升级检查失败") });
            }
            // 透传商城原始 Result<T>，前端直接消费
            return Content(body, "application/json");
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(503, new { ok = false, message = $"无法连接远程商城: {ex.Message}" });
        }
        catch (TaskCanceledException)
        {
            return StatusCode(503, new { ok = false, message = "连接远程商城超时" });
        }
    }

    /// <summary>
    /// 校验 orderNo 格式，防止路径注入（Path Traversal）攻击。
    /// 合法的订单号仅由字母、数字、连字符、下划线组成，长度 1-64。
    /// </summary>
    private static bool ValidateOrderNo(string? orderNo)
    {
        if (string.IsNullOrWhiteSpace(orderNo) || orderNo.Length > 64)
            return false;
        return System.Text.RegularExpressions.Regex.IsMatch(orderNo, @"^[a-zA-Z0-9_\-]+$");
    }
}

/// <summary>
/// 安装插件输入。
/// <para>
/// <c>ReleaseId</c> 可选：传入则按「版本选择对话框」语义走远端 <c>/downloads/token-for-release</c>，
/// 远端会再次校验该 release 是否落在用户 license 的升级窗口内（或为关键安全版本）；
/// 不传则走默认 <c>/downloads/token</c>，由远端自动选取窗口内的最新可用版本。
/// </para>
/// </summary>
public record InstallPluginInput(string PluginId, string EditionId, string? ReleaseId = null);

/// <summary>
/// 购买插件输入
/// </summary>
public record PurchasePluginInput(string PluginId, string EditionId, string? ChannelType);

/// <summary>
/// 插件商城登录输入
/// </summary>
public record StoreLoginInput(string UserName, string Password, string? ClientType);

/// <summary>
/// 远端商城验证码生成输入
/// </summary>
public record StoreCaptchaGenerateInput(string? Type);

/// <summary>
/// 远端商城验证码校验输入
/// </summary>
public record StoreCaptchaValidateInput(string ChallengeId, string? Payload);

/// <summary>
/// JsonElement 辅助扩展
/// </summary>
static file class JsonElementExtensions
{
    public static string? GetProp(this JsonElement el, string name)
        => el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}

