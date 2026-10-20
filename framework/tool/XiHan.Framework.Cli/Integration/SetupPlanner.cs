// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace XiHan.Framework.Cli.Integration;

/// <summary>
/// 一条 setup 动作
/// </summary>
/// <param name="Kind">json：合并进 JSON 配置；file：写文件；print：只打印片段</param>
/// <param name="Path">目标文件，print 时是提示用的位置</param>
/// <param name="Key">JSON 配置里服务表的键：mcpServers 或 servers</param>
/// <param name="Entry">服务配置</param>
/// <param name="Content">要写入或打印的文本</param>
internal sealed record SetupAction(string Kind, string Path, string? Key = null, JsonObject? Entry = null, string? Content = null);

/// <summary>
/// 把 MCP 配置与使用技能写进项目
/// </summary>
internal static class SetupPlanner
{
    /// <summary>
    /// MCP 配置里的服务名
    /// </summary>
    public const string ServerName = "xihan-framework";

    /// <summary>
    /// 支持的客户端
    /// </summary>
    public static readonly IReadOnlyList<string> Clients = ["claude", "cursor", "vscode", "codex"];

    /// <summary>
    /// 支持的模式
    /// </summary>
    public static readonly IReadOnlyList<string> Modes = ["mcp", "skill", "both"];

    private static readonly JsonSerializerOptions IndentedJson = new() { WriteIndented = true, NewLine = "\n", Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>
    /// 启动服务端的命令：经 dnx 按版本运行；Windows 上的 Claude Code 需要经 cmd /c 调用
    /// </summary>
    public static (string Command, string[] Args) ServerCommand(string client, string version, bool windows)
    {
        string[] args = [$"XiHan.Framework.Cli@{version}", "--yes", "--", "mcp"];
        return client == "claude" && windows ? ("cmd", ["/c", "dnx", .. args]) : ("dnx", args);
    }

    /// <summary>
    /// 按客户端与模式算出要做的事，不碰磁盘
    /// </summary>
    public static IReadOnlyList<SetupAction> Plan(string client, string mode, string root, string version, bool windows, string skill)
    {
        var actions = new List<SetupAction>();
        var (command, args) = ServerCommand(client, version, windows);
        JsonObject Entry(bool typed)
        {
            var entry = new JsonObject();
            if (typed)
            {
                entry["type"] = "stdio";
            }

            entry["command"] = command;
            entry["args"] = new JsonArray(args.Select(a => (JsonNode?)JsonValue.Create(a)).ToArray());
            return entry;
        }

        if (mode != "skill")
        {
            switch (client)
            {
                case "claude":
                    actions.Add(new SetupAction("json", System.IO.Path.Combine(root, ".mcp.json"), "mcpServers", Entry(false)));
                    break;
                case "cursor":
                    actions.Add(new SetupAction("json", System.IO.Path.Combine(root, ".cursor", "mcp.json"), "mcpServers", Entry(false)));
                    break;
                case "vscode":
                    actions.Add(new SetupAction("json", System.IO.Path.Combine(root, ".vscode", "mcp.json"), "servers", Entry(true)));
                    break;
                case "codex":
                    actions.Add(new SetupAction("print", "~/.codex/config.toml", Content: string.Join('\n',
                        $"[mcp_servers.{ServerName}]",
                        $"command = \"{command}\"",
                        $"args = [{string.Join(", ", args.Select(a => $"\"{a}\""))}]")));
                    break;
            }
        }

        if (mode != "mcp")
        {
            var directory = client == "claude" ? System.IO.Path.Combine(".claude", "skills") : System.IO.Path.Combine(".agents", "skills");
            actions.Add(new SetupAction("file", System.IO.Path.Combine(root, directory, ServerName, "SKILL.md"), Content: skill));
        }

        return actions;
    }

    /// <summary>
    /// 一条动作的说明
    /// </summary>
    public static string Describe(SetupAction action)
    {
        return action.Kind switch
        {
            "json" => $"在 {action.Path} 的 {action.Key} 里写入 {ServerName}：\n{new JsonObject { [action.Key!] = new JsonObject { [ServerName] = action.Entry!.DeepClone() } }.ToJsonString(IndentedJson)}",
            "file" => $"写入 {action.Path}",
            _ => $"把下面这段加进 {action.Path}：\n{action.Content}",
        };
    }

    /// <summary>
    /// 执行动作：JSON 配置只改本服务一项，其余内容原样保留
    /// </summary>
    public static void Apply(SetupAction action)
    {
        if (action.Kind == "print")
        {
            return;
        }

        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(action.Path)!);
        if (action.Kind == "file")
        {
            File.WriteAllText(action.Path, action.Content);
            return;
        }

        var config = ReadConfig(action.Path);
        if (config[action.Key!] is not JsonObject servers)
        {
            servers = [];
            config[action.Key!] = servers;
        }

        servers[ServerName] = action.Entry!.DeepClone();
        File.WriteAllText(action.Path, config.ToJsonString(IndentedJson) + "\n");
    }

    /// <summary>
    /// 核对动作是否已经生效；返回未生效的说明
    /// </summary>
    public static string? Check(SetupAction action)
    {
        if (action.Kind == "print")
        {
            return null;
        }

        if (!File.Exists(action.Path))
        {
            return $"缺少 {action.Path}";
        }

        if (action.Kind == "file")
        {
            return File.ReadAllText(action.Path) == action.Content ? null : $"{action.Path} 与当前版本的技能不一致";
        }

        var current = ReadConfig(action.Path)[action.Key!]?[ServerName];
        if (current is null)
        {
            return $"{action.Path} 里没有 {ServerName}";
        }

        return JsonNode.DeepEquals(current, action.Entry) ? null : $"{action.Path} 里的 {ServerName} 与期望不一致";
    }

    private static JsonObject ReadConfig(string path)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        var text = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        try
        {
            return JsonNode.Parse(text) as JsonObject ?? throw new InvalidOperationException($"无法解析 {path}（顶层不是对象），文件未改动；请按 --dry-run 打印的片段手动合并");
        }
        catch (JsonException error)
        {
            throw new InvalidOperationException($"无法解析 {path}（{error.Message}），文件未改动；请按 --dry-run 打印的片段手动合并", error);
        }
    }
}
