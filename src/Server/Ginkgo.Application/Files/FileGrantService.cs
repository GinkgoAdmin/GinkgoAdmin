using Ginkgo.Domain;
using Ginkgo.Domain.Files;
using Microsoft.Extensions.Caching.Memory;

namespace Ginkgo.Application.Files;

/// <summary>
/// 文件公开授权服务实现。
/// 使用 MemoryCache 缓存高频公开资源（如 LOGO）的 Grant 校验结果，避免每次请求查 DB。
/// </summary>
public sealed class FileGrantService : IFileGrantService
{
    private readonly IRepository<SysFileGrant> _repo;
    private readonly IMemoryCache _cache;

    // 缓存键前缀
    private const string CachePrefix = "fg:";
    // 缓存有效期（分钟）
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);

    public FileGrantService(IRepository<SysFileGrant> repo, IMemoryCache cache)
    {
        _repo = repo;
        _cache = cache;
    }

    /// <inheritdoc />
    public async Task<string> EnsurePublicGrantAsync(long fileId, string refType, string refId,
        string? fieldName = null, long? operatorId = null, CancellationToken ct = default)
    {
        // 先查找是否已存在有效的 grant
        var existing = _repo.Query()
            .Where(g => g.FileId == fileId
                && g.RefType == refType
                && g.RefId == refId
                && !g.IsRevoked)
            .WhereIF(!string.IsNullOrWhiteSpace(fieldName), g => g.FieldName == fieldName)
            .WhereIF(string.IsNullOrWhiteSpace(fieldName), g => g.FieldName == null)
            .First();

        if (existing != null && existing.IsValid())
        {
            return BuildPublicUrl(existing.GrantKey);
        }

        // 创建新 grant
        var grant = SysFileGrant.Create(fileId, refType, refId, fieldName, null, operatorId);
        await _repo.AddAsync(grant, ct);

        // 写入缓存
        _cache.Set(CachePrefix + grant.GrantKey, grant, CacheTtl);

        return BuildPublicUrl(grant.GrantKey);
    }

    /// <inheritdoc />
    public async Task RevokeGrantsAsync(string refType, string refId, string? fieldName = null,
        long? operatorId = null, CancellationToken ct = default)
    {
        var query = _repo.Query()
            .Where(g => g.RefType == refType && g.RefId == refId && !g.IsRevoked);

        if (!string.IsNullOrWhiteSpace(fieldName))
        {
            query = query.Where(g => g.FieldName == fieldName);
        }

        var grants = await query.ToListAsync();
        if (grants.Count == 0) return;

        foreach (var grant in grants)
        {
            grant.Revoke(operatorId);
            // 从缓存中移除
            _cache.Remove(CachePrefix + grant.GrantKey);
        }

        await _repo.UpdateRangeAsync(grants, ct);
    }

    /// <inheritdoc />
    public Task<SysFileGrant?> ValidateGrantAsync(string grantKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(grantKey)) return Task.FromResult<SysFileGrant?>(null);

        // 优先从缓存读取
        if (_cache.TryGetValue<SysFileGrant>(CachePrefix + grantKey, out var cached) && cached != null)
        {
            return Task.FromResult<SysFileGrant?>(cached.IsValid() ? cached : null);
        }

        // 缓存未命中，查 DB
        var grant = _repo.Query()
            .Where(g => g.GrantKey == grantKey)
            .First();

        if (grant == null) return Task.FromResult<SysFileGrant?>(null);

        if (!grant.IsValid())
        {
            // 无效的 grant 也短暂缓存（防止恶意轮询查 DB）
            _cache.Set(CachePrefix + grantKey, grant, TimeSpan.FromMinutes(2));
            return Task.FromResult<SysFileGrant?>(null);
        }

        // 有效 grant 写入缓存
        _cache.Set(CachePrefix + grantKey, grant, CacheTtl);
        return Task.FromResult<SysFileGrant?>(grant);
    }

    /// <summary>
    /// 构建公开访问 URL。
    /// </summary>
    private static string BuildPublicUrl(string grantKey)
    {
        return $"/api/v1/files/public/{grantKey}";
    }
}
