// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace SimpleApp;

/// <summary>
/// 示例应用的消息键；各语言文案在 <c>Localization/&lt;区域&gt;.json</c>，与框架包同一格式。
/// </summary>
public static class SampleMessages
{
    /// <summary>示例资源不存在。</summary>
    public const string ResourceMissing = "samples.resource.missing";

    /// <summary>MCP tool <c>orders_create</c> 的描述（约定键 <c>&lt;操作 ID&gt;.description</c>）。</summary>
    public const string CreateOrderDescription = "orders.create.description";

    /// <summary>MCP tool <c>orders_list</c> 的描述。</summary>
    public const string ListOrdersDescription = "orders.list.description";

    /// <summary>订单名称只有空白；<c>PlaceOrderRequest</c> 自校验的文案。</summary>
    public const string OrderNameBlank = "samples.order.name_blank";
}
