#requires -Version 5.1
<# Export fixed diagnostic fields only, never raw surrounding text. #>
param([Parameter(Mandatory=$true)][string]$LogPath, [Parameter(Mandatory=$true)][string]$OutputPath)
$ErrorActionPreference = 'Stop'
try {
    $source = [IO.Path]::GetFullPath($LogPath); $target = [IO.Path]::GetFullPath($OutputPath)
    if ($source -ieq $target -or (Test-Path -LiteralPath $target)) { throw 'Refuse overwrite' }
    $oauthStage = 'Unknown Restore Starting BrowserOpen WaitingForRedirect CallbackValidation TokenExchange TokenValidation ProtectedSave ProtectedCleanup Linked'.Split(' ')
    $oauthCode = 'None InvalidClient InvalidRequest InvalidGrant UnauthorizedClient AccessDenied InvalidScope RedirectMismatch PolicyBlocked RateLimited ServerError OtherGoogleError StateMismatch InvalidCallback TimedOut NetworkFailure InvalidTokenResponse ScopeMismatch MissingRefreshToken StorageFailure Cancelled UnexpectedFailure ClientSecretRequired ClientIdRequired AuthorizationCodeRequired RedirectUriRequired GrantTypeRequired CodeVerifierRequired'.Split(' ')
    $chatStage = 'Unknown Credential VideosRequest VideosResponse ActiveLiveChatId StreamRequest StreamHeaders GrpcStatus Receive Parse Retry Stop'.Split(' ')
    $chatCode = 'Unknown Started Completed HeadersReceived Http2Established HttpRejected UnsupportedTransport GrpcOk GrpcRejected MissingGrpcStatus InvalidPayload ApiFailure NetworkFailure Timeout Cancelled Renewal EndOfStream RetryScheduled RetryExhausted TerminalFailure IdleBackoff'.Split(' ')
    $kind = 'None Unknown HttpRequest HttpProtocol Socket TlsAuthentication IO Cancellation Json Api NotSupported InvalidOperation Unexpected'.Split(' ')
    $reason = 'None Unknown MissingCredentials AuthenticationExpired PermissionDenied QuotaExceeded NotLive LiveEnded ChatUnavailable NotFound InvalidResponse TransportUnsupported NetworkInterrupted BufferOverflow CredentialStorageFailure'.Split(' ')
    $socket = @('None','Unknown') + [Enum]::GetNames([Net.Sockets.SocketError])
    $rows = New-Object 'Collections.Generic.List[string]'
    foreach ($line in [IO.File]::ReadLines($source)) {
        $m = [regex]::Match($line, 'YOUTUBE_OAUTH stage=(\w+) code=(\w+) http=(\d{1,3})(?=\s|$)')
        if ($m.Success -and $m.Groups[1].Value -cin $oauthStage -and $m.Groups[2].Value -cin $oauthCode -and [int]$m.Groups[3].Value -le 599) { $rows.Add($m.Value) }
        $m = [regex]::Match($line, 'YOUTUBE_CHAT stage=(\w+) code=(\w+) http=(\d{1,3}) grpc=(unknown|\d{1,2}) httpMajor=([0-3]) exception=(\w+) cause=(\w+) elapsedMs=(\d{1,8}) totalMs=(\d{1,8}) attempt=([0-4]) retryDelaySec=(\d{1,4}) reason=(\w+) socket=(\w+)(?=\s|$)')
        if ($m.Success -and $m.Groups[1].Value -cin $chatStage -and $m.Groups[2].Value -cin $chatCode -and
            [int]$m.Groups[3].Value -le 599 -and ($m.Groups[4].Value -ceq 'unknown' -or [int]$m.Groups[4].Value -le 16) -and
            $m.Groups[6].Value -cin $kind -and $m.Groups[7].Value -cin $kind -and [long]$m.Groups[8].Value -le 86400000 -and
            [long]$m.Groups[9].Value -le 86400000 -and [int]$m.Groups[11].Value -le 3600 -and
            $m.Groups[12].Value -cin $reason -and $m.Groups[13].Value -cin $socket) { $rows.Add($m.Value) }
        if ($rows.Count -ge 2000) { break }
    }
    $file = [IO.FileStream]::new($target,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
    try { $bytes = [Text.Encoding]::UTF8.GetBytes(($rows -join "`r`n")); $file.Write($bytes,0,$bytes.Length) } finally { $file.Dispose() }
    Write-Output ('YOUTUBE_DIAGNOSTIC_EXPORT lines=' + $rows.Count)
} catch { Write-Output 'YOUTUBE_DIAGNOSTIC_EXPORT result=Stopped'; exit 1 }
