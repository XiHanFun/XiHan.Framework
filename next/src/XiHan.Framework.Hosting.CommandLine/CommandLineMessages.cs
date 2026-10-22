// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Hosting.CommandLine;

/// <summary>
/// 命令行宿主的消息键：用法、命令说明与用法错误；各语言文案在本包的 <c>Localization/&lt;区域&gt;.json</c>，<c>AddXiHanCommandLine()</c> 注册 <see cref="Catalog"/>。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class CommandLineMessages
{
    /// <summary>
    /// 目录名。
    /// </summary>
    public const string CatalogName = "hosting.commandline";

    /// <summary>用法行。参数：<c>program</c>。</summary>
    public const string Usage = "hosting.commandline.usage";

    /// <summary>命令列表的标题。</summary>
    public const string Commands = "hosting.commandline.commands";

    /// <summary><c>&lt;resource&gt; &lt;verb&gt;</c> 简写的说明。</summary>
    public const string OperationShorthand = "hosting.commandline.operation_shorthand";

    /// <summary>命令不存在。参数：<c>command</c>。</summary>
    public const string CommandUnknown = "hosting.commandline.command_unknown";

    /// <summary><c>help</c> 命令的说明。</summary>
    public const string HelpDescription = "hosting.commandline.help_description";

    /// <summary><c>graph</c> 命令的说明。</summary>
    public const string GraphDescription = "hosting.commandline.graph_description";

    /// <summary><c>pipeline</c> 命令的说明。</summary>
    public const string PipelineDescription = "hosting.commandline.pipeline_description";

    /// <summary><c>operations</c> 命令的说明。</summary>
    public const string OperationsDescription = "hosting.commandline.operations_description";

    /// <summary><c>invoke</c> 命令的说明。</summary>
    public const string InvokeDescription = "hosting.commandline.invoke_description";

    /// <summary><c>invoke</c> 缺少操作 ID。</summary>
    public const string InvokeOperationMissing = "hosting.commandline.invoke_operation_missing";

    /// <summary><c>--json</c> 不是 JSON 对象。参数：<c>reason</c>。</summary>
    public const string InvokeJsonInvalid = "hosting.commandline.invoke_json_invalid";

    /// <summary><c>--data</c> 指向的文件不存在。参数：<c>path</c>。</summary>
    public const string InvokeDataMissing = "hosting.commandline.invoke_data_missing";

    /// <summary><c>--timeout</c> 不是正数秒。</summary>
    public const string InvokeTimeoutInvalid = "hosting.commandline.invoke_timeout_invalid";

    /// <summary><c>--authorization</c> 格式不对。</summary>
    public const string InvokeAuthorizationInvalid = "hosting.commandline.invoke_authorization_invalid";

    /// <summary>操作未登记或未暴露给命令行。参数：<c>id</c>。</summary>
    public const string OperationNotExposed = "hosting.commandline.operation_not_exposed";

    /// <summary>
    /// 本包的消息目录。
    /// </summary>
    public static MessageCatalog Catalog => MessageCatalog.FromEmbeddedResources(CatalogName, typeof(CommandLineMessages));
}
