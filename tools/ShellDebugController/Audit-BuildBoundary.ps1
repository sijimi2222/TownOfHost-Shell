param([Parameter(Mandatory=$true)][string]$PublicDll,[Parameter(Mandatory=$true)][string]$DevelopmentDll,[string]$BaselineDll)
$ErrorActionPreference='Stop'
$projectRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
[xml]$project=Get-Content -LiteralPath (Join-Path $projectRoot 'TownOfHost.csproj') -Raw
$game=@($project.Project.PropertyGroup.AmongUs | Where-Object {$_})[0]
if(-not $game){$game='C:\Program Files (x86)\Steam\steamapps\common\Among Us'}
Add-Type -Path (Join-Path $game 'BepInEx\core\Mono.Cecil.dll')
$pub=[Mono.Cecil.AssemblyDefinition]::ReadAssembly([IO.Path]::GetFullPath($PublicDll))
$dev=[Mono.Cecil.AssemblyDefinition]::ReadAssembly([IO.Path]::GetFullPath($DevelopmentDll))
$old=if($BaselineDll){[Mono.Cecil.AssemblyDefinition]::ReadAssembly([IO.Path]::GetFullPath($BaselineDll))}else{$null}
$script:count=0
function Assert-Test([string]$name,[bool]$value){if(-not $value){throw "FAIL $name"};$script:count++;Write-Output "PASS $name"}
function All-Types($module){foreach($type in $module.Types){$type;function Nested($t){foreach($child in $t.NestedTypes){$child;Nested $child}};Nested $type}}
function IL($method){
 if(-not $method.HasBody){return ''}
 $instructions=@($method.Body.Instructions);$lines=@($instructions | ForEach-Object {$_.OpCode.Name+' '+[string]$_.Operand})
 for($i=2;$i -lt $instructions.Count;$i++){
  $instruction=$instructions[$i]
  # Compiler-emitted CallerLineNumber/CallerFilePath differ between isolated
  # build folders. Normalize only the final two arguments of Logger calls.
  if($instruction.OpCode.Name -eq 'call' -and $instruction.Operand.DeclaringType.FullName -eq 'TownOfHost.Logger' -and $instructions[$i-1].OpCode.Name -eq 'ldstr' -and $instructions[$i-2].OpCode.Name -like 'ldc.i4*' -and [string]$instructions[$i-1].Operand -match '\.cs$'){
   $lines[$i-2]='caller-line';$lines[$i-1]='caller-source'
  }
 }
 $lines -join "`n"
}
try {
 $publicTypes=@(All-Types $pub.MainModule);$developmentTypes=@(All-Types $dev.MainModule)
 Assert-Test 'public product version remains 2.2.3' ((Get-Item -LiteralPath $PublicDll).VersionInfo.ProductVersion -eq '2.2.3')
 Assert-Test 'development product version remains 2.2.3' ((Get-Item -LiteralPath $DevelopmentDll).VersionInfo.ProductVersion -eq '2.2.3')
 Assert-Test 'public has no controller namespace' (@($publicTypes | Where-Object {$_.FullName -match '^ShellDebugController\.|^TownOfHost\.Development\.'}).Count -eq 0)
 Assert-Test 'public has no controller metadata marker' (@($pub.CustomAttributes | Where-Object {$_.AttributeType.Name -eq 'AssemblyMetadataAttribute' -and $_.ConstructorArguments[0].Value -eq 'ShellDebugController'}).Count -eq 0)
 Assert-Test 'development has debug metadata marker' (@($dev.CustomAttributes | Where-Object {$_.AttributeType.Name -eq 'AssemblyMetadataAttribute' -and $_.ConstructorArguments[0].Value -eq 'ShellDebugController'}).Count -eq 1)
 foreach($name in @('ShellDebugRuntime','NoClipMovement','DebugPipeServer','LocalPipe')){Assert-Test ("development contains "+$name) (@($developmentTypes | Where-Object Name -eq $name).Count -eq 1)}
 Assert-Test 'public contains no named-pipe PInvoke endpoint' (@($publicTypes.Methods | Where-Object {$_.HasPInvokeInfo -and $_.PInvokeInfo.EntryPoint -match 'CreateNamedPipe|GetNamedPipeClientProcessId'}).Count -eq 0)
 $publicZoom=($publicTypes | Where-Object FullName -eq 'TownOfHost.Zoom').Methods | Where-Object Name -eq 'Postfix'
 $developmentZoom=($developmentTypes | Where-Object FullName -eq 'TownOfHost.Zoom').Methods | Where-Object Name -eq 'Postfix'
 Assert-Test 'public zoom has no living-host eligibility branch' ((IL $publicZoom) -notmatch 'ZoomEnabled|get_AmHost|get_IsGamePublic|StandaloneSteamPC')
 Assert-Test 'development living zoom requires controller lease' ((IL $developmentZoom) -match 'get_ZoomEnabled')
 foreach($assembly in @($pub,$dev)){
  $nova=$assembly.MainModule.Types | Where-Object FullName -eq 'TownOfHost.Roles.Impostor.Nova'
  Assert-Test ("range50 preserved in "+$assembly.MainModule.FileName) ((IL ($nova.Methods|Where-Object Name -eq 'Fire')) -match 'ldc.r4 50')
 }
 # Public CNO and Nova retain exactly the previous verified network/gameplay IL.
 if($old){foreach($name in @('TownOfHost.Modules.CustomNetObject','TownOfHost.Roles.Impostor.Nova','TownOfHost.Roles.Impostor.NovaBeamTile','TownOfHost.Modules.NetworkSafety')){
  $current=$publicTypes|Where-Object FullName -eq $name;$previous=@(All-Types $old.MainModule)|Where-Object FullName -eq $name
  $same=$true
  foreach($method in $previous.Methods){$matching=$current.Methods|Where-Object {$_.FullName -eq $method.FullName};if(@($matching).Count -ne 1 -or (IL $matching) -ne (IL $method)){$same=$false;break}}
  Assert-Test ("public preserves gameplay/network IL (Logger caller data normalized): "+$name) $same
 }}
 Write-Output "BOUNDARY_RESULT $script:count passed, 0 failed"
}finally{$pub.Dispose();$dev.Dispose();if($old){$old.Dispose()}}
