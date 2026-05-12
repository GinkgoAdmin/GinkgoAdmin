namespace Ginkgo.Api.Modules;

public sealed class ClientTaskService
{
    private readonly List<ClientModuleTask> _tasks = new();
    private readonly object _gate = new();

    public void Enqueue(string clientId, string moduleId, string version, string action)
    {
        lock (_gate)
        {
            _tasks.Add(new ClientModuleTask
            {
                ClientId = clientId,
                ModuleId = moduleId,
                Version = version,
                Action = action,
                CreatedAtUtc = DateTime.Now
            });
        }
    }

    public IReadOnlyList<ClientModuleTask> Pull(string clientId)
    {
        lock (_gate)
        {
            var list = _tasks.Where(t => string.Equals(t.ClientId, clientId, StringComparison.OrdinalIgnoreCase) || t.ClientId == "*").ToList();
            _tasks.RemoveAll(t => string.Equals(t.ClientId, clientId, StringComparison.OrdinalIgnoreCase) || t.ClientId == "*");
            return list;
        }
    }

    public void EnqueueBroadcast(string moduleId, string version, string action)
    {
        Enqueue("*", moduleId, version, action);
    }
}










