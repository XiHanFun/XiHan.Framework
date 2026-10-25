// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.CodeAnalysis;
using XiHan.Framework.Kernel.Generators.Json;

namespace XiHan.Framework.Kernel.Generators;

/// <summary>
/// 一个待生成的操作：类、契约字段、请求与响应类型及其序列化上下文属性。
/// </summary>
internal sealed class OperationModel
{
    /// <summary>
    /// 创建模型。
    /// </summary>
    public OperationModel(INamedTypeSymbol operationType, string id, int stability, int expose, string? policy, string? permission, ITypeSymbol requestType, ITypeSymbol responseType)
    {
        OperationType = operationType;
        Id = id;
        Stability = stability;
        Expose = expose;
        Policy = policy;
        Permission = permission;
        RequestType = requestType;
        ResponseType = responseType;
    }

    /// <summary>操作实现类型。</summary>
    public INamedTypeSymbol OperationType { get; }

    /// <summary>操作 ID。</summary>
    public string Id { get; }

    /// <summary>稳定性（枚举整数值）。</summary>
    public int Stability { get; }

    /// <summary>暴露的协议（标志整数值）。</summary>
    public int Expose { get; }

    /// <summary>策略名。</summary>
    public string? Policy { get; }

    /// <summary>权限码。</summary>
    public string? Permission { get; }

    /// <summary>请求类型。</summary>
    public ITypeSymbol RequestType { get; }

    /// <summary>响应类型。</summary>
    public ITypeSymbol ResponseType { get; }

    /// <summary>请求类型所在的序列化上下文。</summary>
    public JsonContextModel? RequestContext { get; set; }

    /// <summary>请求类型在上下文 <c>Default</c> 上的属性名。</summary>
    public string? RequestProperty { get; set; }

    /// <summary>响应类型所在的序列化上下文。</summary>
    public JsonContextModel? ResponseContext { get; set; }

    /// <summary>响应类型在上下文 <c>Default</c> 上的属性名。</summary>
    public string? ResponseProperty { get; set; }
}
