while ($true) {
    $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
    $promptFile = Join-Path $scriptDir "Prompt.md"
    
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = "copilot"
    $psi.Arguments = "-i `"$promptFile`" --allow-all-tools"
    $psi.UseShellExecute = $false
    $psi.RedirectStandardOutput = $true
    $psi.WorkingDirectory = $scriptDir
    
    $process = [System.Diagnostics.Process]::Start($psi)
    
    # Stream output with timeout
    $timeout = [TimeSpan]::FromMinutes(15)
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    
    while (!$process.StandardOutput.EndOfStream -and $stopwatch.Elapsed -lt $timeout) {
        $line = $process.StandardOutput.ReadLine()
        Write-Host $line
    }
    
    if ($stopwatch.Elapsed -ge $timeout) {
        $process.Kill()
        Write-Host "TIMEOUT TRIGGERED" -ForegroundColor Red
    }
    
    $process.WaitForExit()
    
    Write-Host "`n`n========================= LOOP =========================`n`n"
    Start-Sleep -Seconds 10
}