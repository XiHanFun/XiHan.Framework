// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;

namespace XiHan.Framework.Kernel.Tests.Fixtures;

/// <summary>
/// 载荷向量里的 profile 请求：必填成员与各种取值
/// </summary>
internal sealed class VectorProfile
{
    /// <summary>
    /// 必填的字符串
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// 可选的字符串
    /// </summary>
    public string? Note { get; set; }

    /// <summary>
    /// 可选的 64 位整数
    /// </summary>
    public long Count { get; set; }

    /// <summary>
    /// 可选的双精度浮点
    /// </summary>
    public double Ratio { get; set; }

    /// <summary>
    /// 可选的布尔
    /// </summary>
    public bool Flag { get; set; }

    /// <summary>
    /// 可选的字符串数组
    /// </summary>
    public List<string>? Tags { get; set; }

    /// <summary>
    /// 可选的 64 位整数数组
    /// </summary>
    public List<long>? Scores { get; set; }

    /// <summary>
    /// 可选的嵌套对象
    /// </summary>
    public VectorProfileInner? Inner { get; set; }

    /// <summary>
    /// 可选的任意 JSON
    /// </summary>
    public JsonElement? Meta { get; set; }
}
