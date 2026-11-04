// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using XiHan.Framework.Cli.Knowledge;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Cli.Integration;

/// <summary>
/// 把 MCP 服务配置与使用技能写进项目
/// </summary>
/// <remarks>
/// <see cref="Plan"/> 只算动作、不碰磁盘；<see cref="Apply"/> 写入，JSON 配置只改本服务一项，其余内容原样保留；<see cref="Check"/> 核对是否已经生效
/// </remarks>
internal static class SetupPlanner
{
    /// <summary>
    /// 支持的编码助手
    /// </summary>
    public static readonly IReadOnlyList<string> Clients = ["claude", "cursor", "vscode", "codex"];

    /// <summary>
    /// 支持的模式：只写 MCP、只写技能、两者都写
    /// </summary>
    public static readonly IReadOnlyList<string> Modes = ["mcp", "skill", "both"];

    /// <summary>
    /// 写 JSON 配置的选项：缩进、LF 换行、不转义中文与引号外的字符
    /// </summary>
    private static readonly JsonWriterOptions _writerOptions = new() { Indented = true, NewLine = "\n", Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>
    /// 启动服务端的命令
    /// </summary>
    /// <remarks>
    /// 经 <c>dnx</c> 按版本运行本工具的 <c>mcp</c> 命令；Windows 上的 Claude Code 不经 shell 启动子进程，要经 <c>cmd /c</c> 才找得到 <c>dnx</c>
    /// </remarks>
    public static (string Command, IReadOnlyList<string> Arguments) ServerCommand(string client, string version, bool windows)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(version);
        string[] arguments = ["XiHan.Framework.Cli@" + version, "--yes", "--", "mcp"];
        return client == "claude" && windows ? ("cmd", ["/c", "dnx", .. arguments]) : ("dnx", arguments);
    }

    /// <summary>
    /// 按编码助手与模式算出要做的事
    /// </summary>
    /// <param name="client">编码助手，见 <see cref="Clients"/></param>
    /// <param name="mode">模式，见 <see cref="Modes"/></param>
    /// <param name="root">项目根目录</param>
    /// <param name="version">服务端钉住的工具版本</param>
    /// <param name="windows">是否在 Windows 上</param>
    /// <param name="skill">使用技能的原文</param>
    public static IReadOnlyList<SetupAction> Plan(string client, string mode, string root, string version, bool windows, string skill)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(mode);
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(skill);
        var actions = new List<SetupAction>();
        var (command, arguments) = ServerCommand(client, version, windows);
        if (mode != "skill")
        {
            actions.Add(client switch
            {
                "claude" => new SetupAction(SetupActionKind.Json, Path.Combine(root, ".mcp.json"), "mcpServers", Entry(command, arguments, typed: false)),
                "cursor" => new SetupAction(SetupActionKind.Json, Path.Combine(root, ".cursor", "mcp.json"), "mcpServers", Entry(command, arguments, typed: false)),
                // VS Code 的服务表键是 servers，每一项要写传输类型
                "vscode" => new SetupAction(SetupActionKind.Json, Path.Combine(root, ".vscode", "mcp.json"), "servers", Entry(command, arguments, typed: true)),
                // Codex 只有用户级配置 ~/.codex/config.toml，不替用户改家目录里的文件，只给出片段
                _ => new SetupAction(SetupActionKind.Print, "~/.codex/config.toml", Content: string.Join('\n',
                    "[mcp_servers." + KnowledgeFeature.ServerName + "]",
                    "command = \"" + command + "\"",
                    "args = [" + string.Join(", ", arguments.Select(static a => "\"" + a + "\"")) + "]")),
            });
        }

        if (mode != "mcp")
        {
            var directory = client == "claude" ? Path.Combine(".claude", "skills") : Path.Combine(".agents", "skills");
            actions.Add(new SetupAction(SetupActionKind.File, Path.Combine(root, directory, KnowledgeFeature.ServerName, "SKILL.md"), Content: skill));
        }

        return actions;
    }

    /// <summary>
    /// 动作对应的片段
    /// </summary>
    /// <remarks>
    /// JSON 动作是 <c>{ 服务表: { 服务名: 配置 } }</c>，其余是要写入或打印的文本
    /// </remarks>
    public static string Snippet(SetupAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return action.Kind == SetupActionKind.Json
            ? Serialize(new JsonObject { [action.Key!] = new JsonObject { [KnowledgeFeature.ServerName] = action.Entry!.DeepClone() } })
            : action.Content ?? string.Empty;
    }

    /// <summary>
    /// 读一份 JSON 配置
    /// </summary>
    /// <remarks>
    /// 文件不存在或为空时是空对象；不是合法 JSON 对象（含注释的 JSONC 也算）时返回 <see cref="CliErrors.SetupConfigInvalid"/>，文件不动，由用户按 <c>--dry-run</c> 打印的片段手工合并
    /// </remarks>
    public static Result<JsonObject> ReadConfig(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (!File.Exists(path))
        {
            return new JsonObject();
        }

        var text = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(text))
        {
            return new JsonObject();
        }

        try
        {
            return JsonNode.Parse(text) is JsonObject config
                ? config
                : Error.Validation(CliErrors.SetupConfigInvalid, CliMessages.SetupConfigNotObject, MessageArguments.Of(("path", path)));
        }
        catch (JsonException exception)
        {
            return Error.Validation(CliErrors.SetupConfigInvalid, CliMessages.SetupConfigInvalid, MessageArguments.Of(("path", path), ("reason", exception.Message)));
        }
    }

    /// <summary>
    /// 执行一条动作
    /// </summary>
    /// <remarks>
    /// JSON 配置只替换服务表里本服务的一项，别的服务与别的顶层键原样保留；只打印的动作什么都不写
    /// </remarks>
    public static Result Apply(SetupAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (action.Kind == SetupActionKind.Print)
        {
            return Result.Success();
        }

        Directory.CreateDirectory(Path.GetDirectoryName(action.Path)!);
        if (action.Kind == SetupActionKind.File)
        {
            File.WriteAllText(action.Path, action.Content, new UTF8Encoding(false));
            return Result.Success();
        }

        var read = ReadConfig(action.Path);
        if (!read.TryGetValue(out var config))
        {
            return read.Error!;
        }

        if (config[action.Key!] is not JsonObject servers)
        {
            servers = [];
            config[action.Key!] = servers;
        }

        servers[KnowledgeFeature.ServerName] = action.Entry!.DeepClone();
        File.WriteAllText(action.Path, Serialize(config) + "\n", new UTF8Encoding(false));
        return Result.Success();
    }

    /// <summary>
    /// 核对一条动作是否已经生效
    /// </summary>
    /// <returns>未生效的说明；已生效或只打印的动作为空</returns>
    public static string? Check(SetupAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (action.Kind == SetupActionKind.Print)
        {
            return null;
        }

        if (!File.Exists(action.Path))
        {
            return CliMessages.SetupMissingFile.L(("path", action.Path));
        }

        if (action.Kind == SetupActionKind.File)
        {
            return File.ReadAllText(action.Path) == action.Content ? null : CliMessages.SetupSkillDiffers.L(("path", action.Path));
        }

        var read = ReadConfig(action.Path);
        if (!read.TryGetValue(out var config))
        {
            return read.Error!.Message;
        }

        var current = config[action.Key!]?[KnowledgeFeature.ServerName];
        if (current is null)
        {
            return CliMessages.SetupMissingEntry.L(("path", action.Path), ("server", KnowledgeFeature.ServerName));
        }

        return JsonNode.DeepEquals(current, action.Entry) ? null : CliMessages.SetupEntryDiffers.L(("path", action.Path), ("server", KnowledgeFeature.ServerName));
    }

    /// <summary>
    /// 一项 stdio 服务配置
    /// </summary>
    private static JsonObject Entry(string command, IReadOnlyList<string> arguments, bool typed)
    {
        var entry = new JsonObject();
        if (typed)
        {
            entry["type"] = "stdio";
        }

        entry["command"] = command;
        entry["args"] = new JsonArray([.. arguments.Select(static a => (JsonNode?)JsonValue.Create(a))]);
        return entry;
    }

    /// <summary>
    /// 缩进写出一个 JSON 节点
    /// </summary>
    private static string Serialize(JsonNode node)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, _writerOptions))
        {
            node.WriteTo(writer);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
