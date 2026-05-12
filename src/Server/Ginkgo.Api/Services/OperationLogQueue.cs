using System.Threading.Channels;
using Ginkgo.Domain.Logs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Ginkgo.Application.Logs;

namespace Ginkgo.Api.Services;

public interface IOperationLogQueue
{
    void Enqueue(OpLog log);
}

public sealed class OperationLogQueue : BackgroundService, IOperationLogQueue
{
    private readonly Channel<OpLog> _channel = Channel.CreateUnbounded<OpLog>();
    private readonly IServiceScopeFactory _scopeFactory;

    public OperationLogQueue(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public void Enqueue(OpLog log)
    {
        if (!_channel.Writer.TryWrite(log))
        {
            // 极端情况下丢弃
        }
    }

    private static List<Ginkgo.Domain.Menus.Menu>? _menuCache = null;
    private static DateTime _menuCacheTime = DateTime.MinValue;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var log = await _channel.Reader.ReadAsync(stoppingToken);
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<SqlSugar.ISqlSugarClient>();
                var app = scope.ServiceProvider.GetRequiredService<ILogAppService>();

                // Refresh Menu Cache every 10 minutes
                if (_menuCache == null || (DateTime.Now - _menuCacheTime).TotalMinutes > 10)
                {
                    try
                    {
                        var menus = await db.Queryable<Ginkgo.Domain.Menus.Menu>().Where(m => !m.IsDeleted).ToListAsync();
                        _menuCache = menus;
                        _menuCacheTime = DateTime.Now;
                    }
                    catch { /* ignore DB issues gracefully and rely on old cache */ }
                }

                // Reverse map FeatureCN / ModuleCN from the Menu Tree exactly
                if (_menuCache != null && log.ModuleCN == "其他" && !string.IsNullOrWhiteSpace(log.Resource) && !string.IsNullOrWhiteSpace(log.Action))
                {
                    // Find the Api or Button that matches Resource & Method 
                    var mKey = log.Action.ToUpperInvariant();
                    var match = _menuCache.FirstOrDefault(m => 
                        (m.Type == "Api" || m.Type == "Button") && 
                        !string.IsNullOrWhiteSpace(m.Resource) && 
                        !string.IsNullOrWhiteSpace(m.Method) &&
                        m.Method.ToUpperInvariant() == mKey &&
                        // Check if log.Resource matches the exact Resource (e.g. /api/demo/tests)
                        // Or if it's parameterized, a starts-with fallback could be used (in future).
                        string.Equals(log.Resource.Split('?')[0], m.Resource.Split('?')[0], StringComparison.OrdinalIgnoreCase));
                    
                    if (match != null)
                    {
                        // Found the action! Set feature name
                        if (!string.IsNullOrWhiteSpace(match.Name))
                        {
                            log.FeatureCN = match.Name;
                        }
                        // Trace up to find module name
                        var parent = _menuCache.FirstOrDefault(m => m.Id == match.ParentId);
                        while (parent != null)
                        {
                            if (parent.Type == "Menu" || parent.Type == "Directory")
                            {
                                log.ModuleCN = parent.Name;
                                break;
                            }
                            parent = _menuCache.FirstOrDefault(m => m.Id == parent.ParentId);
                        }
                        
                        // Update ReviewCN based on new mapping
                        log.ReviewCN = $"{log.ModuleCN}-{log.FeatureCN}-{(log.Result.StartsWith("OK", StringComparison.OrdinalIgnoreCase) ? "成功" : "失败")}";
                    }
                }

                var input = new AppendOpLogInput
                {
                    Action = log.Action,
                    Resource = log.Resource,
                    Result = log.Result,
                    ElapsedMs = log.ElapsedMs,
                    DataJson = log.DataJson,
                    DepartmentId = log.DepartmentId,
                    Ip = log.Ip,
                    UserAgent = log.UserAgent,
                    CreatedAt = log.CreatedAt,
                    CreatedBy = log.CreatedBy,
                    ModuleCN = log.ModuleCN,
                    FeatureCN = log.FeatureCN,
                    ReviewCN = log.ReviewCN
                };
                await app.AppendAsync(input, stoppingToken);
            }
            catch
            {
                // swallow，避免影响主流程；可在此加入重试/死信
            }
        }
    }
}


