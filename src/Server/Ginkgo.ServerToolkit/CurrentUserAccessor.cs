using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Ginkgo.ServerToolkit;

internal sealed class HttpContextCurrentUser : ICurrentUser
{
	private readonly IHttpContextAccessor _accessor;
	public HttpContextCurrentUser(IHttpContextAccessor accessor) { _accessor = accessor; }

	public long? Id => TryFindUserId(_accessor.HttpContext?.User);
	public string? UserName => _accessor.HttpContext?.User?.FindFirst("uname")?.Value;
	public string? DisplayName => _accessor.HttpContext?.User?.FindFirst("name")?.Value;
	public IReadOnlyList<string> Roles => _accessor.HttpContext?.User?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray() ?? Array.Empty<string>();
	public bool IsAuthenticated => _accessor.HttpContext?.User?.Identity?.IsAuthenticated == true;
	public long GetIdOrThrow() => Id ?? throw new UnauthorizedAccessException("Unauthorized");

	private static long? TryFindUserId(ClaimsPrincipal? user)
	{
		var id = user?.FindFirstValue(ClaimTypes.NameIdentifier) ?? user?.FindFirst(c => c.Type.EndsWith("/sub"))?.Value;
		return long.TryParse(id, out var g) ? g : null;
	}
}



