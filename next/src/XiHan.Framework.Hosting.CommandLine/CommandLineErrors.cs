// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Hosting.CommandLine;

/// <summary>
/// 命令行宿主的错误码。
/// </summary>
/// <remarks>
/// 登记在 <c>spec/conventions/error-codes.md</c>。失败以 JSON 写到 stderr，并映射为退出码（<see cref="CommandLineExitCodes"/>）。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public static class CommandLineErrors
{
    /// <summary>
    /// 命令行用法不合法：选项值、载荷或数据文件有误。
    /// </summary>
    public const string UsageInvalid = "HOSTING_VALIDATION_001";

    /// <summary>
    /// 操作未登记或未暴露给命令行。
    /// </summary>
    public const string OperationNotExposed = "HOSTING_NOTFOUND_001";

    /// <summary>
    /// 命令不存在。
    /// </summary>
    public const string CommandUnknown = "HOSTING_NOTFOUND_002";
}
