// 文件功能说明：
// 验证 MemoryCacheServiceAdapter（SqlSugar ICacheService over IMemoryCache）的核心语义：
//   - Add / Get / ContainsKey / Remove
//   - GetAllKey 反映当前所有有效 key
//   - GetOrCreate：缓存命中走缓存、未命中调 create

using Ginkgo.Infrastructure.Persistence.Features;
using Microsoft.Extensions.Caching.Memory;

namespace Ginkgo.Tests.Unit.Features;

public sealed class MemoryCacheServiceAdapterTests
{
    private static MemoryCacheServiceAdapter NewAdapter(int defaultSeconds = 300)
        => new(new MemoryCache(new MemoryCacheOptions()), defaultSeconds);

    [Fact]
    public void Add_And_Get_RoundTrip()
    {
        var ad = NewAdapter();
        ad.Add("k1", "v1");
        Assert.Equal("v1", ad.Get<string>("k1"));
    }

    [Fact]
    public void ContainsKey_TrueAfterAdd_FalseAfterRemove()
    {
        var ad = NewAdapter();
        ad.Add("k1", 123, 60);
        Assert.True(ad.ContainsKey<int>("k1"));
        ad.Remove<int>("k1");
        Assert.False(ad.ContainsKey<int>("k1"));
        Assert.Equal(0, ad.Get<int>("k1")); // default
    }

    [Fact]
    public void GetAllKey_ReturnsLiveKeys()
    {
        var ad = NewAdapter();
        ad.Add("a", 1);
        ad.Add("b", 2);
        ad.Add("c", 3);
        var keys = ad.GetAllKey<object>().OrderBy(k => k).ToArray();
        Assert.Equal(new[] { "a", "b", "c" }, keys);

        ad.Remove<int>("b");
        var after = ad.GetAllKey<object>().OrderBy(k => k).ToArray();
        Assert.Equal(new[] { "a", "c" }, after);
    }

    [Fact]
    public void GetOrCreate_HitsCache_NoSecondCreate()
    {
        var ad = NewAdapter();
        var calls = 0;
        var v1 = ad.GetOrCreate<string>("k", () => { calls++; return "fresh"; });
        var v2 = ad.GetOrCreate<string>("k", () => { calls++; return "should-not-run"; });

        Assert.Equal("fresh", v1);
        Assert.Equal("fresh", v2);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void GetOrCreate_MissingKey_CallsCreate()
    {
        var ad = NewAdapter();
        var v = ad.GetOrCreate<int>("missing", () => 42, cacheDurationInSeconds: 60);
        Assert.Equal(42, v);
        Assert.True(ad.ContainsKey<int>("missing"));
    }

    [Fact]
    public void Get_WrongType_ReturnsDefault()
    {
        var ad = NewAdapter();
        ad.Add("k", "string-value");
        Assert.Equal(0, ad.Get<int>("k")); // 类型不匹配返回默认值
    }

    [Fact]
    public void EmptyKey_NoOp()
    {
        var ad = NewAdapter();
        ad.Add<string>("", "x");
        Assert.False(ad.ContainsKey<string>(""));
        Assert.Null(ad.Get<string>(""));
    }
}
