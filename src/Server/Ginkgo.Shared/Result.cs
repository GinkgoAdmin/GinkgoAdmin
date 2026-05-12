// 文件功能说明：
// 提供统一的 API 返回结果模型，包括无数据与带数据两种形式，统一封装 code/message/data/traceId 等字段。

namespace Ginkgo.Shared;

/// <summary>
/// 标准返回结果（无数据）。
/// </summary>
public class Result
{
    /// <summary>
    /// 业务码。0 表示成功，非 0 表示错误码。
    /// </summary>
    public int Code { get; set; }

    /// <summary>
    /// 提示信息。
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 跟踪标识，用于日志关联与问题排查。
    /// </summary>
    public string TraceId { get; set; } = string.Empty;

    /// <summary>
    /// 构造成功返回。
    /// </summary>
    /// <param name="message">可选的成功提示，默认“ok”。</param>
    /// <returns>返回成功的 <see cref="Result"/> 实例。</returns>
    public static Result Success(string message = "成功") => new Result { Code = 0, Message = message };

    /// <summary>
    /// 构造失败返回。
    /// </summary>
    /// <param name="code">错误码。</param>
    /// <param name="message">错误信息。</param>
    /// <returns>返回失败的 <see cref="Result"/> 实例。</returns>
    public static Result Fail(int code, string message) => new Result { Code = code, Message = message };
}

/// <summary>
/// 标准返回结果（带数据）。
/// </summary>
/// <typeparam name="T">数据类型。</typeparam>
public class Result<T> : Result
{
    /// <summary>
    /// 业务数据。
    /// </summary>
    public T? Data { get; set; }

    /// <summary>
    /// 构造带数据的成功返回。
    /// </summary>
    /// <param name="data">返回的数据。</param>
    /// <param name="message">可选的成功提示，默认“ok”。</param>
    /// <returns>返回成功的 <see cref="Result{T}"/> 实例。</returns>
    public static Result<T> Success(T data, string message = "成功") => new Result<T> { Code = 0, Message = message, Data = data };

    /// <summary>
    /// 构造带数据的失败返回（不含数据）。
    /// </summary>
    /// <param name="code">错误码。</param>
    /// <param name="message">错误信息。</param>
    /// <returns>返回失败的 <see cref="Result{T}"/> 实例。</returns>
    public static new Result<T> Fail(int code, string message) => new Result<T> { Code = code, Message = message };
}


