// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Security.Abstractions.Conditions;

namespace XiHan.Framework.Security.Abstractions.Tests.Fixtures;

/// <summary>
/// 条件测试共用的登记表、编译器与求值助手。
/// </summary>
internal static class ConditionTestKit
{
    /// <summary>
    /// 东八区（自建时区，不依赖运行环境的时区库）。
    /// </summary>
    public static readonly TimeZoneInfo UtcPlus8 = TimeZoneInfo.CreateCustomTimeZone("test+8", TimeSpan.FromHours(8), "test+8", "test+8");

    /// <summary>
    /// 覆盖各值种类、多值、别名与族的登记项。
    /// </summary>
    public static readonly ConditionAttributeDescriptor[] Attributes =
    [
        new("subject.userName", ConditionValueKind.String),
        new("subject.roleCodes", ConditionValueKind.String, IsMultiValued: true, Aliases: ["subject.role"]),
        new("subject.levels", ConditionValueKind.Number, IsMultiValued: true),
        new("subject.isImpersonating", ConditionValueKind.Boolean),
        new("resource.amount", ConditionValueKind.Number),
        new("environment.ip", ConditionValueKind.IpAddress),
        new("environment.time", ConditionValueKind.TimeOfDay),
        new("environment.date", ConditionValueKind.Date),
        new("environment.now", ConditionValueKind.DateTime),
        new("resource.query.*", ConditionValueKind.String),
    ];

    /// <summary>
    /// 用 <see cref="Attributes"/> 建的编译器。
    /// </summary>
    public static ConditionCompiler Compiler { get; } = new(new ConditionAttributeRegistry(Attributes));

    /// <summary>
    /// 求值器。
    /// </summary>
    public static ConditionEvaluator Evaluator { get; } = new();

    /// <summary>
    /// 只含一个子句的条件集。
    /// </summary>
    public static ConditionSet Single(ConditionClause clause) => new([new ConditionGroup([clause])]);

    /// <summary>
    /// 不区分大小写的属性值字典。
    /// </summary>
    public static Dictionary<string, object?> Values(params (string Name, object? Value)[] values)
    {
        var dictionary = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, value) in values)
        {
            dictionary[name] = value;
        }

        return dictionary;
    }

    /// <summary>
    /// 编译并带说明求值一个子句，返回子句结果。
    /// </summary>
    public static async Task<ConditionOutcome> EvaluateAsync(ConditionClause clause, Dictionary<string, object?> values, TimeZoneInfo? timeZone = null)
    {
        var compiled = Compiler.Compile(Single(clause), new ConditionCompileOptions { TimeZone = timeZone ?? TimeZoneInfo.Utc });
        var result = await Evaluator.EvaluateAsync(compiled, new DictionaryConditionAttributeSource(values), explain: true, CancellationToken.None);
        return result.Groups[0].Clauses[0].Outcome;
    }
}
