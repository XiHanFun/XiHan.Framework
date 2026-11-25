// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Kernel.Tests.Fixtures;
using XiHan.Framework.Testing.Workspace;

namespace XiHan.Framework.Kernel.Tests.Operations;

/// <summary>
/// 回放载荷错误路径的一致性向量
/// </summary>
/// <remarks>
/// <c>operations/payload-reasons.json</c>：载荷能否解码、不合法时 <c>reason</c> 的路径（RFC-0011 规则 2，ADR-0235、ADR-0236），经真实的 <see cref="OperationDescriptor"/> 解码，参考实现与各语言 SDK 必须给出同样的结果
/// </remarks>
public sealed class PayloadReasonVectorTests
{
    /// <summary>
    /// 向量文件的内容
    /// </summary>
    private static readonly PayloadVectors _vectors = SpecVectors.Read<PayloadVectors>("operations", "payload-reasons.json");

    /// <summary>
    /// 向量里的三个请求类型对应的描述
    /// </summary>
    private static readonly Dictionary<string, OperationDescriptor> _descriptors = new(StringComparer.Ordinal)
    {
        ["counts"] = new OperationDescriptor<VectorCountsOperation, Dictionary<string, int>, VectorOrder>(VectorCountsOperation.Contract, "vectors", VectorJsonContext.Default.DictionaryStringInt32, VectorJsonContext.Default.VectorOrder),
        ["order"] = new OperationDescriptor<VectorOrderOperation, VectorOrder, VectorOrder>(VectorOrderOperation.Contract, "vectors", VectorJsonContext.Default.VectorOrder, VectorJsonContext.Default.VectorOrder),
        ["profile"] = new OperationDescriptor<VectorProfileOperation, VectorProfile, VectorOrder>(VectorProfileOperation.Contract, "vectors", VectorJsonContext.Default.VectorProfile, VectorJsonContext.Default.VectorOrder),
    };

    [Fact]
    public void DecodeRequest_ReportsTheVectorPath()
    {
        Assert.NotEmpty(_vectors.Cases);
        // 向量说明了几个请求类型，这里就有几个对应的描述
        Assert.Equal(_vectors.Requests.Keys.Order(StringComparer.Ordinal), _descriptors.Keys.Order(StringComparer.Ordinal));
        var failures = new List<string>();
        foreach (var vector in _vectors.Cases)
        {
            var result = _descriptors[vector.Request].DecodeRequest(Encoding.UTF8.GetBytes(vector.Payload));
            if (result.IsSuccess)
            {
                if (vector.Reason is not null)
                {
                    failures.Add($"{vector.Name}: decoded, expected reason '{vector.Reason}'");
                }

                continue;
            }

            var reason = result.Error!.Code == KernelErrors.InvalidOperationPayload && result.Error.Details!.TryGetValue("reason", out var value) ? value as string : null;
            if (reason is null || reason != vector.Reason)
            {
                failures.Add($"{vector.Name}: reason '{reason ?? "<not an invalid payload>"}', expected '{vector.Reason ?? "<decoded>"}'");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    /// <summary>
    /// 向量文件的对象模型
    /// </summary>
    /// <param name="Requests">请求类型的名字到给各语言看的声明说明</param>
    /// <param name="Cases">解码的用例</param>
    private sealed record PayloadVectors(Dictionary<string, string> Requests, PayloadCase[] Cases);

    /// <summary>
    /// 一次解码：载荷原文与期望的 reason，<see langword="null"/> 表示解码成功
    /// </summary>
    private sealed record PayloadCase(string Name, string Request, string Payload, string? Reason);
}
