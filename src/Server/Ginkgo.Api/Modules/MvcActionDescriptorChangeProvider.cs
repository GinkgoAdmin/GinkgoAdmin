using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Primitives;

namespace Ginkgo.Api.Modules;

public sealed class MvcActionDescriptorChangeProvider : IActionDescriptorChangeProvider
{
    public static readonly MvcActionDescriptorChangeProvider Instance = new();
    private CancellationTokenSource _cts = new();

    public IChangeToken GetChangeToken() => new CancellationChangeToken(_cts.Token);

    public void NotifyChanges()
    {
        var prev = Interlocked.Exchange(ref _cts, new CancellationTokenSource());
        prev.Cancel();
    }
}









