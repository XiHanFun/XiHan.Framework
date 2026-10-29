// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Hosting;

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
    /// 解析全部绑定。
    /// </summary>
    /// <remarks>
    /// 同名时配置优先于目录。
    /// </remarks>
    public static IReadOnlyDictionary<string, BindingValue> Resolve(IConfiguration configuration, string? serviceBindingRoot = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var result = new Dictionary<string, BindingValue>(StringComparer.Ordinal);

        // 先读目录再读配置：配置后写入，同名绑定整体覆盖目录来源，而不是逐属性合并。
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

    /// <summary>
    /// 从配置节 <c>XiHan:Bindings:&lt;name&gt;</c> 读取一个绑定。
    /// </summary>
    /// <remarks>
    /// 节本身的标量值或子键 <c>value</c> 作主值，子键 <c>type</c> 作类型，其余子键作属性。
    /// </remarks>
    private static BindingValue FromSection(IConfigurationSection section)
    {
        var properties = new Dictionary<string, string>(StringComparer.Ordinal);
        var type = string.Empty;
        // 允许 "XiHan:Bindings:cache" = "localhost:6379" 这种纯标量写法，没有子键时主值就是节的值。
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

    /// <summary>
    /// 按 Service Binding 规范从一个目录读取绑定。
    /// </summary>
    /// <remarks>
    /// 目录名是绑定名，每个文件是一个属性，<c>type</c> 作类型，<c>uri</c> 同时作主值。
    /// </remarks>
    private static BindingValue FromDirectory(string directory)
    {
        var properties = new Dictionary<string, string>(StringComparer.Ordinal);
        var type = string.Empty;
        string? value = null;

        foreach (var file in Directory.EnumerateFiles(directory))
        {
            // 规范允许挂载工具放置以点开头的元数据文件（如 ..data 符号链接），它们不是绑定属性。
            var key = Path.GetFileName(file);
            if (key.StartsWith('.'))
            {
                continue;
            }

            // 文件内容常带尾部换行，统一修剪；类型文件不进属性，uri 既是主值也保留为属性。
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
