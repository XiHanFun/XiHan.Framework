// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Operations;

/// <summary>
/// 操作对外暴露的协议
/// </summary>
/// <remarks>
/// 协议适配器只发布包含自己的操作
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
[Flags]
public enum Expose
{
    /// <summary>
    /// 不对外暴露，只能进程内调用
    /// </summary>
    None = 0,

    /// <summary>
    /// HTTP（<c>Protocols.Http</c>）
    /// </summary>
    Http = 1,

    /// <summary>
    /// gRPC（<c>Protocols.Grpc</c>）
    /// </summary>
    Grpc = 2,

    /// <summary>
    /// MCP tool（<c>Protocols.Mcp</c>）
    /// </summary>
    Mcp = 4,

    /// <summary>
    /// 命令行（<c>Hosting.CommandLine</c>）
    /// </summary>
    CommandLine = 8,

    /// <summary>
    /// 成为 <c>ai.tools@1</c> 目录里的模型工具（RFC-0022）
    /// </summary>
    Ai = 16,

    /// <summary>
    /// 5.0 的五个协议
    /// </summary>
    /// <remarks>
    /// 值冻结为上面五个协议之和，以后新增的协议不在其中：已经发布的操作不会因为框架加了协议适配器就被自动暴露出去，要在新协议上发布得显式开启。
    /// 枚举常量在编译时写进调用方程序集，冻结也让已编译的程序集与重新编译的程序集含义一致（ADR-0136）
    /// </remarks>
    All = Http | Grpc | Mcp | CommandLine | Ai,
}
