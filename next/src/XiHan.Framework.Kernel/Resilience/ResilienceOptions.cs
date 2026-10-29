// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Resilience;

/// <summary>
/// Resilience 阶段的选项。
/// </summary>
/// <remarks>
/// 默认策略与按操作 ID 的覆盖。从配置节 <c>XiHan:Kernel:Resilience</c> 显式读取，不经反射绑定：节本身的键是默认策略，<c>Operations:&lt;操作 ID&gt;</c> 下同名键覆盖该操作（缺省键沿用默认策略）。没有任何限制时中间件直接透传。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record ResilienceOptions
{
    /// <summary>
    /// 配置节。
    /// </summary>
    public const string ConfigurationSection = "XiHan:Kernel:Resilience";

    /// <summary>
    /// 按操作覆盖的子节名。
    /// </summary>
    public const string OperationsSection = "Operations";

    /// <summary>
    /// 默认策略。
    /// </summary>
    /// <remarks>
    /// 没有配置时没有任何限制。
    /// </remarks>
    public ResiliencePolicy Defaults
    {
        get => field;
        init => field = value ?? throw new ArgumentNullException(nameof(value));
    } = new();

    /// <summary>
    /// 按操作 ID 的覆盖（已与默认策略合并）。
    /// </summary>
    public IReadOnlyDictionary<string, ResiliencePolicy> Operations
    {
        get => field;
        init => field = value ?? throw new ArgumentNullException(nameof(value));
    } = new Dictionary<string, ResiliencePolicy>(StringComparer.Ordinal);

    /// <summary>
    /// 某操作生效的策略：有覆盖用覆盖，否则默认策略。
    /// </summary>
    public ResiliencePolicy PolicyFor(string operationId)
    {
        ArgumentNullException.ThrowIfNull(operationId);
        return Operations.TryGetValue(operationId, out var policy) ? policy : Defaults;
    }

    /// <summary>
    /// 从配置读取。
    /// </summary>
    /// <remarks>
    /// 操作 ID 须符合 <see cref="OperationId"/> 文法。
    /// </remarks>
    public static ResilienceOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var section = configuration.GetSection(ConfigurationSection);
        var defaults = ResiliencePolicy.FromConfiguration(section);
        var operations = new Dictionary<string, ResiliencePolicy>(StringComparer.Ordinal);
        foreach (var child in section.GetSection(OperationsSection).GetChildren())
        {
            if (!OperationId.IsValid(child.Key))
            {
                throw new ArgumentException($"'{ConfigurationSection}:{OperationsSection}' contains '{child.Key}', which is not a valid operation ID.");
            }

            // 覆盖只改写出的键，其余沿用默认策略
            operations[child.Key] = ResiliencePolicy.FromConfiguration(child, defaults);
        }

        return new ResilienceOptions { Defaults = defaults, Operations = operations };
    }
}
