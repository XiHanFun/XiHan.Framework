// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Protocols.Grpc.Methods;

/// <summary>
/// 从操作 ID 推导 gRPC 方法名（<c>spec/conventions/operation-ids.md</c>）。
/// </summary>
/// <remarks>
/// 服务 = 资源段的 PascalCase（连字符分段，<c>order-lines</c> → <c>OrderLines</c>），方法 = 动词的 PascalCase（<c>create</c> → <c>Create</c>）。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public static class GrpcMethodConvention
{
    /// <summary>
    /// 推导方法名。
    /// </summary>
    public static GrpcMethodName Derive(OperationContract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);
        return new GrpcMethodName(Pascal(contract.Resource), Pascal(contract.Verb));
    }

    /// <summary>
    /// 连字符分段的小写标识转 PascalCase。
    /// </summary>
    private static string Pascal(string identifier)
    {
        var builder = new StringBuilder(identifier.Length);
        foreach (var segment in identifier.Split('-', StringSplitOptions.RemoveEmptyEntries))
        {
            builder.Append(char.ToUpperInvariant(segment[0])).Append(segment, 1, segment.Length - 1);
        }

        return builder.ToString();
    }
}
