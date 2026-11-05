// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;

namespace XiHan.Framework.Hosting.CommandLine.Tests;

/// <summary>
/// 带请求 Schema 的测试请求，覆盖各种字段类型
/// </summary>
/// <param name="Name">字符串字段</param>
/// <param name="Times">带范围的整数</param>
/// <param name="Total">可空的 64 位整数</param>
/// <param name="Ratio">可空的浮点数</param>
/// <param name="Verbose">布尔字段</param>
/// <param name="Level">可空引用到整数定义</param>
/// <param name="Tone">可空引用到字符串枚举定义</param>
/// <param name="Sizes">整数数组</param>
/// <param name="Tags">字符串数组</param>
/// <param name="Notes">不受约束的任意 JSON</param>
internal sealed record TypedRequest(string Name, int Times = 1, long? Total = null, double? Ratio = null, bool Verbose = false, int? Level = null, string? Tone = null, int[]? Sizes = null, string[]? Tags = null, JsonElement? Notes = null);
