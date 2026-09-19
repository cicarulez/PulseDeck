param([ValidateSet("mail", "calendar")][string]$Kind = "mail")
$ErrorActionPreference = 'Stop'
$base = 'http://127.0.0.1:5178'
$headers = @{ 'X-PulseDeck-Client' = 'configurator' }
$display = Invoke-RestMethod "$base/api/display"
$state = Invoke-RestMethod "$base/api/state"
if (-not $display.connected -or $display.userDisconnected -or $display.deviceId -ne 'chs_88inch.dev1_rom1.90' -or $state.profile -ne 'desktop') {
    throw 'The verified panel must already be connected in Recon / Desktop.'
}
$pids = @(Get-Process PulseDeck.Agent | Select-Object -ExpandProperty Id)
$initial = Invoke-RestMethod "$base/api/notifications/diagnostics"
$frames = $display.acknowledgedFrames
$recoveries = $display.recoveries
$transfers = @()
$observedCalendar = $false
$observedThree = $false
$observedZero = $false
$backgroundTicks = @{}
for ($cycle = 1; $cycle -le 3; $cycle++) {
    Invoke-RestMethod "$base/api/notifications/test?kind=$Kind" -Method Post -Headers $headers | Out-Null
    $watch = [Diagnostics.Stopwatch]::StartNew()
    while ($watch.Elapsed.TotalSeconds -lt 9) {
        Start-Sleep -Milliseconds 250
        $s = Invoke-RestMethod "$base/api/state" -TimeoutSec 5
        $d = Invoke-RestMethod "$base/api/display" -TimeoutSec 5
        if ($s.notifications.isTest) {
            $backgroundTicks[$s.timestamp] = $true
            if ($s.notifications.arrival.kind -eq 'calendar') { $observedCalendar = $true }
            $count = $s.notifications.sources[0].unreadCount
            if ($count -eq 3) { $observedThree = $true }
            if ($count -eq 0) { $observedZero = $true }
            if ($s.notifications.seconds -lt 3.2 -and $d.lastFrameKind -eq 'full') { $transfers += $d.lastTransferMilliseconds }
        }
        if (-not $d.connected) { throw 'Panel disconnected during runtime probe.' }
    }
}
# State is published by the one-second collector, independently of the renderer.
# Wait for that snapshot to observe expiry rather than asserting on a stale tick.
$deadline = (Get-Date).AddSeconds(4)
do {
    $final = Invoke-RestMethod "$base/api/state"
    if (-not $final.notifications.isTest) { break }
    if ((Get-Date) -gt $deadline) { throw 'Synthetic notification did not expire.' }
    Start-Sleep -Milliseconds 250
} while ($true)
$after = Invoke-RestMethod "$base/api/notifications/diagnostics"
$display = Invoke-RestMethod "$base/api/display"
$result = [pscustomobject]@{
    notificationKind = $Kind
    calendarArrivalObserved = $observedCalendar
    acknowledgedFrames = $display.acknowledgedFrames - $frames
    providerUpdates = $after.providerUpdates - $initial.providerUpdates
    renderUpdates = $after.renderUpdates - $initial.renderUpdates
    meanSampledAnimationTransferMs = [Math]::Round(($transfers | Measure-Object -Average).Average, 1)
    animationTransferSamples = $transfers.Count
    distinctLiveBackgroundTicks = $backgroundTicks.Count
    burstBadgeObserved = $observedThree
    readBadgeObserved = $observedZero
    testRemoved = -not $final.notifications.isTest
    recoveryDelta = $display.recoveries - $recoveries
    displayConnected = $display.connected
    agentPreserved = (($pids -join ',') -eq (@(Get-Process PulseDeck.Agent | Select-Object -ExpandProperty Id) -join ','))
}
$result | ConvertTo-Json -Compress
if (($Kind -eq 'mail' -and (-not $result.burstBadgeObserved -or -not $result.readBadgeObserved)) -or ($Kind -eq 'calendar' -and -not $result.calendarArrivalObserved) -or -not $result.testRemoved -or -not $result.displayConnected -or -not $result.agentPreserved -or $result.renderUpdates -le $result.providerUpdates -or $result.recoveryDelta -ne 0 -or $result.distinctLiveBackgroundTicks -lt 9 -or $result.providerUpdates -gt 40 -or $result.animationTransferSamples -eq 0) { throw 'Runtime probe did not satisfy its checks.' }
