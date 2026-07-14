#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ApiResponseCodes
// Guid:5ad3b310-4347-4d6e-b9e4-8271db55e01e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/02/12 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.ComponentModel;
using System.Text.Json.Serialization;
using XiHan.Framework.Utils.Serialization.Json.Converters;

namespace XiHan.Framework.Application.Contracts.Enums;

/// <summary>
/// 统一返回码
/// </summary>
/// <remarks>
/// 用于统一表示接口调用结果，分两个区段：
/// <para>
/// <b>协议状态（100～599）</b>：与 HTTP Status Code 保持一致（成员采用 HTTP 官方名称），
/// 业务层可直接使用本枚举作为统一状态标识，而无需直接依赖 HTTP 状态码；
/// 该区段同样适用于微服务、消息队列、RPC 等非 HTTP 场景的通用结果表达。
/// </para>
/// <para>
/// <b>业务状态（10000～99999）</b>：表达比协议状态更细粒度的业务语义
/// （如登录过期、令牌失效、数据校验失败等），按千位分类留段扩展。
/// </para>
/// <para>
/// 枚举使用 <see cref="NumericEnumConverter{TEnum}"/> 强制序列化为 int，
/// 即使全局启用了 <see cref="JsonStringEnumConverter"/>，
/// 仍保持 <c>ApiResponse.Code</c> 输出数字，方便前端统一判断。
/// </para>
/// </remarks>
[JsonConverter(typeof(NumericEnumConverter<ApiResponseCodes>))]
public enum ApiResponseCodes
{
    #region 1xx 信息

    /// <summary>
    /// 请求已接收，客户端应继续发送剩余请求内容。
    /// </summary>
    [Description("继续请求")]
    Continue = 100,

    /// <summary>
    /// 请求成功，服务器同意切换到新的协议。
    /// </summary>
    [Description("切换协议")]
    SwitchingProtocols = 101,

    #endregion 1xx 信息

    #region 2xx 成功

    /// <summary>
    /// 请求处理成功，并返回期望的数据。
    /// </summary>
    [Description("请求成功")]
    Success = 200,

    /// <summary>
    /// 请求处理成功，并成功创建了新的资源。
    /// 通常用于 POST 创建操作。
    /// </summary>
    [Description("资源创建成功")]
    Created = 201,

    /// <summary>
    /// 请求已接受，但尚未处理完成。
    /// 通常用于异步任务提交（如导出任务、批量作业），结果需另行查询。
    /// </summary>
    [Description("请求已接受")]
    Accepted = 202,

    /// <summary>
    /// 请求处理成功，但无内容返回。
    /// 通常用于删除操作或无需响应体的更新操作。
    /// </summary>
    [Description("无内容")]
    NoContent = 204,

    #endregion 2xx 成功

    #region 3xx 重定向

    /// <summary>
    /// 请求存在多个可供选择的响应资源。
    /// </summary>
    [Description("多种响应可选")]
    MultipleChoices = 300,

    /// <summary>
    /// 请求的资源已永久移动到新的地址。
    /// 客户端应更新缓存或后续请求地址。
    /// </summary>
    [Description("永久重定向")]
    MovedPermanently = 301,

    /// <summary>
    /// 请求的资源临时移动到新的地址。
    /// 客户端应继续使用原地址发起后续请求。
    /// </summary>
    [Description("临时重定向")]
    Found = 302,

    /// <summary>
    /// 资源自上次请求后未发生变化，可继续使用本地缓存。
    /// 通常配合条件请求（ETag / If-Modified-Since）使用。
    /// </summary>
    [Description("资源未修改")]
    NotModified = 304,

    #endregion 3xx 重定向

    #region 4xx 客户端错误

    /// <summary>
    /// 请求参数错误、格式错误或缺少必要参数。
    /// </summary>
    [Description("请求错误")]
    BadRequest = 400,

    /// <summary>
    /// 当前请求未通过身份认证，需重新登录或提供有效凭据。
    /// </summary>
    [Description("未授权")]
    Unauthorized = 401,

    /// <summary>
    /// 当前用户已通过身份认证，但没有访问该资源的权限。
    /// </summary>
    [Description("禁止访问")]
    Forbidden = 403,

    /// <summary>
    /// 请求的资源不存在，或已被删除。
    /// </summary>
    [Description("资源不存在")]
    NotFound = 404,

    /// <summary>
    /// 当前资源不支持所使用的 HTTP 请求方法。
    /// </summary>
    [Description("请求方法不允许")]
    MethodNotAllowed = 405,

    /// <summary>
    /// 请求等待服务器响应超时。
    /// </summary>
    [Description("请求超时")]
    RequestTimeout = 408,

    /// <summary>
    /// 请求与服务器当前状态冲突，无法完成。
    /// 例如重复创建、乐观锁版本冲突、防重放校验失败等。
    /// </summary>
    [Description("请求冲突")]
    Conflict = 409,

    /// <summary>
    /// 请求的资源已永久删除且不再可用。
    /// 与 404 不同，明确表示资源曾经存在。
    /// </summary>
    [Description("资源已永久删除")]
    Gone = 410,

    /// <summary>
    /// 会话已锁定。
    /// 与 401 的区别在于：用户身份<b>仍然有效</b>，只是当前会话被锁住——客户端应引导用户解锁，
    /// <b>而不是</b>跳转登录页。
    /// <para>
    /// 框架不假设锁定的<b>原因</b>：锁屏只是应用侧可能的一种，也可能是风控挂起、强制改密、
    /// 二次验证未完成等。原因与解锁方式由应用侧定义。
    /// </para>
    /// </summary>
    [Description("会话已锁定")]
    Locked = 423,

    /// <summary>
    /// 请求携带的媒体类型不受支持。
    /// 例如接口仅接受 application/json 却收到其它 Content-Type。
    /// </summary>
    [Description("媒体类型不支持")]
    UnsupportedMediaType = 415,

    /// <summary>
    /// 请求参数格式正确，但业务语义校验未通过。
    /// 例如模型验证失败、字段约束冲突等。
    /// </summary>
    [Description("请求语义错误")]
    UnprocessableEntity = 422,

    /// <summary>
    /// 请求次数超过系统限制，请稍后再试。
    /// 常用于限流或防刷场景。
    /// </summary>
    [Description("请求过于频繁")]
    TooManyRequests = 429,

    #endregion 4xx 客户端错误

    #region 5xx 服务端错误

    /// <summary>
    /// 服务器处理请求时发生未预期的异常。
    /// </summary>
    [Description("服务器内部错误")]
    InternalServerError = 500,

    /// <summary>
    /// 当前服务器尚未实现请求所需功能。
    /// </summary>
    [Description("功能未实现")]
    NotImplemented = 501,

    /// <summary>
    /// 网关或代理从上游服务收到无效响应。
    /// </summary>
    [Description("网关错误")]
    BadGateway = 502,

    /// <summary>
    /// 服务器当前不可用，请稍后重试。
    /// 通常用于系统维护、服务过载或依赖服务不可用。
    /// </summary>
    [Description("服务不可用")]
    ServiceUnavailable = 503,

    /// <summary>
    /// 网关或代理等待上游服务响应超时。
    /// </summary>
    [Description("网关超时")]
    GatewayTimeout = 504,

    #endregion 5xx 服务端错误

    #region 10xxx 认证与授权（业务状态）

    /// <summary>
    /// 登录状态已过期，需重新登录。
    /// 区别于 401：明确表达"曾经登录、现已过期"的语义，前端可据此引导重新登录而非提示无权限。
    /// </summary>
    [Description("登录已过期")]
    LoginExpired = 10001,

    /// <summary>
    /// 令牌无效（格式错误、签名不合法或已被吊销）。
    /// </summary>
    [Description("令牌无效")]
    TokenInvalid = 10002,

    /// <summary>
    /// 令牌已过期，可尝试使用刷新令牌换取新令牌。
    /// </summary>
    [Description("令牌已过期")]
    TokenExpired = 10003,

    /// <summary>
    /// 当前用户缺少执行该操作所需的权限码。
    /// 区别于 403：面向细粒度权限点（如按钮/字段级），便于前端定位具体缺失的权限。
    /// </summary>
    [Description("权限不足")]
    PermissionDenied = 10004,

    #endregion 10xxx 认证与授权（业务状态）

    #region 11xxx 数据校验（业务状态）

    /// <summary>
    /// 数据校验失败（字段格式、取值范围、唯一性等约束未通过）。
    /// 校验明细通常置于响应 Data 中逐项返回。
    /// </summary>
    [Description("数据校验失败")]
    ValidationFailed = 11000,

    #endregion 11xxx 数据校验（业务状态）

    #region 12xxx 业务处理（业务状态）

    /// <summary>
    /// 业务处理失败（业务规则不满足、状态机不允许当前操作等）。
    /// 具体原因由 Message / Data 说明。
    /// </summary>
    [Description("业务处理失败")]
    BusinessFailed = 12000,

    #endregion 12xxx 业务处理（业务状态）

    #region 13xxx 数据访问（业务状态）

    /// <summary>
    /// 数据库访问异常（连接失败、执行超时、约束冲突等持久化层错误）。
    /// </summary>
    [Description("数据访问异常")]
    DatabaseError = 13000,

    #endregion 13xxx 数据访问（业务状态）

    #region 14xxx 外部依赖（业务状态）

    /// <summary>
    /// 第三方服务调用异常（外部接口不可用、返回错误或超时）。
    /// </summary>
    [Description("第三方服务异常")]
    ThirdPartyServiceError = 14000

    #endregion 14xxx 外部依赖（业务状态）
}
