[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ComposeFile,
    [string]$ProjectName = '',
    [string[]]$Services = @('postgres','foundgine-warm'),
    [Parameter(Mandatory)][string]$OutputCsv,
    [Parameter(Mandatory)][string]$StopFile,
    [int]$IntervalMs = 1000
)

$ErrorActionPreference = 'SilentlyContinue'
$dir = Split-Path -Parent $OutputCsv
New-Item -ItemType Directory -Force -Path $dir | Out-Null
if (Test-Path $OutputCsv) { Remove-Item $OutputCsv -Force }

$header = 'TimestampUtc,Service,ContainerId,ContainerName,CpuPercent,MemoryUsageBytes,MemoryLimitBytes,MemoryPercent,NetRxBytes,NetTxBytes,BlockReadBytes,BlockWriteBytes,Pids'
Set-Content -Path $OutputCsv -Value $header -Encoding utf8

function Invoke-ComposePs {
    param([string]$Service)
    if ([string]::IsNullOrWhiteSpace($ProjectName)) {
        & docker compose -f $ComposeFile ps -q $Service 2>$null
    } else {
        & docker compose -p $ProjectName -f $ComposeFile ps -q $Service 2>$null
    }
}

while (-not (Test-Path $StopFile)) {
    $timestamp = [DateTimeOffset]::UtcNow.ToString('o')
    foreach ($service in $Services) {
        $id = (Invoke-ComposePs $service | Select-Object -First 1).Trim()
        if ([string]::IsNullOrWhiteSpace($id)) { continue }
        $line = & docker stats --no-stream --format '{{.ID}}|{{.Name}}|{{.CPUPerc}}|{{.MemUsage}}|{{.MemPerc}}|{{.NetIO}}|{{.BlockIO}}|{{.PIDs}}' $id 2>$null | Select-Object -First 1
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        $p = $line -split '\|'
        if ($p.Count -lt 8) { continue }
        $cpu = ($p[2] -replace '%','').Trim()
        $mem = $p[3] -split '/'
        $memUsed = ($mem[0].Trim() -replace '[^0-9.]','')
        $memLimit = ($mem[1].Trim() -replace '[^0-9.]','')
        $memUnitUsed = if ($mem[0] -match 'GiB') { 1GB } elseif ($mem[0] -match 'MiB') { 1MB } elseif ($mem[0] -match 'KiB') { 1KB } else { 1 }
        $memUnitLimit = if ($mem[1] -match 'GiB') { 1GB } elseif ($mem[1] -match 'MiB') { 1MB } elseif ($mem[1] -match 'KiB') { 1KB } else { 1 }
        $memUsedBytes = [double]$memUsed * $memUnitUsed
        $memLimitBytes = [double]$memLimit * $memUnitLimit
        $net = $p[5] -split '/'
        $block = $p[6] -split '/'
        $rx = Convert-SizeToBytes $net[0]
        $tx = Convert-SizeToBytes $net[1]
        $br = Convert-SizeToBytes $block[0]
        $bw = Convert-SizeToBytes $block[1]
        $name = $p[1]
        $safe = @($timestamp,$service,$id,$name,$cpu,$memUsedBytes,$memLimitBytes,(($p[4] -replace '%','').Trim()),$rx,$tx,$br,$bw,$p[7]) -join ','
        Add-Content -Path $OutputCsv -Value $safe -Encoding utf8
    }
    Start-Sleep -Milliseconds $IntervalMs
}

function Convert-SizeToBytes([string]$value) {
    if ([string]::IsNullOrWhiteSpace($value)) { return 0 }
    $v = ($value.Trim() -replace ',','')
    if ($v -match '^([0-9.]+)\s*(B|kB|KB|KiB|MB|MiB|GB|GiB)$') {
        $n = [double]$Matches[1]
        switch ($Matches[2].ToUpperInvariant()) {
            'B' { return $n }
            'KB' { return $n * 1000 }
            'KIB' { return $n * 1024 }
            'MB' { return $n * 1000 * 1000 }
            'MIB' { return $n * 1024 * 1024 }
            'GB' { return $n * 1000 * 1000 * 1000 }
            'GIB' { return $n * 1024 * 1024 * 1024 }
        }
    }
    return 0
}
