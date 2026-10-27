// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.Abstractions;

/// <summary>
/// AI 子系统的错误码（<c>conventions/error-codes.md</c>）。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class AIErrors
{
    /// <summary>
    /// AI 请求不合法（Provider 缺必填项、提示词参数缺失、工具名重复）。
    /// </summary>
    public const string RequestInvalid = "AI_VALIDATION_001";

    /// <summary>
    /// 嵌入维度与知识库配置的维度不一致。
    /// </summary>
    public const string DimensionsMismatch = "AI_VALIDATION_002";

    /// <summary>
    /// AI Provider 未配置，或其种类没有登记工厂。
    /// </summary>
    public const string ProviderNotFound = "AI_NOTFOUND_001";

    /// <summary>
    /// 提示词模板不存在。
    /// </summary>
    public const string PromptNotFound = "AI_NOTFOUND_002";

    /// <summary>
    /// AI 工具不存在。
    /// </summary>
    public const string ToolNotFound = "AI_NOTFOUND_003";

    /// <summary>
    /// 请求被护栏拦截。
    /// </summary>
    public const string GuardrailBlocked = "AI_DENIED_001";

    /// <summary>
    /// 模型调用超时。
    /// </summary>
    public const string ModelTimeout = "AI_TIMEOUT_001";

    /// <summary>
    /// 模型端点不可达或返回错误。
    /// </summary>
    public const string ModelUnavailable = "AI_UNAVAILABLE_001";

    /// <summary>
    /// 知识库存储未装配、不可达或容量已满。
    /// </summary>
    public const string KnowledgeUnavailable = "AI_UNAVAILABLE_002";

    /// <summary>
    /// 护栏、工具或 Provider 抛出未处理异常。
    /// </summary>
    public const string Unexpected = "AI_UNEXPECTED_001";
}
