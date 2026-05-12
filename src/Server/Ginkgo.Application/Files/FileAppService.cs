using System.Security.Cryptography;
using System.Text;
using Ginkgo.Domain;
using Ginkgo.Domain.Files;
using Ginkgo.Domain.Settings;
using Ginkgo.Domain.Users;
using Microsoft.Extensions.Configuration;
using Ginkgo.Infrastructure.Storage;
using Ginkgo.Infrastructure.Runtime;
using Ginkgo.Shared;
using SkiaSharp;

namespace Ginkgo.Application.Files;

public sealed class FileAppService : IFileAppService
{
    private readonly IRepository<SysFile> _repo;
    private readonly IRepository<User> _userRepo;
    private readonly IRepository<Setting> _settings;
    private readonly IFileStorageProvider _storage; // 仅用于生成公网URL（若实现了 IPublicUrlProvider）
    private readonly ISwitcher<IFileStorageProvider> _storageSwitcher; // 通过 Switcher 访问当前真实提供者

    private readonly IFileDomainService _domain;
    private readonly IConfiguration _config;

    public FileAppService(IRepository<SysFile> repo, IRepository<User> userRepo, IRepository<Setting> settings, IFileStorageProvider storage, ISwitcher<IFileStorageProvider> storageSwitcher, IFileDomainService domain, IConfiguration config)
    {
        _repo = repo;
        _userRepo = userRepo;
        _settings = settings;
        _storage = storage;
        _storageSwitcher = storageSwitcher;
        _domain = domain;
        _config = config;
    }

    public Task<PagedResult<FileListItemDto>> GetPagedAsync(PageRequest request, string? type, long? ownerId = null, string? userName = null, DateTime? from = null, DateTime? to = null, CancellationToken ct = default)
    {
        var page = request.Page <= 0 ? 1 : request.Page;
        var size = request.PageSize <= 0 ? 20 : request.PageSize;
        var q = _repo.Query();
        if (!string.IsNullOrWhiteSpace(type)) q = q.Where(x => x.Type == type);
        // OwnerId 已弃用，统一使用审计字段 CreatedBy 控制数据范围/权限
        if (ownerId != null) q = q.Where(x => x.CreatedBy == ownerId);
        if (from != null) q = q.Where(x => x.CreatedAt >= from);
        if (to != null) q = q.Where(x => x.CreatedAt <= to);
        
        // 如果需要按用户名筛选，先获取匹配的用户ID
        List<long>? matchedUserIds = null;
        if (!string.IsNullOrWhiteSpace(userName))
        {
            var kw = userName.Trim();
            matchedUserIds = _userRepo.Query()
                .Where(u => (u.UserName != null && u.UserName.Contains(kw)) || 
                            (u.DisplayName != null && u.DisplayName.Contains(kw)))
                .Select(u => u.Id)
                .ToList();
            if (matchedUserIds.Any())
                q = q.Where(x => x.CreatedBy != null && matchedUserIds.Contains(x.CreatedBy.Value));
            else
                q = q.Where(x => false); // 没有匹配用户则返回空
        }
        
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

        // 批量加载用户信息
        var userIds = items.Where(x => x.CreatedBy != null).Select(x => x.CreatedBy!.Value).Distinct().ToList();
        if (userIds.Any())
        {
            var usersData = _userRepo.Query().Where(u => userIds.Contains(u.Id))
                .Select(u => new { u.Id, u.UserName, u.DisplayName })
                .ToList();
            var users = usersData.ToDictionary(u => u.Id);

            foreach (var item in items)
            {
                if (item.CreatedBy != null && users.TryGetValue(item.CreatedBy.Value, out var user))
                {
                    item.UserName = user.UserName;
                    item.DisplayName = user.DisplayName;
                }
            }
        }

        return Task.FromResult(new PagedResult<FileListItemDto>
        {
            Total = total,
            Page = page,
            PageSize = size,
            Items = items
        });
    }

    public Task<FileDetailDto?> GetAsync(long id, CancellationToken ct = default)
    {
        var x = _repo.Query().Where(x => x.Id == id).Select(x => new FileDetailDto
        {
            Id = x.Id,
            FileName = x.FileName,
            ContentType = x.ContentType,
            Size = x.Size,
            Hash = x.Hash,
            StorageProvider = x.StorageProvider,
            StoragePath = x.StoragePath,
            Url = x.Url,
            DownloadUrl = x.Url,
            OwnerId = x.OwnerId,
            Tags = x.Tags,
            Version = x.Version,
            Type = x.Type,
            DepartmentId = x.DepartmentId,
            CreatedAt = x.CreatedAt,
            CreatedBy = x.CreatedBy
        }).FirstOrDefault();
        return Task.FromResult(x);
    }

    public async Task<long> CreateAsync(UploadFileInput input, long? operatorId, CancellationToken ct = default)
    {
        // 读取配置：大小与扩展名
        var maxMb = int.TryParse(_settings.Query().FirstOrDefault(s => s.Key == "Upload.MaxSizeMB")?.Value, out var m) ? m : 20;
        var rawAllowed = _settings.Query().FirstOrDefault(s => s.Key == "Upload.AllowedExtensions")?.Value;
        var defaultAllowed = new[] { ".jpg", ".png", ".jpeg", ".gif", ".webp", ".pdf", ".docx", ".mp3", ".mp4" };
        string[] allowed;
        if (string.IsNullOrWhiteSpace(rawAllowed))
        {
            allowed = defaultAllowed;
        }
        else
        {
            try
            {
                allowed = System.Text.Json.JsonSerializer.Deserialize<string[]>(rawAllowed!) ?? defaultAllowed;
            }
            catch
            {
                allowed = rawAllowed!
                    .Split(new[] { ',', ';', ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.StartsWith('.') ? x.ToLowerInvariant() : ($".{x.ToLowerInvariant()}"))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                if (allowed.Length == 0) allowed = defaultAllowed;
            }
        }

        if (input.Size > maxMb * 1024L * 1024L)
            throw new InvalidOperationException($"文件过大，超过 {maxMb} MB 限制。");

        var ext = Path.GetExtension(input.FileName)?.ToLowerInvariant() ?? string.Empty;
        if (allowed.Length > 0 && !allowed.Contains(ext, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException("不允许的文件类型。");

        // 使用内存缓冲，避免底层提供者在保存时处置原始流导致后续再访问抛出异常
        input.Content.Position = 0;
        using var buffered = new MemoryStream();
        await input.Content.CopyToAsync(buffered, ct);
        buffered.Position = 0;
        // 合理推断 ContentType
        var contentType = input.ContentType;
        var extLower = (Path.GetExtension(input.FileName) ?? string.Empty).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(contentType))
        {
            contentType = extLower switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".bmp" => "image/bmp",
                ".mp3" => "audio/mpeg",
                ".wav" => "audio/wav",
                ".wma" => "audio/x-ms-wma",
                ".mp4" => "video/mp4",
                ".webm" => "video/webm",
                ".ogg" => "video/ogg",
                ".avi" => "video/x-msvideo",
                ".mov" => "video/quicktime",
                ".mkv" => "video/x-matroska",
                ".pdf" => "application/pdf",
                _ => "application/octet-stream"
            };
        }
        // 先计算哈希（使用缓冲流），避免保存时底层处置流导致无法再读
        string? hash = null;
        try
        {
            buffered.Position = 0;
            using var sha256 = SHA256.Create();
            var h = await sha256.ComputeHashAsync(buffered, ct);
            hash = BitConverter.ToString(h).Replace("-", "").ToLowerInvariant();
        }
        catch { }
        
        // 检查是否已存在相同哈希的文件（去重）
        // 命中相同哈希时，仍创建新的 SysFile 逻辑记录（独立的 FileId），
        // 但复用已有的物理存储路径，避免重复保存文件内容。
        // 这样每个用户上传的文件拥有独立的授权边界，不会因内容相同而串权限。
        string? reuseStoragePath = null;
        string? reuseStorageProvider = null;
        string? reuseUrl = null;
        if (!string.IsNullOrEmpty(hash))
        {
            var existingFile = _repo.Query()
                .Where(f => f.Hash == hash && f.Size == input.Size && !f.IsDeleted)
                .OrderByDescending(f => f.CreatedAt)
                .FirstOrDefault();
            
            if (existingFile != null)
            {
                // 复用物理存储，但创建新的逻辑文件记录
                reuseStoragePath = existingFile.StoragePath;
                reuseStorageProvider = existingFile.StorageProvider;
                reuseUrl = existingFile.Url;
            }
        }
        
        // 通过 Switcher 获取当前真实提供者，确保使用云存储实现
        buffered.Position = 0;
        var currentProvider = _storageSwitcher.Current;

        // ---------- 图片压缩逻辑 ----------
        Stream uploadStream = buffered;
        var actualSize = input.Size;
        var actualFileName = input.FileName;
        bool isCompressibleImage = IsCompressibleImage(contentType!, extLower);
        bool compressEnabled = false;
        bool keepOriginal = false;
        
        if (isCompressibleImage)
        {
            compressEnabled = string.Equals(
                _settings.Query().FirstOrDefault(s => s.Key == "Upload.ImageCompress.Enabled")?.Value,
                "true", StringComparison.OrdinalIgnoreCase);
        }

        if (isCompressibleImage && compressEnabled)
        {
            var qualitySetting = _settings.Query().FirstOrDefault(s => s.Key == "Upload.ImageCompress.Quality")?.Value;
            int quality = int.TryParse(qualitySetting, out var q) ? Math.Clamp(q, 10, 100) : 75;
            keepOriginal = string.Equals(
                _settings.Query().FirstOrDefault(s => s.Key == "Upload.ImageCompress.KeepOriginal")?.Value,
                "true", StringComparison.OrdinalIgnoreCase);

            // 保留原图：先保存一份原图到存储
            if (keepOriginal)
            {
                buffered.Position = 0;
                // 用安全 ASCII 名作为存储 Key（含 _original 标记），避免中文落到 OSS 对象名导致 422/签名失败等问题
                var origFileName = BuildSafeStorageFileName(input.FileName, extLower, "_original");
                await currentProvider.SaveAsync(buffered, origFileName, contentType!, ct);
            }

            // 执行压缩
            buffered.Position = 0;
            var compressed = CompressImage(buffered, quality, extLower);
            if (compressed != null)
            {
                uploadStream = compressed;
                actualSize = compressed.Length;
            }
            else
            {
                // 压缩失败，使用原流
                buffered.Position = 0;
                uploadStream = buffered;
            }
        }

        // ---------- 物理存储：如果已有相同内容则复用存储路径 ----------
        string storagePath;
        string providerName;
        if (reuseStoragePath != null)
        {
            // 复用已有物理文件，不重新保存
            storagePath = reuseStoragePath;
            providerName = reuseStorageProvider ?? currentProvider.GetType().Name.Replace("Provider", string.Empty);
            // 如果压缩流不是原缓冲流，释放它
            if (uploadStream != buffered && uploadStream is IDisposable d1) d1.Dispose();
        }
        else
        {
            uploadStream.Position = 0;
            // 用安全 ASCII 名作为存储 Key（时间戳 + 随机串 + 原扩展名），
            // 避免中文/空格/特殊字符落到 OSS 对象名导致又拍云等服务签名失败。
            // 原始文件名仍保存在 SysFile.FileName 字段，用于前端展示与下载时的 Content-Disposition。
            var safeStorageFileName = BuildSafeStorageFileName(input.FileName, extLower);
            storagePath = await currentProvider.SaveAsync(uploadStream, safeStorageFileName, contentType!, ct);
            providerName = currentProvider.GetType().Name.Replace("Provider", string.Empty);
            // 如果压缩流不是原缓冲流，释放它
            if (uploadStream != buffered && uploadStream is IDisposable disposable)
                disposable.Dispose();
        }

        var entity = SysFile.CreateNew(
            input.FileName,
            contentType!,
            actualSize,
            operatorId,
            input.Type,
            input.Tags,
            hash);
        entity.AttachStorage(providerName, storagePath);

        await _repo.AddAsync(entity, ct);

        // 保存后计算可公网访问的 URL
        if (reuseUrl != null)
        {
            // 复用已有文件的 URL
            entity.UpdatePublicUrl(reuseUrl);
        }
        else if (currentProvider is IPublicUrlProvider pub)
        {
            entity.UpdatePublicUrl(pub.GetPublicUrl(storagePath));
        }
        else
        {
            // 本地存储：只存相对路径（与 StoragePath 一致，如 2026/02/14/xxx.png）
            // 客户端自行拼接 /uploads/ 前缀和服务器地址
            entity.UpdatePublicUrl(storagePath);
        }
        await _repo.UpdateAsync(entity, ct);
        return entity.Id;
    }

    public async Task<int> BatchMoveAsync(List<long> ids, string targetProvider, long? operatorId, CancellationToken ct = default)
    {
        if (ids == null || ids.Count == 0) return 0;

        var files = _repo.Query().Where(f => ids.Contains(f.Id) && !f.IsDeleted).ToList();
        if (files.Count == 0) return 0;

        var currentProvider = _storageSwitcher.Current;
        var isTargetLocal = targetProvider.Contains("Local", StringComparison.OrdinalIgnoreCase);
        int moved = 0;

        foreach (var file in files)
        {
            var isCurrentLocal = string.IsNullOrWhiteSpace(file.StorageProvider) 
                || file.StorageProvider.Contains("Local", StringComparison.OrdinalIgnoreCase);
            var isCurrentOss = !isCurrentLocal;

            // 如果已经在目标存储中，跳过
            if ((isTargetLocal && isCurrentLocal) || (!isTargetLocal && isCurrentOss))
                continue;

            try
            {
                // 从当前存储读取文件内容
                IFileStorageProvider sourceProvider;
                if (isCurrentLocal)
                {
                    // 需要使用本地存储读取
                    sourceProvider = _storage; // _storage 是注册的默认提供者
                }
                else
                {
                    sourceProvider = currentProvider;
                }

                using var contentStream = await sourceProvider.OpenReadAsync(file.StoragePath, ct);
                using var buffered = new MemoryStream();
                await contentStream.CopyToAsync(buffered, ct);
                buffered.Position = 0;

                // 保存到目标存储
                IFileStorageProvider targetStorageProvider;
                if (isTargetLocal)
                {
                    targetStorageProvider = _storage;
                }
                else
                {
                    targetStorageProvider = currentProvider;
                }

                var newPath = await targetStorageProvider.SaveAsync(buffered, file.FileName, file.ContentType ?? "application/octet-stream", ct);
                var newProviderName = targetStorageProvider.GetType().Name.Replace("Provider", string.Empty);
                file.AttachStorage(newProviderName, newPath);

                // 更新公网 URL
                if (targetStorageProvider is IPublicUrlProvider pub)
                {
                    file.UpdatePublicUrl(pub.GetPublicUrl(newPath));
                }
                else
                {
                    file.UpdatePublicUrl(newPath);
                }

                await _repo.UpdateAsync(file, ct);

                // 删除源存储中的文件（最佳努力）
                try { await sourceProvider.DeleteAsync(file.StoragePath, ct); } catch { }

                moved++;
            }
            catch
            {
                // 单个文件迁移失败不影响其他文件
            }
        }

        return moved;
    }

    public async Task<bool> DeleteAsync(long id, long? operatorId, bool isAdmin, CancellationToken ct = default)
    {
        var file = _repo.Query().Where(f => f.Id == id && !f.IsDeleted).FirstOrDefault();
        if (file == null) return false;

        // 非管理员只能删除自己的文件
        if (!isAdmin)
        {
            var fileOwnerId = file.CreatedBy ?? file.OwnerId;
            if (fileOwnerId != operatorId) return false;
        }

        var storagePath = file.StoragePath;
        var providerTag = file.StorageProvider;

        // 先删除数据库记录
        await _repo.DeleteAsync(file.Id, ct);

        // 检查是否还有其他未删除记录引用同一物理文件（哈希去重场景）
        await TryDeletePhysicalFileAsync(storagePath, providerTag, id, ct);

        return true;
    }

    public async Task<int> BatchDeleteAsync(List<long> ids, long? operatorId, bool isAdmin, CancellationToken ct = default)
    {
        if (ids == null || ids.Count == 0) return 0;

        var files = _repo.Query().Where(f => ids.Contains(f.Id) && !f.IsDeleted).ToList();
        int deleted = 0;

        foreach (var file in files)
        {
            // 非管理员只能删除自己的文件
            if (!isAdmin)
            {
                var fileOwnerId = file.CreatedBy ?? file.OwnerId;
                if (fileOwnerId != operatorId) continue;
            }

            var storagePath = file.StoragePath;
            var providerTag = file.StorageProvider;

            await _repo.DeleteAsync(file.Id, ct);
            deleted++;

            // 删除物理文件（无其他引用时）
            await TryDeletePhysicalFileAsync(storagePath, providerTag, file.Id, ct);
        }

        return deleted;
    }

    /// <summary>
    /// 尝试删除物理存储文件。
    /// 仅当没有其他未删除的 SysFile 记录引用同一 StoragePath 时才执行物理删除。
    /// </summary>
    private async Task TryDeletePhysicalFileAsync(string? storagePath, string? providerTag, long excludeId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(storagePath)) return;

        // 引用计数：检查是否有其他未删除记录使用同一物理文件
        var hasOtherReferences = _repo.Query()
            .Where(f => f.StoragePath == storagePath && f.Id != excludeId && !f.IsDeleted)
            .Any();
        if (hasOtherReferences) return;

        try
        {
            var isLocal = string.IsNullOrWhiteSpace(providerTag)
                || providerTag.Contains("Local", StringComparison.OrdinalIgnoreCase);
            IFileStorageProvider storageProvider = isLocal ? _storage : _storageSwitcher.Current;
            await storageProvider.DeleteAsync(storagePath, ct);
        }
        catch
        {
            // 物理删除失败不阻塞业务（最佳努力）
        }
    }

    /// <summary>
    /// 生成存储用的安全文件名（时间戳毫秒 + 随机串 + 原扩展名）。
    /// 用于传递给 IFileStorageProvider.SaveAsync 作为 Key 拼接的「文件名」部分，
    /// 避免原始文件名中的中文 / 空格 / 特殊字符落到 OSS 对象名 ——
    /// 又拍云等部分对象存储服务在签名认证或 URL 处理上对非 ASCII 字符容错较差，
    /// 容易出现 422 / 签名不匹配 / 下载链接解析失败等问题。
    /// 注意：原始文件名仍会保留在 SysFile.FileName 字段中，作为前端展示与下载时的显示名。
    /// </summary>
    /// <param name="originalFileName">原始上传文件名（仅用来兜底取扩展名）。</param>
    /// <param name="ext">已规范化的小写扩展名，例如 ".jpg"；可为空。</param>
    /// <param name="suffix">可选的语义化后缀，例如 "_original"。</param>
    private static string BuildSafeStorageFileName(string originalFileName, string ext, string suffix = "")
    {
        // 兜底再取一次扩展名（外部传入为空时）
        if (string.IsNullOrWhiteSpace(ext))
        {
            ext = (Path.GetExtension(originalFileName) ?? string.Empty).ToLowerInvariant();
        }
        // 时间戳精确到毫秒（中国本地时间，与全局约定一致）
        var ts = DateTime.Now.ToString("yyyyMMddHHmmssfff");
        // 8 位密码学随机十六进制串，避免同毫秒并发碰撞
        Span<byte> rnd = stackalloc byte[4];
        RandomNumberGenerator.Fill(rnd);
        var rand = Convert.ToHexString(rnd).ToLowerInvariant();
        var safeSuffix = string.IsNullOrEmpty(suffix) ? string.Empty : suffix;
        return $"{ts}_{rand}{safeSuffix}{ext}";
    }

    /// <summary>
    /// 判断是否为可压缩的图片类型（排除 GIF/SVG 等不适合有损压缩的格式）
    /// </summary>
    private static bool IsCompressibleImage(string contentType, string ext)
    {
        if (string.IsNullOrWhiteSpace(contentType)) return false;
        var compressibleTypes = new[] { "image/jpeg", "image/png", "image/webp", "image/bmp" };
        var compressibleExts = new[] { ".jpg", ".jpeg", ".png", ".webp", ".bmp" };
        return compressibleTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase)
            || compressibleExts.Contains(ext, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 使用 SkiaSharp 压缩图片，返回压缩后的 MemoryStream（调用方负责释放）
    /// </summary>
    private static MemoryStream? CompressImage(Stream input, int quality, string ext)
    {
        try
        {
            using var original = SKBitmap.Decode(input);
            if (original == null) return null;

            var format = ext switch
            {
                ".png" => SKEncodedImageFormat.Png,
                ".webp" => SKEncodedImageFormat.Webp,
                _ => SKEncodedImageFormat.Jpeg
            };

            var ms = new MemoryStream();
            using var image = SKImage.FromBitmap(original);
            var data = image.Encode(format, quality);
            if (data == null) return null;
            data.SaveTo(ms);
            ms.Position = 0;
            return ms;
        }
        catch
        {
            return null;
        }
    }
}


