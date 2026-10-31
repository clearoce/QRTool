$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root

$fw = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319"
$csc = Join-Path $fw "csc.exe"
if (-not (Test-Path $csc)) {
    throw "未找到 .NET Framework 4.x 的 csc.exe"
}

$qrcoder = Join-Path $root "lib\QRCoder.dll"
$icon = Join-Path $root "assets\qrtool.ico"
if (-not (Test-Path $qrcoder)) {
    throw "缺少 lib\QRCoder.dll"
}
if (-not (Test-Path $icon)) {
    throw "缺少 assets\qrtool.ico"
}

$refs = @(
    "System.dll",
    "System.Core.dll",
    "System.Drawing.dll",
    "System.Windows.Forms.dll",
    "System.Web.Extensions.dll"
)

$outDir = Join-Path $root "dist"
New-Item -ItemType Directory -Force $outDir | Out-Null
$outExe = Join-Path $outDir "QRTool.exe"

$cscArgs = @(
    "/nologo",
    "/target:winexe",
    "/platform:x64",
    "/optimize+",
    ("/out:" + $outExe),
    ("/win32icon:" + $icon),
    ("/resource:" + $qrcoder + ",QRCoder.dll"),
    ("/reference:" + $qrcoder)
)
foreach ($name in $refs) {
    $path = Join-Path $fw $name
    if (-not (Test-Path $path)) {
        throw "缺少引用：$path"
    }
    $cscArgs += ("/reference:" + $path)
}
$cscArgs += (Get-ChildItem (Join-Path $root "src") -Filter "*.cs" |
    ForEach-Object { $_.FullName })

& $csc $cscArgs
if ($LASTEXITCODE -ne 0) {
    throw "C# 编译失败（退出码 $LASTEXITCODE）"
}

$sizeKb = [math]::Round((Get-Item $outExe).Length / 1KB, 1)
Write-Host "已生成: $outExe ($sizeKb KB)"
