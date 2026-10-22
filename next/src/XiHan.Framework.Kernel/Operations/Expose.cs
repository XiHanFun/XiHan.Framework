// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Operations;

/// <summary>
/// 操作对外暴露的协议；协议适配器只发布包含自己的操作。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
[Flags]
public enum Expose
{
    /// <summary>不对外暴露，只能进程内调用。</summary>
    None = 0,

    /// <summary>HTTP（<c>Protocols.Http</c>）。</summary>
    Http = 1,

    /// <summary>gRPC（<c>Protocols.Grpc</c>）。</summary>
    Grpc = 2,

    /// <summary>MCP tool（<c>Protocols.Mcp</c>）。</summary>
    Mcp = 4,

    /// <summary>命令行（<c>Hosting.CommandLine</c>）。</summary>
    CommandLine = 8,

    /// <summary>全部协议。</summary>
    All = Http | Grpc | Mcp | CommandLine,
}
