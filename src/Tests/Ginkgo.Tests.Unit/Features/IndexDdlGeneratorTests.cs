// 文件功能说明：
// 验证 IndexDdlGenerator 在 MySQL / PostgreSQL / SQL Server 三种方言下生成的 CREATE INDEX DDL 是否符合预期。
// 覆盖：
//   - 单列 B-tree、多列 B-tree、唯一索引、DESC 排序
//   - 前缀索引（仅 MySQL 支持；其它方言抛 NotSupportedException）
//   - 全文索引（MySQL FULLTEXT / PG GIN(to_tsvector) / SQL Server FULLTEXT CATALOG 骨架）
//   - 参数校验（null/空表名/空列）

using Ginkgo.Infrastructure.Abstractions;
using Ginkgo.Infrastructure.Dialects;
using Ginkgo.Infrastructure.Persistence.Features;

namespace Ginkgo.Tests.Unit.Features;

public sealed class IndexDdlGeneratorTests
{
    private static IndexDdlGenerator CreateMySql() => new(new MySqlDialect());
    private static IndexDdlGenerator CreatePg() => new(new PostgreSqlDialect());
    private static IndexDdlGenerator CreateMssql() => new(new SqlServerDialect());

    // ===================== 参数校验 =====================

    [Fact]
    public void GenerateCreateIndex_NullDef_Throws()
    {
        var gen = CreateMySql();
        Assert.Throws<ArgumentNullException>(() => gen.GenerateCreateIndex(null!));
    }

    [Fact]
    public void GenerateCreateIndex_EmptyTableName_Throws()
    {
        var gen = CreateMySql();
        var def = new IndexDefinition
        {
            IndexName = "idx_test",
            TableName = "",
            Columns = new[] { new IndexColumnDefinition { ColumnName = "a" } },
        };
        Assert.Throws<ArgumentException>(() => gen.GenerateCreateIndex(def));
    }

    [Fact]
    public void GenerateCreateIndex_EmptyIndexName_Throws()
    {
        var gen = CreateMySql();
        var def = new IndexDefinition
        {
            IndexName = "",
            TableName = "users",
            Columns = new[] { new IndexColumnDefinition { ColumnName = "a" } },
        };
        Assert.Throws<ArgumentException>(() => gen.GenerateCreateIndex(def));
    }

    [Fact]
    public void GenerateCreateIndex_EmptyColumns_Throws()
    {
        var gen = CreateMySql();
        var def = new IndexDefinition
        {
            IndexName = "idx_test",
            TableName = "users",
            Columns = Array.Empty<IndexColumnDefinition>(),
        };
        Assert.Throws<ArgumentException>(() => gen.GenerateCreateIndex(def));
    }

    // ===================== MySQL B-tree =====================

    [Fact]
    public void MySql_SingleColumn_BTree()
    {
        var def = new IndexDefinition
        {
            IndexName = "idx_users_email",
            TableName = "users",
            Type = IndexType.BTree,
            Columns = new[] { new IndexColumnDefinition { ColumnName = "email" } },
        };
        var sql = CreateMySql().GenerateCreateIndex(def);
        Assert.Equal("CREATE INDEX `idx_users_email` ON `users` (`email`)", sql);
    }

    [Fact]
    public void MySql_Unique_MultiColumn_BTree()
    {
        var def = new IndexDefinition
        {
            IndexName = "uk_users_tenant_email",
            TableName = "users",
            Type = IndexType.BTree,
            IsUnique = true,
            Columns = new[]
            {
                new IndexColumnDefinition { ColumnName = "tenant_id" },
                new IndexColumnDefinition { ColumnName = "email" },
            },
        };
        var sql = CreateMySql().GenerateCreateIndex(def);
        Assert.Equal(
            "CREATE UNIQUE INDEX `uk_users_tenant_email` ON `users` (`tenant_id`, `email`)",
            sql);
    }

    [Fact]
    public void MySql_DescColumn()
    {
        var def = new IndexDefinition
        {
            IndexName = "idx_logs_time_desc",
            TableName = "logs",
            Columns = new[]
            {
                new IndexColumnDefinition { ColumnName = "created_at", Descending = true },
            },
        };
        var sql = CreateMySql().GenerateCreateIndex(def);
        Assert.Equal("CREATE INDEX `idx_logs_time_desc` ON `logs` (`created_at` DESC)", sql);
    }

    // ===================== MySQL 前缀索引 =====================

    [Fact]
    public void MySql_PrefixIndex_Works()
    {
        var def = new IndexDefinition
        {
            IndexName = "idx_articles_content_prefix",
            TableName = "articles",
            Columns = new[]
            {
                new IndexColumnDefinition { ColumnName = "content", PrefixLength = 64 },
            },
        };
        var sql = CreateMySql().GenerateCreateIndex(def);
        Assert.Equal(
            "CREATE INDEX `idx_articles_content_prefix` ON `articles` (`content`(64))",
            sql);
    }

    [Fact]
    public void MySql_SupportsPrefixIndex_True()
    {
        Assert.True(CreateMySql().SupportsPrefixIndex);
    }

    [Fact]
    public void Pg_PrefixIndex_Throws()
    {
        var def = new IndexDefinition
        {
            IndexName = "idx_articles_content_prefix",
            TableName = "articles",
            Columns = new[]
            {
                new IndexColumnDefinition { ColumnName = "content", PrefixLength = 64 },
            },
        };
        Assert.Throws<NotSupportedException>(() => CreatePg().GenerateCreateIndex(def));
    }

    [Fact]
    public void Mssql_PrefixIndex_Throws()
    {
        var def = new IndexDefinition
        {
            IndexName = "idx_articles_content_prefix",
            TableName = "articles",
            Columns = new[]
            {
                new IndexColumnDefinition { ColumnName = "content", PrefixLength = 64 },
            },
        };
        Assert.Throws<NotSupportedException>(() => CreateMssql().GenerateCreateIndex(def));
    }

    [Fact]
    public void Pg_SupportsPrefixIndex_False()
    {
        Assert.False(CreatePg().SupportsPrefixIndex);
    }

    // ===================== MySQL FULLTEXT =====================

    [Fact]
    public void MySql_FullTextIndex()
    {
        var def = new IndexDefinition
        {
            IndexName = "ft_articles_body",
            TableName = "articles",
            Type = IndexType.FullText,
            Columns = new[]
            {
                new IndexColumnDefinition { ColumnName = "title" },
                new IndexColumnDefinition { ColumnName = "body" },
            },
        };
        var sql = CreateMySql().GenerateCreateIndex(def);
        Assert.Equal(
            "CREATE FULLTEXT INDEX `ft_articles_body` ON `articles` (`title`, `body`)",
            sql);
    }

    [Fact]
    public void MySql_SupportsFullText_True()
    {
        Assert.True(CreateMySql().SupportsFullTextIndex);
    }

    // ===================== PostgreSQL =====================

    [Fact]
    public void Pg_BTree_SingleColumn()
    {
        var def = new IndexDefinition
        {
            IndexName = "idx_users_email",
            TableName = "users",
            Columns = new[] { new IndexColumnDefinition { ColumnName = "email" } },
        };
        var sql = CreatePg().GenerateCreateIndex(def);
        Assert.Equal("CREATE INDEX \"idx_users_email\" ON \"users\" (\"email\")", sql);
    }

    [Fact]
    public void Pg_FullText_DefaultLanguage()
    {
        var def = new IndexDefinition
        {
            IndexName = "ft_articles_body",
            TableName = "articles",
            Type = IndexType.FullText,
            Columns = new[]
            {
                new IndexColumnDefinition { ColumnName = "title" },
                new IndexColumnDefinition { ColumnName = "body" },
            },
        };
        var sql = CreatePg().GenerateCreateIndex(def);
        Assert.Equal(
            "CREATE INDEX \"ft_articles_body\" ON \"articles\" USING GIN(to_tsvector('simple', \"title\" || ' ' || \"body\"))",
            sql);
    }

    [Fact]
    public void Pg_FullText_ExplicitLanguage()
    {
        var def = new IndexDefinition
        {
            IndexName = "ft_articles_en",
            TableName = "articles",
            Type = IndexType.FullText,
            FullTextLanguage = "english",
            Columns = new[] { new IndexColumnDefinition { ColumnName = "body" } },
        };
        var sql = CreatePg().GenerateCreateIndex(def);
        Assert.Equal(
            "CREATE INDEX \"ft_articles_en\" ON \"articles\" USING GIN(to_tsvector('english', \"body\"))",
            sql);
    }

    // ===================== SQL Server =====================

    [Fact]
    public void Mssql_BTree_SingleColumn()
    {
        var def = new IndexDefinition
        {
            IndexName = "idx_users_email",
            TableName = "users",
            Columns = new[] { new IndexColumnDefinition { ColumnName = "email" } },
        };
        var sql = CreateMssql().GenerateCreateIndex(def);
        Assert.Equal("CREATE INDEX [idx_users_email] ON [users] ([email])", sql);
    }

    [Fact]
    public void Mssql_BTree_WithSchema()
    {
        var def = new IndexDefinition
        {
            IndexName = "idx_users_email",
            Schema = "dbo",
            TableName = "users",
            Columns = new[] { new IndexColumnDefinition { ColumnName = "email" } },
        };
        var sql = CreateMssql().GenerateCreateIndex(def);
        Assert.Equal("CREATE INDEX [idx_users_email] ON [dbo].[users] ([email])", sql);
    }

    [Fact]
    public void Mssql_FullText_Skeleton()
    {
        var def = new IndexDefinition
        {
            IndexName = "ft_articles_body",
            TableName = "articles",
            Type = IndexType.FullText,
            Columns = new[] { new IndexColumnDefinition { ColumnName = "body" } },
        };
        var sql = CreateMssql().GenerateCreateIndex(def);
        Assert.Equal(
            "CREATE FULLTEXT INDEX ON [articles] ([body]) KEY INDEX [PK_articles] ON [ft_catalog_default]",
            sql);
    }
}
