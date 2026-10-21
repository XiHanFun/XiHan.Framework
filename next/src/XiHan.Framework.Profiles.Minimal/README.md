# XiHan.Framework.Profiles.Minimal

Minimal 组合包（Ring 6）：`Primitives` + `Kernel` + `Hosting.AspNetCore` + `Localization.InMemory`。一个包引用即得到内核管道、探针、Problem Details 与默认语言文案；没有任何业务子系统。

```csharp
builder.AddXiHan(xihan => xihan.UseMinimalProfile().UseFeature<MyFeature>());
```

`UseMinimalProfile()` 装配 `InMemoryLocalizationFeature`：注册原语层与内核的语言文件目录并设置进程默认消息作用域，于是 `KernelMessages.ManifestEmpty.L()`、`Error.Message` 与 Problem Details 的 `detail` 都有文案（默认 `en-US`，内置 10 种区域，RFC-0010）。

## 依赖方向

组合包可引用任何环；被应用引用。

## 状态

P2：可用。
