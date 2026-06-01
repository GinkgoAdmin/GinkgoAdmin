// 文件功能说明：
// 多端通用插件业务入口特性（multi-client-plugin-portal）属性测试用的内存测试数据库上下文。
// 基于 SqlSugar + SQLite 内存库构建，为应用服务纯逻辑测试提供真实的 ISugarQueryable<T> 查询能力，
// 从而无需 mock SqlSugar 即可让 MenuGroupAppService 的 LINQ 查询在内存中运行。

using System;
using System.Collections.Generic;
using System.Linq;
using Ginkgo.Domain.Menus;
using Ginkgo.Domain.Roles;
using Ginkgo.Domain.Users;
using SqlSugar;

namespace Ginkgo.Application.Tests.Infrastructure;

/// <summary>
/// 内存测试数据库上下文：持有一个独立的 SQLite 内存连接，并完成本特性相关实体的建表。
/// 每个测试场景应创建独立实例，确保测试之间数据隔离（每个实例拥有各自的私有内存库）。
/// 使用完毕后应 Dispose 以释放底层连接与内存库。
/// </summary>
public sealed class InMemoryTestDatabase : IDisposable
{
    private readonly SqlSugarClient _client;

    /// <summary>
    /// 暴露底层 SqlSugar 客户端，供内存仓储构造查询。
    /// </summary>
    public ISqlSugarClient Client => _client;

    /// <summary>
    /// 构造内存数据库：使用进程内唯一命名的 SQLite 共享内存库，
    /// 通过保活连接保证库在上下文存活期间不被回收，并按本特性所需实体建表。
    /// </summary>
    public InMemoryTestDatabase()
    {
        // 使用唯一库名 + 共享缓存模式，确保该上下文内多次取连接访问的是同一份内存库，
        // 且与其他测试上下文相互隔离。
        var dbName = "mcpp_" + Guid.NewGuid().ToString("N");
        var connectionString = $"Data Source={dbName};Mode=Memory;Cache=Shared";

        _client = new SqlSugarClient(new ConnectionConfig
        {
            DbType = DbType.Sqlite,
            ConnectionString = connectionString,
            IsAutoCloseConnection = false, // 关闭自动关闭，保证内存库在上下文存活期间持续存在
            InitKeyType = InitKeyType.Attribute
        });

        // 主动打开连接作为保活连接，避免共享内存库在无连接时被销毁。
        _client.Ado.Connection.Open();

        // 为本特性相关实体建表。
        // 说明：SqlSugar 的 SQLite CodeFirst 在解析复合 [SugarIndex]（如 "Enabled,IsDeleted"）时
        //       与 MySQL 行为不一致会报错；而内存属性测试并不依赖索引。
        //       因此改为根据实体列元数据手动建表（不创建索引），既保证查询/读写正确，又规避方言差异。
        CreateTable<MenuGroup>();
        CreateTable<MenuGroupItem>();
        CreateTable<RoleMenuGroup>();
        CreateTable<RoleMenuGroupItem>();
        CreateTable<Menu>();
        CreateTable<UserRole>();
        CreateTable<Role>();
        CreateTable<RolePermission>();
    }

    /// <summary>
    /// 依据实体列元数据建表（不创建索引），列类型按 SQLite 的类型亲和性映射。
    /// </summary>
    private void CreateTable<T>() where T : class, new()
    {
        var info = _client.EntityMaintenance.GetEntityInfo<T>();
        var columns = new List<DbColumnInfo>();
        foreach (var col in info.Columns)
        {
            if (col.IsIgnore) continue;
            var rawType = col.PropertyInfo.PropertyType;
            var nullableUnderlying = Nullable.GetUnderlyingType(rawType);
            var underlying = nullableUnderlying ?? rawType;

            // 列是否允许为空：以 SqlSugar 元数据为准；同时兼容「C# 为 Nullable<T>（如 long?）
            // 但实体未显式标注 [SugarColumn(IsNullable = true)]」的情形（例如 Role.ParentId）。
            // 此类列在领域模型中本就可空，建表时必须允许 NULL，否则插入 null 会触发 NOT NULL 约束失败。
            var isNullable = col.IsNullable || nullableUnderlying != null;

            columns.Add(new DbColumnInfo
            {
                DbColumnName = col.DbColumnName,
                DataType = MapToSqliteType(underlying),
                IsPrimarykey = col.IsPrimarykey,
                IsIdentity = false, // 雪花 Id 由应用层显式赋值，非自增
                IsNullable = isNullable,
                Length = col.Length,
                ColumnDescription = col.ColumnDescription
            });
        }
        _client.DbMaintenance.CreateTable(info.DbTableName, columns, true);
    }

    /// <summary>
    /// 将 .NET 类型映射为 SQLite 列类型（利用 SQLite 动态类型亲和性，足以满足读写与查询）。
    /// </summary>
    private static string MapToSqliteType(Type type)
    {
        if (type == typeof(bool) || type == typeof(byte) || type == typeof(short)
            || type == typeof(int) || type == typeof(long) || type.IsEnum)
        {
            return "INTEGER";
        }
        if (type == typeof(float) || type == typeof(double) || type == typeof(decimal))
        {
            return "REAL";
        }
        // string / DateTime / Guid / 其他 → TEXT
        return "TEXT";
    }

    /// <summary>
    /// 释放底层连接与内存库。
    /// </summary>
    public void Dispose()
    {
        try
        {
            if (_client.Ado.Connection.State != System.Data.ConnectionState.Closed)
            {
                _client.Ado.Connection.Close();
            }
        }
        catch
        {
            // 释放阶段忽略关闭异常，避免影响测试收尾。
        }
        _client.Dispose();
    }
}
