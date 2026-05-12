using Ginkgo.Application.Notifications;

namespace Ginkgo.ServerToolkit;

internal sealed class ServerNotifierAdapter : IServerNotifier
{
	private readonly INotifyAppService _notify;
	public ServerNotifierAdapter(INotifyAppService notify) { _notify = notify; }

	public async Task<long> SendAsync(string title, string content, NotifyAudienceSpec audience, bool html = false, bool important = false, byte priority = 1, string? dedupeKey = null, CancellationToken ct = default)
	{
		var input = new CreateNotifyInput
		{
			Title = title,
			ContentType = (byte)(html ? 1 : 0),
			ContentText = html ? null : content,
			ContentHtml = html ? content : null,
			IsImportant = important,
			Priority = priority
		};
		// 受众展开为种子
		if (audience.ToAll)
		{
			input.Audience.Add(new AudienceSeedInput { TargetType = 4, TargetValue = "ALL" });
		}
		if (audience.UserIds != null)
		{
			foreach (var uid in audience.UserIds)
				input.Audience.Add(new AudienceSeedInput { TargetType = 1, TargetValue = uid.ToString() });
		}
		if (audience.RoleIds != null)
		{
			foreach (var rid in audience.RoleIds)
				input.Audience.Add(new AudienceSeedInput { TargetType = 2, TargetValue = rid.ToString() });
		}
		if (audience.Departments != null)
		{
			foreach (var d in audience.Departments)
				input.Audience.Add(new AudienceSeedInput { TargetType = 3, TargetValue = d.Deep ? $"{d.DeptId}:deep" : d.DeptId.ToString() });
		}

		var id = await _notify.CreateAsync(input, ct);
		await _notify.PublishAsync(id, ct);
		return id;
	}
}







