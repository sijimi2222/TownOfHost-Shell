#requires -Version 5.1
<# Offline only. Never pass a secret on the command line. Reads the Google
   Desktop download, checks the existing client ID, then stores DPAPI ciphertext.
   Does not edit oauth-client.json, tokens, game settings, or the Steam DLL. #>
param(
    [Parameter(Mandatory=$true)][string]$GoogleClientJson,
    [Parameter(Mandatory=$true)][string]$ClientConfiguration,
    [string]$CredentialDirectory = (Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'TOH-Shell\YouTube'),
    [switch]$ReplaceExisting
)
$ErrorActionPreference = 'Stop'
$plain = $null; $encrypted = $null; $entropy = $null; $temporary = $null; $stage = 'ValidateFiles'
try {
    if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) { throw 'Unavailable' }
    Add-Type -AssemblyName System.Security
    foreach ($entry in @(@($GoogleClientJson,32768),@($ClientConfiguration,4096))) {
        $file = Get-Item -LiteralPath $entry[0] -Force
        if ($file.PSIsContainer -or $file.Length -gt $entry[1] -or ($file.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Invalid configuration' }
    }
    $stage = 'ParseConfiguration'
    $google = [IO.File]::ReadAllText((Get-Item -LiteralPath $GoogleClientJson).FullName) | ConvertFrom-Json
    $configured = [IO.File]::ReadAllText((Get-Item -LiteralPath $ClientConfiguration).FullName) | ConvertFrom-Json
    if (@($google.PSObject.Properties).Count -ne 1 -or !$google.installed -or @($configured.PSObject.Properties).Count -ne 1 -or !$configured.client_id) { throw 'Invalid configuration' }
    $stage = 'ValidatePair'
    $id = $google.installed.client_id; $secret = $google.installed.client_secret
    if ($id -isnot [string] -or $id -cne $configured.client_id -or $id -cnotmatch '\A[A-Za-z0-9._-]{1,960}\.apps\.googleusercontent\.com\z' -or $secret -isnot [string] -or $secret -cnotmatch '\A[\x21-\x7e]{1,4096}\z') { throw 'Invalid configuration' }
    $stage = 'PrepareStorage'
    if (![IO.Path]::IsPathRooted($CredentialDirectory)) { throw 'Invalid output directory' }
    $folder = [IO.Path]::GetFullPath($CredentialDirectory)
    if (!(Test-Path -LiteralPath $folder)) { [IO.Directory]::CreateDirectory($folder) | Out-Null }
    if ((Get-Item -LiteralPath $folder -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Unsafe output directory' }
    $destination = Join-Path $folder 'client-secret.dpapi'
    if (Test-Path -LiteralPath $destination) {
        if ((Get-Item -LiteralPath $destination -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Unsafe output file' }
        if (!$ReplaceExisting) { throw 'Already configured; explicit replacement required' }
    }
    $stage = 'ApplyDirectoryAcl'
    $sid = [Security.Principal.WindowsIdentity]::GetCurrent().User
    $directoryAcl = New-Object Security.AccessControl.DirectorySecurity
    $directoryAcl.SetAccessRuleProtection($true,$false)
    $directoryAcl.AddAccessRule((New-Object Security.AccessControl.FileSystemAccessRule($sid,'FullControl','ContainerInherit,ObjectInherit','None','Allow')))
    ([IO.DirectoryInfo]::new($folder)).SetAccessControl($directoryAcl)
    $fileAcl = New-Object Security.AccessControl.FileSecurity
    $fileAcl.SetAccessRuleProtection($true,$false)
    $fileAcl.AddAccessRule((New-Object Security.AccessControl.FileSystemAccessRule($sid,'FullControl','Allow')))
    $stage = 'Protect'
    $payload = @{kind='desktop-client-secret-v1';client_id=$id;client_secret=$secret} | ConvertTo-Json -Compress
    $plain = [Text.Encoding]::UTF8.GetBytes($payload)
    if ($plain.Length -gt 8192) { throw 'Invalid protected payload' }
    $entropy = [Text.Encoding]::UTF8.GetBytes('TOH-Shell.YouTube.DesktopOAuth.v1')
    $encrypted = [Security.Cryptography.ProtectedData]::Protect($plain,$entropy,[Security.Cryptography.DataProtectionScope]::CurrentUser)
    if ($encrypted.Length -gt 65536) { throw 'Invalid protected output' }
    $stage = 'Write'
    $temporary = Join-Path $folder ('.client-secret-'+[Guid]::NewGuid().ToString('N')+'.tmp')
    $stream = New-Object IO.FileStream($temporary,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
    try { $stream.Write($encrypted,0,$encrypted.Length); $stream.Flush($true) } finally { $stream.Dispose() }
    ([IO.FileInfo]::new($temporary)).SetAccessControl($fileAcl)
    if (Test-Path -LiteralPath $destination) {
        ([IO.FileInfo]::new($destination)).SetAccessControl($fileAcl)
        [IO.File]::Replace($temporary,$destination,[NullString]::Value)
    } else { [IO.File]::Move($temporary,$destination) }
    $temporary = $null
    Write-Output 'Desktop client configuration imported with current-user DPAPI. Restart the game before linking. No credentials are printed.'
} catch {
    Write-Output ('IMPORT_CLIENT stage=' + $stage)
    Write-Error 'Import stopped. Check the Desktop JSON, matching client ID, Windows user, file permissions, and whether explicit replacement is needed. Existing game settings and tokens were not edited.' -ErrorAction Continue
    exit 1
} finally {
    foreach ($bytes in @($plain,$encrypted,$entropy)) { if ($null -ne $bytes) { [Array]::Clear($bytes,0,$bytes.Length) } }
    if ($temporary -and (Test-Path -LiteralPath $temporary)) { Remove-Item -LiteralPath $temporary -Force -ErrorAction SilentlyContinue }
    $secret = $null; $payload = $null; $google = $null
}
