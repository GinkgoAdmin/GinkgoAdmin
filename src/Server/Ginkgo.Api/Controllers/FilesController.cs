using System.Net;
using System.Net.Sockets;
using Ginkgo.Application.Files;
using Ginkgo.Domain;
using Ginkgo.Domain.Files;
using Ginkgo.Infrastructure.Runtime;
using Ginkgo.Infrastructure.Storage;
using Ginkgo.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ginkgo.Api.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/files")]
[ApiVersion("1.0")]
[Authorize]
public sealed class FilesController : ControllerBase
{
    private readonly IFileAppService _service;
    private readonly IFileStorageProvider _storage;

    public FilesController(
        IFileAppService service,
        IFileStorageProvider storage)
    {
        _service = service;
        _storage = storage;
    }

    [HttpGet]
    public async Task<Result<PagedResult<FileListItemDto>>> GetAsync(
        [FromQuery] PageRequest request,
        [FromQuery] string? type,
        [FromQuery] string? filter,
        [FromQuery] string? dataScope,
        [FromServices] Ginkgo.Domain.IRepository<Ginkgo.Domain.Files.SysFile> fileRepo,
        [FromServices] Ginkgo.Domain.IRepository<Ginkgo.Domain.Settings.Setting> settingsRepo)
    {
        // 获取当前用户信息
        long? me = null;
        var uid = User?.Claims.FirstOrDefault(c => c.Type.EndsWith("/nameidentifier") || c.Type.EndsWith("/sub"))?.Value;
        if (long.TryParse(uid, out var gid)) me = gid;
        var isAdmin = User?.IsInRole("ADMIN") == true || User?.IsInRole("admin") == true;

        // 解析 filter
        string? userName = null;
        DateTime? from = null;
        DateTime? to = null;
        if (!string.IsNullOrWhiteSpace(filter))
        {
            try
            {
                var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, System.Text.Json.JsonElement>>(filter);
                if (dict != null)
                {
                    if (dict.TryGetValue("userName", out var un) && un.ValueKind != System.Text.Json.JsonValueKind.Null)
                        userName = un.GetString();
                    if (dict.TryGetValue("dateRange", out var dr) && dr.ValueKind == System.Text.Json.JsonValueKind.Array && dr.GetArrayLength() == 2)
                    {
                        var s = dr[0].GetString();
                        var e = dr[1].GetString();
                        if (DateTime.TryParse(s, out var sdt)) from = sdt;
                        if (DateTime.TryParse(e, out var edt)) to = edt;
                    }
                }
            }
            catch { }
        }

        // ADMIN 角色不受数据范围限制，查看所有附件
        if (isAdmin)
        {
            var data = await _service.GetPagedAsync(request, type, null, userName, from, to);
            return Result<PagedResult<FileListItemDto>>.Success(data);
        }

        // 非 ADMIN 用户：根据 dataScope 参数或系统配置决定数据范围
        // dataScope 接受值（兼容历史）：Self/OwnOnly、Dept/DepartmentOnly、DeptAndChildren/DepartmentAndChildren、All
        var scope = dataScope;
        if (string.IsNullOrWhiteSpace(scope))
        {
            // 从系统配置读取默认数据范围
            var scopeSetting = settingsRepo.Query().FirstOrDefault(s => s.Key == "DataPermission.DefaultScope");
            scope = scopeSetting?.Value ?? "OwnOnly";
        }
        // 规范化 enum：把所有别名归一到框架统一名称（OwnOnly/DepartmentOnly/DepartmentAndChildren/All）
        var normalizedScope = Ginkgo.Infrastructure.Persistence.DataScopeProvider
            .NormalizeScope(scope, Ginkgo.Infrastructure.Persistence.DataScopeType.OwnOnly);

        if (normalizedScope == Ginkgo.Infrastructure.Persistence.DataScopeType.All)
        {
            // 非 ADMIN 用户不允许 dataScope=All，强制降级为 OwnOnly
            var data = await _service.GetPagedAsync(request, type, me, userName, from, to);
            return Result<PagedResult<FileListItemDto>>.Success(data);
        }

        if (normalizedScope == Ginkgo.Infrastructure.Persistence.DataScopeType.OwnOnly)
        {
            // 仅本人
            var data = await _service.GetPagedAsync(request, type, me, userName, from, to);
            return Result<PagedResult<FileListItemDto>>.Success(data);
        }

        // DepartmentOnly / DepartmentAndChildren / SpecifiedDepartments / Custom：通过 IUserDepartmentRepository 查询用户所属部门
        var userDeptRepo = HttpContext.RequestServices.GetRequiredService<Ginkgo.Domain.Users.IUserDepartmentRepository>();
        var myDeptIds = me != null
            ? await userDeptRepo.GetDepartmentIdsAsync(me.Value)
            : new List<long>();

        if (myDeptIds.Count == 0)
        {
            // 用户没有部门，回退到仅本人
            var data = await _service.GetPagedAsync(request, type, me, userName, from, to);
            return Result<PagedResult<FileListItemDto>>.Success(data);
        }

        // 构建部门ID集合
        var deptIds = new HashSet<long>(myDeptIds);

        if (normalizedScope == Ginkgo.Infrastructure.Persistence.DataScopeType.DepartmentAndChildren)
        {
            // 查询子部门（BFS）
            var deptRepo = HttpContext.RequestServices.GetRequiredService<Ginkgo.Domain.IRepository<Ginkgo.Domain.Departments.Department>>();
            var allDepts = deptRepo.Query().Select(d => new { d.Id, d.ParentId }).ToList();
            var queue = new Queue<long>(myDeptIds);
            while (queue.Count > 0)
            {
                var parentId = queue.Dequeue();
                foreach (var child in allDepts.Where(d => d.ParentId == parentId))
                {
                    if (deptIds.Add(child.Id))
                    {
                        queue.Enqueue(child.Id);
                    }
                }
            }
        }

        // 获取这些部门下的所有用户ID，用于按 CreatedBy 过滤
        var deptIdList = deptIds.ToList();
        var deptUserIds = await userDeptRepo.GetUserIdsByDepartmentIdsAsync(deptIdList);
        var deptUserIdSet = new HashSet<long>(deptUserIds);
        // 确保当前用户也在集合中
        if (me != null) deptUserIdSet.Add(me.Value);
        var allUserIds = deptUserIdSet.ToList();

        // 按部门过滤文件：CreatedBy 属于部门用户 OR DepartmentId 属于部门集合
        var page = request.Page <= 0 ? 1 : request.Page;
        var size = request.PageSize <= 0 ? 20 : request.PageSize;
        var q = fileRepo.Query().Where(x => !x.IsDeleted);
        if (!string.IsNullOrWhiteSpace(type)) q = q.Where(x => x.Type == type);
        if (from != null) q = q.Where(x => x.CreatedAt >= from);
        if (to != null) q = q.Where(x => x.CreatedAt <= to);

        q = q.Where(x =>
            (x.CreatedBy != null && allUserIds.Contains(x.CreatedBy.Value))
            || (x.DepartmentId != null && deptIdList.Contains(x.DepartmentId.Value)));

        var total = q.LongCount();
        var items = q.OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(x => new FileListItemDto
            {
                Id = x.Id,
                FileName = x.FileName,
                ContentType = x.ContentType,
                Size = x.Size,
                StorageProvider = x.StorageProvider,
                Url = x.Url,
                DownloadUrl = x.Url,
                Type = x.Type,
                CreatedAt = x.CreatedAt,
                CreatedBy = x.CreatedBy
            }).ToList();

        return Result<PagedResult<FileListItemDto>>.Success(new PagedResult<FileListItemDto>
        {
            Total = total,
            Page = page,
            PageSize = size,
            Items = items
        });
    }

    [HttpGet("{id}")]
    public async Task<Result<FileDetailDto?>> GetByIdAsync(long id)
    {
        var data = await _service.GetAsync(id);
        if (data == null) return Result<FileDetailDto?>.Fail(404, "文件不存在");

        // 非 ADMIN 用户只能查看自己上传的文件详情
        var isAdmin = User?.IsInRole("ADMIN") == true || User?.IsInRole("admin") == true;
        if (!isAdmin)
        {
            long? me = null;
            var uid = User?.Claims.FirstOrDefault(c => c.Type.EndsWith("/nameidentifier") || c.Type.EndsWith("/sub"))?.Value;
            if (long.TryParse(uid, out var gid)) me = gid;
            if (data.CreatedBy != me)
            {
                return Result<FileDetailDto?>.Fail(403, "无权查看此文件详情");
            }
        }

        return Result<FileDetailDto?>.Success(data);
    }

    public sealed class FileUploadForm
    {
        public List<IFormFile>? files { get; set; }
        public IFormFile? file { get; set; }
        public string? type { get; set; }
        public string? tags { get; set; }
    }

    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(long.MaxValue)]
    public async Task<Result<List<long>>> UploadAsync([FromForm] FileUploadForm form)
    {
        var files = form.files ?? (form.file != null ? new List<IFormFile> { form.file } : new List<IFormFile>());
        if (files.Count == 0) return Result<List<long>>.Fail(400, "未选择文件");
        long? userId = null;
        var uid = User?.Claims.FirstOrDefault(c => c.Type.EndsWith("/nameidentifier") || c.Type.EndsWith("/sub"))?.Value;
        if (long.TryParse(uid, out var gid)) userId = gid;

        var ids = new List<long>(files.Count);
        foreach (var f in files)
        {
            await using var s = f.OpenReadStream();
            var id = await _service.CreateAsync(new UploadFileInput
            {
                FileName = f.FileName,
                ContentType = f.ContentType,
                Size = f.Length,
                Content = s,
                Type = string.IsNullOrWhiteSpace(form.type) ? "default" : form.type,
                Tags = form.tags
            }, userId);
            ids.Add(id);
        }

        return Result<List<long>>.Success(ids, $"上传成功，共 {ids.Count} 个");
    }

    /// <summary>
    /// 按文件 Id 读取内容。需要登录。
    /// 一般资源（图片、LOGO 等）建议通过 /uploads/ 静态文件路径直接访问，无需调用此端点。
    /// </summary>
    [HttpGet("{id}/content")]
    public async Task<IActionResult> DownloadAsync(long id,
        [FromServices] IRepository<SysFile> repo,
        [FromServices] ISwitcher<IFileStorageProvider> storageSwitcher)
    {
        var f = await repo.GetByIdAsync(id);
        if (f == null) return NotFound();
        return await ServeFileContentAsync(f, storageSwitcher);
    }

    /// <summary>
    /// 按文件 Id 下载附件。需要登录，通过 access_token 查询参数或 Authorization 头鉴权。
    /// </summary>
    [HttpGet("{id}/download")]
    public async Task<IActionResult> DownloadAttachmentAsync(long id,
        [FromServices] IRepository<SysFile> repo,
        [FromServices] ISwitcher<IFileStorageProvider> storageSwitcher)
    {
        var f = await repo.GetByIdAsync(id);
        if (f == null) return NotFound();
        return await ServeFileContentAsync(f, storageSwitcher, f.FileName);
    }

    // ---------- 私有方法：统一文件内容输出 ----------

    /// <summary>
    /// 读取 SysFile 的物理内容并返回给客户端。
    /// 支持本地存储直读、OSS 302 重定向、兜底流式读取三种模式。
    /// </summary>
    /// <param name="file">文件实体</param>
    /// <param name="storageSwitcher">存储切换器</param>
    /// <param name="downloadFileName">如非 null，则作为附件下载（Content-Disposition: attachment）</param>
    private async Task<IActionResult> ServeFileContentAsync(
        SysFile file,
        ISwitcher<IFileStorageProvider> storageSwitcher,
        string? downloadFileName = null)
    {
        var isLocal = string.IsNullOrWhiteSpace(file.StorageProvider)
            || file.StorageProvider.Contains("Local", StringComparison.OrdinalIgnoreCase);

        // 仅在下载模式（downloadFileName != null）时，非本地文件做 302 重定向到 CDN/OSS 直链（节省带宽）。
        // 预览/内容模式（downloadFileName == null）直接代理内容流，保证内嵌预览不依赖 CDN 可达性。
        if (!isLocal && downloadFileName != null && !string.IsNullOrWhiteSpace(file.Url))
        {
            if (Uri.TryCreate(file.Url, UriKind.Absolute, out var parsedUri))
                return Redirect(parsedUri.AbsoluteUri);

            var current = storageSwitcher.Current;
            if (current is IPublicUrlProvider pub && !string.IsNullOrWhiteSpace(pub.PublicBaseUrl))
            {
                var fullUrl = $"{pub.PublicBaseUrl.TrimEnd('/')}/{file.Url.TrimStart('/')}";
                if (Uri.TryCreate(fullUrl, UriKind.Absolute, out var fullUri))
                    return Redirect(fullUri.AbsoluteUri);
            }
        }

        // 本地文件：直接从磁盘读取
        if (isLocal)
        {
            var webRoot = ((Microsoft.AspNetCore.Hosting.IWebHostEnvironment)HttpContext.RequestServices
                .GetRequiredService(typeof(Microsoft.AspNetCore.Hosting.IWebHostEnvironment))).WebRootPath
                ?? Path.Combine(AppContext.BaseDirectory, "wwwroot");
            var localPath = !string.IsNullOrWhiteSpace(file.Url) && file.Url.StartsWith("/uploads/")
                ? Path.Combine(webRoot, file.Url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar))
                : Path.Combine(webRoot, "uploads", (file.StoragePath ?? file.Url ?? "").Replace('/', Path.DirectorySeparatorChar));
            if (System.IO.File.Exists(localPath))
            {
                var stream = System.IO.File.OpenRead(localPath);
                return downloadFileName != null
                    ? File(stream, file.ContentType ?? "application/octet-stream", fileDownloadName: downloadFileName, enableRangeProcessing: true)
                    : File(stream, file.ContentType ?? "application/octet-stream", enableRangeProcessing: true);
            }
        }

        // 兜底：通过当前存储提供者读取
        var fallbackStream = await _storage.OpenReadAsync(file.StoragePath!);
        return downloadFileName != null
            ? File(fallbackStream, file.ContentType ?? "application/octet-stream", fileDownloadName: downloadFileName, enableRangeProcessing: true)
            : File(fallbackStream, file.ContentType ?? "application/octet-stream", enableRangeProcessing: true);
    }

    /// <summary>
    /// 删除文件（含物理文件删除）。
    /// 普通用户只能删除自己上传的文件，管理员可以删除任何文件。
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<Result> DeleteAsync(long id)
    {
        long? currentUserId = null;
        var uid = User?.Claims.FirstOrDefault(c => c.Type.EndsWith("/nameidentifier") || c.Type.EndsWith("/sub"))?.Value;
        if (long.TryParse(uid, out var gid)) currentUserId = gid;
        var isAdmin = User?.IsInRole("ADMIN") == true || User?.IsInRole("admin") == true;

        var ok = await _service.DeleteAsync(id, currentUserId, isAdmin);
        return ok ? Result.Success("删除成功") : Result.Fail(404, "文件不存在或无权删除");
    }

    /// <summary>
    /// 图片代理接口，用于前端导出 PDF 时绕过浏览器跨域限制获取 OSS 等外链图片
    /// </summary>
    [HttpGet("proxy")]
    [AllowAnonymous]
    public async Task<IActionResult> ProxyImageAsync(
        [FromQuery] string url,
        [FromServices] IHttpClientFactory httpClientFactory)
    {
        if (string.IsNullOrWhiteSpace(url))
            return BadRequest("url 参数不能为空");

        // 只允许 http/https 协议
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != "http" && uri.Scheme != "https"))
            return BadRequest("仅支持 http/https 协议的图片 URL");

        // 第一次校验：对主机名做 DNS 预解析，拦截内网 / 回环 / 云元数据地址
        if (IsBlockedHost(uri.Host))
            return BadRequest("目标地址不允许访问");

        IPAddress[] addresses;
        try
        {
            addresses = await Dns.GetHostAddressesAsync(uri.Host);
        }
        catch
        {
            return BadRequest("无法解析目标主机");
        }

        if (addresses.Length == 0)
            return BadRequest("无法解析目标主机");

        foreach (var addr in addresses)
        {
            if (IsBlockedIpAddress(addr))
                return BadRequest("目标地址不允许访问");
        }

        try
        {
            // 使用带连接回调的 HttpClient，在 TCP 连接建立前二次校验实际 IP，防止 DNS Rebinding
            using var handler = new SocketsHttpHandler
            {
                ConnectCallback = async (context, cancellationToken) =>
                {
                    var host = context.DnsEndPoint.Host;
                    var port = context.DnsEndPoint.Port;
                    var resolved = await Dns.GetHostAddressesAsync(host, cancellationToken);
                    foreach (var ip in resolved)
                    {
                        if (IsBlockedIpAddress(ip))
                            throw new InvalidOperationException("目标地址不允许访问");
                    }
                    // 只连接到安全的 IP
                    var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
                    try
                    {
                        await socket.ConnectAsync(resolved, port, cancellationToken);
                        return new NetworkStream(socket, ownsSocket: true);
                    }
                    catch
                    {
                        socket.Dispose();
                        throw;
                    }
                },
                PooledConnectionLifetime = TimeSpan.Zero // 不复用连接，确保每次都触发回调校验
            };

            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };
            // 禁止自动跟随重定向，防止通过 30x 跳转绕过校验
            handler.AllowAutoRedirect = false;

            var response = await client.GetAsync(uri);
            if (!response.IsSuccessStatusCode)
                return StatusCode((int)response.StatusCode, "上游图片请求失败");

            var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/png";
            var bytes = await response.Content.ReadAsByteArrayAsync();
            return File(bytes, contentType);
        }
        catch (InvalidOperationException ex) when (ex.Message == "目标地址不允许访问")
        {
            return BadRequest("目标地址不允许访问");
        }
        catch
        {
            return StatusCode(502, "代理请求失败");
        }
    }

    /// <summary>
    /// 判断主机名是否属于禁止访问的地址（回环、云元数据等已知危险主机名）
    /// </summary>
    private static bool IsBlockedHost(string host)
    {
        // 回环地址
        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
            return true;

        // 云厂商元数据地址
        if (string.Equals(host, "169.254.169.254", StringComparison.Ordinal))
            return true;
        if (host.EndsWith(".internal", StringComparison.OrdinalIgnoreCase))
            return true;
        if (string.Equals(host, "metadata.google.internal", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    /// <summary>
    /// 判断 IP 地址是否属于禁止访问的内网 / 回环 / 链路本地 / 保留地址段
    /// </summary>
    private static bool IsBlockedIpAddress(IPAddress address)
    {
        // IPv6 映射的 IPv4 地址，先转换后再检查
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        // 回环地址 (127.0.0.0/8, ::1)
        if (IPAddress.IsLoopback(address))
            return true;

        // 链路本地 (169.254.0.0/16, fe80::/10) —— 包含云元数据 169.254.169.254
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            // 10.0.0.0/8 — A 类私有
            if (bytes[0] == 10)
                return true;
            // 172.16.0.0/12 — B 类私有
            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                return true;
            // 192.168.0.0/16 — C 类私有
            if (bytes[0] == 192 && bytes[1] == 168)
                return true;
            // 169.254.0.0/16 — 链路本地（含云元数据）
            if (bytes[0] == 169 && bytes[1] == 254)
                return true;
            // 0.0.0.0/8
            if (bytes[0] == 0)
                return true;
            // 100.64.0.0/10 — CGN 共享地址
            if (bytes[0] == 100 && bytes[1] >= 64 && bytes[1] <= 127)
                return true;
            // 198.18.0.0/15 — 基准测试
            if (bytes[0] == 198 && (bytes[1] == 18 || bytes[1] == 19))
                return true;
        }
        else if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal)
                return true;
            // ULA fc00::/7
            var v6Bytes = address.GetAddressBytes();
            if ((v6Bytes[0] & 0xFE) == 0xFC)
                return true;
        }

        return false;
    }

    public sealed class BatchMoveInput
    {
        public List<long>? Ids { get; set; }
        public string? TargetProvider { get; set; }
    }

    public sealed class BatchDeleteInput
    {
        public List<long>? Ids { get; set; }
    }

    /// <summary>
    /// 批量迁移文件到目标存储区块。
    /// </summary>
    [HttpPost("batch-move")]
    public async Task<Result> BatchMoveAsync([FromBody] BatchMoveInput input)
    {
        if (input?.Ids == null || input.Ids.Count == 0)
            return Result.Fail(400, "未选择文件");
        if (string.IsNullOrWhiteSpace(input.TargetProvider))
            return Result.Fail(400, "未指定目标存储区块");

        long? userId = null;
        var uid = User?.Claims.FirstOrDefault(c => c.Type.EndsWith("/nameidentifier") || c.Type.EndsWith("/sub"))?.Value;
        if (long.TryParse(uid, out var gid)) userId = gid;

        try
        {
            var moved = await _service.BatchMoveAsync(input.Ids, input.TargetProvider, userId);
            return Result.Success($"成功迁移 {moved} 个文件");
        }
        catch (Exception ex)
        {
            return Result.Fail(500, $"批量迁移失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 批量删除文件。
    /// </summary>
    [HttpPost("batch-delete")]
    public async Task<Result> BatchDeleteAsync([FromBody] BatchDeleteInput input,
        [FromServices] Ginkgo.Domain.IRepository<Ginkgo.Domain.Files.SysFile> repo)
    {
        if (input?.Ids == null || input.Ids.Count == 0)
            return Result.Fail(400, "未选择文件");

        long? userId = null;
        var uid = User?.Claims.FirstOrDefault(c => c.Type.EndsWith("/nameidentifier") || c.Type.EndsWith("/sub"))?.Value;
        if (long.TryParse(uid, out var gid)) userId = gid;
        var isAdmin = User?.IsInRole("ADMIN") == true || User?.IsInRole("admin") == true;

        try
        {
            var deleted = await _service.BatchDeleteAsync(input.Ids, userId, isAdmin);
            return Result.Success($"成功删除 {deleted} 个文件");
        }
        catch (Exception ex)
        {
            return Result.Fail(500, $"批量删除失败：{ex.Message}");
        }
    }
}


