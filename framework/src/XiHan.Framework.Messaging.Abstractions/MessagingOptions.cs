// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.Abstractions;

/// <summary>
/// 消息选项
/// </summary>
/// <remarks>
/// 从配置节 <c>XiHan:Messaging</c> 显式读取
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed partial record MessagingOptions
{
    /// <summary>
    /// 配置节
    /// </summary>
    public const string ConfigurationSection = "XiHan:Messaging";

    /// <summary>
    /// 数据模式的配置节
    /// </summary>
    /// <remarks>
    /// 数组，每项 <c>Type</c> 与 <c>DataSchema</c>
    /// </remarks>
    private const string DataSchemasSection = ConfigurationSection + ":DataSchemas";

    /// <summary>
    /// 共享的空数据模式表
    /// </summary>
    /// <remarks>
    /// 没有配置数据模式的选项都指向它，缺省选项之间照旧相等
    /// </remarks>
    private static readonly IReadOnlyDictionary<string, string> _noDataSchemas = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// 一条消息最多投递次数（含首次）
    /// </summary>
    /// <remarks>
    /// 超过进入死信。默认 3
    /// </remarks>
    public int MaxAttempts
    {
        get => field;
        init => field = value >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The maximum number of attempts must be positive.");
    } = 3;

    /// <summary>
    /// 死信主题后缀
    /// </summary>
    /// <remarks>
    /// 默认 <c>.dlq</c>；不能只有空白，也不能有孤立的代理项：它拼进各家的队列、主题与流的名字，经宽松的 UTF-8 编码（ADR-0321）
    /// </remarks>
    public string DeadLetterSuffix
    {
        get => field;
        init => field = string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("The dead-letter suffix must not be empty.", nameof(value))
            : !UnicodeText.HasLoneSurrogate(value) ? value : throw new ArgumentException("The dead-letter suffix must not contain lone surrogates.", nameof(value));
    } = ".dlq";

    /// <summary>
    /// 配置的实例标识
    /// </summary>
    /// <remarks>
    /// 为空（缺省）时由 <see cref="MessagingInstance"/> 每次启动生成；给出时就是本实例在消息代理上的标识（RabbitMQ 的广播队列、Kafka 的广播消费组，RFC-0009「订阅方式」第 6 条，ADR-0085）。文法 <c>^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$</c>：1 到 64 个字符，字母或数字开头，不含 <c>@</c>。必须每个同时在线的实例各不相同：重复时 Kafka 的广播退化成同一消费组里的竞争，RabbitMQ 后启动的实例声明同名独占队列失败、启动失败；适合配成 Pod 名这类部署平台保证唯一的值
    /// </remarks>
    public string? InstanceId
    {
        get => field;
        init => field = value is null || IsValidInstanceId(value)
            ? value
            : throw new ArgumentException($"The messaging instance ID '{value}' must be 1 to 64 letters, digits, '.', '_' or '-', starting with a letter or digit.", nameof(value));
    }

    /// <summary>
    /// 从配置登记的数据模式
    /// </summary>
    /// <remarks>
    /// CloudEvents <c>type</c> → <c>dataschema</c>，读自数组形式的配置节 <c>XiHan:Messaging:DataSchemas</c>（每项 <c>Type</c> 与 <c>DataSchema</c>；<c>type</c> 带点号写不成配置键，所以用数组，环境变量写成 <c>XiHan__Messaging__DataSchemas__0__Type</c>）。
    /// 消息 Provider 装配时由 <see cref="MessagingRegistration.Configure"/> 登记进 <see cref="MessageDataSchemas"/>，与 <see cref="MessagingServiceCollectionExtensions.AddMessageDataSchema"/> 同一张表、同一套规则：同一个 <c>type</c> 值不同即装配失败，不论谁先登记（RFC-0009「框架事件的数据模式」，ADR-0332）。
    /// 每项照 <see cref="MessagingServiceCollectionExtensions.AddMessageDataSchema"/> 核对（不能为空或只有空白，不能有孤立的代理项），不合抛出 <see cref="ArgumentException"/>。缺省为空
    /// </remarks>
    public IReadOnlyDictionary<string, string> DataSchemas
    {
        get => field;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            foreach (var (type, dataSchema) in value)
            {
                MessageDataSchemas.Validate(type, dataSchema);
            }

            field = value;
        }
    } = _noDataSchemas;

    /// <summary>
    /// 从配置读取：<c>MaxAttempts</c>、<c>DeadLetterSuffix</c>、<c>InstanceId</c>、<c>DataSchemas</c>
    /// </summary>
    /// <remarks>
    /// 值不合法时抛出 <see cref="ArgumentException"/>，消息带出错的配置键
    /// </remarks>
    public static MessagingOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var options = new MessagingOptions();
        if (ConfigurationValues.ReadInt32(configuration.GetSection(ConfigurationSection), "MaxAttempts", 1) is { } attempts)
        {
            options = options with { MaxAttempts = attempts };
        }

        if (configuration[$"{ConfigurationSection}:DeadLetterSuffix"] is { } suffix)
        {
            options = options with { DeadLetterSuffix = suffix };
        }

        // 只记配置值：没有配置时保持为空，由 MessagingInstance 生成随机标识——随机值进了选项，同样的配置就读不出同样的选项
        // 写了这个键就必须合文法（空字符串同样拒绝），不想固定标识就不写
        if (configuration[$"{ConfigurationSection}:InstanceId"] is { } instance)
        {
            options = IsValidInstanceId(instance)
                ? options with { InstanceId = instance }
                : throw new ArgumentException($"{ConfigurationSection}:InstanceId must be 1 to 64 letters, digits, '.', '_' or '-', starting with a letter or digit, but was '{instance}'.");
        }

        var dataSchemas = ReadDataSchemas(configuration.GetSection(DataSchemasSection));
        return dataSchemas.Count > 0 ? options with { DataSchemas = dataSchemas } : options;
    }

    /// <summary>
    /// 读数据模式的配置节
    /// </summary>
    /// <remarks>
    /// 每项的 <c>Type</c> 与 <c>DataSchema</c> 都要有；值照 <see cref="MessageDataSchemas.Validate"/> 核对。同一个 <c>type</c> 再写同一个值只记一次，另一个值抛出：与代码登记的规则相同（ADR-0332）
    /// </remarks>
    private static Dictionary<string, string> ReadDataSchemas(IConfigurationSection section)
    {
        var schemas = new Dictionary<string, string>(StringComparer.Ordinal);
        var origins = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in section.GetChildren())
        {
            var type = DataSchemaMember(entry, "Type");
            var dataSchema = DataSchemaMember(entry, "DataSchema");

            // 同一个 type 有分歧是装配错误：不取先写的也不取后写的，消息里指出两处
            if (schemas.TryGetValue(type, out var existing))
            {
                if (!string.Equals(existing, dataSchema, StringComparison.Ordinal))
                {
                    throw new ArgumentException($"'{entry.Path}:DataSchema' gives event type '{type}' the data schema '{dataSchema}', but '{origins[type]}:DataSchema' already gives it '{existing}'.");
                }

                continue;
            }

            schemas[type] = dataSchema;
            origins[type] = entry.Path;
        }

        return schemas;
    }

    /// <summary>
    /// 读一项数据模式里的一个成员
    /// </summary>
    /// <remarks>
    /// 没有、空串或只有空白、有孤立的代理项都抛出 <see cref="ArgumentException"/>，消息带这个成员的配置键
    /// </remarks>
    private static string DataSchemaMember(IConfigurationSection entry, string member)
    {
        var path = $"{entry.Path}:{member}";
        var value = entry[member];
        if (value is null)
        {
            throw new ArgumentException($"'{path}' must be set: each entry of '{DataSchemasSection}' has a 'Type' and a 'DataSchema'.");
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"'{path}' must not be empty.");
        }

        return !UnicodeText.HasLoneSurrogate(value) ? value : throw new ArgumentException($"'{path}' must not contain lone surrogates.");
    }

    /// <summary>
    /// 实例标识是否合文法
    /// </summary>
    /// <remarks>
    /// 可直接用作 RabbitMQ 队列名与 Kafka 消费组名的一部分；不含 <c>@</c>，它是名字里实例标识前的分隔符
    /// </remarks>
    internal static bool IsValidInstanceId(string value) => InstanceIdPattern().IsMatch(value);

    /// <summary>
    /// 实例标识的正则：字母或数字开头，其余是字母、数字、<c>.</c>、<c>_</c>、<c>-</c>，共 1 到 64 个字符
    /// </summary>
    /// <remarks>
    /// 结尾用 <c>\z</c> 而不是 <c>$</c>：<c>$</c> 还匹配结尾换行之前的位置，从文件或环境变量读进来的值带着换行也会被放过
    /// </remarks>
    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9._-]{0,63}\z")]
    private static partial Regex InstanceIdPattern();
}
