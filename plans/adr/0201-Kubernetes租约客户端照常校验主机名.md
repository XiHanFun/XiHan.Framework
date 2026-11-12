# ADR-0201 Kubernetes 租约客户端照常校验主机名

## 状态

已采纳（RFC-0013「领导选举」Kubernetes Provider 一条修订）。

## 日期

2026-11-12

## 背景

5.0 安全审查 S-17（`plans/5.0-安全审查.md`），中危，这里复现确认。

- `KubernetesLeaseClient.CreateInCluster` 在 ServiceAccount 的 CA 存在时，自己写了 `RemoteCertificateValidationCallback`：只用自定义根建链，忽略平台给出的 `SslPolicyErrors`。
- 主机名不对、用途不对的证书，只要链通到集群 CA 就放行。集群 CA 签发的证书很多：别的服务的服务端证书、kubelet 的客户端证书。
- 持有其中任意一张的中间人能冒充 API Server：拿到 ServiceAccount 的 Bearer 令牌，并回伪造的租约造成双主。

复现：`KubernetesLeaseTlsTests`。修正前，签给另一个主机名的证书、只有客户端认证用途的证书都被接受，请求照常发出。

## 备选

1. **回调里再判 `SslPolicyErrors.RemoteCertificateNameMismatch`**：否决。还要自己判用途、链错误与证书缺失，回调越写越像平台的校验，漏一项就是旁路。
2. **换成 `SslClientAuthenticationOptions.CertificateChainPolicy`**：采纳。平台照常做全部校验（含主机名），只把信任根换成集群 CA。

## 决策

1. CA 存在时设 `CertificateChainPolicy`：`CustomRootTrust`、信任库只有这个 CA、不查吊销（集群 CA 没有吊销端点），`ApplicationPolicy` 要求服务端认证（`1.3.6.1.5.5.7.3.1`）。
2. 不再设 `RemoteCertificateValidationCallback`，主机名由平台校验。
3. CA 文件不存在时照旧走系统信任。

## 影响

- API Server 的证书要签给客户端用的地址：`KUBERNETES_SERVICE_HOST` 的 IP 或 `kubernetes.default.svc`。各发行版的 API Server 证书本来就带这些主体备用名。
- 自己配了 `ApiServer` 的，地址要与证书的主体备用名一致。
- 公开 API 不变。
