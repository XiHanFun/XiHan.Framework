# ADR-0128 命令行字段选项按请求 Schema 写类型

## 状态

已采纳（`spec/protocol/commandline.md` 随之修订）。

## 日期

2026-11-05

## 背景

规范原来规定字段选项的值都是字符串，由请求类型的序列化上下文（`AllowReadingFromString`）读成数字。这和 RFC-0011 的校验顺序对不上：Validate 阶段在解码之前按请求 Schema 校验载荷，`"times":"2"` 走不到解码，就被 `KERNEL_VALIDATION_003` 拒绝。

所以凡是带 `OperationSchema` 的操作（`Kernel.Generators` 生成的都带），`greetings create --name Ada --times 2` 都以退出码 2 失败，而文档一直把这条命令写成成功。用例没发现，是因为测试夹具登记操作时没带 Schema，校验被跳过了。

## 决策

1. 操作带 `OperationSchema` 时，`invoke` 在进入管道前按请求 Schema 写字段选项。属性取根的 `properties`，没有声明的取 `additionalProperties`，与校验器同序；`$ref`、`allOf`、`anyOf`、`oneOf` 展开后取并集。
2. 允许 `string` 或不约束类型的写字符串，`--name 42` 不变成数字；否则 `boolean`、`integer`、`number` 写 JSON 字面量，`array` 写数组（单值也是一元数组，元素按 `items`）。`object` 不从文本解析，嵌套对象用 `--json` 或 `--data`。
3. 转换不了的值留字符串，不另造用法错误：由 Validate 阶段报 `KERNEL_VALIDATION_003`，退出码 2，违规落在字段路径上，与其他载荷来源一致。重复的选项写到非数组字段时同样由 Validate 阶段拒绝。
4. 只转换字段选项。`--json` 与 `--data` 是调用方写好的 JSON，原样进入。
5. 操作没有 `OperationSchema` 时维持现状：值都写字符串，由序列化上下文决定怎么读。

## 备选

1. **放宽校验器，让 `integer` 接受数字样的字符串**：校验器是所有协议共用的，HTTP、gRPC、MCP 的载荷也会跟着放宽，发布出去的 Schema 不再是真实契约。否决。
2. **按序列化元数据转换（`Protocols.Http` 的 `OperationRequestBinder` 的做法）**：插件代理的操作只有 Schema 没有元数据，在命令行上仍然跑不通；而校验看的正是 Schema，按它转换才保证两边一致。否决。
3. **命令行跳过 Schema 校验**：同一份契约在不同协议上校验结果不同，违背 RFC-0011。否决。
4. **把文档里的命令改成 `--json`**：文档对了，字段选项对带 Schema 的操作仍然只能传字符串字段。否决。

## 影响

带 Schema 的操作不再需要为命令行打开 `AllowReadingFromString`，`samples/SimpleCli` 已去掉。单值进数组字段从失败变成一元数组；`--times 99` 这类值现在报范围违规，不再报类型不符。没有 Schema 的操作行为不变。
