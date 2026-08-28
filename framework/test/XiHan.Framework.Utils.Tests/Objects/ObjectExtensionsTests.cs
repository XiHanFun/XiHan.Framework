// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Utils.Objects;

namespace XiHan.Framework.Utils.Tests.Objects;

/// <summary>
/// 对象扩展方法测试
/// </summary>
/// <remarks>
/// 判空只覆盖 null/字符串/DBNull/普通对象这几种语义明确的输入；
/// 集合输入的行为见交付报告的疑似缺陷段落。
/// </remarks>
public class ObjectExtensionsTests
{
    /// <summary>
    /// 引用类型转换成功，类型不符时抛无效转换异常
    /// </summary>
    [Fact]
    public void As_CastsReferenceTypeOrThrows()
    {
        object text = "abc";

        Assert.Equal("abc", text.As<string>());
        Assert.Throws<InvalidCastException>(() => text.As<Uri>());
    }

    /// <summary>
    /// 值类型转换走 Convert.ChangeType，不变文化解析
    /// </summary>
    [Fact]
    public void To_ConvertsValueTypes()
    {
        object text = "123";
        object number = 1;

        Assert.Equal(123, text.To<int>());
        Assert.Equal(123d, text.To<double>());
        Assert.True(number.To<bool>());
    }

    /// <summary>
    /// GUID 走类型转换器
    /// </summary>
    [Fact]
    public void To_ConvertsGuidThroughTypeConverter()
    {
        var expected = Guid.NewGuid();
        object text = expected.ToString();

        Assert.Equal(expected, text.To<Guid>());
    }

    /// <summary>
    /// 无法解析时抛格式异常
    /// </summary>
    [Fact]
    public void To_WhenNotParsable_Throws()
    {
        object text = "abc";

        Assert.Throws<FormatException>(() => text.To<int>());
    }

    /// <summary>
    /// 判断元素是否落在参数列表或序列中
    /// </summary>
    [Fact]
    public void IsIn_ChecksMembership()
    {
        var value = 3;
        IEnumerable<int> sequence = [1, 2, 3];

        Assert.True(value.IsIn(1, 2, 3));
        Assert.False(value.IsIn(4, 5));
        Assert.True(value.IsIn(sequence));
        Assert.False(value.IsIn([]));
    }

    /// <summary>
    /// 条件成立时用函数改写对象，否则原样返回
    /// </summary>
    [Fact]
    public void If_WithFunc_TransformsOnlyWhenConditionHolds()
    {
        var value = 1;

        Assert.Equal(2, value.If(true, x => x + 1));
        Assert.Equal(1, value.If(false, x => x + 1));
    }

    /// <summary>
    /// 条件成立时执行操作，但始终返回原对象
    /// </summary>
    [Fact]
    public void If_WithAction_AlwaysReturnsOriginalObject()
    {
        List<int> visited = [];
        var value = 1;

        var kept = value.If(true, x =>
        {
            visited.Add(x);
        });
        var skipped = value.If(false, x =>
        {
            visited.Add(x);
        });

        Assert.Equal(1, kept);
        Assert.Equal(1, skipped);
        Assert.Equal(new[] { 1 }, visited);
    }

    /// <summary>
    /// 取对象全名时返回调用点的实参表达式文本
    /// </summary>
    [Fact]
    public void GetObjectFullNameOf_ReturnsCallerArgumentExpression()
    {
        var sample = new Sample();

        // 用静态调用形式：实参表达式文本由编译器在调用点填入，这样最稳
        Assert.Equal(nameof(sample), ObjectExtensions.GetObjectFullNameOf(sample));
    }

    /// <summary>
    /// 实例为 null 时抛参数空异常
    /// </summary>
    [Fact]
    public void GetObjectFullNameOf_WhenInstanceIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ObjectExtensions.GetObjectFullNameOf(null!));
    }

    /// <summary>
    /// 字段存在性判断覆盖公有与非公有字段
    /// </summary>
    [Fact]
    public void IsObjectContainField_ChecksPublicAndNonPublicFields()
    {
        var sample = new Sample();

        Assert.True(sample.IsObjectContainField(nameof(Sample.PublicField)));
        Assert.False(sample.IsObjectContainField("NotExists"));
        Assert.False(sample.IsObjectContainField(string.Empty));
        Assert.False(ObjectExtensions.IsObjectContainField(null, nameof(Sample.PublicField)));
    }

    /// <summary>
    /// 取字段信息，不存在时返回 null
    /// </summary>
    [Fact]
    public void GetObjectField_ReturnsFieldInfoOrNull()
    {
        var sample = new Sample();

        Assert.NotNull(sample.GetObjectField(nameof(Sample.PublicField)));
        Assert.Null(sample.GetObjectField("NotExists"));
    }

    /// <summary>
    /// 实例为 null 或字段名为空白时抛异常
    /// </summary>
    [Fact]
    public void GetObjectField_WhenArgumentInvalid_Throws()
    {
        var sample = new Sample();

        Assert.Throws<ArgumentNullException>(() => ObjectExtensions.GetObjectField(null, "x"));
        Assert.Throws<ArgumentException>(() => sample.GetObjectField("   "));
    }

    /// <summary>
    /// 取全部字段包含自动属性的后备字段
    /// </summary>
    [Fact]
    public void GetObjectFields_IncludesBackingFields()
    {
        var sample = new Sample();

        var fields = sample.GetObjectFields();

        Assert.Contains(fields, f => f.Name == nameof(Sample.PublicField));
        Assert.True(fields.Length > 1);
        Assert.Throws<ArgumentNullException>(() => ObjectExtensions.GetObjectFields(null));
    }

    /// <summary>
    /// 属性存在性判断覆盖公有与非公有属性
    /// </summary>
    [Fact]
    public void IsContainObjectProperty_ChecksPublicAndNonPublicProperties()
    {
        var sample = new Sample();

        Assert.True(sample.IsContainObjectProperty(nameof(Sample.PublicProperty)));
        Assert.True(sample.IsContainObjectProperty("PrivateProperty"));
        Assert.False(sample.IsContainObjectProperty("NotExists"));
        Assert.False(sample.IsContainObjectProperty(string.Empty));
        Assert.False(ObjectExtensions.IsContainObjectProperty(null, nameof(Sample.PublicProperty)));
    }

    /// <summary>
    /// 取属性信息，不存在时返回 null
    /// </summary>
    [Fact]
    public void GetObjectProperty_ReturnsPropertyInfoOrNull()
    {
        var sample = new Sample();

        Assert.NotNull(sample.GetObjectProperty(nameof(Sample.PublicProperty)));
        Assert.Null(sample.GetObjectProperty("NotExists"));
        Assert.Throws<ArgumentNullException>(() => ObjectExtensions.GetObjectProperty(null, "x"));
        Assert.Throws<ArgumentException>(() => sample.GetObjectProperty("   "));
    }

    /// <summary>
    /// 取全部属性同时包含公有与非公有
    /// </summary>
    [Fact]
    public void GetObjectProperties_IncludesNonPublic()
    {
        var sample = new Sample();

        var properties = sample.GetObjectProperties();

        Assert.Contains(properties, p => p.Name == nameof(Sample.PublicProperty));
        Assert.Contains(properties, p => p.Name == "PrivateProperty");
        Assert.Throws<ArgumentNullException>(() => ObjectExtensions.GetObjectProperties(null));
    }

    /// <summary>
    /// 判空覆盖 null、空串、纯空白与 DBNull
    /// </summary>
    [Fact]
    public void IsNullOrEmpty_DetectsNullBlankStringAndDbNull()
    {
        object? nothing = null;
        object zero = 0;

        Assert.True(nothing.IsNullOrEmpty());
        Assert.True(string.Empty.IsNullOrEmpty());
        Assert.True("   ".IsNullOrEmpty());
        Assert.True(DBNull.Value.IsNullOrEmpty());
        Assert.False("a".IsNullOrEmpty());
        Assert.False(new Sample().IsNullOrEmpty());
        Assert.False(zero.IsNullOrEmpty());
    }

    /// <summary>
    /// 测试用承载类型：混合公有字段、公有属性与非公有属性
    /// </summary>
    private sealed class Sample
    {
        public int PublicField = 1;

        public string PublicProperty { get; set; } = "x";

        private string PrivateProperty { get; set; } = "y";

        /// <summary>
        /// 读取非公有属性，避免它被判定为完全未使用
        /// </summary>
        public string ReadPrivateProperty()
        {
            return PrivateProperty;
        }
    }
}
