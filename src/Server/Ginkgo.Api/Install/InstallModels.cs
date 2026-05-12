using System;
using System.Collections.Generic;

namespace Ginkgo.Api.Install;

/// <summary>
/// 一键安装请求参数。
/// </summary>
public sealed class InstallRequest
{
    // 数据库
    public string Provider { get; set; } = "SqlServer"; // SqlServer/MySql
    public string ConnectionString { get; set; } = string.Empty;

    // 管理员
    public string AdminUserName { get; set; } = "admin";
    public string AdminPassword { get; set; } = string.Empty;
    public string AdminDisplayName { get; set; } = "管理员";
    public string? AdminEmail { get; set; }

    // 站点
    public string SiteName { get; set; } = "Ginkgo";
}

/// <summary>
/// 安装步骤日志。
/// </summary>
public sealed class InstallLog
{
    public DateTime At { get; set; } = DateTime.Now;
    public string Level { get; set; } = "INFO";
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// 数据库连接测试请求。
/// </summary>
public sealed class TestConnectionRequest
{
    public string Server { get; set; } = string.Empty;
    public string Port { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Provider { get; set; } = "SqlServer";
}

/// <summary>
/// 一键安装结果。
/// </summary>
public sealed class InstallResult
{
    public bool Success { get; set; }
    public bool AlreadyInstalled { get; set; }
    public string? Error { get; set; }
    /// <summary>
    /// 安装时随机生成的管理后台路径标识
    /// </summary>
    public string? AdminSlug { get; set; }
    public List<InstallLog> Logs { get; set; } = new();
}

