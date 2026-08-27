// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using XiHan.Framework.Web.Docs.Swagger;

namespace XiHan.Framework.Web.Docs.Tests.Swagger;

/// <summary>
/// 动态 API XML 注释操作转换器测试
/// </summary>
/// <remarks>
/// 转换器的关键契约是"越界即静默返回"：非控制器动作、缺少 OriginalMethodAttribute、原始方法反查不到，
/// 三种情况都必须原样放行，绝不能因为文档增强把 OpenAPI 生成打断。
/// 测试工程关闭了 GenerateDocumentationFile，所以自身程序集没有 xml 文档，
/// 走的是"无 xml 文档但有 DynamicApi.Description"这条兜底分支。
/// OpenApiOperationTransformerContext 的属性是 required init，无法用对象初始化器稳妥构造
/// （required 成员集合随 Microsoft.AspNetCore.OpenApi 版本变化），这里用未初始化对象加属性反射赋值，
/// 构造不出来就跳过而不是误报失败。
/// </remarks>
public class DynamicApiXmlCommentsOperationTransformerTests
{
    private const string ContextUnavailableReason =
        "无法反射构造 OpenApiOperationTransformerContext（Microsoft.AspNetCore.OpenApi 结构变化），跳过该组验证。";

    /// <summary>
    /// 转换器实现 OpenAPI 操作转换器接口，否则注册进 OpenApiOptions 时会被忽略
    /// </summary>
    [Fact]
    public void Transformer_ImplementsOpenApiOperationTransformer()
    {
        Assert.True(typeof(DynamicApiXmlCommentsOperationTransformer).IsAssignableTo(typeof(IOpenApiOperationTransformer)));
    }

    /// <summary>
    /// 动作描述符不是控制器动作时原样放行
    /// </summary>
    [Fact]
    public async Task TransformAsync_WhenActionDescriptorIsNotControllerAction_LeavesOperationUntouched()
    {
        var description = new ApiDescription
        {
            ActionDescriptor = new ActionDescriptor()
        };
        Assert.SkipUnless(TryCreateContext(description, out var context), ContextUnavailableReason);

        var operation = new OpenApiOperation();

        await new DynamicApiXmlCommentsOperationTransformer()
            .TransformAsync(operation, context!, TestContext.Current.CancellationToken);

        Assert.Null(operation.Summary);
        Assert.Null(operation.Description);
    }

    /// <summary>
    /// 控制器动作没有原始方法标记时原样放行
    /// </summary>
    [Fact]
    public async Task TransformAsync_WhenOriginalMethodAttributeMissing_LeavesOperationUntouched()
    {
        var description = CreateControllerDescription(nameof(DocsGeneratedController.GetWithoutMarker));
        Assert.SkipUnless(TryCreateContext(description, out var context), ContextUnavailableReason);

        var operation = new OpenApiOperation();

        await new DynamicApiXmlCommentsOperationTransformer()
            .TransformAsync(operation, context!, TestContext.Current.CancellationToken);

        Assert.Null(operation.Summary);
    }

    /// <summary>
    /// 原始方法反查不到时吞掉异常并原样放行，不打断文档生成
    /// </summary>
    [Fact]
    public async Task TransformAsync_WhenOriginalMethodUnresolvable_SwallowsAndLeavesOperationUntouched()
    {
        var description = CreateControllerDescription(nameof(DocsGeneratedController.GetUnresolvable));
        Assert.SkipUnless(TryCreateContext(description, out var context), ContextUnavailableReason);

        var operation = new OpenApiOperation();

        await new DynamicApiXmlCommentsOperationTransformer()
            .TransformAsync(operation, context!, TestContext.Current.CancellationToken);

        Assert.Null(operation.Summary);
    }

    /// <summary>
    /// 原始方法带 DynamicApi 描述时用它填充摘要
    /// </summary>
    [Fact]
    public async Task TransformAsync_WhenDynamicApiDescriptionPresent_FillsSummary()
    {
        var description = CreateControllerDescription(nameof(DocsGeneratedController.GetDescribed));
        Assert.SkipUnless(TryCreateContext(description, out var context), ContextUnavailableReason);

        var operation = new OpenApiOperation();

        await new DynamicApiXmlCommentsOperationTransformer()
            .TransformAsync(operation, context!, TestContext.Current.CancellationToken);

        Assert.Equal("取样方法的自定义描述", operation.Summary);
    }

    /// <summary>
    /// 摘要已有值时不被覆盖，先前的转换器结果优先
    /// </summary>
    [Fact]
    public async Task TransformAsync_WhenSummaryAlreadySet_DoesNotOverwrite()
    {
        var description = CreateControllerDescription(nameof(DocsGeneratedController.GetDescribed));
        Assert.SkipUnless(TryCreateContext(description, out var context), ContextUnavailableReason);

        var operation = new OpenApiOperation
        {
            Summary = "已有摘要"
        };

        await new DynamicApiXmlCommentsOperationTransformer()
            .TransformAsync(operation, context!, TestContext.Current.CancellationToken);

        Assert.Equal("已有摘要", operation.Summary);
    }

    /// <summary>
    /// 既没有自定义描述又没有 xml 文档时不写入摘要
    /// </summary>
    [Fact]
    public async Task TransformAsync_WhenNoDescriptionAndNoXmlDocument_LeavesSummaryNull()
    {
        var description = CreateControllerDescription(nameof(DocsGeneratedController.GetPlain));
        Assert.SkipUnless(TryCreateContext(description, out var context), ContextUnavailableReason);

        var operation = new OpenApiOperation();

        await new DynamicApiXmlCommentsOperationTransformer()
            .TransformAsync(operation, context!, TestContext.Current.CancellationToken);

        Assert.Null(operation.Summary);
    }

    /// <summary>
    /// 转换器是同步完成的，不应引入额外的异步调度开销
    /// </summary>
    [Fact]
    public void TransformAsync_CompletesSynchronously()
    {
        var description = CreateControllerDescription(nameof(DocsGeneratedController.GetDescribed));
        Assert.SkipUnless(TryCreateContext(description, out var context), ContextUnavailableReason);

        var task = new DynamicApiXmlCommentsOperationTransformer()
            .TransformAsync(new OpenApiOperation(), context!, TestContext.Current.CancellationToken);

        Assert.True(task.IsCompletedSuccessfully);
    }

    /// <summary>
    /// 构造指向动态生成控制器动作的 API 描述
    /// </summary>
    /// <param name="actionMethodName">控制器动作方法名</param>
    /// <returns>API 描述</returns>
    private static ApiDescription CreateControllerDescription(string actionMethodName)
    {
        return new ApiDescription
        {
            ActionDescriptor = new ControllerActionDescriptor
            {
                MethodInfo = typeof(DocsGeneratedController).GetMethod(actionMethodName)!
            }
        };
    }

    // 转换器只读取 context.Description，其余成员保持默认即可；
    // 走 GetUninitializedObject 是为了绕开 required init 成员在编译期的强制要求（其集合随包版本变化）。
    private static bool TryCreateContext(ApiDescription description, out OpenApiOperationTransformerContext? context)
    {
        try
        {
            var created = (OpenApiOperationTransformerContext)RuntimeHelpers.GetUninitializedObject(
                typeof(OpenApiOperationTransformerContext));

            var descriptionProperty = typeof(OpenApiOperationTransformerContext)
                .GetProperty(nameof(OpenApiOperationTransformerContext.Description));
            descriptionProperty!.SetValue(created, description);

            context = created;
            return true;
        }
        catch
        {
            context = null;
            return false;
        }
    }
}
