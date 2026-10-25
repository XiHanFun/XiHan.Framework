// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Protocols.Grpc.Tests.Fixtures;

/// <summary>
/// 测试用订单响应；字段号：operation = 1，id = 2，name = 3，page = 4，tags = 5，reason = 6。
/// </summary>
/// <param name="Operation">处理请求的操作 ID。</param>
/// <param name="Id">绑定到的标识。</param>
/// <param name="Name">绑定到的名字。</param>
/// <param name="Page">绑定到的页码。</param>
/// <param name="Tags">绑定到的标签。</param>
/// <param name="Reason">绑定到的原因。</param>
internal sealed record OrderReply(string Operation, string? Id, string? Name, int Page, string[] Tags, string? Reason);
