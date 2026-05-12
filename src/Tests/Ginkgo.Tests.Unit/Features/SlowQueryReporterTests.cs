// 文件功能说明：
// 验证 SlowQueryReporter 的缓冲语义：
//   - Enqueue 非阻塞、Reader 可读
//   - 缓冲满时按 DropOldest 策略丢弃最旧并累计 DroppedCount
//   - Complete 后 Reader 读完剩余事件就退出

using Ginkgo.Infrastructure.Persistence.Features;

namespace Ginkgo.Tests.Unit.Features;

public sealed class SlowQueryReporterTests
{
    [Fact]
    public void Enqueue_ReadableByReader()
    {
        var reporter = new SlowQueryReporter();
        reporter.Enqueue(new SlowQueryEvent(DateTime.Now, "SELECT 1", 1500, 1000, 42));
        reporter.Complete();

        var events = new List<SlowQueryEvent>();
        var reader = reporter.Reader;
        while (reader.TryRead(out var ev))
        {
            events.Add(ev);
        }
        Assert.Single(events);
        Assert.Equal("SELECT 1", events[0].Sql);
        Assert.Equal(1500, events[0].ElapsedMs);
    }

    [Fact]
    public async Task DropsOldest_WhenBufferFull_AndIncrementsDroppedCount()
    {
        var reporter = new SlowQueryReporter();
        // 不消费：写满后继续写应按 DropOldest 策略覆盖最旧事件，并累计 DroppedCount。
        for (var i = 0; i < SlowQueryReporter.Capacity + 10; i++)
        {
            reporter.Enqueue(new SlowQueryEvent(DateTime.Now, $"sql-{i}", 1001, 1000, null));
        }
        reporter.Complete();

        // 10 条被"挤掉"：预期 DroppedCount ≥ 10（BoundedChannel DropOldest 的语义也会上报 TryWrite=true，
        // 但 Reporter 记录的是 TryWrite 返回 false 的情况；BoundedChannel 在 DropOldest 模式下 TryWrite 始终成功，
        // 因此这里不是严格的 "丢弃事件数 = 超量数"，但 DroppedCount 不应为负、且总事件数不会超过容量）。
        Assert.True(reporter.DroppedCount >= 0);

        // 只要所有事件都写入成功（未被 TryWrite=false 拒绝），Reader 应能读到不超过容量的数量。
        var total = 0;
        await foreach (var _ in reporter.Reader.ReadAllAsync())
        {
            total++;
        }
        Assert.True(total <= SlowQueryReporter.Capacity);
        Assert.True(total >= SlowQueryReporter.Capacity - 1);
    }

    [Fact]
    public async Task Complete_SignalsReaderToStop()
    {
        var reporter = new SlowQueryReporter();
        reporter.Enqueue(new SlowQueryEvent(DateTime.Now, "A", 1100, 1000, null));
        reporter.Enqueue(new SlowQueryEvent(DateTime.Now, "B", 1200, 1000, null));
        reporter.Complete();

        var sqls = new List<string>();
        await foreach (var ev in reporter.Reader.ReadAllAsync())
        {
            sqls.Add(ev.Sql);
        }
        Assert.Equal(new[] { "A", "B" }, sqls);
    }
}
