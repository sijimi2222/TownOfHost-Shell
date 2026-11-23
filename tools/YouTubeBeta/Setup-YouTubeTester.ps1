#requires -Version 5.1
<# Offline provisioning only, AFTER written permission for the proposed delivery
   has been confirmed. An operator record is not itself permission from Google.
   This script never installs a DLL, starts a game, or authenticates to Google. #>
param(
    [string]$GoogleClientJson,
    [string]$GameDirectory,
    [string]$ApprovalRecord = (Join-Path $PSScriptRoot 'tester-authorization.local.json'),
    [string]$CredentialDirectory = (Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'TOH-Shell\YouTube'),
    [switch]$ReplaceClientSecret
)
$ErrorActionPreference = 'Stop'
$stage = 'Approval'; $temporary = $null; $google = $null
function Read-SafeJson([string]$path, [long]$maximum) {
    $file = Get-Item -LiteralPath $path -Force
    if ($file.PSIsContainer -or $file.Length -gt $maximum -or ($file.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Rejected' }
    return ([IO.File]::ReadAllText($file.FullName) | ConvertFrom-Json)
}
function Check-Parents([string]$path) {
    $item = [IO.DirectoryInfo]::new([IO.Path]::GetFullPath($path))
    while ($item) {
        if ($item.Exists -and ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Rejected' }
        $item = $item.Parent
    }
}
try {
    if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) { throw 'Unsupported' }
    $approval = Read-SafeJson $ApprovalRecord 4096
    if ($approval.purpose -cne 'youtube-desktop-credential-provisioning' -or $approval.decision -cne 'approved' -or
        $approval.reference -isnot [string] -or [string]::IsNullOrWhiteSpace($approval.reference) -or $approval.reference.Length -gt 256) { throw 'Not approved' }
    # No installer/user checkbox can grant external permission; the developer must
    # verify this reference and the actual tester/delivery conditions beforehand.
    $stage = 'Selection'
    if (!$GoogleClientJson -or !$GameDirectory) {
        Add-Type -AssemblyName System.Windows.Forms
        if (!$GoogleClientJson) {
            $pick = New-Object Windows.Forms.OpenFileDialog
            try { $pick.Title = 'Select the approved Desktop OAuth JSON'; $pick.Filter = 'JSON (*.json)|*.json'; if ($pick.ShowDialog() -ne 'OK') { throw 'Cancelled' }; $GoogleClientJson = $pick.FileName } finally { $pick.Dispose() }
        }
        if (!$GameDirectory) {
            $pick = New-Object Windows.Forms.FolderBrowserDialog
            try { $pick.Description = 'Select your test Among Us folder (contains Among Us.exe and BepInEx)'; if ($pick.ShowDialog() -ne 'OK') { throw 'Cancelled' }; $GameDirectory = $pick.SelectedPath } finally { $pick.Dispose() }
        }
    }
    $stage = 'Validate'
    if (![IO.Path]::IsPathRooted($GameDirectory) -or ![IO.Path]::IsPathRooted($CredentialDirectory)) { throw 'Rejected' }
    $GameDirectory = [IO.Path]::GetFullPath($GameDirectory)
    if (!(Test-Path -LiteralPath (Join-Path $GameDirectory 'Among Us.exe') -PathType Leaf) -or !(Test-Path -LiteralPath (Join-Path $GameDirectory 'BepInEx\plugins') -PathType Container)) { throw 'Rejected' }
    if (Get-Process -Name 'Among Us' -ErrorAction SilentlyContinue) { throw 'Game is running' }
    $configFolder = Join-Path $GameDirectory 'BepInEx\config\TOH-Shell\YouTube'
    Check-Parents $configFolder; Check-Parents $CredentialDirectory
    $google = Read-SafeJson $GoogleClientJson 32768
    if (@($google.PSObject.Properties).Count -ne 1 -or !$google.installed) { throw 'Rejected' }
    $id = $google.installed.client_id; $secret = $google.installed.client_secret
    if ($id -isnot [string] -or $id -cnotmatch '\A[A-Za-z0-9._-]{1,960}\.apps\.googleusercontent\.com\z' -or
        $secret -isnot [string] -or $secret -cnotmatch '\A[\x21-\x7e]{1,4096}\z') { throw 'Rejected' }
    $configuration = Join-Path $configFolder 'oauth-client.json'
    if (Test-Path -LiteralPath $configuration) {
        $existing = Read-SafeJson $configuration 4096
        if (@($existing.PSObject.Properties).Count -ne 1 -or $existing.client_id -cne $id) { throw 'Client mismatch' }
    } elseif (Test-Path -LiteralPath (Join-Path $CredentialDirectory 'oauth.dpapi')) { throw 'Existing grant has no matching client configuration' }
    $stage = 'Protect'
    $import = Join-Path $PSScriptRoot 'Import-YouTubeDesktopClient.ps1'
    if (!(Test-Path -LiteralPath $import)) { $import = Join-Path $PSScriptRoot '..\Import-YouTubeDesktopClient.ps1' }
    if (!(Test-Path -LiteralPath $import -PathType Leaf)) { throw 'Importer unavailable' }
    # The temporary file contains only a public client identifier, never the secret.
    $temporary = [IO.Path]::GetTempFileName()
    $clientJson = @{client_id=$id} | ConvertTo-Json -Compress
    [IO.File]::WriteAllText($temporary, $clientJson, [Text.UTF8Encoding]::new($false))
    $global:LASTEXITCODE = 0
    & $import -GoogleClientJson $GoogleClientJson -ClientConfiguration $temporary -CredentialDirectory $CredentialDirectory -ReplaceExisting:$ReplaceClientSecret
    if ($LASTEXITCODE -ne 0) { throw 'Import failed' }
    $stage = 'ClientConfiguration'
    if (!(Test-Path -LiteralPath $configuration)) {
        [IO.Directory]::CreateDirectory($configFolder) | Out-Null
        # CreateNew prevents overwriting a file created by another process meanwhile.
        $file = [IO.FileStream]::new($configuration, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
        try { $bytes = [Text.Encoding]::UTF8.GetBytes($clientJson); $file.Write($bytes,0,$bytes.Length); $file.Flush($true) } finally { $file.Dispose() }
    }
    Write-Output 'TESTER_SETUP result=Configured (current-user DPAPI; existing tokens/settings unchanged; no network)'
} catch {
    Write-Output ('TESTER_SETUP result=Stopped stage=' + $stage)
    Write-Output 'No automatic authentication or DLL installation was performed. Ask the developer using only the fixed stage above.'
    exit 1
} finally {
    if ($temporary -and (Test-Path -LiteralPath $temporary)) { Remove-Item -LiteralPath $temporary -Force -ErrorAction SilentlyContinue }
    $google = $null; $secret = $null; $id = $null; $clientJson = $null
}
