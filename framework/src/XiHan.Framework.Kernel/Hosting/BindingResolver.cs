// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Text;
using Microsoft.Extensions.Configuration;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Hosting;

/// <summary>
/// 从配置与 Service Binding 目录解析绑定
/// </summary>
/// <remarks>
/// 目录来源有大小与条目数的上限：一个文件最多 <see cref="MaxFileBytes"/> 字节，一个绑定目录里读的文件合计最多 <see cref="MaxBindingBytes"/> 字节，读的时候到上限就停（RFC-0016「资源绑定与配置节」第 9 条，ADR-0292）；解析全部绑定时根目录下最多 <see cref="MaxBindings"/> 个绑定，一个绑定目录里读的文件最多 <see cref="MaxBindingFiles"/> 个，先数、后读（第 10 条，ADR-0301）。超过时抛出携带 <see cref="KernelErrors.BindingTooLarge"/> 的 <see cref="FrameworkException"/>。
/// 根目录下以 <c>.</c> 开头的子目录不是绑定，不读、不计，点了名也不读；目录项可以是符号链接，名字取目录项自己的，内容取链接最终指向的：Kubernetes 把 projected volume 直接挂到根目录时的 <c>..data</c> 与带时间戳的目录因此不算绑定，绑定按链接读出卷里当前的内容（第 11 条，ADR-0311）
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public static class BindingResolver
{
    /// <summary>
    /// 配置节
    /// </summary>
    public const string ConfigurationSection = "XiHan:Bindings";

    /// <summary>
    /// Service Binding 规范的根目录环境变量
    /// </summary>
    public const string ServiceBindingRootVariable = "SERVICE_BINDING_ROOT";

    /// <summary>
    /// 绑定目录里一个文件的字节数上限
    /// </summary>
    /// <remarks>
    /// 按文件的原始字节数计（去首尾空白之前）。不小于下发给插件的一个绑定的属性上限（262144 字节），整份证书链也放得下（ADR-0292）
    /// </remarks>
    public const int MaxFileBytes = 524288;

    /// <summary>
    /// 一个绑定目录里读的文件合计的字节数上限
    /// </summary>
    /// <remarks>
    /// <c>type</c>、<c>uri</c> 与其余属性文件都计入，以 <c>.</c> 开头的不读、不计；与 Kubernetes 一个 Secret 的上限相同（ADR-0292）
    /// </remarks>
    public const int MaxBindingBytes = 1048576;

    /// <summary>
    /// 一个绑定目录里读的文件数上限
    /// </summary>
    /// <remarks>
    /// 以 <c>.</c> 开头的不读、不计。下发给插件的一个绑定最多 64 个属性（ADR-0285）的两倍：<c>type</c> 文件之外，下发得了的属性都放得下（ADR-0301）
    /// </remarks>
    public const int MaxBindingFiles = 128;

    /// <summary>
    /// 解析全部绑定时根目录下的绑定数上限
    /// </summary>
    /// <remarks>
    /// 根目录下不以 <c>.</c> 开头的每个子目录都是一个绑定、都计入，以 <c>.</c> 开头的不是绑定、不计（ADR-0311）；与 <see cref="MaxBindingFiles"/> 同一个数，解析全部绑定读进内存的内容因此不超过 128 × <see cref="MaxBindingBytes"/> 字节（ADR-0301）。只解析点了名的绑定时不核对
    /// </remarks>
    public const int MaxBindings = 128;

    /// <summary>
    /// 读一个文件时每次读的字节数
    /// </summary>
    private const int ChunkBytes = 4096;

    /// <summary>
    /// 解析全部绑定
    /// </summary>
    /// <remarks>
    /// 同名时配置优先于目录
    /// <c>serviceBindingRoot</c> 取 <c>null</c>
    /// </remarks>
    /// <exception cref="FrameworkException">目录里的文件超过 <see cref="MaxFileBytes"/>、一个绑定合计超过 <see cref="MaxBindingBytes"/>、一个绑定的文件多于 <see cref="MaxBindingFiles"/> 个或根目录下的绑定多于 <see cref="MaxBindings"/> 个（<see cref="KernelErrors.BindingTooLarge"/>）</exception>
    public static IReadOnlyDictionary<string, BindingValue> Resolve(IConfiguration configuration)
        => Resolve(configuration, null);

    /// <summary>
    /// 解析全部绑定
    /// </summary>
    /// <remarks>
    /// 同名时配置优先于目录
    /// </remarks>
    /// <exception cref="FrameworkException">目录里的文件超过 <see cref="MaxFileBytes"/>、一个绑定合计超过 <see cref="MaxBindingBytes"/>、一个绑定的文件多于 <see cref="MaxBindingFiles"/> 个或根目录下的绑定多于 <see cref="MaxBindings"/> 个（<see cref="KernelErrors.BindingTooLarge"/>）</exception>
    public static IReadOnlyDictionary<string, BindingValue> Resolve(IConfiguration configuration, string? serviceBindingRoot)
        => Resolve(configuration, serviceBindingRoot, null);

    /// <summary>
    /// 解析绑定，可以只解析点了名的那些
    /// </summary>
    /// <remarks>
    /// 同名时配置优先于目录。<paramref name="names"/> 给了时只读这些名字的绑定目录与配置节，其余的不读、不进结果：每个绑定的读法与不限名字时相同，名字按字面比。插件宿主在配置一个插件时只解析它的清单声明的绑定，别的绑定读不出来不影响它（ADR-0268）。点了名的绑定目录按 <paramref name="names"/> 的顺序读：读不出或超过大小、文件数的上限时，报的是这个顺序里的第一个（ADR-0292）；只读点了名的那几个目录，根目录下的绑定数不核对（ADR-0301）
    /// </remarks>
    /// <param name="configuration">宿主配置</param>
    /// <param name="serviceBindingRoot">Service Binding 根目录；<see langword="null"/> 时取环境变量</param>
    /// <param name="names">要解析的绑定名；<see langword="null"/> 时解析全部</param>
    internal static IReadOnlyDictionary<string, BindingValue> Resolve(IConfiguration configuration, string? serviceBindingRoot, IReadOnlyCollection<string>? names)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var result = new Dictionary<string, BindingValue>(StringComparer.Ordinal);

        // 先读目录再读配置：配置后写入，同名绑定整体覆盖目录来源，而不是逐属性合并
        serviceBindingRoot ??= Environment.GetEnvironmentVariable(ServiceBindingRootVariable);
        if (!string.IsNullOrEmpty(serviceBindingRoot) && Directory.Exists(serviceBindingRoot))
        {
            if (names is null)
            {
                // 先数、后读：最多枚举到上限再多一个，多出来即失败，一个文件也不读；根目录指错到一棵大目录树时不把每个子目录都当成绑定读进内存
                // 以点开头的子目录先跳过、不占名额：projected volume 挂到根目录时的 ..data 与带时间戳的目录不是绑定
                var directories = Directory.EnumerateDirectories(serviceBindingRoot).Where(IsBinding).Take(MaxBindings + 1).ToList();
                if (directories.Count > MaxBindings)
                {
                    throw new FrameworkException(KernelErrors.BindingTooLarge, string.Create(CultureInfo.InvariantCulture, $"The service binding root contains more than {MaxBindings} bindings."));
                }

                foreach (var directory in directories)
                {
                    var binding = FromDirectory(directory);
                    result[binding.Name] = binding;
                }
            }
            else
            {
                // 没点名的目录连文件都不打开；比的是目录的实际名字，与不限名字时放进结果的键相同（大小写不敏感的文件系统上 db 点不到 DB）
                // 以点开头的子目录与不限名字时一样不是绑定，点到了也不读
                var named = Directory.EnumerateDirectories(serviceBindingRoot)
                    .Where(directory => IsBinding(directory) && names.Contains(Path.GetFileName(directory)))
                    .ToDictionary(static directory => Path.GetFileName(directory), StringComparer.Ordinal);

                // 按点名的顺序读：插件宿主按清单的顺序报第一个读不出或超限的绑定，与文件系统枚举的先后无关
                foreach (var name in names)
                {
                    if (named.TryGetValue(name, out var directory))
                    {
                        var binding = FromDirectory(directory);
                        result[binding.Name] = binding;
                    }
                }
            }
        }

        foreach (var section in configuration.GetSection(ConfigurationSection).GetChildren())
        {
            if (names is not null && !names.Contains(section.Key))
            {
                continue;
            }

            var binding = FromSection(section);
            result[binding.Name] = binding;
        }

        return result;
    }

    /// <summary>
    /// 根目录下的一个子目录是不是绑定
    /// </summary>
    /// <remarks>
    /// 以 <c>.</c> 开头的不是：挂载工具放的元数据目录（<c>..data</c>、带时间戳的 <c>..&lt;时间&gt;</c>），与绑定目录里以 <c>.</c> 开头的文件同一个口径（ADR-0311）。指向目录的符号链接按目录列出，按它自己的名字判断
    /// </remarks>
    /// <param name="directory">枚举出的子目录路径</param>
    private static bool IsBinding(string directory) => !Path.GetFileName(directory).StartsWith('.');

    /// <summary>
    /// 从配置节 <c>XiHan:Bindings:&lt;name&gt;</c> 读取一个绑定
    /// </summary>
    /// <remarks>
    /// 节本身的标量值或子键 <c>value</c> 作主值，子键 <c>type</c> 作类型，其余子键作属性
    /// </remarks>
    private static BindingValue FromSection(IConfigurationSection section)
    {
        var properties = new Dictionary<string, string>(StringComparer.Ordinal);
        var type = string.Empty;
        // 允许 "XiHan:Bindings:cache" = "localhost:6379" 这种纯标量写法，没有子键时主值就是节的值
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

        return new BindingValue { Name = section.Key, Type = type, Value = value, Properties = properties };
    }

    /// <summary>
    /// 按 Service Binding 规范从一个目录读取绑定
    /// </summary>
    /// <remarks>
    /// 目录名是绑定名，每个文件是一个属性，<c>type</c> 作类型，<c>uri</c> 同时作主值；子目录不是属性、不读（Secret 卷里的 <c>..data</c> 与带时间戳的目录），指向文件的符号链接读它最终指向的内容。读的文件不多于 <see cref="MaxBindingFiles"/> 个，合计不超过 <see cref="MaxBindingBytes"/>，每个不超过 <see cref="MaxFileBytes"/>
    /// </remarks>
    private static BindingValue FromDirectory(string directory)
    {
        var name = Path.GetFileName(directory);
        var properties = new Dictionary<string, string>(StringComparer.Ordinal);
        var type = string.Empty;
        string? value = null;
        var remaining = MaxBindingBytes;

        // 先数、后读：文件多于上限时这个绑定的内容一个字节也不读；空文件不占字节的额度，只有文件数能限住它们
        var files = new List<string>();
        foreach (var file in Directory.EnumerateFiles(directory))
        {
            // 规范允许挂载工具放置以点开头的元数据文件（如 ..data 符号链接），它们不是绑定属性，不读也不计
            if (Path.GetFileName(file).StartsWith('.'))
            {
                continue;
            }

            if (files.Count == MaxBindingFiles)
            {
                throw new BindingTooLargeException(name, Path.GetFileName(file), BindingLimit.FileCount);
            }

            files.Add(file);
        }

        foreach (var file in files)
        {
            // 文件内容常带尾部换行，统一修剪；类型文件不进属性，uri 既是主值也保留为属性
            var key = Path.GetFileName(file);
            var content = ReadBounded(name, file, ref remaining).Trim();
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

        return new BindingValue { Name = name, Type = type, Value = value, Properties = properties };
    }

    /// <summary>
    /// 读一个绑定文件的文本，读到的字节不超过额度
    /// </summary>
    /// <remarks>
    /// 额度是 <see cref="MaxFileBytes"/> 与这个绑定剩余的合计额度中较小的一个；解码与 <see cref="File.ReadAllText(string)"/> 相同（按字节顺序标记识别编码，缺省 UTF-8）
    /// </remarks>
    /// <param name="binding">绑定名</param>
    /// <param name="path">文件路径</param>
    /// <param name="remaining">这个绑定剩余的合计额度，读完扣掉这个文件的字节数</param>
    private static string ReadBounded(string binding, string path, ref int remaining)
    {
        var limit = Math.Min(MaxFileBytes, remaining);
        using var content = new MemoryStream();
        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 1))
        {
            // 一块一块地读、边读边数：到额度再多一个字节就停，不先把整个文件读进内存，也不信文件系统报的长度（符号链接、虚拟文件）
            Span<byte> chunk = stackalloc byte[ChunkBytes];
            int read;
            while ((read = stream.Read(chunk)) > 0)
            {
                if (content.Length + read > limit)
                {
                    // 剩余的合计额度还有一整个文件那么多时，超的是单个文件的上限，否则是合计的上限
                    throw new BindingTooLargeException(binding, Path.GetFileName(path), limit < MaxFileBytes ? BindingLimit.TotalBytes : BindingLimit.FileBytes);
                }

                content.Write(chunk[..read]);
            }
        }

        remaining -= (int)content.Length;
        content.Position = 0;
        using var reader = new StreamReader(content, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }
}
