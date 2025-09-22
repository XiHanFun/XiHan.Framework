#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ITemplateSecurityChecker
// Guid:9g4i8i3e-7h0j-8f3g-4i9i-8e3h0j7g4i9i
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/16 3:42:14
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Templating.Abstractions;

/// <summary>
/// 模板安全检查器接口
/// </summary>
public interface ITemplateSecurityChecker
{
    /// <summary>
    /// 检查模板安全性
    /// </summary>
    /// <param name="templateSource">模板源码</param>
    /// <param name="options">安全选项</param>
    /// <returns>安全检查结果</returns>
    TemplateSecurityResult CheckSecurity(string templateSource, TemplateSecurityOptions? options = null);

    /// <summary>
    /// 异步检查模板安全性
    /// </summary>
    /// <param name="templateSource">模板源码</param>
    /// <param name="options">安全选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>安全检查结果</returns>
    Task<TemplateSecurityResult> CheckSecurityAsync(string templateSource, TemplateSecurityOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 检查表达式安全性
    /// </summary>
    /// <param name="expression">表达式</param>
    /// <param name="options">安全选项</param>
    /// <returns>安全检查结果</returns>
    TemplateSecurityResult CheckExpression(string expression, TemplateSecurityOptions? options = null);

    /// <summary>
    /// 验证上下文安全性
    /// </summary>
    /// <param name="context">模板上下文</param>
    /// <param name="options">安全选项</param>
    /// <returns>安全检查结果</returns>
    TemplateSecurityResult ValidateContext(ITemplateContext context, TemplateSecurityOptions? options = null);
}

/// <summary>
/// 模板安全分析器
/// </summary>
public interface ITemplateSecurityAnalyzer
{
    /// <summary>
    /// 分析模板安全风险
    /// </summary>
    /// <param name="templateSource">模板源码</param>
    /// <returns>安全风险报告</returns>
    TemplateSecurityReport AnalyzeSecurity(string templateSource);

    /// <summary>
    /// 检测危险模式
    /// </summary>
    /// <param name="templateSource">模板源码</param>
    /// <returns>危险模式集合</returns>
    ICollection<SecurityThreat> DetectThreats(string templateSource);

    /// <summary>
    /// 验证白名单
    /// </summary>
    /// <param name="expression">表达式</param>
    /// <param name="whitelist">白名单</param>
    /// <returns>是否通过验证</returns>
    bool ValidateWhitelist(string expression, ICollection<string> whitelist);

    /// <summary>
    /// 检查黑名单
    /// </summary>
    /// <param name="expression">表达式</param>
    /// <param name="blacklist">黑名单</param>
    /// <returns>是否存在违规</returns>
    bool CheckBlacklist(string expression, ICollection<string> blacklist);

    /// <summary>
    /// 扫描文件包含
    /// </summary>
    /// <param name="templateSource">模板源码</param>
    /// <returns>文件包含列表</returns>
    ICollection<string> ScanFileIncludes(string templateSource);
}

/// <summary>
/// 模板安全选项
/// </summary>
public record TemplateSecurityOptions
{
    /// <summary>
    /// 最大模板大小（字节）
    /// </summary>
    public int MaxTemplateSize { get; init; } = 1024 * 1024; // 1MB

    /// <summary>
    /// 最大表达式深度
    /// </summary>
    public int MaxExpressionDepth { get; init; } = 10;

    /// <summary>
    /// 最大循环次数
    /// </summary>
    public int MaxLoopIterations { get; init; } = 10000;

    /// <summary>
    /// 最大包含文件数
    /// </summary>
    public int MaxIncludeFiles { get; init; } = 100;

    /// <summary>
    /// 是否允许文件访问
    /// </summary>
    public bool AllowFileAccess { get; init; } = false;

    /// <summary>
    /// 是否允许网络访问
    /// </summary>
    public bool AllowNetworkAccess { get; init; } = false;

    /// <summary>
    /// 是否允许反射
    /// </summary>
    public bool AllowReflection { get; init; } = false;

    /// <summary>
    /// 是否允许类型实例化
    /// </summary>
    public bool AllowTypeInstantiation { get; init; } = false;

    /// <summary>
    /// 允许的命名空间
    /// </summary>
    public ICollection<string> AllowedNamespaces { get; init; } = [];

    /// <summary>
    /// 禁止的命名空间
    /// </summary>
    public ICollection<string> ForbiddenNamespaces { get; init; } = [];

    /// <summary>
    /// 允许的类型
    /// </summary>
    public ICollection<Type> AllowedTypes { get; init; } = [];

    /// <summary>
    /// 禁止的类型
    /// </summary>
    public ICollection<Type> ForbiddenTypes { get; init; } = [];

    /// <summary>
    /// 允许的方法名
    /// </summary>
    public ICollection<string> AllowedMethods { get; init; } = [];

    /// <summary>
    /// 禁止的方法名
    /// </summary>
    public ICollection<string> ForbiddenMethods { get; init; } = [];

    /// <summary>
    /// 默认安全选项
    /// </summary>
    public static TemplateSecurityOptions Default => new();

    /// <summary>
    /// 严格安全选项
    /// </summary>
    public static TemplateSecurityOptions Strict => new()
    {
        AllowFileAccess = false,
        AllowNetworkAccess = false,
        AllowReflection = false,
        AllowTypeInstantiation = false,
        MaxTemplateSize = 100 * 1024, // 100KB
        MaxExpressionDepth = 5,
        MaxLoopIterations = 1000,
        MaxIncludeFiles = 10
    };

    /// <summary>
    /// 宽松安全选项
    /// </summary>
    public static TemplateSecurityOptions Relaxed => new()
    {
        AllowFileAccess = true,
        AllowNetworkAccess = false,
        AllowReflection = true,
        AllowTypeInstantiation = true,
        MaxTemplateSize = 5 * 1024 * 1024, // 5MB
        MaxExpressionDepth = 20,
        MaxLoopIterations = 100000,
        MaxIncludeFiles = 1000
    };
}

/// <summary>
/// 模板安全检查结果
/// </summary>
public record TemplateSecurityResult
{
    /// <summary>
    /// 是否安全
    /// </summary>
    public bool IsSecure { get; init; }

    /// <summary>
    /// 风险级别
    /// </summary>
    public SecurityRiskLevel RiskLevel { get; init; }

    /// <summary>
    /// 安全威胁
    /// </summary>
    public ICollection<SecurityThreat> Threats { get; init; } = [];

    /// <summary>
    /// 检查时间（毫秒）
    /// </summary>
    public long CheckTimeMs { get; init; }

    /// <summary>
    /// 建议措施
    /// </summary>
    public ICollection<string> Recommendations { get; init; } = [];

    /// <summary>
    /// 安全结果
    /// </summary>
    /// <param name="checkTime">检查时间</param>
    /// <returns>安全结果</returns>
    public static TemplateSecurityResult Secure(long checkTime = 0)
        => new() { IsSecure = true, RiskLevel = SecurityRiskLevel.Low, CheckTimeMs = checkTime };

    /// <summary>
    /// 不安全结果
    /// </summary>
    /// <param name="threats">安全威胁</param>
    /// <param name="riskLevel">风险级别</param>
    /// <param name="checkTime">检查时间</param>
    /// <returns>不安全结果</returns>
    public static TemplateSecurityResult Insecure(ICollection<SecurityThreat> threats, SecurityRiskLevel riskLevel = SecurityRiskLevel.High, long checkTime = 0)
        => new() { IsSecure = false, Threats = threats, RiskLevel = riskLevel, CheckTimeMs = checkTime };
}

/// <summary>
/// 模板安全报告
/// </summary>
public record TemplateSecurityReport
{
    /// <summary>
    /// 模板大小
    /// </summary>
    public int TemplateSize { get; init; }

    /// <summary>
    /// 表达式数量
    /// </summary>
    public int ExpressionCount { get; init; }

    /// <summary>
    /// 最大表达式深度
    /// </summary>
    public int MaxExpressionDepth { get; init; }

    /// <summary>
    /// 循环数量
    /// </summary>
    public int LoopCount { get; init; }

    /// <summary>
    /// 文件包含数量
    /// </summary>
    public int IncludeCount { get; init; }

    /// <summary>
    /// 使用的类型
    /// </summary>
    public ICollection<string> UsedTypes { get; init; } = [];

    /// <summary>
    /// 使用的方法
    /// </summary>
    public ICollection<string> UsedMethods { get; init; } = [];

    /// <summary>
    /// 检测到的威胁
    /// </summary>
    public ICollection<SecurityThreat> Threats { get; init; } = [];

    /// <summary>
    /// 总体风险级别
    /// </summary>
    public SecurityRiskLevel OverallRisk { get; init; }
}

/// <summary>
/// 安全威胁
/// </summary>
public record SecurityThreat
{
    /// <summary>
    /// 威胁类型
    /// </summary>
    public SecurityThreatType Type { get; init; }

    /// <summary>
    /// 风险级别
    /// </summary>
    public SecurityRiskLevel RiskLevel { get; init; }

    /// <summary>
    /// 威胁描述
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 威胁位置
    /// </summary>
    public TemplateSourceLocation? Location { get; init; }

    /// <summary>
    /// 相关代码
    /// </summary>
    public string? Code { get; init; }

    /// <summary>
    /// 修复建议
    /// </summary>
    public string? Recommendation { get; init; }
}

/// <summary>
/// 安全威胁类型
/// </summary>
public enum SecurityThreatType
{
    /// <summary>
    /// 文件访问
    /// </summary>
    FileAccess,

    /// <summary>
    /// 网络访问
    /// </summary>
    NetworkAccess,

    /// <summary>
    /// 反射操作
    /// </summary>
    Reflection,

    /// <summary>
    /// 类型实例化
    /// </summary>
    TypeInstantiation,

    /// <summary>
    /// 危险方法调用
    /// </summary>
    DangerousMethodCall,

    /// <summary>
    /// 无限循环风险
    /// </summary>
    InfiniteLoopRisk,

    /// <summary>
    /// 内存耗尽风险
    /// </summary>
    MemoryExhaustionRisk,

    /// <summary>
    /// 代码注入
    /// </summary>
    CodeInjection,

    /// <summary>
    /// 路径遍历
    /// </summary>
    PathTraversal,

    /// <summary>
    /// 敏感信息泄露
    /// </summary>
    InformationDisclosure
}

/// <summary>
/// 安全风险级别
/// </summary>
public enum SecurityRiskLevel
{
    /// <summary>
    /// 低风险
    /// </summary>
    Low = 0,

    /// <summary>
    /// 中风险
    /// </summary>
    Medium = 1,

    /// <summary>
    /// 高风险
    /// </summary>
    High = 2,

    /// <summary>
    /// 严重风险
    /// </summary>
    Critical = 3
}
