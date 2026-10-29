// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace SimpleApp;

/// <summary>
/// 示例应用的消息键；各语言文案在 <c>Localization/&lt;区域&gt;.json</c>，与框架包同一格式。
/// </summary>
public static class SampleMessages
{
    /// <summary>
    /// 示例资源不存在。
    /// </summary>
    public const string ResourceMissing = "samples.resource.missing";

    /// <summary>
    /// 下单操作的标题。
    /// </summary>
    /// <remarks>
    /// 约定键 <c>&lt;操作 ID&gt;.title</c>：MCP tool 的 <c>title</c> 与 OpenAPI 的 <c>summary</c>。
    /// </remarks>
    public const string CreateOrderTitle = "orders.create.title";

    /// <summary>
    /// 下单操作的说明。
    /// </summary>
    /// <remarks>
    /// 约定键 <c>&lt;操作 ID&gt;.description</c>：MCP tool 的 <c>description</c> 与 OpenAPI 的 <c>description</c>；不写授权要求，由适配器统一呈现。
    /// </remarks>
    public const string CreateOrderDescription = "orders.create.description";

    /// <summary>
    /// 列出订单操作的标题。
    /// </summary>
    public const string ListOrdersTitle = "orders.list.title";

    /// <summary>
    /// 列出订单操作的说明。
    /// </summary>
    public const string ListOrdersDescription = "orders.list.description";

    /// <summary>
    /// 订单名称只有空白；<c>PlaceOrderRequest</c> 自校验的文案。
    /// </summary>
    public const string OrderNameBlank = "samples.order.name_blank";
}
