// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Activities;
using XiHan.Framework.Workflow.Abstractions.Definitions;
using XiHan.Framework.Workflow.Abstractions.Expressions;
using XiHan.Framework.Workflow.Abstractions.Instances;
using XiHan.Framework.Workflow.Abstractions.Serialization;

namespace XiHan.Framework.Workflow.Abstractions.Tests;

/// <summary>
/// 表达式诊断、活动描述符、状态名与 lastError 的本地化（ADR-0106）
/// </summary>
public sealed class WorkflowLocalizationTests
{
    /// <summary>
    /// 测试用的解析器
    /// </summary>
    private static readonly WorkflowCatalogResolver _messages = new();

    /// <summary>
    /// 默认选项的求值器
    /// </summary>
    private static readonly WorkflowExpressionEvaluator _evaluator = new(new WorkflowOptions());

    [Fact]
    public void ExpressionDiagnostics_AreMessageKeysWithTheExpression()
    {
        var unknownFunction = _evaluator.Validate("unknownFn(1)").Error!;
        Assert.Equal((WorkflowErrors.ExpressionInvalid, WorkflowMessages.ExpressionUnknownFunction, (object?)"unknownFn(1)", (object?)"unknownFn"), (unknownFunction.Code, unknownFunction.MessageKey, unknownFunction.Details!["expression"], unknownFunction.Details["function"]));
        Assert.Equal("表达式 'unknownFn(1)' 引用了未知函数 'unknownFn'。", Render("zh-CN", unknownFunction));
        Assert.Equal("Expression 'unknownFn(1)': function 'unknownFn' is unknown.", Render("en-US", unknownFunction));

        var arity = _evaluator.Validate("len()").Error!;
        Assert.Equal("表达式 'len()'：函数 'len' 需要 1 个参数。", Render("zh-CN", arity));
        Assert.Equal("Expression 'len()': function 'len' takes 1 argument(s).", Render("en-US", arity));

        // 位置从 1 起
        var unexpected = _evaluator.Validate("1 # 2").Error!;
        Assert.Equal((WorkflowMessages.ExpressionUnexpectedCharacter, (object?)"#", (object?)3), (unexpected.MessageKey, unexpected.Details!["character"], unexpected.Details["position"]));
        Assert.Equal("表达式 '1 # 2' 的第 3 个字符处出现意外的 '#'。", Render("zh-CN", unexpected));

        // 运行期：除以零、未知变量
        var scope = new ExpressionScope(new JsonObject { ["x"] = 1 }, TimeProvider.System);
        var division = _evaluator.Evaluate("x / 0", scope).Error!;
        Assert.Equal((WorkflowMessages.ExpressionDivisionByZero, "表达式 'x / 0' 除以零。", "Expression 'x / 0' divides by zero."), (division.MessageKey, Render("zh-CN", division), Render("en-US", division)));
        Assert.Equal(WorkflowMessages.ExpressionUnknownVariable, _evaluator.Evaluate("nope", scope).Error?.MessageKey);
    }

    [Fact]
    public void ActivityDescriptors_LocalizeOrKeepTheirFallbacks()
    {
        var services = new ServiceCollection();
        services.AddWorkflowEngine(new WorkflowOptions());
        using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<ActivityRegistry>();

        // 每个内置活动的显示名、说明与分类都有键，10 种语言里都有文案（这里核对中文）
        foreach (var descriptor in registry.Activities.Select(static activity => activity.Descriptor))
        {
            Assert.All(new[] { descriptor.DisplayNameKey, descriptor.DescriptionKey, descriptor.CategoryKey }, key => Assert.True(key is not null && WorkflowMessages.Catalog.Find(key, "zh-CN").HasValue, $"{descriptor.Type}: {key}"));
        }

        using (MessageScope.Use(_messages, "zh-CN"))
        {
            var http = BuiltInActivityDescriptors.Http.Localize();
            Assert.Equal(("HTTP 请求", "集成", "发送 HTTP 请求。", BuiltInActivityDescriptors.IntegrationCategory), (http.DisplayName, http.CategoryName, http.Description, http.Category));
            Assert.Equal("WaitAll（等全部）或 WaitAny（等任一）。", Assert.Single(BuiltInActivityDescriptors.Join.Localize().Inputs).Description);
        }

        // 目录里没有这个区域的文案：保留回退值（显示名是活动类型名），不显示键本身
        using (MessageScope.Use(_messages, "xx-XX"))
        {
            var http = BuiltInActivityDescriptors.Http.Localize();
            Assert.Equal((ActivityTypes.Http, BuiltInActivityDescriptors.IntegrationCategory, null), (http.DisplayName, http.CategoryName, http.Description));
        }

        // 应用自己的描述符只给文本：原样
        var custom = new ActivityDescriptor("Custom", "Custom step", "Mine", "Does custom work.").Localize();
        Assert.Equal(("Custom step", "Mine", "Does custom work."), (custom.DisplayName, custom.CategoryName, custom.Description));
    }

    [Fact]
    public void StatusNames_HaveMessageKeysForEveryValue()
    {
        foreach (var status in Enum.GetValues<WorkflowInstanceStatus>())
        {
            Assert.True(WorkflowMessages.Catalog.Find(WorkflowStatusMessages.KeyOf(status), "zh-CN").HasValue, status.ToString());
        }

        foreach (var status in Enum.GetValues<WorkflowNodeInstanceStatus>())
        {
            Assert.True(WorkflowMessages.Catalog.Find(WorkflowStatusMessages.KeyOf(status), "zh-CN").HasValue, status.ToString());
        }

        foreach (var status in Enum.GetValues<WorkflowDefinitionStatus>())
        {
            Assert.True(WorkflowMessages.Catalog.Find(WorkflowStatusMessages.KeyOf(status), "zh-CN").HasValue, status.ToString());
        }

        using (MessageScope.Use(_messages, "zh-CN"))
        {
            Assert.Equal(("故障", "已补偿", "已发布"), (WorkflowInstanceStatus.Faulted.Localize(), WorkflowNodeInstanceStatus.Compensated.Localize(), WorkflowDefinitionStatus.Published.Localize()));
        }

        using (MessageScope.Use(_messages, "en-US"))
        {
            Assert.Equal(("Faulted", "Compensated", "Published"), (WorkflowInstanceStatus.Faulted.Localize(), WorkflowNodeInstanceStatus.Compensated.Localize(), WorkflowDefinitionStatus.Published.Localize()));
        }
    }

    [Fact]
    public void LastError_IsTheFaultShape_ThatOldDocumentsStillRead()
    {
        var fault = WorkflowFault.From(Error.Validation(WorkflowErrors.InputInvalid, WorkflowMessages.PropertyNotArray, MessageArguments.Of(("name", "Items"))));
        var variable = WorkflowJson.SerializeFault(fault);
        Assert.Equal((WorkflowErrors.InputInvalid, WorkflowMessages.PropertyNotArray, "Items"), (variable["code"]!.GetValue<string>(), variable["messageKey"]!.GetValue<string>(), variable["arguments"]!["name"]!.GetValue<string>()));
        Assert.NotNull(variable["message"]);

        // 表达式照旧按 code 判断
        var scope = new ExpressionScope(new JsonObject { ["lastError"] = variable }, TimeProvider.System);
        Assert.True(_evaluator.EvaluateCondition($"lastError.code == '{WorkflowErrors.InputInvalid}' && lastError.message != null", scope).ValueOrThrow());

        using (MessageScope.Use(_messages, "zh-CN"))
        {
            Assert.Equal("活动属性 'Items' 的求值结果必须是数组。", WorkflowJson.DeserializeFault(variable)!.Localize());
        }

        // 升级前写入的 lastError 只有 code 与 message：照读，展示原文
        var old = WorkflowJson.DeserializeFault(new JsonObject { ["code"] = WorkflowErrors.ActivityFailed, ["message"] = "boom" })!;
        Assert.Equal((WorkflowErrors.ActivityFailed, null, "boom"), (old.Code, old.MessageKey, old.Localize()));
        Assert.Null(WorkflowJson.DeserializeFault(null));
    }

    /// <summary>
    /// 在给定区域渲染错误
    /// </summary>
    private static string Render(string locale, Error error)
    {
        using var scope = MessageScope.Use(_messages, locale);
        return error.MessageKey!.L(error.Details);
    }
}
