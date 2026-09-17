param([Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$manifest = Get-Content (Join-Path $PSScriptRoot 'gaming-dependencies.json') -Raw | ConvertFrom-Json
$temp = Join-Path ([IO.Path]::GetTempPath()) ([Guid]::NewGuid().ToString())
New-Item -ItemType Directory $temp | Out-Null
try {
    foreach ($item in $manifest) {
        $download = Join-Path $temp 'dependency'
        Invoke-WebRequest -Uri $item.url -OutFile $download -UseBasicParsing
        if ((Get-FileHash $download -Algorithm SHA256).Hash -ne $item.sha256) { throw "Dependency checksum mismatch: $($item.url)" }
        foreach ($file in $item.files) {
            $destination = Join-Path $OutputDirectory $file.target
            New-Item -ItemType Directory -Force (Split-Path $destination -Parent) | Out-Null
            if ($file.source) {
                $archive = [IO.Compression.ZipFile]::OpenRead($download)
                try { [IO.Compression.ZipFileExtensions]::ExtractToFile($archive.GetEntry($file.source), $destination, $true) }
                finally { $archive.Dispose() }
            } else { Copy-Item $download $destination -Force }
        }
    }
} finally { Remove-Item $temp -Recurse -Force }
Write-Host 'PresentMon and Discord Voice dependencies verified and packaged.'
