// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Localization.Abstractions.Messages;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.Abstractions;

/// <summary>
/// AI 子系统的消息键。
/// </summary>
/// <remarks>
/// 各语言文案在本包的 <c>Localization/&lt;区域&gt;.json</c>（RFC-0010）。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public static class AIMessages
{
    /// <summary>
    /// 目录名。
    /// </summary>
    public const string CatalogName = "ai";

    /// <summary>
    /// Provider 未配置。
    /// </summary>
    /// <remarks>
    /// 参数：<c>name</c>。
    /// </remarks>
    public const string ProviderNotFound = "ai.provider.not_found";

    /// <summary>
    /// Provider 种类没有登记工厂。
    /// </summary>
    /// <remarks>
    /// 参数：<c>name</c>、<c>kind</c>。
    /// </remarks>
    public const string ProviderKindUnknown = "ai.provider.kind_unknown";

    /// <summary>
    /// Provider 配置不合法。
    /// </summary>
    /// <remarks>
    /// 参数：<c>name</c>、<c>reason</c>。
    /// </remarks>
    public const string ProviderInvalid = "ai.provider.invalid";

    /// <summary>
    /// 提示词模板不存在。
    /// </summary>
    /// <remarks>
    /// 参数：<c>name</c>。
    /// </remarks>
    public const string PromptNotFound = "ai.prompt.not_found";

    /// <summary>
    /// 提示词参数缺失。
    /// </summary>
    /// <remarks>
    /// 参数：<c>name</c>、<c>argument</c>。
    /// </remarks>
    public const string PromptArgumentMissing = "ai.prompt.argument_missing";

    /// <summary>
    /// 工具不存在。
    /// </summary>
    /// <remarks>
    /// 参数：<c>name</c>。
    /// </remarks>
    public const string ToolNotFound = "ai.tool.not_found";

    /// <summary>
    /// 工具名重复。
    /// </summary>
    /// <remarks>
    /// 参数：<c>name</c>、<c>first</c>、<c>second</c>。
    /// </remarks>
    public const string ToolDuplicate = "ai.tool.duplicate";

    /// <summary>
    /// 工具参数无法序列化。
    /// </summary>
    /// <remarks>
    /// 参数：<c>name</c>、<c>argument</c>。
    /// </remarks>
    public const string ToolArgumentInvalid = "ai.tool.argument_invalid";

    /// <summary>
    /// 请求被护栏拦截。
    /// </summary>
    /// <remarks>
    /// 参数：<c>guardrail</c>、<c>reason</c>。
    /// </remarks>
    public const string GuardrailBlocked = "ai.guardrail.blocked";

    /// <summary>
    /// 护栏检查失败。
    /// </summary>
    /// <remarks>
    /// 参数：<c>guardrail</c>、<c>reason</c>。
    /// </remarks>
    public const string GuardrailFailed = "ai.guardrail.failed";

    /// <summary>
    /// 命中敏感词。
    /// </summary>
    /// <remarks>
    /// 参数：<c>keyword</c>。
    /// </remarks>
    public const string GuardrailKeyword = "ai.guardrail.keyword";

    /// <summary>
    /// 疑似提示注入。
    /// </summary>
    public const string GuardrailInjection = "ai.guardrail.injection";

    /// <summary>
    /// 模型端点不可达或返回错误。
    /// </summary>
    /// <remarks>
    /// 参数：<c>provider</c>、<c>reason</c>。
    /// </remarks>
    public const string ModelUnavailable = "ai.model.unavailable";

    /// <summary>
    /// 模型调用超时。
    /// </summary>
    /// <remarks>
    /// 参数：<c>provider</c>。
    /// </remarks>
    public const string ModelTimeout = "ai.model.timeout";

    /// <summary>
    /// 嵌入维度不一致。
    /// </summary>
    /// <remarks>
    /// 参数：<c>actual</c>、<c>expected</c>。
    /// </remarks>
    public const string KnowledgeDimensions = "ai.knowledge.dimensions";

    /// <summary>
    /// 知识库存储未装配或不可达。
    /// </summary>
    /// <remarks>
    /// 参数：<c>reason</c>。
    /// </remarks>
    public const string KnowledgeUnavailable = "ai.knowledge.unavailable";

    /// <summary>
    /// 知识库存储容量已满。
    /// </summary>
    /// <remarks>
    /// 参数：<c>collection</c>、<c>limit</c>。
    /// </remarks>
    public const string KnowledgeCapacity = "ai.knowledge.capacity";

    /// <summary>
    /// 请求不合法。
    /// </summary>
    /// <remarks>
    /// 参数：<c>reason</c>。
    /// </remarks>
    public const string RequestInvalid = "ai.request.invalid";

    /// <summary>
    /// 本包的消息目录。
    /// </summary>
    public static MessageCatalog Catalog => MessageCatalog.FromEmbeddedResources(CatalogName, typeof(AIMessages));
}
