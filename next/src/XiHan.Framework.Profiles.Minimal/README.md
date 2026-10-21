# XiHan.Framework.Profiles.Minimal

Minimal 组合包（Ring 6）：`Primitives` + `Kernel` + `Hosting.AspNetCore`。一个包引用即得到内核管道、探针与 Problem Details；没有任何子系统。

```csharp
builder.AddXiHan(xihan => xihan.UseMinimalProfile().UseFeature<MyFeature>());
```

`UseMinimalProfile()` 目前不装配任何特性，只表达意图并为后续内置特性保留入口。

## 依赖方向

组合包可引用任何环；被应用引用。

## 状态

P2：可用。
