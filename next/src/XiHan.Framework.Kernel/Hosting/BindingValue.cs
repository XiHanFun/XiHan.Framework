// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Hosting;

/// <summary>
/// 宿主解析出的一个资源绑定。来源：配置节 <c>XiHan:Bindings:&lt;name&gt;</c>，或 Service Binding 规范的 <c>SERVICE_BINDING_ROOT</c> 目录。
/// </summary>
/// <param name="Name">绑定名。</param>
/// <param name="Type">资源类型；来源未声明时为空字符串。</param>
/// <param name="Value">主值（连接字符串、URI 等）；没有时为 <see langword="null"/>。</param>
/// <param name="Properties">其余属性。</param>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record BindingValue(string Name, string Type, string? Value, IReadOnlyDictionary<string, string> Properties)
{
    /// <summary>
    /// 取属性。
    /// </summary>
    public Maybe<string> Property(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return Properties.TryGetValue(key, out var value) ? Maybe.Some(value) : default;
    }
}

/// <summary>
/// 从配置与 Service Binding 目录解析绑定。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static class BindingResolver
{
    /// <summary>
    /// 配置节。
    /// </summary>
    public const string ConfigurationSection = "XiHan:Bindings";

    /// <summary>
    /// Service Binding 规范的根目录环境变量。
    /// </summary>
    public const string ServiceBindingRootVariable = "SERVICE_BINDING_ROOT";

    /// <summary>
    /// 解析全部绑定。同名时配置优先于目录。
    /// </summary>
    public static IReadOnlyDictionary<string, BindingValue> Resolve(IConfiguration configuration, string? serviceBindingRoot = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var result = new Dictionary<string, BindingValue>(StringComparer.Ordinal);

        serviceBindingRoot ??= Environment.GetEnvironmentVariable(ServiceBindingRootVariable);
        if (!string.IsNullOrEmpty(serviceBindingRoot) && Directory.Exists(serviceBindingRoot))
        {
            foreach (var directory in Directory.EnumerateDirectories(serviceBindingRoot))
            {
                var binding = FromDirectory(directory);
                result[binding.Name] = binding;
            }
        }

        foreach (var section in configuration.GetSection(ConfigurationSection).GetChildren())
        {
            var binding = FromSection(section);
            result[binding.Name] = binding;
        }

        return result;
    }

    private static BindingValue FromSection(IConfigurationSection section)
    {
        var properties = new Dictionary<string, string>(StringComparer.Ordinal);
        var type = string.Empty;
        string? value = section.Value;

        foreach (var child in section.GetChildren())
        {
            if (child.Value is null)
            {
                continue;
            }

            switch (child.Key)
            {
                case "type":
                    type = child.Value;
                    break;
                case "value":
                    value = child.Value;
                    break;
                default:
                    properties[child.Key] = child.Value;
                    break;
            }
        }

        return new BindingValue(section.Key, type, value, properties);
    }

    private static BindingValue FromDirectory(string directory)
    {
        var properties = new Dictionary<string, string>(StringComparer.Ordinal);
        var type = string.Empty;
        string? value = null;

        foreach (var file in Directory.EnumerateFiles(directory))
        {
            var key = Path.GetFileName(file);
            if (key.StartsWith('.'))
            {
                continue;
            }

            var content = File.ReadAllText(file).Trim();
            switch (key)
            {
                case "type":
                    type = content;
                    break;
                case "uri":
                    value = content;
                    properties[key] = content;
                    break;
                default:
                    properties[key] = content;
                    break;
            }
        }

        return new BindingValue(Path.GetFileName(directory), type, value, properties);
    }
}
