// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;

namespace XiHan.Framework.Cli.Packaging;

/// <summary>
/// 生成 Kustomize 清单。
/// </summary>
/// <remarks>
/// Deployment、Service、ConfigMap、HPA、PDB、Lease 的 RBAC、Secret 示例与 kustomization。
/// </remarks>
internal static class KustomizeGenerator
{
    /// <summary>
    /// 产物目录。
    /// </summary>
    private const string Directory = "kustomize/";

    /// <summary>
    /// Service Binding 挂载根。
    /// </summary>
    private const string BindingRoot = "/bindings";

    /// <summary>
    /// 生成全部文件。
    /// </summary>
    /// <remarks>
    /// <paramref name="manifestDirectory"/> 用来读取配置文件内容。
    /// </remarks>
    public static IEnumerable<PackOutput> Generate(AppManifest manifest, string manifestDirectory)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(manifestDirectory);
        var resources = new List<string> { "deployment.yaml", "service.yaml" };
        yield return new PackOutput(Directory + "deployment.yaml", Deployment(manifest));
        yield return new PackOutput(Directory + "service.yaml", Service(manifest));
        if (manifest.Config.Files.Count > 0)
        {
            resources.Add("configmap.yaml");
            yield return new PackOutput(Directory + "configmap.yaml", ConfigMap(manifest, manifestDirectory));
        }

        if (manifest.Scaling is not null)
        {
            resources.Add("hpa.yaml");
            yield return new PackOutput(Directory + "hpa.yaml", HorizontalPodAutoscaler(manifest));
        }

        if (ReplicaCount(manifest) > 1)
        {
            resources.Add("pdb.yaml");
            yield return new PackOutput(Directory + "pdb.yaml", PodDisruptionBudget(manifest));
        }

        if (manifest.Leader is not null)
        {
            resources.Add("rbac.yaml");
            yield return new PackOutput(Directory + "rbac.yaml", Rbac(manifest));
        }

        if (manifest.Bindings.Count > 0)
        {
            yield return new PackOutput(Directory + "bindings.example.yaml", BindingSecrets(manifest));
        }

        yield return new PackOutput(Directory + "kustomization.yaml", Kustomization(manifest, resources));
    }

    /// <summary>
    /// 副本数：有伸缩时取最少副本。
    /// </summary>
    private static int ReplicaCount(AppManifest manifest) => manifest.Scaling?.Min ?? manifest.Replicas;

    /// <summary>
    /// 绑定的 Secret 名。
    /// </summary>
    private static string SecretName(AppManifest manifest, string binding, BindingSettings settings) => settings.Secret ?? manifest.Name + "-" + binding;

    /// <summary>
    /// 公共标签。
    /// </summary>
    private static YamlText Labels(YamlText yaml, int indent, AppManifest manifest)
        => yaml.Pair(indent, "app.kubernetes.io/name", manifest.Name)
            .Pair(indent, "app.kubernetes.io/version", manifest.Image.Tag)
            .Pair(indent, "xihan.profile", manifest.Profile);

    /// <summary>
    /// Deployment。
    /// </summary>
    private static string Deployment(AppManifest manifest)
    {
        var yaml = new YamlText()
            .Line(0, "apiVersion: apps/v1")
            .Line(0, "kind: Deployment")
            .Line(0, "metadata:")
            .Pair(2, "name", manifest.Name)
            .Line(2, "labels:");
        Labels(yaml, 4, manifest)
            .Line(0, "spec:")
            .Pair(2, "replicas", ReplicaCount(manifest))
            .Line(2, "selector:")
            .Line(4, "matchLabels:")
            .Pair(6, "app.kubernetes.io/name", manifest.Name)
            .Line(2, "template:")
            .Line(4, "metadata:")
            .Line(6, "labels:");
        Labels(yaml, 8, manifest)
            .Line(4, "spec:");
        if (manifest.Leader is not null)
        {
            yaml.Pair(6, "serviceAccountName", manifest.Name);
        }

        yaml.Pair(6, "terminationGracePeriodSeconds", manifest.Shutdown.TerminationGracePeriodSeconds)
            .Line(6, "securityContext:")
            .Pair(8, "runAsNonRoot", true)
            .Line(8, "seccompProfile:")
            .Pair(10, "type", "RuntimeDefault")
            .Line(6, "containers:")
            .Line(8, "- name: app")
            .Pair(10, "image", manifest.ImageReference)
            .Line(10, "ports:")
            .Line(12, "- name: http")
            .Pair(14, "containerPort", manifest.Ports.Http);
        if (manifest.Ports.Grpc is { } grpc)
        {
            yaml.Line(12, "- name: grpc").Pair(14, "containerPort", grpc);
        }

        yaml.Line(10, "env:");
        Env(yaml, "XiHan__Kernel__StopTimeoutSeconds", manifest.Shutdown.GraceSeconds.ToString(CultureInfo.InvariantCulture));
        if (manifest.Bindings.Count > 0)
        {
            Env(yaml, "SERVICE_BINDING_ROOT", BindingRoot);
        }

        if (manifest.Config.Files.Count > 0)
        {
            Env(yaml, "XIHAN_CONFIG_PATH", manifest.Config.MountPath);
            // ConfigMap 更新以符号链接切换，inotify 监视看不到；轮询监视才能触发重载
            Env(yaml, "DOTNET_USE_POLLING_FILE_WATCHER", "1");
        }

        if (manifest.Leader is { } leader)
        {
            Env(yaml, "XiHan__Scheduling__Kubernetes__Lease", leader.Lease);
            FieldEnv(yaml, "XIHAN_POD_NAME", "metadata.name");
            FieldEnv(yaml, "POD_NAMESPACE", "metadata.namespace");
        }

        foreach (var (key, value) in manifest.Environment.OrderBy(static e => e.Key, StringComparer.Ordinal))
        {
            Env(yaml, key, value);
        }

        Probe(yaml, "startupProbe", "/health/startup", manifest.Probes, manifest.Probes.StartupFailureThreshold);
        Probe(yaml, "readinessProbe", "/health/ready", manifest.Probes, 3);
        Probe(yaml, "livenessProbe", "/health/live", manifest.Probes, 3);
        if (manifest.Resources is { } resources)
        {
            yaml.Line(10, "resources:");
            Quantities(yaml, "requests", resources.Requests);
            Quantities(yaml, "limits", resources.Limits);
        }

        yaml.Line(10, "securityContext:")
            .Pair(12, "allowPrivilegeEscalation", false)
            .Pair(12, "readOnlyRootFilesystem", true)
            .Line(12, "capabilities:")
            .Line(14, "drop:")
            .Line(16, "- ALL")
            .Line(10, "volumeMounts:")
            .Line(12, "- name: tmp")
            .Pair(14, "mountPath", "/tmp");
        foreach (var binding in manifest.Bindings.Keys.OrderBy(static k => k, StringComparer.Ordinal))
        {
            yaml.Line(12, "- name: binding-" + binding)
                .Pair(14, "mountPath", BindingRoot + "/" + binding)
                .Pair(14, "readOnly", true);
        }

        if (manifest.Config.Files.Count > 0)
        {
            yaml.Line(12, "- name: config")
                .Pair(14, "mountPath", manifest.Config.MountPath)
                .Pair(14, "readOnly", true);
        }

        yaml.Line(6, "volumes:")
            .Line(8, "- name: tmp")
            .Line(10, "emptyDir: {}");
        foreach (var (binding, settings) in manifest.Bindings.OrderBy(static b => b.Key, StringComparer.Ordinal))
        {
            yaml.Line(8, "- name: binding-" + binding)
                .Line(10, "secret:")
                .Pair(12, "secretName", SecretName(manifest, binding, settings));
        }

        if (manifest.Config.Files.Count > 0)
        {
            yaml.Line(8, "- name: config")
                .Line(10, "configMap:")
                .Pair(12, "name", manifest.Name + "-config");
        }

        return yaml.ToString();
    }

    /// <summary>
    /// 一个环境变量。
    /// </summary>
    private static void Env(YamlText yaml, string name, string value) => yaml.Line(12, "- name: " + name).Pair(14, "value", value);

    /// <summary>
    /// 一个取自 Pod 字段的环境变量。
    /// </summary>
    private static void FieldEnv(YamlText yaml, string name, string fieldPath)
        => yaml.Line(12, "- name: " + name)
            .Line(14, "valueFrom:")
            .Line(16, "fieldRef:")
            .Pair(18, "fieldPath", fieldPath);

    /// <summary>
    /// 一个 HTTP 探针。
    /// </summary>
    private static void Probe(YamlText yaml, string kind, string path, ProbeSettings probes, int failureThreshold)
        => yaml.Line(10, kind + ":")
            .Line(12, "httpGet:")
            .Pair(14, "path", path)
            .Pair(14, "port", "http")
            .Pair(12, "periodSeconds", probes.PeriodSeconds)
            .Pair(12, "timeoutSeconds", probes.TimeoutSeconds)
            .Pair(12, "failureThreshold", failureThreshold);

    /// <summary>
    /// 一组资源数量。
    /// </summary>
    private static void Quantities(YamlText yaml, string key, ResourceQuantities? quantities)
    {
        if (quantities is null || (quantities.Cpu is null && quantities.Memory is null))
        {
            return;
        }

        yaml.Line(12, key + ":");
        if (quantities.Cpu is { } cpu)
        {
            yaml.Pair(14, "cpu", cpu);
        }

        if (quantities.Memory is { } memory)
        {
            yaml.Pair(14, "memory", memory);
        }
    }

    /// <summary>
    /// Service。
    /// </summary>
    private static string Service(AppManifest manifest)
    {
        var yaml = new YamlText()
            .Line(0, "apiVersion: v1")
            .Line(0, "kind: Service")
            .Line(0, "metadata:")
            .Pair(2, "name", manifest.Name)
            .Line(2, "labels:");
        Labels(yaml, 4, manifest)
            .Line(0, "spec:")
            .Pair(2, "type", "ClusterIP")
            .Line(2, "selector:")
            .Pair(4, "app.kubernetes.io/name", manifest.Name)
            .Line(2, "ports:")
            .Line(4, "- name: http")
            .Pair(6, "port", manifest.Ports.Http)
            .Pair(6, "targetPort", "http");
        if (manifest.Ports.Grpc is { } grpc)
        {
            yaml.Line(4, "- name: grpc").Pair(6, "port", grpc).Pair(6, "targetPort", "grpc");
        }

        return yaml.ToString();
    }

    /// <summary>
    /// ConfigMap：列出的文件存在时嵌入内容，否则为空对象。
    /// </summary>
    private static string ConfigMap(AppManifest manifest, string manifestDirectory)
    {
        var yaml = new YamlText()
            .Line(0, "apiVersion: v1")
            .Line(0, "kind: ConfigMap")
            .Line(0, "metadata:")
            .Pair(2, "name", manifest.Name + "-config")
            .Line(0, "data:");
        foreach (var file in manifest.Config.Files)
        {
            var path = Path.Combine(manifestDirectory, file);
            yaml.Block(2, YamlText.Scalar(file), File.Exists(path) ? File.ReadAllText(path) : "{}\n");
        }

        return yaml.ToString();
    }

    /// <summary>
    /// HPA。
    /// </summary>
    private static string HorizontalPodAutoscaler(AppManifest manifest)
        => new YamlText()
            .Line(0, "apiVersion: autoscaling/v2")
            .Line(0, "kind: HorizontalPodAutoscaler")
            .Line(0, "metadata:")
            .Pair(2, "name", manifest.Name)
            .Line(0, "spec:")
            .Line(2, "scaleTargetRef:")
            .Pair(4, "apiVersion", "apps/v1")
            .Pair(4, "kind", "Deployment")
            .Pair(4, "name", manifest.Name)
            .Pair(2, "minReplicas", manifest.Scaling!.Min)
            .Pair(2, "maxReplicas", manifest.Scaling.Max)
            .Line(2, "metrics:")
            .Line(4, "- type: Resource")
            .Line(6, "resource:")
            .Pair(8, "name", "cpu")
            .Line(8, "target:")
            .Pair(10, "type", "Utilization")
            .Pair(10, "averageUtilization", manifest.Scaling.CpuPercent)
            .ToString();

    /// <summary>
    /// PDB：至少一个可用。
    /// </summary>
    private static string PodDisruptionBudget(AppManifest manifest)
        => new YamlText()
            .Line(0, "apiVersion: policy/v1")
            .Line(0, "kind: PodDisruptionBudget")
            .Line(0, "metadata:")
            .Pair(2, "name", manifest.Name)
            .Line(0, "spec:")
            .Pair(2, "minAvailable", 1)
            .Line(2, "selector:")
            .Line(4, "matchLabels:")
            .Pair(6, "app.kubernetes.io/name", manifest.Name)
            .ToString();

    /// <summary>
    /// Lease 的 ServiceAccount、Role 与 RoleBinding。
    /// </summary>
    private static string Rbac(AppManifest manifest)
        => new YamlText()
            .Line(0, "apiVersion: v1")
            .Line(0, "kind: ServiceAccount")
            .Line(0, "metadata:")
            .Pair(2, "name", manifest.Name)
            .Line(0, "---")
            .Line(0, "apiVersion: rbac.authorization.k8s.io/v1")
            .Line(0, "kind: Role")
            .Line(0, "metadata:")
            .Pair(2, "name", manifest.Name + "-leader")
            .Line(0, "rules:")
            .Line(2, "- apiGroups: [\"coordination.k8s.io\"]")
            .Line(4, "resources: [\"leases\"]")
            .Line(4, "resourceNames: [" + YamlText.Scalar(manifest.Leader!.Lease) + "]")
            .Line(4, "verbs: [\"get\", \"update\", \"patch\"]")
            .Line(2, "- apiGroups: [\"coordination.k8s.io\"]")
            .Line(4, "resources: [\"leases\"]")
            .Line(4, "verbs: [\"create\"]")
            .Line(0, "---")
            .Line(0, "apiVersion: rbac.authorization.k8s.io/v1")
            .Line(0, "kind: RoleBinding")
            .Line(0, "metadata:")
            .Pair(2, "name", manifest.Name + "-leader")
            .Line(0, "subjects:")
            .Line(2, "- kind: ServiceAccount")
            .Pair(4, "name", manifest.Name)
            .Line(0, "roleRef:")
            .Pair(2, "apiGroup", "rbac.authorization.k8s.io")
            .Pair(2, "kind", "Role")
            .Pair(2, "name", manifest.Name + "-leader")
            .ToString();

    /// <summary>
    /// 绑定 Secret 的示例（Service Binding 规范的 type 与 uri），不进 kustomization。
    /// </summary>
    private static string BindingSecrets(AppManifest manifest)
    {
        var yaml = new YamlText().Line(0, "# 示例：按 Service Binding 规范为每个绑定创建 Secret，再挂到 " + BindingRoot + "/<绑定名>；真实值不要提交到仓库。");
        var first = true;
        foreach (var (binding, settings) in manifest.Bindings.OrderBy(static b => b.Key, StringComparer.Ordinal))
        {
            if (!first)
            {
                yaml.Line(0, "---");
            }

            first = false;
            yaml.Line(0, "apiVersion: v1")
                .Line(0, "kind: Secret")
                .Line(0, "metadata:")
                .Pair(2, "name", SecretName(manifest, binding, settings))
                .Line(0, "stringData:")
                .Pair(2, "type", settings.Type)
                .Pair(2, "uri", "<" + binding + " connection string>");
        }

        return yaml.ToString();
    }

    /// <summary>
    /// kustomization。
    /// </summary>
    private static string Kustomization(AppManifest manifest, IReadOnlyList<string> resources)
    {
        var yaml = new YamlText()
            .Line(0, "apiVersion: kustomize.config.k8s.io/v1beta1")
            .Line(0, "kind: Kustomization")
            .Line(0, "resources:");
        foreach (var resource in resources)
        {
            yaml.Line(2, "- " + resource);
        }

        yaml.Line(0, "images:")
            .Line(2, "- name: " + YamlText.Scalar(manifest.ImageReference.Contains(':', StringComparison.Ordinal) ? manifest.ImageReference[..manifest.ImageReference.LastIndexOf(':')] : manifest.ImageReference))
            .Pair(4, "newTag", manifest.Image.Tag);
        return yaml.ToString();
    }
}
