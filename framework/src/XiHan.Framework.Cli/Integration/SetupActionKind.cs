// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Cli.Integration;

/// <summary>
/// 一条 setup 动作的种类
/// </summary>
internal enum SetupActionKind
{
    /// <summary>
    /// 把服务配置合并进 JSON 配置文件，只改本服务一项
    /// </summary>
    Json,

    /// <summary>
    /// 整份写入文件（使用技能）
    /// </summary>
    File,

    /// <summary>
    /// 只打印片段，由用户自己放进配置（Codex 的用户级配置）
    /// </summary>
    Print,
}
