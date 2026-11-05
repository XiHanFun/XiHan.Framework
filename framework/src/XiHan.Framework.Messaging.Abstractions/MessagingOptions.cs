// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
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
    /// 默认 <c>.dlq</c>
    /// </remarks>
    public string DeadLetterSuffix
    {
        get => field;
        init => field = !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("The dead-letter suffix must not be empty.", nameof(value));
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
    /// 从配置读取：<c>MaxAttempts</c>、<c>DeadLetterSuffix</c>、<c>InstanceId</c>
    /// </summary>
    public static MessagingOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var options = new MessagingOptions();
        var attempts = configuration[$"{ConfigurationSection}:MaxAttempts"];
        if (attempts is not null)
        {
            options = int.TryParse(attempts, NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value >= 1
                ? options with { MaxAttempts = value }
                : throw new ArgumentException($"{ConfigurationSection}:MaxAttempts must be a positive integer, but was '{attempts}'.");
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

        return options;
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
