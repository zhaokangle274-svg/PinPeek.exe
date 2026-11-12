<#
    PinPeek 一键编译脚本
    依赖：Windows 自带的 .NET Framework 4.8（csc.exe）+ 首次联网拉取 WebView2 SDK
    用法：powershell -ExecutionPolicy Bypass -File .\build.ps1
#>

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$libDir = Join-Path $root 'lib'
$outDir = Join-Path $root 'build'
$wv2Dir = Join-Path $libDir 'wv2'
$nupkg = Join-Path $libDir 'microsoft.web.webview2.nupkg'

$WV2_VERSION = '1.0.4314.35'
$WV2_URL = "https://api.nuget.org/v3-flatcontainer/microsoft.web.webview2/$WV2_VERSION/microsoft.web.webview2.$WV2_VERSION.nupkg"

function Write-Step($msg) { Write-Host "`n==> $msg" -ForegroundColor Cyan }

# 1) 准备 WebView2 SDK
if (-not (Test-Path (Join-Path $wv2Dir 'lib\net462\Microsoft.Web.WebView2.Core.dll'))) {
    Write-Step "下载 WebView2 SDK $WV2_VERSION"
    New-Item -ItemType Directory -Force -Path $libDir | Out-Null
    if (-not (Test-Path $nupkg)) {
        try {
            Invoke-WebRequest -Uri $WV2_URL -OutFile $nupkg -UseBasicParsing
        } catch {
            Write-Host "下载失败：$($_.Exception.Message)" -ForegroundColor Red
            Write-Host "请手动下载后放到 lib\ 下：" -ForegroundColor Yellow
            Write-Host "  $WV2_URL" -ForegroundColor Yellow
            exit 1
        }
    }
    if (Test-Path $wv2Dir) { Remove-Item $wv2Dir -Recurse -Force }
    Copy-Item $nupkg (Join-Path $libDir 'wv2.zip') -Force
    Expand-Archive (Join-Path $libDir 'wv2.zip') -DestinationPath $wv2Dir -Force
}
$netDir = Join-Path $wv2Dir 'lib\net462'
$loader = Join-Path $wv2Dir 'build\native\x64\WebView2Loader.dll'

# 2) 找 csc.exe
$cscCandidates = @(
    (Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'),
    (Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe')
)
$csc = $cscCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $csc) {
    Write-Host "找不到 csc.exe，请确认已安装 .NET Framework 4.x（Windows 10/11 自带）" -ForegroundColor Red
    exit 1
}
Write-Step "编译器：$csc"

# 3) 编译
Write-Step "编译 src\Program.cs"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$cscArgs = @(
    '/nologo', '/target:winexe', '/platform:x64', '/optimize+', '/codepage:65001',
    "/out:$outDir\PinPeek.exe",
    "/win32manifest:$root\src\app.manifest",
    "/win32icon:$root\src\icon.ico",
    "/r:$netDir\Microsoft.Web.WebView2.Core.dll",
    "/r:$netDir\Microsoft.Web.WebView2.WinForms.dll",
    '/r:System.dll', '/r:System.Core.dll', '/r:System.Drawing.dll', '/r:System.Windows.Forms.dll',
    "$root\src\Program.cs"
)
& $csc $cscArgs
if ($LASTEXITCODE -ne 0) { Write-Host "编译失败" -ForegroundColor Red; exit $LASTEXITCODE }

# 4) 拷贝运行期依赖
Write-Step "组装输出目录"
Copy-Item (Join-Path $netDir 'Microsoft.Web.WebView2.Core.dll') $outDir -Force
Copy-Item (Join-Path $netDir 'Microsoft.Web.WebView2.WinForms.dll') $outDir -Force
Copy-Item $loader $outDir -Force
foreach ($f in @('使用说明.txt', 'enable_autostart_admin.bat', 'disable_autostart_admin.bat')) {
    # 说明文档放在 docs/ 下，根目录也可能有同名文件，两处都找
    foreach ($dir in @($root, (Join-Path $root 'docs'))) {
        $p = Join-Path $dir $f
        if (Test-Path $p) { Copy-Item $p $outDir -Force; break }
    }
}

Write-Host "`n构建完成：$outDir" -ForegroundColor Green
Get-ChildItem $outDir | Select-Object Name, Length | Format-Table -AutoSize

