// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Cli;

/// <summary>
/// 命令行工具的错误码。
/// </summary>
/// <remarks>
/// 登记在 <c>spec/conventions/error-codes.md</c>。
/// </remarks>
internal static class CliErrors
{
    /// <summary>
    /// 应用清单文件不存在。
    /// </summary>
    public const string ManifestNotFound = "CLI_NOTFOUND_001";

    /// <summary>
    /// 应用清单不合法。
    /// </summary>
    public const string ManifestInvalid = "CLI_VALIDATION_001";

    /// <summary>
    /// 无法启动 <c>dotnet</c>。
    /// </summary>
    public const string DotnetUnavailable = "CLI_UNAVAILABLE_001";

    /// <summary>
    /// <c>migrate</c> 的路径不存在。
    /// </summary>
    public const string MigratePathNotFound = "CLI_NOTFOUND_002";

    /// <summary>
    /// <c>migrate</c> 的来源版本不支持。
    /// </summary>
    public const string MigrateVersionUnknown = "CLI_VALIDATION_002";

    /// <summary>
    /// <c>migrate --fixers</c> 里有不认识的规则编号。
    /// </summary>
    public const string MigrateFixerUnknown = "CLI_VALIDATION_003";

    /// <summary>
    /// 知识库里查不到包、能力、配置节或文档页。
    /// </summary>
    public const string KnowledgeNotFound = "CLI_NOTFOUND_003";

    /// <summary>
    /// 文档检索的参数不合法。
    /// </summary>
    /// <remarks>
    /// 既没给检索词也没给页面，或条数超出范围。
    /// </remarks>
    public const string KnowledgeSearchInvalid = "CLI_VALIDATION_004";

    /// <summary>
    /// <c>setup</c> 找不到项目根目录。
    /// </summary>
    public const string SetupRootNotFound = "CLI_NOTFOUND_004";

    /// <summary>
    /// <c>setup</c> 的 <c>--client</c> 或 <c>--mode</c> 不认识。
    /// </summary>
    public const string SetupOptionInvalid = "CLI_VALIDATION_005";

    /// <summary>
    /// <c>setup</c> 要合并的编码助手配置不是合法的 JSON 对象。
    /// </summary>
    /// <remarks>
    /// 不改动任何文件。
    /// </remarks>
    public const string SetupConfigInvalid = "CLI_VALIDATION_006";

    /// <summary>
    /// <c>setup --check</c>：配置未就绪。
    /// </summary>
    public const string SetupNotReady = "CLI_CONFLICT_001";
}
