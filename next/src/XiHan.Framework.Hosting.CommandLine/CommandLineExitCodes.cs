// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Hosting.CommandLine;

/// <summary>
/// 退出码：成功 0，用法错误 64（BSD <c>EX_USAGE</c>），调用失败按错误码的 KIND 段映射。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class CommandLineExitCodes
{
    /// <summary>成功。</summary>
    public const int Success = 0;

    /// <summary>未预期的失败（UNEXPECTED 与未知 KIND）。</summary>
    public const int Unexpected = 1;

    /// <summary>VALIDATION。</summary>
    public const int Validation = 2;

    /// <summary>NOTFOUND。</summary>
    public const int NotFound = 3;

    /// <summary>DENIED。</summary>
    public const int Denied = 4;

    /// <summary>CONFLICT。</summary>
    public const int Conflict = 5;

    /// <summary>UNAVAILABLE。</summary>
    public const int Unavailable = 6;

    /// <summary>TIMEOUT。</summary>
    public const int Timeout = 7;

    /// <summary>CANCELLED。</summary>
    public const int Cancelled = 8;

    /// <summary>命令行用法错误。</summary>
    public const int Usage = 64;

    /// <summary>
    /// 错误码的 KIND 段 → 退出码。
    /// </summary>
    public static int For(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return error.Kind switch
        {
            "VALIDATION" => Validation,
            "NOTFOUND" => NotFound,
            "DENIED" => Denied,
            "CONFLICT" => Conflict,
            "UNAVAILABLE" => Unavailable,
            "TIMEOUT" => Timeout,
            "CANCELLED" => Cancelled,
            _ => Unexpected,
        };
    }
}
