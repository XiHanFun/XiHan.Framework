// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Configuration.Abstractions;

/// <summary>
/// 配置子系统的错误码；登记在 <c>spec/conventions/error-codes.md</c>。选项与名字不合法是编程错误，在构造时抛出。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class ConfigurationErrors
{
    /// <summary>
    /// 远程配置的命名空间不存在。
    /// </summary>
    public const string NamespaceNotFound = "CONFIGURATION_NOTFOUND_001";

    /// <summary>
    /// 密钥不存在。
    /// </summary>
    public const string SecretNotFound = "CONFIGURATION_NOTFOUND_002";

    /// <summary>
    /// 远程配置不可达或超时。
    /// </summary>
    public const string RemoteUnavailable = "CONFIGURATION_UNAVAILABLE_001";

    /// <summary>
    /// 密钥库不可达或超时。
    /// </summary>
    public const string SecretsUnavailable = "CONFIGURATION_UNAVAILABLE_002";

    /// <summary>
    /// 没有读取该密钥的权限。
    /// </summary>
    public const string SecretDenied = "CONFIGURATION_DENIED_001";

    /// <summary>
    /// 远程配置文档不是声明的格式。
    /// </summary>
    public const string DocumentInvalid = "CONFIGURATION_VALIDATION_001";
}
