// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Cli;

/// <summary>
/// 命令行工具的错误码；登记在 <c>spec/conventions/error-codes.md</c>。
/// </summary>
internal static class CliErrors
{
    /// <summary>应用清单文件不存在。</summary>
    public const string ManifestNotFound = "CLI_NOTFOUND_001";

    /// <summary>应用清单不合法。</summary>
    public const string ManifestInvalid = "CLI_VALIDATION_001";

    /// <summary>无法启动 <c>dotnet</c>。</summary>
    public const string DotnetUnavailable = "CLI_UNAVAILABLE_001";
}
