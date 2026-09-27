# FontBuilder.ps1
# 统一的字体生成脚本 - 支持批量生成和单独生成

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Push-Location $ScriptDir

# 读取配置文件
$ConfigFile = Join-Path $ScriptDir "config.json"
if (-not (Test-Path $ConfigFile)) {
    Write-Host "✗ 配置文件不存在: $ConfigFile" -ForegroundColor Red
    exit 1
}

$Config = Get-Content -Raw -Path $ConfigFile -Encoding UTF8 | ConvertFrom-Json

# 将配置中的相对路径解析为基于脚本目录的绝对路径，
# 避免工作目录不同导致 BMFont 或 XnaFontRebuilder 找不到文件
function Resolve-FontWorkPath {
    param([string]$Path)
    if ([string]::IsNullOrEmpty($Path)) { return $Path }
    if ([System.IO.Path]::IsPathRooted($Path)) { return [System.IO.Path]::GetFullPath($Path) }
    return [System.IO.Path]::GetFullPath((Join-Path $ScriptDir $Path))
}

# 字体配置列表
$fontConfigs = @{}
foreach ($fontName in $Config.fonts.PSObject.Properties.Name) {
    $fontData = $Config.fonts.$fontName
    $fontConfigs[$fontName] = @{
        ConfigFile = Resolve-FontWorkPath $fontData.configFile
        OutputDir = Resolve-FontWorkPath $fontData.outputDir
        FontFile = $fontData.fontFile
        TxtFile = $fontData.txtFile
        Description = $fontData.description
        CharInfoFile = Resolve-FontWorkPath $fontData.charInfoFile
        ExtraChars = [string]$fontData.extraChars
        ExtraCharsFile = Resolve-FontWorkPath $fontData.extraCharsFile
    }
}

# 全局配置
$BMFontExe = Resolve-FontWorkPath $Config.global.bmfontExe
$RebuilderDll = Resolve-FontWorkPath $Config.global.xnaFontRebuilder
$SourceFont = Resolve-FontWorkPath $Config.global.sourceFont

# 全局额外字符配置（应用到所有字体，与各字体自身的 extraChars 合并）
$GlobalExtraChars = [string]$Config.global.extraChars
$GlobalExtraCharsFile = Resolve-FontWorkPath $Config.global.extraCharsFile

# 纹理画布尺寸（像素，默认 1024；2048 可让每页容量翻 4 倍，从而减少总页数）
$PageSize = [int]$Config.conversion.pageSize
if ($PageSize -lt 1) { $PageSize = 1024 }

# 转换参数
$LatinCompensation = $Config.conversion.latinCompensation
$CharSpacing = $Config.conversion.charSpacing

# 公共函数：检查环境
function Test-Environment {
    Write-Host "`n[环境检查]" -ForegroundColor Cyan
    
    # 检查 .NET SDK
    $dotnetVersion = dotnet --version 2>$null
    if (-not $dotnetVersion) {
        Write-Host "  ✗ 未检测到 .NET SDK" -ForegroundColor Red
        Write-Host "    请安装 .NET 8.0 SDK: https://dotnet.microsoft.com/download/dotnet/8.0" -ForegroundColor Yellow
        return $false
    }
    Write-Host "  ✓ .NET SDK $dotnetVersion" -ForegroundColor Green
    
    # 检查 BMFont
    if (-not (Test-Path $BMFontExe)) {
        Write-Host "  ✗ 未找到 bmfont64.com" -ForegroundColor Red
        return $false
    }
    Write-Host "  ✓ bmfont64.com" -ForegroundColor Green
    
    # 检查源字体
    if (-not (Test-Path $SourceFont)) {
        Write-Host "  ✗ 未找到 font.otf" -ForegroundColor Red
        return $false
    }
    Write-Host "  ✓ font.otf" -ForegroundColor Green
    
    # 检查 XnaFontRebuilder 项目
    if (-not (Test-Path ".\XnaFontRebuilder\XnaFontRebuilder.csproj")) {
        Write-Host "  ✗ 未找到 XnaFontRebuilder 项目" -ForegroundColor Red
        return $false
    }
    Write-Host "  ✓ XnaFontRebuilder 项目" -ForegroundColor Green
    
    return $true
}

# 公共函数：构建 XnaFontRebuilder
function Build-XnaFontRebuilder {
    Write-Host "`n[构建 XnaFontRebuilder]" -ForegroundColor Cyan
    
    if (-not (Test-Path $RebuilderDll)) {
        Write-Host "  正在构建..." -ForegroundColor Yellow
        try {
            Push-Location ".\XnaFontRebuilder"
            dotnet build -c Release --no-incremental | Out-Null
            if ($LASTEXITCODE -ne 0) {
                throw "构建失败"
            }
            Pop-Location
            Write-Host "  ✓ 构建成功" -ForegroundColor Green
        }
        catch {
            Write-Host "  ✗ 构建失败: $_" -ForegroundColor Red
            return $false
        }
    }
    else {
        Write-Host "  ✓ 已存在，跳过构建" -ForegroundColor Green
    }
    
    return $true
}

# 公共函数：从字符信息文件生成 BMFont 配置文件（含额外字符）
function Generate-ConfigFile {
    param(
        [string]$FontName,
        [hashtable]$FontConfig
    )
    
    Write-Host "  [0/3] 生成配置文件..." -ForegroundColor Yellow
    
    # 检查字符信息文件是否存在
    if (-not (Test-Path $FontConfig.CharInfoFile)) {
        Write-Host "    ✗ 字符信息文件不存在: $($FontConfig.CharInfoFile)" -ForegroundColor Red
        return $false
    }
    
    try {
        # 使用 --build-cfg-auto 命令生成配置文件
        # 通过 --extra-chars / --extra-chars-file 追加 config.json 中配置的额外字符（如额外汉字）
        $builderArgs = @(
            $RebuilderDll,
            "--build-cfg-auto",
            $FontConfig.CharInfoFile,
            $FontConfig.ConfigFile,
            $SourceFont
        )
        if ($GlobalExtraChars) {
            $builderArgs += "--extra-chars"
            $builderArgs += $GlobalExtraChars
        }
        if ($GlobalExtraCharsFile) {
            $builderArgs += "--extra-chars-file"
            $builderArgs += $GlobalExtraCharsFile
        }
        if ($FontConfig.ExtraChars) {
            $builderArgs += "--extra-chars"
            $builderArgs += $FontConfig.ExtraChars
        }
        if ($FontConfig.ExtraCharsFile) {
            $builderArgs += "--extra-chars-file"
            $builderArgs += $FontConfig.ExtraCharsFile
        }
        if ($PageSize -ne 1024) {
            $builderArgs += "--page-size"
            $builderArgs += $PageSize
        }

        $extraSummary = $GlobalExtraChars + $FontConfig.ExtraChars
        if (-not [string]::IsNullOrEmpty($extraSummary)) {
            $preview = (($extraSummary.ToCharArray() | Select-Object -First 40) -join '')
            Write-Host "    · 额外字符 $($extraSummary.Length) 个: '$preview'..." -ForegroundColor Gray
        }

        # 捕获 dotnet 输出并转发到日志，避免其 stdout 泄漏进函数返回值
        $cfgOut = & dotnet $builderArgs 2>&1
        $cfgCode = $LASTEXITCODE
        $cfgOut | ForEach-Object { Write-Host "    $_" -ForegroundColor Gray }
        
        if ($cfgCode -ne 0) {
            throw "配置文件生成失败，退出代码: $cfgCode"
        }
        
        if (-not (Test-Path $FontConfig.ConfigFile)) {
            throw "未找到生成的配置文件"
        }
        
        Write-Host "    ✓ 配置文件生成成功: $($FontConfig.ConfigFile)" -ForegroundColor Green
        return $true
    }
    catch {
        Write-Host "    ✗ 失败: $_" -ForegroundColor Red
        return $false
    }
}

# 公共函数：生成单个字体
function Generate-Font {
    param(
        [string]$FontName,
        [hashtable]$FontConfig
    )
    
    Write-Host "`n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Magenta
    Write-Host "  生成字体: $FontName" -ForegroundColor Cyan
    Write-Host "  描述: $($FontConfig.Description)" -ForegroundColor Gray
    Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Magenta
    
    $startTime = Get-Date
    
    # 检查/生成配置文件：
    # - 配置了额外字符（global 或该字体）时，必须用 --build-cfg-auto 从字符信息文件重新生成配置；
    # - 未配置额外字符且已有 .bmfc 时，直接使用现有配置。
    $hasExtraChars = -not [string]::IsNullOrEmpty($GlobalExtraChars) -or `
                     -not [string]::IsNullOrEmpty($GlobalExtraCharsFile) -or `
                     -not [string]::IsNullOrEmpty($FontConfig.ExtraChars) -or `
                     -not [string]::IsNullOrEmpty($FontConfig.ExtraCharsFile)
    
    if ($hasExtraChars -or -not (Test-Path $FontConfig.ConfigFile)) {
        if (-not (Generate-ConfigFile -FontName $FontName -FontConfig $FontConfig)) {
            return $false
        }
    } else {
        Write-Host "  [0/3] 使用现有配置文件: $($FontConfig.ConfigFile)" -ForegroundColor Yellow
    }
    
    # 确保输出目录存在
    if (-not (Test-Path $FontConfig.OutputDir)) {
        New-Item -ItemType Directory -Path $FontConfig.OutputDir -Force | Out-Null
        Write-Host "  ✓ 创建输出目录: $($FontConfig.OutputDir)" -ForegroundColor Gray
    }
    
    $fontPath = Join-Path $FontConfig.OutputDir $FontConfig.FontFile
    $txtPath = Join-Path $FontConfig.OutputDir $FontConfig.TxtFile
    
    # 步骤1: 生成 BMFont
    Write-Host "  [1/3] 生成 BMFont 文件..." -ForegroundColor Yellow
    try {
        # 清理旧的纹理文件，避免上次运行残留的多余页干扰统计
        Get-ChildItem -Path $FontConfig.OutputDir -Filter "$($FontName)_*.png" -ErrorAction SilentlyContinue | Remove-Item -Force

        $configAbs = Resolve-Path $FontConfig.ConfigFile
        # OutputDir 已解析为绝对路径，$fontPath 即为绝对路径
        $fontAbs = $fontPath

        # BMFont 是 GUI 程序，不能用 -NoNewWindow（会导致 ExitCode 为空）；
        # 用 Start-Process -Wait 等待其完成
        $bmfontArgs = '-c "{0}" -o "{1}"' -f $configAbs, $fontAbs
        $process = Start-Process -FilePath $BMFontExe `
            -ArgumentList $bmfontArgs `
            -Wait -PassThru -WorkingDirectory $ScriptDir

        $bmExitCode = $process.ExitCode
        if ($null -ne $bmExitCode -and $bmExitCode -ne 0) {
            throw "BMFont 生成失败，退出代码: $bmExitCode"
        }
        
        if (-not (Test-Path $fontPath)) {
            throw "未找到生成的 .fnt 文件"
        }
        
        # 打印 .fnt 的字符数与页数
        $fntCharCount = 0
        $fntPageCount = 0
        try {
            [xml]$fntXml = Get-Content $fontPath -Raw
            $fntCharCount = [int]$fntXml.font.chars.count
            $fntPageCount = [int]$fntXml.font.common.pages
            Write-Host "    · .fnt 字符数: $fntCharCount, 页数: $fntPageCount" -ForegroundColor Gray
        } catch {
            Write-Host "    · 无法解析 .fnt 统计信息: $_" -ForegroundColor Gray
        }
        
        # 统计生成的图片
        $pngFiles = Get-ChildItem -Path $FontConfig.OutputDir -Filter "$($FontName)_*.png" -ErrorAction SilentlyContinue
        Write-Host "    ✓ 生成成功，纹理图片: $($pngFiles.Count) 张" -ForegroundColor Green

        # 读取原始字体的页数与字符数（FontInfo 二进制文件头部），用于页数异常检测
        $originalPageCount = 0
        $originalCharCount = 0
        try {
            $fs = [System.IO.File]::OpenRead($FontConfig.CharInfoFile)
            $originalPageCount = $fs.ReadByte()
            $b1 = $fs.ReadByte(); $b2 = $fs.ReadByte(); $b3 = $fs.ReadByte(); $b4 = $fs.ReadByte()
            $originalCharCount = ($b4 -shl 24) -bor ($b3 -shl 16) -bor ($b2 -shl 8) -bor $b1
            $fs.Close()
        } catch {}

        if ($originalPageCount -gt 1 -and $fntPageCount -le 1) {
            Write-Host "    ⚠ 警告: 原字体需要 $originalPageCount 页，但只生成了 $fntPageCount 张纹理！BMFont 可能未正确加载源字体" -ForegroundColor Yellow
            Write-Host "    ⚠ .fnt 大小: $((Get-Item $fontPath).Length) bytes" -ForegroundColor Yellow
        }
        if ($fntPageCount -gt $originalPageCount) {
            Write-Host "    · 页数提示: 生成 $fntPageCount 页 > 原字体 $originalPageCount 页，多余纹理页将由 UnpackTerrariaTextAsset 自动新增到游戏 bundle" -ForegroundColor Cyan
        }
        if ($originalCharCount -gt 0) {
            Write-Host "    · 字符数对比: 原 $originalCharCount -> 新 $fntCharCount (+$($fntCharCount - $originalCharCount))" -ForegroundColor Gray
        }
    }
    catch {
        Write-Host "    ✗ 失败: $_" -ForegroundColor Red
        return $false
    }
    
    # 步骤2: 转换格式
    Write-Host "  [2/3] 转换为 TXT 格式..." -ForegroundColor Yellow
    try {
        # 捕获 dotnet 输出并转发到日志，避免其 stdout 泄漏进函数返回值
        $convOut = & dotnet $RebuilderDll --convert $fontPath $txtPath --latin-compensation $LatinCompensation --character-spacing-compensation $CharSpacing 2>&1
        $convCode = $LASTEXITCODE
        $convOut | ForEach-Object { Write-Host "    $_" -ForegroundColor Gray }
        
        if ($convCode -ne 0) {
            throw "格式转换失败，退出代码: $convCode"
        }
        
        if (-not (Test-Path $txtPath)) {
            throw "未找到生成的 .txt 文件"
        }
        
        Write-Host "    ✓ 转换成功" -ForegroundColor Green
    }
    catch {
        Write-Host "    ✗ 失败: $_" -ForegroundColor Red
        return $false
    }
    
    # 步骤3: 验证输出
    Write-Host "  [3/3] 验证输出文件..." -ForegroundColor Yellow
    
    $fntSize = (Get-Item $fontPath).Length
    $txtSize = (Get-Item $txtPath).Length
    $pngCount = (Get-ChildItem -Path $FontConfig.OutputDir -Filter "*.png").Count
    
    Write-Host "    ✓ .fnt: $([math]::Round($fntSize/1KB, 2)) KB" -ForegroundColor Green
    Write-Host "    ✓ .txt: $([math]::Round($txtSize/1KB, 2)) KB" -ForegroundColor Green
    Write-Host "    ✓ 纹理: $pngCount 张图片" -ForegroundColor Green
    
    $endTime = Get-Date
    $duration = ($endTime - $startTime).TotalSeconds
    Write-Host "  ✅ $FontName 生成完成，耗时: $([math]::Round($duration, 2)) 秒" -ForegroundColor Green
    
    return $true
}

# 公共函数：列出所有可用字体
function Show-AvailableFonts {
    Write-Host "`n可用字体列表:" -ForegroundColor Cyan
    Write-Host ("{0,-15} {1,-30} {2,-12} {3}" -f "名称", "描述", "额外字符", "配置文件") -ForegroundColor Gray
    Write-Host ("{0,-15} {1,-30} {2,-12} {3}" -f "----", "----", "--------", "--------") -ForegroundColor Gray
    
    foreach ($name in $fontConfigs.Keys | Sort-Object) {
        $fontCfg = $fontConfigs[$name]
        $exists = if (Test-Path $fontCfg.ConfigFile) { "✓" } else { "✗" }
        $extraCount = ($GlobalExtraChars + $fontCfg.ExtraChars).Length
        $extraInfo = if ($extraCount -gt 0) { "+$extraCount" } else { "-" }
        Write-Host ("{0,-15} {1,-30} {2,-12} {3}" -f $name, $fontCfg.Description, $extraInfo, $exists) -NoNewline
        if ($exists -eq "✓") {
            Write-Host " (存在)" -ForegroundColor Green
        } else {
            Write-Host " (缺失)" -ForegroundColor Red
        }
    }
}

# 显示帮助信息
function Show-Help {
    Write-Host @"
╔══════════════════════════════════════════════════════════════╗
║                    字体生成工具 v2.0                         ║
╠══════════════════════════════════════════════════════════════╣
║ 用法:                                                        ║
║   .\FontBuilder.ps1 [参数]                                   ║
╠══════════════════════════════════════════════════════════════╣
║ 参数:                                                        ║
║   无参数           - 生成所有字体                            ║
║   -List           - 列出所有可用字体                         ║
║   -Help           - 显示此帮助信息                           ║
║   -Font <名称>    - 生成指定字体                             ║
║   -Rebuild        - 强制重新构建 XnaFontRebuilder            ║
╠══════════════════════════════════════════════════════════════╣
║ 示例:                                                        ║
║   .\FontBuilder.ps1                    # 生成所有字体        ║
║   .\FontBuilder.ps1 -List              # 列出所有字体        ║
║   .\FontBuilder.ps1 -Font Item_Stack   # 生成单个字体        ║
║   .\FontBuilder.ps1 -Font Combat_Crit  # 生成战斗暴击字体    ║
║   .\FontBuilder.ps1 -Rebuild           # 重新构建并生成所有  ║
╚══════════════════════════════════════════════════════════════╝
"@
}

# 主函数
function Main {
    param(
        [switch]$List,
        [switch]$Help,
        [string]$Font,
        [switch]$Rebuild
    )
    
    # 显示帮助
    if ($Help) {
        Show-Help
        return
    }
    
    # 列出字体
    if ($List) {
        Show-AvailableFonts
        return
    }
    
    # 显示标题
    Write-Host "╔══════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
    Write-Host "║                    字体批量生成工具 v2.0                      ║" -ForegroundColor Cyan
    Write-Host "╚══════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
    Write-Host "开始时间: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor Yellow
    
    # 环境检查
    if (-not (Test-Environment)) {
        Write-Host "`n❌ 环境检查失败，请解决上述问题后重试" -ForegroundColor Red
        exit 1
    }
    
    # 构建 XnaFontRebuilder
    if ($Rebuild) {
        Write-Host "`n[强制重新构建]" -ForegroundColor Yellow
        if (Test-Path $RebuilderDll) {
            Remove-Item $RebuilderDll -Force
        }
    }
    
    if (-not (Build-XnaFontRebuilder)) {
        Write-Host "`n❌ XnaFontRebuilder 构建失败" -ForegroundColor Red
        exit 1
    }
    
    # 确定要生成的字体列表
    $fontsToGenerate = @{}
    
    if ($Font) {
        # 生成单个字体
        if ($fontConfigs.ContainsKey($Font)) {
            $fontsToGenerate[$Font] = $fontConfigs[$Font]
            Write-Host "`n🎯 目标字体: $Font" -ForegroundColor Cyan
        } else {
            Write-Host "`n❌ 未知字体: $Font" -ForegroundColor Red
            Write-Host "可用字体: $($fontConfigs.Keys -join ', ')" -ForegroundColor Yellow
            exit 1
        }
    } else {
        # 生成所有字体
        $fontsToGenerate = $fontConfigs
        Write-Host "`n🎯 目标: 生成所有字体 ($($fontConfigs.Count) 个)" -ForegroundColor Cyan
    }
    
    # 执行生成
    $successList = @()
    $failList = @()
    $totalStart = Get-Date
    
    foreach ($name in $fontsToGenerate.Keys | Sort-Object) {
        $result = Generate-Font -FontName $name -FontConfig $fontsToGenerate[$name]
        if ($result) {
            $successList += $name
        } else {
            $failList += $name
        }
    }
    
    # 输出总结
    $totalEnd = Get-Date
    $totalDuration = ($totalEnd - $totalStart).TotalSeconds
    
    Write-Host "`n╔══════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
    Write-Host "║                        执行结果汇总                           ║" -ForegroundColor Cyan
    Write-Host "╚══════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
    Write-Host "完成时间: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor Yellow
    Write-Host "总耗时: $([math]::Round($totalDuration, 2)) 秒" -ForegroundColor Yellow
    Write-Host ""
    
    Write-Host "✅ 成功: $($successList.Count) 个" -ForegroundColor Green
    if ($successList.Count -gt 0) {
        foreach ($name in $successList) {
            $outputDir = $fontConfigs[$name].OutputDir
            Write-Host "   • $name -> $outputDir" -ForegroundColor Gray
        }
    }
    
    if ($failList.Count -gt 0) {
        Write-Host "`n❌ 失败: $($failList.Count) 个" -ForegroundColor Red
        foreach ($name in $failList) {
            Write-Host "   • $name" -ForegroundColor Red
        }
    }
    
    Write-Host ""
    
    if ($failList.Count -eq 0) {
        Write-Host "🎉 所有字体生成成功！" -ForegroundColor Green
        exit 0
    } else {
        Write-Host "⚠️  部分字体生成失败，请检查上述错误信息" -ForegroundColor Yellow
        exit 1
    }
}

# 解析参数并执行
Main @args

Pop-Location
