// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.RegularExpressions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Protocols.Grpc;

/// <summary>
/// 一个 gRPC 方法的名字：服务名与方法名，都是 protobuf 标识符（字母、数字、下划线，不以数字开头）。文本形式 <c>Service/Method</c>；完整路径 <c>/{package}.{Service}/{Method}</c>。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed partial record GrpcMethodName
{
    /// <summary>
    /// 创建；名字不合法时抛出 <see cref="ArgumentException"/>。
    /// </summary>
    public GrpcMethodName(string service, string method)
    {
        Service = ValidateIdentifier(service, nameof(service));
        Method = ValidateIdentifier(method, nameof(method));
    }

    /// <summary>
    /// 服务名（如 <c>Orders</c>）。
    /// </summary>
    public string Service { get; }

    /// <summary>
    /// 方法名（如 <c>Create</c>）。
    /// </summary>
    public string Method { get; }

    /// <summary>
    /// 解析 <c>Service/Method</c>。
    /// </summary>
    public static GrpcMethodName Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var parts = text.Split('/');
        if (parts.Length != 2)
        {
            throw new ArgumentException($"gRPC method '{text}' must be written as 'Service/Method'.", nameof(text));
        }

        return new GrpcMethodName(parts[0].Trim(), parts[1].Trim());
    }

    /// <summary>
    /// 带包名的完整服务名：<c>{package}.{Service}</c>；没有包名时就是服务名。
    /// </summary>
    public string FullServiceName(string? package) => string.IsNullOrEmpty(package) ? Service : package + "." + Service;

    /// <summary>
    /// HTTP/2 路径：<c>/{package}.{Service}/{Method}</c>。
    /// </summary>
    public string PathFor(string? package) => "/" + FullServiceName(package) + "/" + Method;

    /// <inheritdoc />
    public override string ToString() => Service + "/" + Method;

    /// <summary>
    /// 校验 protobuf 标识符。
    /// </summary>
    private static string ValidateIdentifier(string value, string paramName)
    {
        ArgumentNullException.ThrowIfNull(value, paramName);
        if (!Identifier().IsMatch(value))
        {
            throw new ArgumentException($"'{value}' is not a valid protobuf identifier (letters, digits and underscores, not starting with a digit).", paramName);
        }

        return value;
    }

    /// <summary>
    /// protobuf 标识符的匹配。
    /// </summary>
    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex Identifier();
}
