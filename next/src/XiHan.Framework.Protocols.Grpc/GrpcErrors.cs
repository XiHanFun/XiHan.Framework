// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Protocols.Grpc;

/// <summary>
/// gRPC 协议适配器的错误码。
/// </summary>
/// <remarks>
/// 登记在 <c>spec/conventions/error-codes.md</c>。调用期错误随 gRPC 尾部元数据 <c>xihan-error-code</c> 返回，启动期错误以 <see cref="FrameworkException"/> 抛出。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public static class GrpcErrors
{
    /// <summary>
    /// 请求不是合法的 protobuf 消息，或与操作的 Schema 不符。
    /// </summary>
    public const string RequestMalformed = "PROTOCOLS_VALIDATION_005";

    /// <summary>
    /// 暴露给 gRPC 的操作没有 Schema，或其 Schema 无法映射为 protobuf（启动失败）。
    /// </summary>
    public const string SchemaUnsupported = "PROTOCOLS_VALIDATION_006";

    /// <summary>
    /// 推导或配置的服务名 / 方法名不合法（启动失败）。
    /// </summary>
    public const string InvalidMethodName = "PROTOCOLS_VALIDATION_007";

    /// <summary>
    /// 两个操作得到同一个服务 / 方法名（启动失败）。
    /// </summary>
    public const string DuplicateMethod = "PROTOCOLS_CONFLICT_002";

    /// <summary>
    /// 同名 protobuf 消息在不同操作里形状不同（启动失败）。
    /// </summary>
    public const string MessageConflict = "PROTOCOLS_CONFLICT_003";

    /// <summary>
    /// 操作的响应无法按 Schema 编码为 protobuf。
    /// </summary>
    public const string ResponseUnencodable = "PROTOCOLS_UNEXPECTED_001";
}
