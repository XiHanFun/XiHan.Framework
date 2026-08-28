// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;
using XiHan.Framework.ObjectMapping.Extensions;
using XiHan.Framework.ObjectMapping.Extensions.Data;
using XiHan.Framework.ObjectMapping.Tests.Fakes;
using XiHan.Framework.Validation.Abstractions;

namespace XiHan.Framework.ObjectMapping.Tests;

/// <summary>
/// 可扩展对象验证器测试
/// </summary>
/// <remarks>
/// 验证器把三类来源的错误聚合到同一个 ValidationResult 列表：
/// 属性上的 ValidationAttribute、属性级自定义验证委托、对象级自定义验证委托。
/// 两个入口的覆盖范围并不相同：整对象重载会跑对象级验证器，单属性重载不会——
/// 这正是 SetProperty(validate: true) 只做「本属性合法性」检查而不做整对象校验的原因。
/// 验证器读取的是 ObjectExtensionManager.Instance 这个进程级单例，所以每个用例都用
/// 自己专属的标记类型注册，互不干扰。
/// </remarks>
public class ExtensibleObjectValidatorTests
{
    /// <summary>
    /// 类型未注册扩展信息时视为合法，不产生任何错误
    /// </summary>
    [Fact]
    public void GetValidationErrors_WhenTypeNotRegistered_ReturnsNoError()
    {
        var target = new UnregisteredTarget();

        Assert.Empty(ExtensibleObjectValidator.GetValidationErrors(target));
        Assert.True(ExtensibleObjectValidator.IsValid(target));
    }

    /// <summary>
    /// 非空基元类型属性缺值时命中自动补上的 Required 特性
    /// </summary>
    [Fact]
    public void GetValidationErrors_WhenRequiredPropertyMissing_ReportsMemberError()
    {
        ObjectExtensionManager.Instance.AddOrUpdateProperty(typeof(RequiredTarget), typeof(int), "Age");
        var target = new RequiredTarget();

        var errors = ExtensibleObjectValidator.GetValidationErrors(target);

        var error = Assert.Single(errors);
        Assert.Contains("Age", error.MemberNames);
        Assert.False(ExtensibleObjectValidator.IsValid(target));
    }

    /// <summary>
    /// 非空基元类型属性有值时通过校验
    /// </summary>
    [Fact]
    public void GetValidationErrors_WhenRequiredPropertyPresent_ReturnsNoError()
    {
        ObjectExtensionManager.Instance.AddOrUpdateProperty(typeof(RequiredFilledTarget), typeof(int), "Age");
        var target = new RequiredFilledTarget();
        target.ExtraProperties["Age"] = 18;

        Assert.Empty(ExtensibleObjectValidator.GetValidationErrors(target));
        Assert.True(ExtensibleObjectValidator.IsValid(target));
    }

    /// <summary>
    /// 手工追加的验证特性同样参与校验
    /// </summary>
    [Fact]
    public void GetValidationErrors_WhenCustomAttributeViolated_ReportsError()
    {
        ObjectExtensionManager.Instance.AddOrUpdateProperty(typeof(StringLengthTarget), typeof(string), "Title", property =>
        {
            property.Attributes.Add(new StringLengthAttribute(3));
        });
        var target = new StringLengthTarget();

        target.ExtraProperties["Title"] = "abc";
        Assert.Empty(ExtensibleObjectValidator.GetValidationErrors(target));

        target.ExtraProperties["Title"] = "abcd";
        Assert.Single(ExtensibleObjectValidator.GetValidationErrors(target));
    }

    /// <summary>
    /// 属性级自定义验证委托可以往错误集合里追加结果，并拿到完整上下文
    /// </summary>
    [Fact]
    public void GetValidationErrors_RunsCustomPropertyValidatorWithFullContext()
    {
        ObjectExtensionPropertyValidationContext? captured = null;
        ObjectExtensionManager.Instance.AddOrUpdateProperty(typeof(PropertyValidatorTarget), typeof(string), "Title", property =>
        {
            property.Validators.Add(context =>
            {
                captured = context;
                context.ValidationErrors.Add(new ValidationResult("标题不合法"));
            });
        });
        var target = new PropertyValidatorTarget();
        target.ExtraProperties["Title"] = "值";

        var errors = ExtensibleObjectValidator.GetValidationErrors(target);

        Assert.Single(errors);
        Assert.Equal("标题不合法", errors[0].ErrorMessage);
        Assert.NotNull(captured);
        Assert.Same(target, captured.ValidatingObject);
        Assert.Equal("值", captured.Value);
        Assert.Equal("Title", captured.ExtensionPropertyInfo.Name);
        Assert.Same(errors, captured.ValidationErrors);
        Assert.NotNull(captured.ValidationContext);
        Assert.Same(captured.ValidationContext, captured.ServiceProvider);
    }

    /// <summary>
    /// 对象级自定义验证委托在整对象校验时执行，并拿到完整上下文
    /// </summary>
    [Fact]
    public void GetValidationErrors_RunsCustomObjectValidatorWithFullContext()
    {
        ObjectExtensionValidationContext? captured = null;
        ObjectExtensionManager.Instance.AddOrUpdate(typeof(ObjectValidatorTarget), info =>
        {
            info.Validators.Add(context =>
            {
                captured = context;
                context.ValidationErrors.Add(new ValidationResult("对象不合法"));
            });
        });
        var target = new ObjectValidatorTarget();

        var errors = ExtensibleObjectValidator.GetValidationErrors(target);

        Assert.Single(errors);
        Assert.Equal("对象不合法", errors[0].ErrorMessage);
        Assert.NotNull(captured);
        Assert.Same(target, captured.ValidatingObject);
        Assert.Equal(typeof(ObjectValidatorTarget), captured.ObjectExtensionInfo.Type);
        Assert.Same(errors, captured.ValidationErrors);
        Assert.Same(captured.ValidationContext, captured.ServiceProvider);
    }

    /// <summary>
    /// 单属性重载只校验该属性，不触发对象级验证委托
    /// </summary>
    [Fact]
    public void GetValidationErrors_ForSingleProperty_SkipsObjectLevelValidators()
    {
        ObjectExtensionManager.Instance.AddOrUpdate(typeof(SinglePropertyScopeTarget), info =>
        {
            info.AddOrUpdateProperty<int>("Age");
            info.Validators.Add(context => context.ValidationErrors.Add(new ValidationResult("对象级错误")));
        });
        var target = new SinglePropertyScopeTarget();

        var propertyErrors = ExtensibleObjectValidator.GetValidationErrors(target, "Age", 18);
        var objectErrors = ExtensibleObjectValidator.GetValidationErrors(target);

        Assert.Empty(propertyErrors);
        Assert.Contains(objectErrors, error => error.ErrorMessage == "对象级错误");
    }

    /// <summary>
    /// 单属性重载校验的是传入的候选值，而不是对象里已存的值
    /// </summary>
    [Fact]
    public void GetValidationErrors_ForSingleProperty_ValidatesGivenCandidateValue()
    {
        ObjectExtensionManager.Instance.AddOrUpdateProperty(typeof(CandidateValueTarget), typeof(int), "Age");
        var target = new CandidateValueTarget();
        target.ExtraProperties["Age"] = 18;

        Assert.Empty(ExtensibleObjectValidator.GetValidationErrors(target, "Age", 20));
        Assert.Single(ExtensibleObjectValidator.GetValidationErrors(target, "Age", null));
        Assert.True(ExtensibleObjectValidator.IsValid(target, "Age", 20));
        Assert.False(ExtensibleObjectValidator.IsValid(target, "Age", null));
    }

    /// <summary>
    /// 单属性重载遇到未定义的属性名时静默放行
    /// </summary>
    [Fact]
    public void GetValidationErrors_ForUnknownProperty_ReturnsNoError()
    {
        ObjectExtensionManager.Instance.AddOrUpdateProperty(typeof(UnknownPropertyTarget), typeof(int), "Age");
        var target = new UnknownPropertyTarget();

        Assert.Empty(ExtensibleObjectValidator.GetValidationErrors(target, "NotDefined", null));
    }

    /// <summary>
    /// AddValidationErrors 是往调用方的集合里追加，不会清空已有错误
    /// </summary>
    [Fact]
    public void AddValidationErrors_AppendsToExistingCollection()
    {
        ObjectExtensionManager.Instance.AddOrUpdateProperty(typeof(AppendErrorsTarget), typeof(int), "Age");
        var target = new AppendErrorsTarget();
        var errors = new List<ValidationResult>
        {
            new("先前的错误")
        };

        ExtensibleObjectValidator.AddValidationErrors(target, errors);

        Assert.Equal(2, errors.Count);
        Assert.Equal("先前的错误", errors[0].ErrorMessage);
    }

    /// <summary>
    /// 校验失败时 GuardValue 抛出携带全部错误的验证异常
    /// </summary>
    [Fact]
    public void GuardValue_WhenValueInvalid_ThrowsValidationExceptionCarryingErrors()
    {
        ObjectExtensionManager.Instance.AddOrUpdateProperty(typeof(GuardValueTarget), typeof(int), "Age");
        var target = new GuardValueTarget();

        var exception = Assert.Throws<XiHanValidationException>(
            () => ExtensibleObjectValidator.GuardValue(target, "Age", null));

        Assert.Single(exception.ValidationErrors);
        Assert.Contains("Age", exception.ValidationErrors[0].MemberNames);
    }

    /// <summary>
    /// 校验通过时 GuardValue 与 CheckValue 都不抛异常
    /// </summary>
    [Fact]
    public void GuardValueAndCheckValue_WhenValueValid_DoNotThrow()
    {
        ObjectExtensionManager.Instance.AddOrUpdateProperty(typeof(CheckValueTarget), typeof(int), "Age");
        var target = new CheckValueTarget();

        ExtensibleObjectValidator.GuardValue(target, "Age", 18);
        ExtensibleObjectValidator.CheckValue(target, "Age", 18);
    }

    /// <summary>
    /// CheckValue 是 GuardValue 的别名，失败行为一致
    /// </summary>
    [Fact]
    public void CheckValue_WhenValueInvalid_ThrowsSameValidationException()
    {
        ObjectExtensionManager.Instance.AddOrUpdateProperty(typeof(CheckValueInvalidTarget), typeof(int), "Age");
        var target = new CheckValueInvalidTarget();

        Assert.Throws<XiHanValidationException>(
            () => ExtensibleObjectValidator.CheckValue(target, "Age", null));
    }

    /// <summary>
    /// SetProperty 默认开启校验，非法值会被拦下且不写入字典
    /// </summary>
    [Fact]
    public void SetProperty_WhenValidationEnabledAndValueInvalid_ThrowsAndLeavesDictionaryUntouched()
    {
        ObjectExtensionManager.Instance.AddOrUpdateProperty(typeof(SetPropertyValidateTarget), typeof(int), "Age");
        var target = new SetPropertyValidateTarget();

        Assert.Throws<XiHanValidationException>(() => target.SetProperty("Age", null));
        Assert.False(target.ExtraProperties.ContainsKey("Age"));
    }

    /// <summary>
    /// 关闭校验后非法值可以直接写入
    /// </summary>
    [Fact]
    public void SetProperty_WhenValidationDisabled_WritesInvalidValue()
    {
        ObjectExtensionManager.Instance.AddOrUpdateProperty(typeof(SetPropertySkipValidateTarget), typeof(int), "Age");
        var target = new SetPropertySkipValidateTarget();

        target.SetProperty("Age", null, validate: false);

        Assert.True(target.ExtraProperties.ContainsKey("Age"));
        Assert.Null(target.ExtraProperties["Age"]);
    }

    /// <summary>
    /// 可扩展对象为 null 时抛出 ArgumentNullException
    /// </summary>
    [Fact]
    public void AddValidationErrors_WhenExtensibleObjectNull_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () => ExtensibleObjectValidator.AddValidationErrors(null!, []));

        Assert.Equal("extensibleObject", exception.ParamName);
    }

    /// <summary>
    /// 错误集合为 null 时抛出 ArgumentNullException
    /// </summary>
    [Fact]
    public void AddValidationErrors_WhenErrorCollectionNull_ThrowsArgumentNullException()
    {
        var target = new NullArgumentTarget();

        var exception = Assert.Throws<ArgumentNullException>(
            () => ExtensibleObjectValidator.AddValidationErrors(target, null!));

        Assert.Equal("validationErrors", exception.ParamName);
    }

    /// <summary>
    /// 单属性重载的属性名为空白时抛出 ArgumentException
    /// </summary>
    [Fact]
    public void AddValidationErrors_WhenPropertyNameBlank_ThrowsArgumentException()
    {
        var target = new NullArgumentTarget();

        var exception = Assert.Throws<ArgumentException>(
            () => ExtensibleObjectValidator.AddValidationErrors(target, [], "   ", null));

        Assert.Equal("propertyName", exception.ParamName);
    }

    /// <summary>
    /// 外部传入的验证上下文会被沿用，不会另建一个
    /// </summary>
    [Fact]
    public void GetValidationErrors_WhenValidationContextGiven_ReusesIt()
    {
        ValidationContext? observed = null;
        ObjectExtensionManager.Instance.AddOrUpdate(typeof(ContextReuseTarget), info =>
        {
            info.Validators.Add(context => observed = context.ValidationContext);
        });
        var target = new ContextReuseTarget();
        var validationContext = new ValidationContext(target);

        ExtensibleObjectValidator.GetValidationErrors(target, validationContext);

        Assert.Same(validationContext, observed);
    }

    /// <summary>
    /// 未注册扩展信息的标记类型
    /// </summary>
    private sealed class UnregisteredTarget : FakeExtensibleObject
    {
    }

    /// <summary>
    /// Required 缺值用例标记类型
    /// </summary>
    private sealed class RequiredTarget : FakeExtensibleObject
    {
    }

    /// <summary>
    /// Required 有值用例标记类型
    /// </summary>
    private sealed class RequiredFilledTarget : FakeExtensibleObject
    {
    }

    /// <summary>
    /// 自定义验证特性用例标记类型
    /// </summary>
    private sealed class StringLengthTarget : FakeExtensibleObject
    {
    }

    /// <summary>
    /// 属性级验证委托用例标记类型
    /// </summary>
    private sealed class PropertyValidatorTarget : FakeExtensibleObject
    {
    }

    /// <summary>
    /// 对象级验证委托用例标记类型
    /// </summary>
    private sealed class ObjectValidatorTarget : FakeExtensibleObject
    {
    }

    /// <summary>
    /// 单属性校验范围用例标记类型
    /// </summary>
    private sealed class SinglePropertyScopeTarget : FakeExtensibleObject
    {
    }

    /// <summary>
    /// 候选值校验用例标记类型
    /// </summary>
    private sealed class CandidateValueTarget : FakeExtensibleObject
    {
    }

    /// <summary>
    /// 未定义属性名用例标记类型
    /// </summary>
    private sealed class UnknownPropertyTarget : FakeExtensibleObject
    {
    }

    /// <summary>
    /// 错误集合追加用例标记类型
    /// </summary>
    private sealed class AppendErrorsTarget : FakeExtensibleObject
    {
    }

    /// <summary>
    /// GuardValue 用例标记类型
    /// </summary>
    private sealed class GuardValueTarget : FakeExtensibleObject
    {
    }

    /// <summary>
    /// CheckValue 通过用例标记类型
    /// </summary>
    private sealed class CheckValueTarget : FakeExtensibleObject
    {
    }

    /// <summary>
    /// CheckValue 失败用例标记类型
    /// </summary>
    private sealed class CheckValueInvalidTarget : FakeExtensibleObject
    {
    }

    /// <summary>
    /// SetProperty 开启校验用例标记类型
    /// </summary>
    private sealed class SetPropertyValidateTarget : FakeExtensibleObject
    {
    }

    /// <summary>
    /// SetProperty 关闭校验用例标记类型
    /// </summary>
    private sealed class SetPropertySkipValidateTarget : FakeExtensibleObject
    {
    }

    /// <summary>
    /// 空引用参数用例标记类型
    /// </summary>
    private sealed class NullArgumentTarget : FakeExtensibleObject
    {
    }

    /// <summary>
    /// 验证上下文复用用例标记类型
    /// </summary>
    private sealed class ContextReuseTarget : FakeExtensibleObject
    {
    }
}
