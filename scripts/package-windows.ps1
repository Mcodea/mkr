param(
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [string]$Python = 'python'
)
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
if (Test-Path $OutputDirectory) { throw 'Choose a new output directory.' }
$package = New-Item -ItemType Directory -Path $OutputDirectory
$runtime = Join-Path $package.FullName 'python'
$pythonRoot = (& $Python -I -c 'import sys; print(sys.base_prefix)').Trim()
$version = (& $Python -I -c 'import platform; print(platform.python_version())').Trim()
if ($version -notmatch '^3\.12\.' -or !(Test-Path "$pythonRoot/python312.dll")) {
    throw 'MKR packaging requires a Windows CPython 3.12 installation.'
}
dotnet publish src/Mkr.Desktop/Mkr.Desktop.csproj -c Release -r win-x64 --self-contained true -o $package.FullName
New-Item -ItemType Directory -Path $runtime | Out-Null
foreach ($pattern in @('python.exe','python3.dll','python312.dll','vcruntime*.dll','LICENSE*')) {
    Copy-Item (Join-Path $pythonRoot $pattern) $runtime
}
Copy-Item -Recurse "$pythonRoot/DLLs" $runtime
$lib = New-Item -ItemType Directory -Path "$runtime/Lib"
Get-ChildItem "$pythonRoot/Lib" | Where-Object { $_.Name -notin @('site-packages','__pycache__','test','idlelib','tkinter') } |
    Copy-Item -Destination $lib.FullName -Recurse
& $Python -I -m pip install --disable-pip-version-check --only-binary=:all: --no-deps --no-compile --target "$lib/site-packages" -r engine/requirements.txt
$env:PYTHONNOUSERSITE = '1'
& "$runtime/python.exe" -I -c 'import sys, pathlib, scipy.optimize, openpyxl, zoneinfo; zoneinfo.ZoneInfo('Europe/Paris'); assert pathlib.Path(scipy.__file__).is_relative_to(pathlib.Path(sys.executable).parent)'
$reply = '{"version":1,"requestId":"portable","operation":"ping","payload":{}}' | & "$runtime/python.exe" -I "$package/engine/mkr_engine.py" | ConvertFrom-Json
if ($reply.requestId -ne 'portable' -or !$reply.engineVersion -or $reply.error) { throw 'Bundled engine did not answer ping.' }
$transition = @{
    version = 1
    requestId = 'portable-transfer'
    operation = 'simulate_pi_transition'
    payload = @{
        gain = 2; tauSeconds = 4; delaySeconds = 0; sampleSeconds = 0.1
        durationSeconds = 2; setpoint = 1; minimumMv = -2; maximumMv = 2
        manualMv = 9; automaticAtSeconds = 1; kp = 0.5; tiSeconds = 4
    }
}
$transfer = $transition | ConvertTo-Json -Compress | & "$runtime/python.exe" -I "$package/engine/mkr_engine.py" | ConvertFrom-Json
if ($transfer.error -or $transfer.requestId -ne 'portable-transfer' -or $transfer.result.points.Count -ne 21) {
    throw 'The packaged PI transition returned an invalid response.'
}
$points = $transfer.result.points
if ($points[9].mode -ne 'manual' -or $points[9].mv -ne 2 -or
    $points[10].mode -ne 'automatic' -or $points[10].cv -ne $points[9].mv -or
    $points[10].mv -ne $points[9].mv) {
    throw 'The packaged PI transition failed to track the saturated manual output.'
}
[ordered]@{ commit = (git rev-parse HEAD); python = $version; dotnetSdk = (dotnet --version); engine = $reply.engineVersion } |
    ConvertTo-Json | Set-Content "$package/build-info.json" -Encoding utf8
