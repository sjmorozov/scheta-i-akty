param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot 'dist'),
    [string]$FileName = 'Счета.exe',
    [string]$DefaultsFile
)

$ErrorActionPreference = 'Stop'
if ($FileName -ne [IO.Path]::GetFileName($FileName) -or
    -not $FileName.EndsWith('.exe', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Укажи имя файла с расширением .exe.'
}

$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
if (-not (Test-Path -LiteralPath (Join-Path $framework 'csc.exe'))) {
    $framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319'
}
$compiler = Join-Path $framework 'csc.exe'
if (-not (Test-Path -LiteralPath $compiler -PathType Leaf)) {
    throw 'Не найден встроенный компилятор .NET Framework.'
}

$output = (New-Item -ItemType Directory -Path $OutputDirectory -Force).FullName
$exe = Join-Path $output $FileName
$arguments = @(
    '/nologo', '/target:winexe', '/optimize+', '/platform:anycpu', '/codepage:65001',
    ('/out:' + $exe),
    '/reference:System.Drawing.dll', '/reference:System.Windows.Forms.dll',
    '/reference:System.Runtime.Serialization.dll', '/reference:System.Xml.dll',
    ('/reference:' + (Join-Path $framework 'WPF\WindowsBase.dll')),
    ('/reference:' + (Join-Path $framework 'WPF\PresentationCore.dll'))
)
if ($DefaultsFile) {
    $resolvedDefaults = (Resolve-Path -LiteralPath $DefaultsFile).ProviderPath
    $arguments += ('/resource:' + $resolvedDefaults + ',defaults.json')
}
$arguments += @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' -File |
    Sort-Object Name | Select-Object -ExpandProperty FullName)

& $compiler @arguments
if ($LASTEXITCODE -ne 0) {
    throw "Сборка не удалась. Код ошибки $LASTEXITCODE."
}
Get-Item -LiteralPath $exe
