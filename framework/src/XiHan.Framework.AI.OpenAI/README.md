# XiHan.Framework.AI.OpenAI

## 关于

`XiHan.Framework.AI.OpenAI` 是 AI 的 OpenAI 兼容提供程序：`Kind = openai` 的模型都由它创建聊天客户端与嵌入生成器，覆盖 OpenAI 官方端点，以及 Ollama、vLLM、LM Studio 等兼容端点。

## 主要特性

* 聊天与嵌入模型，按配置创建并缓存
* 聊天内置函数调用循环，模型发起的工具调用自动执行
* `Endpoint` 留空走官方端点，本地端点不校验密钥
* 装配与启动不访问网络

## 如何使用

### 安装

```bash
dotnet add package XiHan.Framework.AI.OpenAI --prerelease
```

### 使用

启用特性，并在 `XiHan:AI:Providers` 下配置模型（`ApiKey` 换成你自己的密钥）：

```csharp
builder.AddXiHan(xihan => xihan
    .UseStandardProfile()
    .UseFeature<AIToolsFeature>()
    .UseFeature<OpenAIFeature>());
```

```json
{
  "XiHan": { "AI": {
    "DefaultProvider": "cloud",
    "Providers": {
      "cloud": { "Kind": "openai", "ApiKey": "<your-api-key>", "ChatModel": "gpt-4o-mini", "EmbeddingModel": "text-embedding-3-small" },
      "local": { "Kind": "openai", "Endpoint": "http://localhost:11434/v1", "ChatModel": "qwen2.5", "TimeoutSeconds": 90 }
    }
  } }
}
```

## 主要类型

此库提供的主要类型：

* `OpenAIFeature`：启用入口
* `OpenAIChatClientFactory`：聊天客户端工厂
* `OpenAIEmbeddingGeneratorFactory`：嵌入生成器工厂

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/ai-openai)
* [AI 指南](https://framework.docs.xihanfun.com/guide/ai)

## 相关包

* AI 抽象：[XiHan.Framework.AI.Abstractions](https://www.nuget.org/packages/XiHan.Framework.AI.Abstractions)
* 向量知识库：[XiHan.Framework.AI.VectorData](https://www.nuget.org/packages/XiHan.Framework.AI.VectorData)

## 反馈与贡献

`XiHan.Framework.AI.OpenAI` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
