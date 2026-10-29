// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;
using XiHan.Framework.Protocols.Grpc.Methods;

namespace XiHan.Framework.Protocols.Grpc;

/// <summary>
/// gRPC 协议适配器选项。
/// </summary>
/// <remarks>
/// 从配置节 <c>XiHan:Protocols:Grpc</c> 显式读取：<c>Package</c>（protobuf 包名，也是服务路径前缀），<c>Methods:&lt;操作 ID&gt;</c> = <c>Service/Method</c> 显式覆盖推导的方法名。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed partial record GrpcProtocolOptions
{
    /// <summary>
    /// 配置节。
    /// </summary>
    public const string ConfigurationSection = "XiHan:Protocols:Grpc";

    /// <summary>
    /// protobuf 包名（如 <c>acme.orders</c>）。
    /// </summary>
    /// <remarks>
    /// 写进 <c>.proto</c> 的 <c>package</c>，服务路径为 <c>/{package}.{Service}/{Method}</c>；空表示没有包。
    /// </remarks>
    public string? Package
    {
        get => field;
        init
        {
            if (value is not null && !PackageName().IsMatch(value))
            {
                throw new ArgumentException($"protobuf package '{value}' must be lower-case identifiers separated by dots.", nameof(value));
            }

            field = value;
        }
    }

    /// <summary>
    /// 显式覆盖。
    /// </summary>
    /// <remarks>
    /// 操作 ID → 方法名；未覆盖的操作按 <see cref="GrpcMethodConvention"/> 推导。
    /// </remarks>
    public IReadOnlyDictionary<string, GrpcMethodName> Methods
    {
        get => field;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            foreach (var id in value.Keys)
            {
                OperationId.Validate(id);
            }

            field = value;
        }
    } = new Dictionary<string, GrpcMethodName>(StringComparer.Ordinal);

    /// <summary>
    /// 从配置读取。
    /// </summary>
    public static GrpcProtocolOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var section = configuration.GetSection(ConfigurationSection);
        var methods = new Dictionary<string, GrpcMethodName>(StringComparer.Ordinal);
        foreach (var child in section.GetSection("Methods").GetChildren())
        {
            if (child.Value is { } text)
            {
                methods[child.Key] = GrpcMethodName.Parse(text);
            }
        }

        return new GrpcProtocolOptions
        {
            Package = string.IsNullOrWhiteSpace(section["Package"]) ? null : section["Package"],
            Methods = methods,
        };
    }

    /// <summary>
    /// 操作最终的方法名：显式覆盖优先，否则按约定推导。
    /// </summary>
    public GrpcMethodName MethodFor(OperationContract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);
        return Methods.TryGetValue(contract.Id, out var overridden) ? overridden : GrpcMethodConvention.Derive(contract);
    }

    /// <summary>
    /// protobuf 包名的匹配。
    /// </summary>
    [GeneratedRegex(@"^[a-z_][a-z0-9_]*(\.[a-z_][a-z0-9_]*)*$")]
    private static partial Regex PackageName();
}
