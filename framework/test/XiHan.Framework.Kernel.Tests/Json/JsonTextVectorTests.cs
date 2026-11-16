// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using System.Text.Json;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Kernel.Tests.Fixtures;

namespace XiHan.Framework.Kernel.Tests.Json;

/// <summary>
/// 回放共同的 JSON 读取检查的一致性向量
/// </summary>
/// <remarks>
/// <c>primitives/json-text.json</c>（RFC-0001「JSON 文本」，ADR-0236）：经请求是任意 JSON 的操作解码观察，语法错误的 <c>reason</c> 是 <c>$</c>，重复成员是第一处重复的路径
/// </remarks>
public sealed class JsonTextVectorTests
{
    /// <summary>
    /// 向量文件的内容
    /// </summary>
    private static readonly JsonTextVectors _vectors = SpecVectors.Load<JsonTextVectors>("primitives/json-text.json");

    /// <summary>
    /// 请求是任意 JSON 的描述
    /// </summary>
    private static readonly OperationDescriptor _descriptor = new OperationDescriptor<VectorJsonOperation, JsonElement, VectorOrder>(VectorJsonOperation.Contract, "vectors", VectorJsonContext.Default.JsonElement, VectorJsonContext.Default.VectorOrder);

    [Fact]
    public void DecodeRequest_FollowsTheJsonTextVectors()
    {
        Assert.NotEmpty(_vectors.Cases);
        var failures = new List<string>();
        foreach (var vector in _vectors.Cases)
        {
            var bytes = vector.Text is { } text ? Encoding.UTF8.GetBytes(text) : Convert.FromHexString(vector.Hex!);

            // 空载荷按 XGI 等价于 {}，不经这项检查；空文本的语法错误由清单读取的向量覆盖
            if (bytes.Length == 0)
            {
                continue;
            }

            var result = _descriptor.DecodeRequest(bytes);
            var actual = result.IsSuccess ? "valid" : (string)result.Error!.Details!["reason"]! == "$" ? "syntax" : "duplicate " + result.Error.Details["reason"];
            var expected = vector.Expect == "duplicate" ? "duplicate " + vector.Path : vector.Expect;
            if (actual != expected)
            {
                failures.Add($"{vector.Name}: {actual}, expected {expected}");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    /// <summary>
    /// 向量文件的对象模型
    /// </summary>
    private sealed record JsonTextVectors(JsonTextCase[] Cases);

    /// <summary>
    /// 一条样例：原文或十六进制字节、期望与重复成员的路径
    /// </summary>
    private sealed record JsonTextCase(string Name, string? Text, string? Hex, string Expect, string? Path);
}
