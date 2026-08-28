// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Authorization.Abac;

namespace XiHan.Framework.Authorization.Tests.Abac;

/// <summary>
/// 默认 ABAC 评估器测试
/// </summary>
/// <remarks>
/// 评估器按“空策略 → allow → 租户一致 → 仅本人 → 比较表达式 → 兜底拒绝”的固定顺序短路，
/// 这里逐档验证：既锁死每档的命中条件与结论文案，也锁死值比较的三条规则
/// （集合按包含、数值按数值、其余按大小写不敏感字符串）。
/// </remarks>
public class DefaultAbacEvaluatorTests
{
    /// <summary>
    /// 上下文为空时抛参数异常
    /// </summary>
    [Fact]
    public async Task EvaluateAsync_WhenContextNull_Throws()
    {
        var evaluator = new DefaultAbacEvaluator();

        await Assert.ThrowsAsync<ArgumentNullException>(() => evaluator.EvaluateAsync(null!));
    }

    /// <summary>
    /// 令牌已取消时抛取消异常，不做任何评估
    /// </summary>
    [Fact]
    public async Task EvaluateAsync_WhenTokenCancelled_Throws()
    {
        var evaluator = new DefaultAbacEvaluator();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => evaluator.EvaluateAsync(BuildContext("allow"), cts.Token));
    }

    /// <summary>
    /// 未配置策略编码时放行（ABAC 只在显式配置时收紧）
    /// </summary>
    /// <param name="policyCode">策略编码</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task EvaluateAsync_WhenPolicyBlank_Allows(string policyCode)
    {
        var result = await new DefaultAbacEvaluator().EvaluateAsync(
            BuildContext(policyCode),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsAllowed);
        Assert.Equal("未配置 ABAC 策略", result.Reason);
    }

    /// <summary>
    /// 策略编码为 null 时同样按未配置处理
    /// </summary>
    [Fact]
    public async Task EvaluateAsync_WhenPolicyNull_Allows()
    {
        var result = await new DefaultAbacEvaluator().EvaluateAsync(
            BuildContext(null),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsAllowed);
        Assert.Equal("未配置 ABAC 策略", result.Reason);
    }

    /// <summary>
    /// allow 策略无条件放行，且忽略大小写与首尾空白
    /// </summary>
    /// <param name="policyCode">策略编码</param>
    [Theory]
    [InlineData("allow")]
    [InlineData("ALLOW")]
    [InlineData("  Allow  ")]
    public async Task EvaluateAsync_WhenPolicyIsAllow_Allows(string policyCode)
    {
        var result = await new DefaultAbacEvaluator().EvaluateAsync(
            BuildContext(policyCode),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsAllowed);
        Assert.Equal("命中 allow 策略", result.Reason);
    }

    /// <summary>
    /// 租户一致策略：两个别名、三个资源键位都能命中
    /// </summary>
    /// <param name="policyCode">策略编码</param>
    /// <param name="resourceKey">承载租户的资源属性键</param>
    [Theory]
    [InlineData("same_tenant", "tenant_id")]
    [InlineData("same_tenant", "route.tenant_id")]
    [InlineData("same_tenant", "query.tenant_id")]
    [InlineData("tenant_match", "tenant_id")]
    [InlineData("TENANT_MATCH", "route.tenant_id")]
    public async Task EvaluateAsync_WhenTenantMatches_Allows(string policyCode, string resourceKey)
    {
        var result = await new DefaultAbacEvaluator().EvaluateAsync(
            BuildContext(
                policyCode,
                subject: Attributes(("tenant_id", "t1")),
                resource: Attributes((resourceKey, "t1"))),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsAllowed);
        Assert.Equal("租户匹配", result.Reason);
    }

    /// <summary>
    /// 租户不一致时拒绝
    /// </summary>
    [Fact]
    public async Task EvaluateAsync_WhenTenantDiffers_Denies()
    {
        var result = await new DefaultAbacEvaluator().EvaluateAsync(
            BuildContext(
                "same_tenant",
                subject: Attributes(("tenant_id", "t1")),
                resource: Attributes(("tenant_id", "t2"))),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsAllowed);
        Assert.Equal("租户不匹配", result.Reason);
    }

    /// <summary>
    /// 主体没有租户属性时拒绝，不能因为“取不到”而放行
    /// </summary>
    [Fact]
    public async Task EvaluateAsync_WhenSubjectTenantMissing_Denies()
    {
        var result = await new DefaultAbacEvaluator().EvaluateAsync(
            BuildContext("same_tenant", resource: Attributes(("tenant_id", "t1"))),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsAllowed);
        Assert.Equal("租户不匹配", result.Reason);
    }

    /// <summary>
    /// 资源上一个候选键都没有时拒绝
    /// </summary>
    [Fact]
    public async Task EvaluateAsync_WhenResourceTenantMissing_Denies()
    {
        var result = await new DefaultAbacEvaluator().EvaluateAsync(
            BuildContext("same_tenant", subject: Attributes(("tenant_id", "t1"))),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsAllowed);
        Assert.Equal("租户不匹配", result.Reason);
    }

    /// <summary>
    /// 仅本人策略：两个别名、四个资源键位都能命中
    /// </summary>
    /// <param name="policyCode">策略编码</param>
    /// <param name="resourceKey">承载归属人的资源属性键</param>
    [Theory]
    [InlineData("self_only", "user_id")]
    [InlineData("self_only", "owner_user_id")]
    [InlineData("self_only", "route.user_id")]
    [InlineData("self_only", "query.user_id")]
    [InlineData("owner_match", "owner_user_id")]
    public async Task EvaluateAsync_WhenOwnerMatches_Allows(string policyCode, string resourceKey)
    {
        var result = await new DefaultAbacEvaluator().EvaluateAsync(
            BuildContext(
                policyCode,
                subject: Attributes(("user_id", "u1")),
                resource: Attributes((resourceKey, "u1"))),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsAllowed);
        Assert.Equal("用户归属匹配", result.Reason);
    }

    /// <summary>
    /// 归属人不同时拒绝
    /// </summary>
    [Fact]
    public async Task EvaluateAsync_WhenOwnerDiffers_Denies()
    {
        var result = await new DefaultAbacEvaluator().EvaluateAsync(
            BuildContext(
                "self_only",
                subject: Attributes(("user_id", "u1")),
                resource: Attributes(("owner_user_id", "u2"))),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsAllowed);
        Assert.Equal("用户归属不匹配", result.Reason);
    }

    /// <summary>
    /// 属性值比较忽略大小写，避免同一标识不同写法被判成越权
    /// </summary>
    [Fact]
    public async Task EvaluateAsync_WhenOwnerCaseDiffers_StillMatches()
    {
        var result = await new DefaultAbacEvaluator().EvaluateAsync(
            BuildContext(
                "self_only",
                subject: Attributes(("user_id", "U1")),
                resource: Attributes(("user_id", "u1"))),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsAllowed);
    }

    /// <summary>
    /// 主体值是集合时，只要有一项与资源值相等即命中
    /// </summary>
    [Fact]
    public async Task EvaluateAsync_WhenSubjectValueIsCollection_MatchesAnyItem()
    {
        var result = await new DefaultAbacEvaluator().EvaluateAsync(
            BuildContext(
                "self_only",
                subject: Attributes(("user_id", new[] { "u0", "u1" })),
                resource: Attributes(("user_id", "u1"))),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsAllowed);
    }

    /// <summary>
    /// 等值表达式两边都能解析且相等时放行
    /// </summary>
    [Fact]
    public async Task EvaluateAsync_WhenEqualityExpressionHolds_Allows()
    {
        var result = await new DefaultAbacEvaluator().EvaluateAsync(
            BuildContext(
                "subject.tenant_id == resource.tenant_id",
                subject: Attributes(("tenant_id", "t1")),
                resource: Attributes(("tenant_id", "t1"))),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsAllowed);
        Assert.Equal("表达式评估通过", result.Reason);
    }

    /// <summary>
    /// 等值表达式不成立时拒绝
    /// </summary>
    [Fact]
    public async Task EvaluateAsync_WhenEqualityExpressionFails_Denies()
    {
        var result = await new DefaultAbacEvaluator().EvaluateAsync(
            BuildContext(
                "subject.tenant_id == resource.tenant_id",
                subject: Attributes(("tenant_id", "t1")),
                resource: Attributes(("tenant_id", "t2"))),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsAllowed);
        Assert.Equal("表达式评估未通过", result.Reason);
    }

    /// <summary>
    /// 不等表达式在两边不同时放行
    /// </summary>
    [Fact]
    public async Task EvaluateAsync_WhenInequalityExpressionHolds_Allows()
    {
        var result = await new DefaultAbacEvaluator().EvaluateAsync(
            BuildContext(
                "subject.tenant_id != resource.tenant_id",
                subject: Attributes(("tenant_id", "t1")),
                resource: Attributes(("tenant_id", "t2"))),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsAllowed);
        Assert.Equal("表达式评估通过", result.Reason);
    }

    /// <summary>
    /// 不等表达式在两边相同时拒绝
    /// </summary>
    [Fact]
    public async Task EvaluateAsync_WhenInequalityExpressionFails_Denies()
    {
        var result = await new DefaultAbacEvaluator().EvaluateAsync(
            BuildContext(
                "subject.tenant_id != resource.tenant_id",
                subject: Attributes(("tenant_id", "t1")),
                resource: Attributes(("tenant_id", "t1"))),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsAllowed);
        Assert.Equal("表达式评估未通过", result.Reason);
    }

    /// <summary>
    /// 单等号与双等号同义，都是等值判断
    /// </summary>
    [Fact]
    public async Task EvaluateAsync_SingleEqualsOperator_MeansEquality()
    {
        var result = await new DefaultAbacEvaluator().EvaluateAsync(
            BuildContext(
                "subject.user_id = resource.owner_user_id",
                subject: Attributes(("user_id", "u1")),
                resource: Attributes(("owner_user_id", "u1"))),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsAllowed);
    }

    /// <summary>
    /// 引号包裹的字面量会被剥掉引号后参与比较
    /// </summary>
    /// <param name="literal">带引号的字面量</param>
    [Theory]
    [InlineData("'t1'")]
    [InlineData("\"t1\"")]
    public async Task EvaluateAsync_QuotedLiteral_IsUnquoted(string literal)
    {
        var result = await new DefaultAbacEvaluator().EvaluateAsync(
            BuildContext(
                $"subject.tenant_id == {literal}",
                subject: Attributes(("tenant_id", "t1"))),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsAllowed);
    }

    /// <summary>
    /// 数值按数值比较，不受字面写法差异影响
    /// </summary>
    /// <param name="literal">右侧数值字面量</param>
    [Theory]
    [InlineData("10")]
    [InlineData("10.0")]
    [InlineData("10.00")]
    public async Task EvaluateAsync_NumericLiteral_ComparesNumerically(string literal)
    {
        var result = await new DefaultAbacEvaluator().EvaluateAsync(
            BuildContext(
                $"subject.level == {literal}",
                subject: Attributes(("level", 10))),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsAllowed);
    }

    /// <summary>
    /// 布尔属性与布尔字面量可以比较
    /// </summary>
    [Fact]
    public async Task EvaluateAsync_BooleanLiteral_ComparesWithBooleanAttribute()
    {
        var result = await new DefaultAbacEvaluator().EvaluateAsync(
            BuildContext(
                "subject.is_authenticated == true",
                subject: Attributes(("is_authenticated", true))),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsAllowed);
    }

    /// <summary>
    /// 集合属性与单值字面量按“包含”判定
    /// </summary>
    [Fact]
    public async Task EvaluateAsync_CollectionAttribute_MatchesByContains()
    {
        var allowed = await new DefaultAbacEvaluator().EvaluateAsync(
            BuildContext(
                "subject.roles == 'admin'",
                subject: Attributes(("roles", new[] { "ops", "admin" }))),
            TestContext.Current.CancellationToken);
        var denied = await new DefaultAbacEvaluator().EvaluateAsync(
            BuildContext(
                "subject.roles == 'root'",
                subject: Attributes(("roles", new[] { "ops", "admin" }))),
            TestContext.Current.CancellationToken);

        Assert.True(allowed.IsAllowed);
        Assert.False(denied.IsAllowed);
    }

    /// <summary>
    /// environment 前缀从环境属性取值
    /// </summary>
    [Fact]
    public async Task EvaluateAsync_EnvironmentPrefix_ReadsEnvironmentAttributes()
    {
        var result = await new DefaultAbacEvaluator().EvaluateAsync(
            BuildContext(
                "environment.request_method == 'POST'",
                environment: Attributes(("request_method", "POST"))),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsAllowed);
    }

    /// <summary>
    /// resource 前缀从资源属性取值
    /// </summary>
    [Fact]
    public async Task EvaluateAsync_ResourcePrefix_ReadsResourceAttributes()
    {
        var result = await new DefaultAbacEvaluator().EvaluateAsync(
            BuildContext(
                "resource.status == 'draft'",
                resource: Attributes(("status", "draft"))),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsAllowed);
    }

    /// <summary>
    /// 既不是已知关键字也不含比较运算符的策略一律拒绝，并在说明里回显策略编码
    /// </summary>
    [Fact]
    public async Task EvaluateAsync_WhenPolicyUnsupported_Denies()
    {
        var result = await new DefaultAbacEvaluator().EvaluateAsync(
            BuildContext("business_hours_only"),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsAllowed);
        Assert.Equal("不支持的 ABAC 策略: business_hours_only", result.Reason);
    }

    /// <summary>
    /// 策略编码前后的空白在回显前已被裁剪
    /// </summary>
    [Fact]
    public async Task EvaluateAsync_WhenPolicyUnsupported_ReportsTrimmedCode()
    {
        var result = await new DefaultAbacEvaluator().EvaluateAsync(
            BuildContext("  business_hours_only  "),
            TestContext.Current.CancellationToken);

        Assert.Equal("不支持的 ABAC 策略: business_hours_only", result.Reason);
    }

    private static AbacEvaluationContext BuildContext(
        string? policyCode,
        Dictionary<string, object?>? subject = null,
        Dictionary<string, object?>? resource = null,
        Dictionary<string, object?>? environment = null)
    {
        return new AbacEvaluationContext
        {
            UserId = "u1",
            PermissionCode = "Sys.User.Read",
            PolicyCode = policyCode!,
            SubjectAttributes = subject ?? Attributes(),
            ResourceAttributes = resource ?? Attributes(),
            EnvironmentAttributes = environment ?? Attributes()
        };
    }

    private static Dictionary<string, object?> Attributes(params (string Key, object? Value)[] pairs)
    {
        var attributes = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in pairs)
        {
            attributes[pair.Key] = pair.Value;
        }

        return attributes;
    }
}
