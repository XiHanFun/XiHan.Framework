// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;

namespace XiHan.Framework.Cli.Integration;

/// <summary>
/// 一条 setup 动作
/// </summary>
/// <param name="Kind">种类</param>
/// <param name="Path">目标文件；只打印时是提示用的位置</param>
/// <param name="Key">JSON 配置里服务表的键：<c>mcpServers</c> 或 <c>servers</c></param>
/// <param name="Entry">本服务的配置项</param>
/// <param name="Content">要写入或打印的文本</param>
internal sealed record SetupAction(SetupActionKind Kind, string Path, string? Key = null, JsonObject? Entry = null, string? Content = null);
