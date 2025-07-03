# 枚举工具使用指南

## 概述

XiHan.Framework.Utils.Enums 提供了一套完整的枚举操作工具，包括扩展方法、辅助类、验证器、转换器等功能。

## 核心组件

### 1. 枚举扩展方法 (EnumExtensions)

提供针对枚举实例的扩展方法：

```csharp
public enum UserStatus
{
    [Description("活跃用户")]
    [EnumTheme(ThemeType.Success)]
    Active = 1,

    [Description("非活跃用户")]
    [EnumTheme(ThemeType.Warning)]
    Inactive = 2,

    [Description("已禁用")]
    [EnumTheme(ThemeType.Error)]
    Disabled = 3
}

// 使用示例
var status = UserStatus.Active;
var value = status.GetValue();        // 1
var name = status.GetName();          // "Active"
var description = status.GetDescription(); // "活跃用户"
var theme = status.GetTheme();        // ThemeColor { Theme = "success", Color = "#19BE6B" }

// 枚举导航
var nextStatus = status.GetNext();    // UserStatus.Inactive
var prevStatus = status.GetPrevious(); // UserStatus.Disabled (循环)
```

### 2. 枚举辅助类 (EnumHelper)

提供静态方法和缓存功能：

```csharp
// 基本操作
var allValues = EnumHelper.GetValues<UserStatus>();
var allNames = EnumHelper.GetNames<UserStatus>();
var count = EnumHelper.GetCount<UserStatus>();

// 验证
bool isValid = EnumHelper.IsDefined<UserStatus>(1);
bool isNameValid = EnumHelper.IsDefined<UserStatus>("Active");
bool isDescValid = EnumHelper.IsDescriptionDefined<UserStatus>("活跃用户");

// 获取枚举
var statusByValue = EnumHelper.GetEnum<UserStatus>(1);
var statusByName = EnumHelper.GetEnum<UserStatus>("Active");
var statusByDesc = EnumHelper.GetEnumByDescription<UserStatus>("活跃用户");

// 安全获取
if (EnumHelper.TryGetEnum<UserStatus>("Unknown", out var result))
{
    // 获取成功
}

// 获取信息
var enumInfos = EnumHelper.GetEnumInfos<UserStatus>();
var nameValueDict = EnumHelper.GetNameValueDict<UserStatus>();
var valueDescDict = EnumHelper.GetValueDescriptionDict<UserStatus>();
```

### 3. 泛型枚举辅助类 (EnumHelper<T>)

提供高性能的泛型操作：

```csharp
// 静态缓存，性能更好
var values = EnumHelper<UserStatus>.GetValues();
var names = EnumHelper<UserStatus>.GetNames();
var count = EnumHelper<UserStatus>.Count;
var minValue = EnumHelper<UserStatus>.MinValue;
var maxValue = EnumHelper<UserStatus>.MaxValue;

// 验证
bool isValid = EnumHelper<UserStatus>.IsDefined(1);
bool isNameValid = EnumHelper<UserStatus>.IsDefined("Active");

// 随机获取
var randomStatus = EnumHelper<UserStatus>.GetRandomValue();
```

### 4. 枚举验证器 (EnumValidator)

提供完整的验证功能：

```csharp
// 基本验证
bool isValid = EnumValidator.IsValid<UserStatus>(UserStatus.Active);
bool isIntValid = EnumValidator.IsValid<UserStatus>(1);
bool isNameValid = EnumValidator.IsValid<UserStatus>("Active");

// 验证结果
var validationResult = EnumValidator.IsValid<UserStatus>(someValue);

// 范围验证
bool inRange = EnumValidator.IsInRange(UserStatus.Active, UserStatus.Active, UserStatus.Inactive);
bool inValidValues = EnumValidator.IsInValidValues(UserStatus.Active, UserStatus.Active, UserStatus.Inactive);

// 抛出异常验证
EnumValidator.ValidateOrThrow<UserStatus>(1, nameof(someParameter));

// 批量验证
var results = EnumValidator.ValidateMany<UserStatus>(new[] { 1, 2, 3, 999 });
```

### 5. 枚举转换器 (EnumConverter)

提供各种转换功能：

```csharp
// 基本转换
var status = EnumConverter.ToEnum<UserStatus>(1);
var statusFromString = EnumConverter.ToEnum<UserStatus>("Active");

// 描述转换
var statusFromDesc = EnumConverter.FromDescription<UserStatus>("活跃用户");

// 集合转换
var statusList = EnumConverter.ToEnumCollection<UserStatus>(new object[] { 1, "Active", 2 });

// 字典转换
var nameValueDict = EnumConverter.ToNameValueDictionary<UserStatus>();
var valueDescDict = EnumConverter.ToValueDescriptionDictionary<UserStatus>();
var fullInfoDict = EnumConverter.ToFullInfoDictionary<UserStatus>();

// 选择项转换
var selectItems = EnumConverter.ToSelectItems<UserStatus>();

// 标志枚举转换
[Flags]
public enum Permission
{
    Read = 1,
    Write = 2,
    Execute = 4
}

var permission = Permission.Read | Permission.Write;
var flags = EnumConverter.ToFlagArray(permission); // [Permission.Read, Permission.Write]
var combined = EnumConverter.FromFlagArray(flags); // Permission.Read | Permission.Write

// 字符串格式化
var nameString = EnumConverter.ToString(UserStatus.Active, "Name");
var valueString = EnumConverter.ToString(UserStatus.Active, "Value");
var descString = EnumConverter.ToString(UserStatus.Active, "Description");
```

### 6. 主题支持

使用主题属性美化枚举：

```csharp
public enum Priority
{
    [Description("低优先级")]
    [EnumTheme(ThemeType.Secondary)]
    Low = 1,

    [Description("正常优先级")]
    [EnumTheme(ThemeType.Primary)]
    Normal = 2,

    [Description("高优先级")]
    [EnumTheme(ThemeType.Warning)]
    High = 3,

    [Description("紧急")]
    [EnumTheme(ThemeType.Error)]
    Urgent = 4,

    [Description("自定义主题")]
    [EnumTheme("custom", "#FF6B6B", "fas fa-star")]
    Custom = 5
}

// 使用主题
var priority = Priority.High;
var theme = priority.GetTheme();
var cssClass = theme.GetCssClass();     // "theme-warning"
var inlineStyle = theme.ToInlineStyle(); // "color: #FF9900"
var cssVariables = theme.ToCssVariables(); // {"--theme-color": "#FF9900"}
```

### 7. 状态和优先级主题

使用专门的状态和优先级主题：

```csharp
public enum TaskStatus
{
    [Description("进行中")]
    [EnumStatusTheme(StatusType.Active)]
    InProgress = 1,

    [Description("已完成")]
    [EnumStatusTheme(StatusType.Processing)]
    Completed = 2,

    [Description("已取消")]
    [EnumStatusTheme(StatusType.Error)]
    Cancelled = 3
}

public enum TaskPriority
{
    [Description("低")]
    [EnumPriorityTheme(PriorityType.Low)]
    Low = 1,

    [Description("高")]
    [EnumPriorityTheme(PriorityType.High)]
    High = 2
}
```

### 8. JSON 序列化支持

使用 JSON 转换器：

```csharp
public class UserDto
{
    [JsonConverter(typeof(EnumJsonConverter<UserStatus>))]
    public UserStatus Status { get; set; }

    [JsonConverter(typeof(EnumJsonConverter<Priority>))]
    public Priority Priority { get; set; }
}

// 配置转换模式
services.AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new EnumJsonConverter<UserStatus>(EnumConvertMode.Description));
});
```

### 9. 类型转换器

注册类型转换器：

```csharp
TypeDescriptor.AddAttributes(typeof(UserStatus), new TypeConverterAttribute(typeof(EnumTypeConverter<UserStatus>)));

// 现在可以自动转换
var status = (UserStatus)TypeDescriptor.GetConverter(typeof(UserStatus)).ConvertFrom("Active");
```

### 10. 验证特性

使用验证特性：

```csharp
public class UserModel
{
    [EnumValidation<UserStatus>]
    public UserStatus Status { get; set; }

    [EnumValidation<Priority>(AllowNull = true)]
    public Priority? Priority { get; set; }
}
```

## 性能优化

1. **缓存机制**: 所有反射操作都有缓存支持
2. **泛型版本**: `EnumHelper<T>` 提供更好的性能
3. **Lazy 初始化**: 静态缓存使用 Lazy 模式
4. **并发安全**: 使用 ConcurrentDictionary 确保线程安全

## 最佳实践

1. **使用描述特性**: 为枚举值添加用户友好的描述
2. **使用主题特性**: 为 UI 显示添加主题支持
3. **优先使用泛型版本**: `EnumHelper<T>` 性能更好
4. **验证输入**: 使用 `EnumValidator` 验证外部输入
5. **统一转换**: 使用 `EnumConverter` 进行类型转换

## 示例项目

```csharp
// 定义枚举
public enum OrderStatus
{
    [Description("等待付款")]
    [EnumTheme(ThemeType.Warning)]
    WaitingPayment = 1,

    [Description("已付款")]
    [EnumTheme(ThemeType.Primary)]
    Paid = 2,

    [Description("已发货")]
    [EnumTheme(ThemeType.Info)]
    Shipped = 3,

    [Description("已完成")]
    [EnumTheme(ThemeType.Success)]
    Completed = 4,

    [Description("已取消")]
    [EnumTheme(ThemeType.Error)]
    Cancelled = 5
}

// 使用示例
public class OrderService
{
    public List<SelectItem> GetOrderStatusOptions()
    {
        return EnumConverter.ToSelectItems<OrderStatus>().ToList();
    }

    public bool IsValidStatus(string status)
    {
        return EnumValidator.IsValid<OrderStatus>(status);
    }

    public OrderStatus GetNextStatus(OrderStatus currentStatus)
    {
        return currentStatus.GetNext(false); // 不循环
    }

    public string GetStatusDisplay(OrderStatus status)
    {
        return status.GetDescription();
    }

    public ThemeColor GetStatusTheme(OrderStatus status)
    {
        return status.GetTheme();
    }
}
```

这套枚举工具提供了完整的枚举操作功能，既保持了高性能，又提供了丰富的功能特性，可以满足各种业务场景的需求。
