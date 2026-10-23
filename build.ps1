$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $compiler)) {
    throw '.NET Framework 4.x C# compiler is required on Windows.'
}
Push-Location $PSScriptRoot
try {
    & $compiler /nologo /target:winexe /optimize+ /codepage:65001 /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Core.dll /win32icon:mode-switcher.ico /out:WorkGameModeSwitcher.exe OneKeyModeManager.cs
    if ($LASTEXITCODE -ne 0) { throw 'Application build failed.' }
    & $compiler /nologo /target:winexe /optimize+ /codepage:65001 /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /win32icon:mode-switcher.ico /out:Setup-ModeSwitcher.exe Installer.cs
    if ($LASTEXITCODE -ne 0) { throw 'Installer build failed.' }
    Write-Host 'Built WorkGameModeSwitcher.exe and Setup-ModeSwitcher.exe'
} finally {
    Pop-Location
}
