// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace XiHan.Framework.Validation.Abstractions.Tests;

/// <summary>
/// 验证错误承载接口的契约测试
/// </summary>
/// <remarks>
/// 接口本身没有实现，能测的只有它对外承诺的形状，而这个形状是跨模块的硬契约：
/// 属性只读（实现方必须在构造期就把集合准备好）、元素类型是 <see cref="ValidationResult"/>、
/// 集合本身可写（调用方允许在拿到实例后继续追加明细）。这三点一旦漂移，
/// 下游按接口取错误明细的代码会静默拿到空集合，所以用反射把它们锁死。
/// </remarks>
public class IHasValidationErrorsTests
{
    /// <summary>
    /// 接口只暴露一个只读的 ValidationErrors 属性
    /// </summary>
    [Fact]
    public void Interface_ExposesSingleReadOnlyValidationErrorsProperty()
    {
        var properties = typeof(IHasValidationErrors).GetProperties();

        var property = Assert.Single(properties);
        Assert.Equal(nameof(IHasValidationErrors.ValidationErrors), property.Name);
        Assert.Equal(typeof(IList<ValidationResult>), property.PropertyType);
        Assert.NotNull(property.GetMethod);

        // 只读是刻意设计：实现方不得在事后整体替换集合引用，否则已持有引用的调用方会看不到后续变更
        Assert.Null(property.SetMethod);
    }

    /// <summary>
    /// 接口除了 ValidationErrors 属性外不声明任何方法
    /// </summary>
    [Fact]
    public void Interface_DeclaresNoAdditionalMethods()
    {
        var methods = typeof(IHasValidationErrors)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => !method.IsSpecialName)
            .ToArray();

        Assert.Empty(methods);
    }

    /// <summary>
    /// 实现方通过接口暴露的必须是底层集合本身，而不是防御性副本
    /// </summary>
    [Fact]
    public void ValidationErrors_ThroughInterface_ReturnsUnderlyingListInstance()
    {
        IList<ValidationResult> errors =
        [
            new ValidationResult("用户名不能为空", ["UserName"])
        ];

        IHasValidationErrors sut = new ValidationErrorsHolder(errors);

        Assert.Same(errors, sut.ValidationErrors);
        Assert.Single(sut.ValidationErrors);
    }

    /// <summary>
    /// 通过接口拿到的集合可以继续追加验证错误
    /// </summary>
    [Fact]
    public void ValidationErrors_ThroughInterface_IsMutable()
    {
        IHasValidationErrors sut = new ValidationErrorsHolder([]);

        Assert.Empty(sut.ValidationErrors);

        sut.ValidationErrors.Add(new ValidationResult("邮箱格式不正确", ["Email"]));

        var error = Assert.Single(sut.ValidationErrors);
        Assert.Equal("邮箱格式不正确", error.ErrorMessage);
    }

    /// <summary>
    /// 框架内置的验证异常必须是该接口的一个实现
    /// </summary>
    [Fact]
    public void XiHanValidationException_ImplementsInterface()
    {
        var exception = new XiHanValidationException();

        Assert.IsAssignableFrom<IHasValidationErrors>(exception);

        // 编译期隐式转换加运行期同引用，共同证明异常没有把集合再包一层
        IHasValidationErrors sut = exception;
        Assert.Same(exception.ValidationErrors, sut.ValidationErrors);
    }

    /// <summary>
    /// 最小实现：只把构造期传入的集合原样暴露出去
    /// </summary>
    private sealed class ValidationErrorsHolder : IHasValidationErrors
    {
        public ValidationErrorsHolder(IList<ValidationResult> validationErrors)
        {
            ValidationErrors = validationErrors;
        }

        public IList<ValidationResult> ValidationErrors { get; }
    }
}
