// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Primitives;

/// <summary>
/// 执行管道的固定阶段，顺序即枚举值顺序。阶段名与顺序是跨语言规范（RFC-0004）。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public enum PipelineStage
{
    /// <summary>
    /// 协议消息归一化、大小限制、构造上下文。
    /// </summary>
    Ingress = 0,

    /// <summary>
    /// Trace、日志、指标。
    /// </summary>
    Observe = 1,

    /// <summary>
    /// 超时、限流、自适应并发、熔断。
    /// </summary>
    Resilience = 2,

    /// <summary>
    /// 主体解析。
    /// </summary>
    Authenticate = 3,

    /// <summary>
    /// 租户解析与隔离上下文。
    /// </summary>
    Tenant = 4,

    /// <summary>
    /// 策略判定。
    /// </summary>
    Authorize = 5,

    /// <summary>
    /// 契约校验。
    /// </summary>
    Validate = 6,

    /// <summary>
    /// 幂等键去重。
    /// </summary>
    Idempotency = 7,

    /// <summary>
    /// 工作单元。
    /// </summary>
    Transaction = 8,

    /// <summary>
    /// 调用操作。
    /// </summary>
    Handle = 9,

    /// <summary>
    /// 结果与错误翻译、响应信封、写回传播字段。
    /// </summary>
    Egress = 10,
}
