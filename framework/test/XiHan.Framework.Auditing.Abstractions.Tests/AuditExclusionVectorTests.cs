// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using XiHan.Framework.Auditing.Abstractions.Pipeline;
using XiHan.Framework.Auditing.Abstractions.Tests.Fixtures;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Testing;
using XiHan.Framework.Testing.Workspace;

namespace XiHan.Framework.Auditing.Abstractions.Tests;

/// <summary>
/// 回放 <c>exclusions.json</c>：审计按操作排除的项与匹配
/// </summary>
/// <remarks>
/// 规范 RFC-0015「事件的产生」（ADR-0080、ADR-0277）：排除项是操作 ID，或以 <c>*</c> 结尾的操作 ID 前缀，不合法是配置错误；匹配按序数，不带 <c>*</c> 的精确、带 <c>*</c> 的按前缀。向量是规范的一部分，这里只回放，不另存一份
/// </remarks>
public sealed class AuditExclusionVectorTests
{
    /// <summary>
    /// 向量文件的内容
    /// </summary>
    private static readonly ExclusionVectors _vectors = SpecVectors.Read<ExclusionVectors>("auditing", "exclusions.json");

    [Fact]
    public void Exclude_AcceptsTheValidEntries()
    {
        Assert.NotEmpty(_vectors.Entries.Valid);
        var rejected = _vectors.Entries.Valid.Where(static entry => !AcceptedWhenAssigned(entry) || !AcceptedFromConfiguration(entry)).ToList();

        Assert.True(rejected.Count == 0, "Valid entries that were rejected: " + Describe(rejected));
    }

    [Fact]
    public void Exclude_RejectsTheInvalidEntries()
    {
        Assert.NotEmpty(_vectors.Entries.Invalid);
        // 赋值与读配置两条路都要拒绝：不合法的项是配置错误，读取选项时即失败、应用不启动
        var accepted = _vectors.Entries.Invalid.Where(static entry => AcceptedWhenAssigned(entry) || AcceptedFromConfiguration(entry)).ToList();

        Assert.True(accepted.Count == 0, "Invalid entries that were accepted: " + Describe(accepted));
    }

    [Fact]
    public void Excludes_MatchesEveryCase()
    {
        Assert.NotEmpty(_vectors.Matches);
        var failures = new List<string>();
        foreach (var match in _vectors.Matches)
        {
            var options = new AuditingOptions { Exclude = match.Exclude };
            var excluded = new AuditExclusions(options.Exclude).Excludes(match.Operation);
            if (excluded != match.Excluded)
            {
                failures.Add($"{match.Name}: '{match.Operation}' against [{string.Join(", ", match.Exclude)}] was {excluded}, the vector has {match.Excluded}");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public async Task Middleware_SkipsExactlyTheExcludedOperations()
    {
        var failures = new List<string>();
        foreach (var match in _vectors.Matches)
        {
            // 匹配器是内部类型，看得见的是中间件：记所有操作时，被排除的不记、其余照记
            var options = new AuditingOptions { Selection = AuditSelection.All, Exclude = match.Exclude };
            var context = TestInvocationContext.Create();
            context.SetOperation(new ContractOnlyOperationDescriptor(new OperationContract(match.Operation)));

            var (events, thrown) = await AuditReplay.InvokeAsync(options, context, static _ => ValueTask.CompletedTask);

            Assert.Null(thrown);
            string[] expected = match.Excluded ? [] : [match.Operation];
            var recorded = events.Select(static e => e.Action).ToArray();
            if (!recorded.SequenceEqual(expected, StringComparer.Ordinal))
            {
                failures.Add($"{match.Name}: recorded [{string.Join(", ", recorded)}], the vector has [{string.Join(", ", expected)}]");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void TheVectors_CoverBothKindsOfEntries_AndBothOutcomes()
    {
        // 防止向量换了写法后某一类一条都没核对还照样通过：精确项与前缀项都要有，单独的 * 与 * 不在末尾的都要拒
        Assert.Contains(_vectors.Entries.Valid, static entry => !entry.Contains('*'));
        Assert.Contains(_vectors.Entries.Valid, static entry => entry.EndsWith('*'));
        Assert.Contains("*", _vectors.Entries.Invalid);
        Assert.Contains(_vectors.Entries.Invalid, static entry => entry.Contains('*') && !entry.EndsWith('*'));

        // 匹配：精确命中、前缀命中、有排除项而不命中、没有排除项
        Assert.Contains(_vectors.Matches, static match => match.Excluded && match.Exclude.Contains(match.Operation, StringComparer.Ordinal));
        Assert.Contains(_vectors.Matches, static match => match.Excluded && !match.Exclude.Contains(match.Operation, StringComparer.Ordinal));
        Assert.Contains(_vectors.Matches, static match => !match.Excluded && match.Exclude.Length > 0);
        Assert.Contains(_vectors.Matches, static match => match.Exclude.Length == 0);
    }

    /// <summary>
    /// 把这一项赋给 <see cref="AuditingOptions.Exclude"/> 是否被接受
    /// </summary>
    private static bool AcceptedWhenAssigned(string entry)
    {
        try
        {
            return new AuditingOptions { Exclude = [entry] }.Exclude.SequenceEqual([entry], StringComparer.Ordinal);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>
    /// 把这一项写在配置的 <c>XiHan:Auditing:Exclude:0</c> 是否被接受
    /// </summary>
    private static bool AcceptedFromConfiguration(string entry)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal) { [$"{AuditingOptions.ConfigurationSection}:Exclude:0"] = entry })
            .Build();
        try
        {
            return AuditingOptions.FromConfiguration(configuration).Exclude.SequenceEqual([entry], StringComparer.Ordinal);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>
    /// 失败信息里的若干项
    /// </summary>
    /// <remarks>
    /// 各自加引号：空串、空白与结尾的换行才看得出来
    /// </remarks>
    private static string Describe(IEnumerable<string> entries)
        => string.Join(", ", entries.Select(static entry => $"'{entry.Replace("\n", "\\n", StringComparison.Ordinal)}'"));

    /// <summary>
    /// 向量文件的对象模型
    /// </summary>
    /// <param name="Entries">排除项的文法用例</param>
    /// <param name="Matches">匹配的用例</param>
    private sealed record ExclusionVectors(ExclusionEntries Entries, ExclusionMatch[] Matches);

    /// <summary>
    /// 合法与不合法的排除项
    /// </summary>
    /// <param name="Valid">必须接受的</param>
    /// <param name="Invalid">必须拒绝的</param>
    private sealed record ExclusionEntries(string[] Valid, string[] Invalid);

    /// <summary>
    /// 一个匹配用例
    /// </summary>
    /// <param name="Name">用例的说明</param>
    /// <param name="Exclude">排除列表</param>
    /// <param name="Operation">按选择规则挑出的操作的 ID</param>
    /// <param name="Excluded">它是否命中列表里的任一项</param>
    private sealed record ExclusionMatch(string Name, string[] Exclude, string Operation, bool Excluded);
}
