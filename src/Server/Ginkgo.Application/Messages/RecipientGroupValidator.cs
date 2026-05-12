// 文件功能说明：
// 接收对象组校验器，确保消息创建时的接收对象规则正确。

namespace Ginkgo.Application.Messages;

/// <summary>
/// 接收对象组校验器。
/// 校验消息创建输入中主送和知会接收对象的合法性。
/// </summary>
public static class RecipientGroupValidator
{
    /// <summary>
    /// 有效的接收方式集合。
    /// </summary>
    private static readonly HashSet<string> ValidModes = new() { "all", "users", "roles", "departments" };

    /// <summary>
    /// 校验消息创建输入的接收对象规则。
    /// <para>规则：</para>
    /// <list type="number">
    ///   <item>Primary 必填且 Mode 必须是四种有效值之一</item>
    ///   <item>Cc 可选，若提供则 Mode 也必须是四种有效值之一</item>
    ///   <item>每组只能选一种 Mode（结构上已保证）</item>
    ///   <item>当 Mode 不是 all 时，Ids 不能为空</item>
    /// </list>
    /// </summary>
    /// <param name="input">消息创建输入。</param>
    /// <returns>校验结果元组，IsValid 为 true 表示通过，否则 Error 包含错误信息。</returns>
    public static (bool IsValid, string? Error) Validate(CreateMessageInput input)
    {
        if (input.Primary == null)
            return (false, "请选择主送接收对象");

        var (primaryValid, primaryError) = ValidateGroup(input.Primary, "主送");
        if (!primaryValid) return (false, primaryError);

        if (input.Cc != null)
        {
            var (ccValid, ccError) = ValidateGroup(input.Cc, "知会");
            if (!ccValid) return (false, ccError);
        }

        return (true, null);
    }

    /// <summary>
    /// 校验单个接收对象组的 Mode 和 Ids 规则。
    /// </summary>
    /// <param name="group">接收对象组输入。</param>
    /// <param name="label">组标签（主送/知会），用于错误提示。</param>
    /// <returns>校验结果元组。</returns>
    private static (bool IsValid, string? Error) ValidateGroup(RecipientGroupInput group, string label)
    {
        if (!ValidModes.Contains(group.Mode))
            return (false, $"{label}的接收方式无效，必须是 all/users/roles/departments 之一");

        if (group.Mode != "all" && (group.Ids == null || group.Ids.Count == 0))
            return (false, $"{label}选择了 {group.Mode} 方式但未指定 ID 列表");

        return (true, null);
    }
}
