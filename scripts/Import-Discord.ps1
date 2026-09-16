param([string]$SourcePath = 'E:\CodeProjects\discord-overlay')
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Security
$sourceConfig = @{}
$jsonPath = Join-Path $SourcePath 'config.json'
if (Test-Path $jsonPath) { $sourceConfig = Get-Content -LiteralPath $jsonPath -Raw | ConvertFrom-Json }
$envValues = @{}
$envPath = Join-Path $SourcePath 'services\discord-service\.env'
if (Test-Path $envPath) {
    foreach ($line in Get-Content -LiteralPath $envPath) {
        if ($line -match '^\s*(?:export\s+)?([A-Za-z_][A-Za-z_0-9]*)\s*=\s*(.*?)\s*$') {
            $key = $matches[1]; $value = $matches[2]
            if ($value.StartsWith('"') -or $value.StartsWith("'")) {
                $quote = $value.Substring(0, 1)
                $end = $value.IndexOf($quote, 1)
                if ($end -gt 0) { $value = $value.Substring(1, $end - 1) }
            } else { $value = ($value -replace '\s+#.*$', '').Trim() }
            $envValues[$key] = $value
        }
    }
}
function Get-Setting($name, $fallback) { if ($envValues[$name]) { return $envValues[$name] }; return $fallback }
$credentials = @{
    botToken = Get-Setting 'DISCORD_BOT_TOKEN' $sourceConfig.botToken
    guildId = Get-Setting 'GUILD_ID' $sourceConfig.guildId
    voiceChannelId = Get-Setting 'VOICE_CHANNEL_ID' $sourceConfig.voiceChannelId
}
if (-not $credentials.botToken -or $credentials.guildId -notmatch '^\d+$' -or $credentials.voiceChannelId -notmatch '^\d+$') {
    throw 'Token, server o canale Discord mancanti o non validi nel progetto sorgente.'
}
$dataDir = if ($env:PULSEDECK_DATA_DIR) { $env:PULSEDECK_DATA_DIR } else { Join-Path $env:LOCALAPPDATA 'PulseDeck' }
New-Item -ItemType Directory -Path $dataDir -Force | Out-Null
$destination = Join-Path $dataDir 'discord.credentials'
$clear = [Text.Encoding]::UTF8.GetBytes(($credentials | ConvertTo-Json -Compress))
try {
    $encrypted = [Security.Cryptography.ProtectedData]::Protect($clear, $null, [Security.Cryptography.DataProtectionScope]::CurrentUser)
    [IO.File]::WriteAllBytes("$destination.tmp", $encrypted)
    if (Test-Path $destination) { Copy-Item -LiteralPath $destination -Destination "$destination.bak" -Force }
    Move-Item -LiteralPath "$destination.tmp" -Destination $destination -Force
} finally { [Array]::Clear($clear, 0, $clear.Length) }
# Preserve existing settings. Import the tracked user only if no selection exists yet.
$settingsPath = Join-Path $dataDir 'config.json'
if (Test-Path $settingsPath) {
    $settings = Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json
    $settings | Add-Member -NotePropertyName discordMode -NotePropertyValue 'embedded' -Force
    if (-not $settings.trackedMemberId -and $sourceConfig.trackedMember.mode -eq 'id') {
        $settings | Add-Member -NotePropertyName trackedMemberId -NotePropertyValue ([string]$sourceConfig.trackedMember.value) -Force
    }
    $settings | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath "$settingsPath.tmp" -Encoding UTF8
    Copy-Item -LiteralPath $settingsPath -Destination "$settingsPath.bak" -Force
    Move-Item -LiteralPath "$settingsPath.tmp" -Destination $settingsPath -Force
}
Write-Host 'Configurazione Discord importata e protetta per questo utente Windows. Riavvia PulseDeck.'
