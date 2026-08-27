// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Authorization.Abac;

namespace XiHan.Framework.Authorization.Tests.Abac;

/// <summary>
/// ABAC 评估上下文测试
/// </summary>
/// <remarks>
/// 评估器在拿不到属性时会退化成字面量比较，所以上下文的三组属性必须默认是空字典而不是 null，
/// 否则第一条策略表达式就会炸在空引用上。
/// </remarks>
public class AbacEvaluationContextTests
{
    /// <summary>
    /// 字符串字段默认是空串而不是 null
    /// </summary>
    [Fact]
    public void New_ByDefault_HasEmptyStringFields()
    {
        var context = new AbacEvaluationContext();

        Assert.Equal(string.Empty, context.UserId);
        Assert.Equal(string.Empty, context.PermissionCode);
        Assert.Equal(string.Empty, context.PolicyCode);
        Assert.Null(context.Resource);
    }

    /// <summary>
    /// 三组属性字典默认是空集合而不是 null
    /// </summary>
    [Fact]
    public void New_ByDefault_HasEmptyAttributeDictionaries()
    {
        var context = new AbacEvaluationContext();

        Assert.NotNull(context.SubjectAttributes);
        Assert.NotNull(context.ResourceAttributes);
        Assert.NotNull(context.EnvironmentAttributes);
        Assert.Empty(context.SubjectAttributes);
        Assert.Empty(context.ResourceAttributes);
        Assert.Empty(context.EnvironmentAttributes);
    }

    /// <summary>
    /// 评估时间默认取构造时刻的 UTC 时间
    /// </summary>
    [Fact]
    public void New_ByDefault_StampsUtcNow()
    {
        var before = DateTimeOffset.UtcNow.AddSeconds(-5);
        var context = new AbacEvaluationContext();
        var after = DateTimeOffset.UtcNow.AddSeconds(5);

        Assert.InRange(context.EvaluationTime, before, after);
        Assert.Equal(TimeSpan.Zero, context.EvaluationTime.Offset);
    }

    /// <summary>
    /// 属性字典可整体替换，替换后按引用生效
    /// </summary>
    [Fact]
    public void Attributes_CanBeReplaced()
    {
        var subject = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { ["user_id"] = "u1" };
        var context = new AbacEvaluationContext { SubjectAttributes = subject };

        Assert.Same(subject, context.SubjectAttributes);
        Assert.Equal("u1", context.SubjectAttributes["user_id"]);
    }
}
