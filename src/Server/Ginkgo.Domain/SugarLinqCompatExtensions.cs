// 兼容扩展：为 ISugarQueryable 提供常用的 LINQ 方法名适配，降低从 EF Core 迁移的改动量。
using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using SqlSugar;

namespace Ginkgo.Domain;

public static class SugarLinqCompatExtensions
{
    /// <summary>
    /// EF 的 LongCount 适配到 SqlSugar 的 Count。
    /// </summary>
    public static long LongCount<T>(this ISugarQueryable<T> query)
    {
        return (long)query.Count();
    }

    /// <summary>
    /// EF 的 LongCount(predicate) 适配。
    /// </summary>
    public static long LongCount<T>(this ISugarQueryable<T> query, Expression<Func<T, bool>> predicate)
    {
        return (long)query.Where(predicate).Count();
    }

    /// <summary>
    /// EF 的 ThenBy 适配：在 SqlSugar 中多次调用 OrderBy 会累计排序条件。
    /// </summary>
    public static ISugarQueryable<T> ThenBy<T>(this ISugarQueryable<T> query, Expression<Func<T, object>> keySelector)
    {
        return query.OrderBy(keySelector, OrderByType.Asc);
    }

    /// <summary>
    /// EF 的 ThenByDescending 适配。
    /// </summary>
    public static ISugarQueryable<T> ThenByDescending<T>(this ISugarQueryable<T> query, Expression<Func<T, object>> keySelector)
    {
        return query.OrderBy(keySelector, OrderByType.Desc);
    }

    /// <summary>
    /// EF 的 OrderByDescending 适配（若原代码直接调用了该扩展，可映射到 SqlSugar 的 OrderBy）。
    /// </summary>
    public static ISugarQueryable<T> OrderByDescending<T>(this ISugarQueryable<T> query, Expression<Func<T, object>> keySelector)
    {
        return query.OrderBy(keySelector, OrderByType.Desc);
    }

    /// <summary>
    /// EF 的 FirstOrDefault 适配。
    /// </summary>
    public static T? FirstOrDefault<T>(this ISugarQueryable<T> query)
    {
        return query.First();
    }

    /// <summary>
    /// EF 的 FirstOrDefault(predicate) 适配。
    /// </summary>
    public static T? FirstOrDefault<T>(this ISugarQueryable<T> query, Expression<Func<T, bool>> predicate)
    {
        return query.Where(predicate).First();
    }

    /// <summary>
    /// 常用的 FirstOrDefaultAsync 适配（便于异步场景）。
    /// </summary>
    public static Task<T> FirstOrDefaultAsync<T>(this ISugarQueryable<T> query, Expression<Func<T, bool>> predicate)
    {
        return query.Where(predicate).FirstAsync();
    }
}

