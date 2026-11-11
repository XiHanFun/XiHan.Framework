// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Auditing.Abstractions;

/// <summary>
/// 审计中间件的选项
/// </summary>
/// <remarks>
/// 从配置节 <c>XiHan:Auditing</c> 显式读取：<c>Selection</c>、<c>IncludeSuccesses</c>、<c>Exclude</c>
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed partial record AuditingOptions
{
    /// <summary>
    /// 配置节
    /// </summary>
    public const string ConfigurationSection = "XiHan:Auditing";

    /// <summary>
    /// 没有显式动作时记哪些操作
    /// </summary>
    /// <remarks>
    /// 默认只记需要授权的操作（契约声明了权限码或策略名）
    /// </remarks>
    public AuditSelection Selection { get; init; } = AuditSelection.Protected;

    /// <summary>
    /// 是否记录成功的调用
    /// </summary>
    /// <remarks>
    /// 默认 <see langword="true"/>，为假时只记失败
    /// </remarks>
    public bool IncludeSuccesses { get; init; } = true;

    /// <summary>
    /// 按操作排除：每项是操作 ID（精确匹配），或以 <c>*</c> 结尾的前缀（<c>profile.*</c>、<c>my-*</c>）
    /// </summary>
    /// <remarks>
    /// 按 <see cref="Selection"/> 挑出的操作，ID 等于某项或以某个前缀开头就不记；显式声明动作（<c>SetAuditAction</c>）的调用不受影响。默认空，与不排除完全一样。每项必须是合法的操作 ID，或 <c>*</c> 之前是非空的操作 ID 前缀（小写字母开头，只含小写字母、数字与 <c>-</c>，至多一个 <c>.</c>）；不合法抛出 <see cref="ArgumentException"/>。不核对操作是否已登记：模块可按配置开关，插件的操作可能在启动之后才登记
    /// </remarks>
    public IReadOnlyList<string> Exclude
    {
        get => field;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            foreach (var entry in value)
            {
                if (!IsExclusion(entry))
                {
                    throw new ArgumentException(InvalidExclusion(entry, nameof(Exclude)), nameof(value));
                }
            }

            field = value;
        }
    } = [];

    /// <summary>
    /// 从配置读取
    /// </summary>
    /// <remarks>
    /// <c>Exclude</c> 必须写成列表（<c>Exclude:0</c>、<c>Exclude:1</c>……）；写成单个值或某一项不合法都抛出 <see cref="ArgumentException"/>，消息指出配置键与那一项
    /// </remarks>
    public static AuditingOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var options = new AuditingOptions();
        if (configuration[$"{ConfigurationSection}:Selection"] is { } selection)
        {
            options = Enum.TryParse<AuditSelection>(selection, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)
                ? options with { Selection = parsed }
                : throw new ArgumentException($"{ConfigurationSection}:Selection must be one of 'Protected', 'All' or 'None', but was '{selection}'.");
        }

        if (configuration[$"{ConfigurationSection}:IncludeSuccesses"] is { } include)
        {
            options = bool.TryParse(include, out var parsed)
                ? options with { IncludeSuccesses = parsed }
                : throw new ArgumentException($"{ConfigurationSection}:IncludeSuccesses must be 'true' or 'false', but was '{include}'.");
        }

        var exclude = configuration.GetSection($"{ConfigurationSection}:Exclude");
        // 写成单个值（环境变量 XiHan__Auditing__Exclude=orders.list）时 GetChildren 为空：静默当作没配会让排除悄悄失效，按配置错误处理
        if (exclude.Value is not null)
        {
            throw new ArgumentException($"{ConfigurationSection}:Exclude must be a list such as '{ConfigurationSection}:Exclude:0', but was the single value '{exclude.Value}'.");
        }

        var entries = new List<string>();
        foreach (var child in exclude.GetChildren())
        {
            if (child.Value is not { } entry || !IsExclusion(entry))
            {
                throw new ArgumentException(InvalidExclusion(child.Value, child.Path));
            }

            entries.Add(entry);
        }

        return entries.Count > 0 ? options with { Exclude = entries } : options;
    }

    /// <summary>
    /// 一项排除是否合法：操作 ID，或 <c>*</c> 结尾的非空操作 ID 前缀
    /// </summary>
    private static bool IsExclusion(string? entry)
        => entry is not null && (OperationId.IsValid(entry) || ExclusionPrefix().IsMatch(entry));

    /// <summary>
    /// 不合法的排除项的异常消息
    /// </summary>
    /// <param name="entry">那一项（可能为空）</param>
    /// <param name="where">配置键或属性名</param>
    private static string InvalidExclusion(string? entry, string where)
        => $"'{where}' is '{entry}', which is neither an operation ID such as 'orders.list' nor an operation ID prefix ending with '*' such as 'orders.*'.";

    /// <summary>
    /// 排除前缀的文法
    /// </summary>
    /// <remarks>
    /// 小写字母开头，只含小写字母、数字与连字符，至多一个点，以 <c>*</c> 结尾
    /// </remarks>
    [GeneratedRegex("^[a-z][a-z0-9-]*(\\.[a-z0-9-]*)?\\*$")]
    private static partial Regex ExclusionPrefix();
}
