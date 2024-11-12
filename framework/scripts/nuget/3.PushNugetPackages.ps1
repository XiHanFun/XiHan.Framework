# NuGet 包路径
$nupkgsPath = "..\..\nupkgs"
# 从环境变量读取 API 密钥
$apiKey = [System.Environment]::GetEnvironmentVariable("NUGET_API_KEY", "User")
# NuGet 源地址
$sourceUrl = "https://api.nuget.org/v3/index.json"

# 获取包路径目录下所有 .nupkg 文件
$nupkgFiles = Get-ChildItem -Path $nupkgsPath -Filter *.nupkg

# 用于存储最新版本的文件
$latestFiles = @{}

# 查找最新版本的 nupkg 文件
foreach ($file in $nupkgFiles) {
    if ($file.Name -match "^(.*?)\.(\d+)\.(\d+)\.(\d+)\.nupkg$") {
        $packageName = $matches[1]
        $version = [Version]::Parse("$($matches[2]).$($matches[3]).$($matches[4])")

        if ($latestFiles.ContainsKey($packageName)) {
            if ($version -gt $latestFiles[$packageName].Version) {
                $latestFiles[$packageName] = [PSCustomObject]@{ File = $file; Version = $version }
            }
        }
        else {
            $latestFiles[$packageName] = [PSCustomObject]@{ File = $file; Version = $version }
        }
    }
}

# 删除旧版本的文件
foreach ($file in $nupkgFiles) {
    if ($file.Name -match "^(.*?)\.(\d+)\.(\d+)\.(\d+)\.nupkg$") {
        $packageName = $matches[1]
        $version = [Version]::Parse("$($matches[2]).$($matches[3]).$($matches[4])")

        if ($latestFiles[$packageName].File.Name -ne $file.Name) {
            Write-Output "删除旧版本文件：$($file.Name)"
            Remove-Item $file.FullName -Force
        }
    }
}

# 推送最新的 nupkg 文件
foreach ($latestFile in $latestFiles.Values) {
    Write-Output "正在推送文件：$($latestFile.File.Name)"
    dotnet nuget push $latestFile.File.FullName --api-key $apiKey --source $sourceUrl --skip-duplicate
    if ($LASTEXITCODE -eq 0) {
        Write-Output "推送成功：$($latestFile.File.Name)"
    }
    else {
        Write-Output "推送失败：$($latestFile.File.Name)"
    }
}
