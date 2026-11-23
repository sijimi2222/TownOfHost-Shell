param([string]$BaselineDll,[switch]$SkipPublicAudit)
$ErrorActionPreference='Stop'
if($SkipPublicAudit){throw 'Public build-boundary auditing cannot be skipped.'}
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$project=Join-Path $root 'TownOfHost.csproj'
$argsCommon=@('-p:AllowIsolatedRoleBuild=true','-p:CopyToAmongUs=false','-p:NuGetAudit=false','-p:UseSharedCompilation=false','-p:GitInfoDependsOn=_GitReadCache','-p:MatchmakingDevelopment=false')
$output=Join-Path $root ('artifacts\debug-controller\'+(Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
if(Test-Path -LiteralPath $output){throw 'Output already exists.'}
$null=New-Item -ItemType Directory -Path $output
foreach($configuration in @('Release','ShellDevelopment')){
 & dotnet build $project -c $configuration @argsCommon
 if($LASTEXITCODE -ne 0){throw "$configuration build failed. No DLL is installed."}
}
& dotnet publish (Join-Path $PSScriptRoot 'ShellDebugController.csproj') -c Release --self-contained false -o (Join-Path $output 'app') @argsCommon
if($LASTEXITCODE -ne 0){throw 'Controller app publish failed.'}
$public=Join-Path $root 'bin\Release\net6.0\TownOfHost-Shell.dll'
$development=Join-Path $root 'bin\ShellDevelopment\net6.0\TownOfHost-Shell.dll'
& (Join-Path $PSScriptRoot 'Audit-BuildBoundary.ps1') -PublicDll $public -DevelopmentDll $development -BaselineDll $BaselineDll
foreach($pair in @(@('public',$public),@('development',$development))){$folder=Join-Path $output $pair[0];$null=New-Item -ItemType Directory -Path $folder;Copy-Item -LiteralPath $pair[1] -Destination (Join-Path $folder 'TownOfHost-Shell.dll')}
Get-ChildItem -LiteralPath $output -Recurse -Filter '*.dll' -File | ForEach-Object {Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256}
$pointer=Join-Path $root 'artifacts\debug-controller\latest-app.local.txt'
(Join-Path $output 'app\ShellDebugController.exe') | Set-Content -LiteralPath $pointer -Encoding utf8
Write-Output "READY_LOCAL_ONLY $output"
Write-Output 'No game launch, game DLL install, Git operation or release was performed.'
