# 动态 JSON 操作指南

## 概述

`XiHan.Framework.Utils.Text.Json.Dynamic` 命名空间提供了一套强大的动态 JSON 操作类，类似于 Newtonsoft.Json 的 `JObject`、`JArray` 和 `JValue` 的体验，但基于 `System.Text.Json`。

## 主要类

- `DynamicJsonObject` - 动态 JSON 对象，类似 `JObject`
- `DynamicJsonArray` - 动态 JSON 数组，类似 `JArray`
- `DynamicJsonValue` - 动态 JSON 值，类似 `JValue`
- `DynamicJsonProperty` - 动态 JSON 属性，类似 `JProperty`

## 使用方式

### 1. 基本用法

```csharp
// 原来的方式（会报错）
var resultObj = (DynamicJsonObject)response.Data;
var status = (string)resultObj.status; // 编译错误

// 推荐的方式 1：使用 dynamic 关键字
dynamic resultObj = (DynamicJsonObject)response.Data;
var status = (string)resultObj.status; // ✅ 正确

// 推荐的方式 2：使用索引器
var resultObj = (DynamicJsonObject)response.Data;
var status = (string)resultObj["status"]; // ✅ 正确

// 推荐的方式 3：使用强类型方法
var resultObj = (DynamicJsonObject)response.Data;
var status = resultObj.Get("status").AsString(); // ✅ 正确
```

### 2. 强类型访问方法

```csharp
var jsonObj = DynamicJsonObject.FromJson(jsonString);

// 使用 Get 方法访问属性
var status = jsonObj.Get("status").AsString();
var count = jsonObj.Get("count").AsInt();
var isValid = jsonObj.Get("isValid").AsBool();
var price = jsonObj.Get("price").AsDecimal();
var createdAt = jsonObj.Get("createdAt").AsDateTime();

// 使用 Set 方法设置属性（支持链式调用）
jsonObj.Set("status", "success")
       .Set("count", 10)
       .Set("isValid", true);
```

### 3. 扩展方法

```csharp
using XiHan.Framework.Utils.Text.Json.Dynamic;

var jsonObj = DynamicJsonObject.FromJson(jsonString);

// 批量获取属性值
var values = jsonObj.GetValues("prop1", "prop2", "prop3");

// 批量设置属性值
jsonObj.SetValues(new { prop1 = "value1", prop2 = "value2" });

// 检查是否包含属性
var hasAll = jsonObj.HasAll("prop1", "prop2"); // 是否包含所有属性
var hasAny = jsonObj.HasAny("prop1", "prop2"); // 是否包含任一属性

// 获取第一个非空属性值
var firstValue = jsonObj.GetFirstNonNull("prop1", "prop2", "prop3");

// 安全获取嵌套属性值
var name = jsonObj.SafeGet<string>("user", "profile", "name");

// 转换为强类型对象
var user = jsonObj.ToObject<User>();

// 从强类型对象创建 DynamicJsonObject
var newJsonObj = user.ToDynamicJson();
```

### 4. 动态访问

```csharp
// 方式 1：直接使用 dynamic
dynamic obj = DynamicJsonObject.FromJson(jsonString);
var status = obj.status;
var count = obj.count;
obj.newProperty = "new value";

// 方式 2：使用 AsDynamic() 方法
var jsonObj = DynamicJsonObject.FromJson(jsonString);
dynamic obj = jsonObj.AsDynamic();
var status = obj.status;

// 方式 3：使用 AsExpando() 方法
var jsonObj = DynamicJsonObject.FromJson(jsonString);
dynamic obj = jsonObj.AsExpando();
var status = obj.status;
```

### 5. 深度路径访问

```csharp
var jsonObj = DynamicJsonObject.FromJson(jsonString);

// 使用点号路径访问嵌套属性
var userName = jsonObj.SelectToken("user.profile.name");
var firstItemId = jsonObj.SelectToken("items[0].id");

// 带类型转换的路径访问
var userName = jsonObj.SelectToken<string>("user.profile.name");
var firstItemId = jsonObj.SelectToken<int>("items[0].id");
```

### 6. 数组操作

```csharp
var jsonArray = DynamicJsonArray.FromJson(jsonArrayString);

// 索引访问
var firstItem = jsonArray[0];
var secondItem = jsonArray[1];

// 动态访问
dynamic arr = jsonArray.AsDynamic();
var firstItem = arr[0];

// 类型转换
var firstItemAsString = jsonArray.GetValue<string>(0);
```

### 7. 类型转换

```csharp
var jsonValue = new DynamicJsonValue("123");

// 多种转换方式
var asString = jsonValue.AsString();
var asInt = jsonValue.AsInt();
var asBool = jsonValue.AsBool();
var asDouble = jsonValue.AsDouble();
var asDecimal = jsonValue.AsDecimal();
var asDateTime = jsonValue.AsDateTime();
var asGuid = jsonValue.AsGuid();

// 带默认值的转换
var asIntWithDefault = jsonValue.AsInt(0);
var asStringWithDefault = jsonValue.AsString("default");
```

## 最佳实践

1. **使用 `dynamic` 关键字**：当你需要直接属性访问时，使用 `dynamic` 关键字。

2. **使用强类型方法**：当你需要类型安全和智能提示时，使用 `Get()` 和 `As*()` 方法。

3. **使用扩展方法**：利用扩展方法来简化常见操作。

4. **错误处理**：使用带默认值的转换方法来避免异常。

5. **性能考虑**：对于性能敏感的场景，优先使用索引器或强类型方法。

## 迁移指南

### 从 Newtonsoft.Json 迁移

```csharp
// Newtonsoft.Json 方式
JObject jObj = JObject.Parse(jsonString);
var status = (string)jObj["status"];
var count = (int)jObj["count"];

// 新的方式
var jsonObj = DynamicJsonObject.FromJson(jsonString);
var status = jsonObj.Get("status").AsString();
var count = jsonObj.Get("count").AsInt();

// 或者使用 dynamic
dynamic obj = DynamicJsonObject.FromJson(jsonString);
var status = (string)obj.status;
var count = (int)obj.count;
```

### 解决编译错误

如果遇到类似 `"DynamicJsonObject"未包含"status"的定义` 的编译错误，请使用以下方法之一：

1. **使用 dynamic 关键字**：

   ```csharp
   dynamic obj = (DynamicJsonObject)response.Data;
   var status = (string)obj.status;
   ```

2. **使用索引器**：

   ```csharp
   var obj = (DynamicJsonObject)response.Data;
   var status = (string)obj["status"];
   ```

3. **使用强类型方法**：
   ```csharp
   var obj = (DynamicJsonObject)response.Data;
   var status = obj.Get("status").AsString();
   ```
