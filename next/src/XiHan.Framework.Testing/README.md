# XiHan.Framework.Testing

测试基座（工具链）。供框架自身与使用者的测试项目引用。

## 内容

| 类型 | 用途 |
| --- | --- |
| `TestHost` | 在内存里装配并启动一个应用：自建服务集合与配置，`InvokeAsync` 直接走管道，`DisposeAsync` 逆序停止 |
| `StubFeature` | 可注入配置、启动、停止行为并记录事件的特性桩 |
| `TestManifests` | 用能力 ID 文本快速构造清单 |
| `TestInvocationContext` | 不依赖宿主的调用上下文 |

## 依赖方向

依赖 `XiHan.Framework.Kernel` 与 `Microsoft.Extensions.DependencyInjection`、`Microsoft.Extensions.Configuration` 实现包。不被任何正式包引用。

## 状态

P1：可用。一致性向量回放工具随协议适配器（P3）补充。
