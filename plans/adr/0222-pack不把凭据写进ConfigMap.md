# ADR-0222 pack 不把凭据写进 ConfigMap

## 状态

已采纳（`spec/packaging/app-manifest.md` 的 `config.files` 一条修订，`conventions/error-codes.md` 登记 `CLI_VALIDATION_007`）。

## 日期

2026-11-13

## 背景

5.0 安全审查「加固建议」（`plans/5.0-安全审查.md` 第四节）。

- `xihan pack` 把应用清单 `config.files` 列出的配置文件原样嵌进生成的 ConfigMap。
- ConfigMap 不加密，命名空间里能读它的人多；生成的 `deploy/` 目录通常还会提交进仓库。
- 配置文件里常有连接串、签名密钥这类值，它们本该走绑定的 Secret（`pack` 已经为绑定生成 Secret 示例）或环境变量。

复现：`CliHostTests.Pack_RefusesToPutCredentialsIntoTheConfigMap`。修正前，带连接串与签名密钥的 `appsettings.Production.json` 原样进了 ConfigMap，`pack` 成功退出。

## 备选

1. **把这些值替换成占位再嵌入**：否决。生成的 ConfigMap 与应用以为的配置不一样，部署之后才发现少了值；静默改写比失败更难查。
2. **改为生成 Secret**：否决。配置文件里多数是非敏感的值，整份放进 Secret 不利于审阅与热更新；凭据本来就有绑定这条路。
3. **发现带值的凭据就失败**：采纳。

## 决策

1. 生成 kustomize 时，逐个读 `config.files` 里存在的 JSON 文件（允许注释与尾随逗号；解析不了的不判定）。
2. 凭据键：名字按 RFC-0014「选项里的凭据」的规则（`Password`、`Passphrase`、`Secret`、`SecretKey`、`AccessKey`、`AccessKeyId`、`ApiKey`、`PrivateKey`、`ConnectionString`、`Credential(s)` 结尾，或就是 `Token` / `AccessToken` / `RefreshToken` / `BearerToken` / `SessionToken`），外加 `ConnectionStrings` 节；不区分大小写。
3. 凭据键的值（或它子树里的任一值）非空即命中；空串与 `null` 是占位，不算。
4. 有命中就以 `CLI_VALIDATION_007` 失败（参数 `file`、`keys`：配置路径以 `:` 连接，不带值），不写任何文件。只生成别的产物（`--only compose` 等）时不看。

## 影响

- 配置文件里带着凭据的部署要把凭据移到绑定的 Secret 或环境变量，或在这些文件里留空。
- 名字像凭据、其实不是的键（如某个叫 `Token` 的开关）会误报：改名，或把它挪出进 ConfigMap 的文件。
- 公开 API：`CliErrors.ConfigSecretEmbedded`。
