// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Protocols.Grpc.Tests.Fixtures;

/// <summary>
/// 测试用订单请求；字段顺序决定 protobuf 字段号：id = 1，name = 2，page = 3，tags = 4，reason = 5。
/// </summary>
/// <param name="Id">标识。</param>
/// <param name="Name">名字；为 <c>fail</c> 时操作返回冲突。</param>
/// <param name="Page">页码。</param>
/// <param name="Tags">标签。</param>
/// <param name="Reason">原因。</param>
internal sealed record OrderRequest(string? Id, string? Name, int Page = 0, string[]? Tags = null, string? Reason = null);
