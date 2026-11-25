// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using System.Text.Json;
using XiHan.Framework.Kernel.Json;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Kernel.Tests.Fixtures;
using XiHan.Framework.Testing.Workspace;

namespace XiHan.Framework.Kernel.Tests.Json;

/// <summary>
/// 回放共同的 JSON 读取检查的一致性向量
/// </summary>
/// <remarks>
/// <c>primitives/json-text.json</c>（RFC-0001「JSON 文本」，ADR-0236）：直接经 <see cref="JsonText.FindProblem"/> 回放（除重复成员之外的种类都是向量的 <c>syntax</c>，ADR-0291），再经请求是任意 JSON 的操作解码观察，语法错误的 <c>reason</c> 是 <c>$</c>，重复成员是第一处重复的路径
/// </remarks>
public sealed class JsonTextVectorTests
{
    /// <summary>
    /// 向量文件的内容
    /// </summary>
    private static readonly JsonTextVectors _vectors = SpecVectors.Read<JsonTextVectors>("primitives", "json-text.json");

    /// <summary>
    /// 请求是任意 JSON 的描述
    /// </summary>
    private static readonly OperationDescriptor _descriptor = new OperationDescriptor<VectorJsonOperation, JsonElement, VectorOrder>(VectorJsonOperation.Contract, "vectors", VectorJsonContext.Default.JsonElement, VectorJsonContext.Default.VectorOrder);

    [Fact]
    public void FindProblem_FollowsTheJsonTextVectors()
    {
        Assert.NotEmpty(_vectors.Cases);
        var failures = new List<string>();
        foreach (var vector in _vectors.Cases)
        {
            var bytes = vector.Text is { } text ? Encoding.UTF8.GetBytes(text) : Convert.FromHexString(vector.Hex!);
            var actual = JsonText.FindProblem(bytes).Match(
                static problem => problem.Kind == JsonTextProblemKind.DuplicateMember ? "duplicate " + problem.Path : "syntax " + problem.Path,
                static () => "valid");

            // 语法错误的位置一律是 $
            var expected = vector.Expect switch
            {
                "duplicate" => "duplicate " + vector.Path,
                "syntax" => "syntax $",
                _ => vector.Expect,
            };
            if (actual != expected)
            {
                failures.Add($"{vector.Name}: {actual}, expected {expected}");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

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
    private sealed record JsonTextCase(string Name, string Expect, string? Text = null, string? Hex = null, string? Path = null);
}
