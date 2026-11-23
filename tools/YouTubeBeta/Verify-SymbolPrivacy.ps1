param([Parameter(Mandatory=$true)][string]$BuildVerification, [Parameter(Mandatory=$true)][string]$Output)
$ErrorActionPreference = 'Stop'
Add-Type -Path (Join-Path $env:USERPROFILE '.nuget\packages\fody\6.9.3\tasks\netstandard2.0\Mono.Cecil.dll')
$rows = Get-Content -LiteralPath $BuildVerification -Raw | ConvertFrom-Json
foreach ($row in $rows) {
    $options = [Mono.Cecil.ReaderParameters]::new(); $options.ReadSymbols = $true
    $options.SymbolReaderProvider = [Mono.Cecil.Cil.EmbeddedPortablePdbReaderProvider]::new()
    $module = [Mono.Cecil.ModuleDefinition]::ReadModule($row.Dll,$options)
    try {
        $count = 0; $unsafe = 0
        foreach ($type in $module.GetTypes()) {
            foreach ($method in $type.Methods) {
                foreach ($point in $method.DebugInformation.SequencePoints) {
                    $count++
                    if ($point.Document.Url -match '(?i)([A-Z]:[\\/](Users|Documents and Settings)[\\/]|/Users/|/home/)') { $unsafe++ }
                }
                if ($method.HasBody) {
                    foreach ($instruction in $method.Body.Instructions) {
                        if ($instruction.OpCode.Name -eq 'ldstr' -and [string]$instruction.Operand -match '(?i)([A-Z]:[\\/](Users|Documents and Settings)[\\/]|/Users/|/home/)') { $unsafe++ }
                    }
                }
            }
        }
        if ($count -eq 0 -or $unsafe -ne 0) { throw 'Distribution symbol privacy verification failed (values withheld)' }
        $row | Add-Member -NotePropertyName DistributionSymbolsSanitized -NotePropertyValue $true -Force
        Write-Output ('SYMBOL_PRIVACY configuration=' + $row.Configuration + ' result=Passed')
    } finally { $module.Dispose() }
}
$rows | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $Output -Encoding UTF8
