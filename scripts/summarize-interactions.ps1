param(
    [Parameter(Mandatory = $true)]
    [string]$Path,

    [string]$CsvOut = ""
)

if (-not (Test-Path -LiteralPath $Path)) {
    Write-Error "Log file not found: $Path"
    exit 1
}

function Convert-DetailsJson {
    param([string]$Json)

    if ([string]::IsNullOrWhiteSpace($Json)) {
        return @{}
    }

    try {
        $parsed = $Json | ConvertFrom-Json
        if ($parsed -is [System.Collections.IDictionary]) {
            return $parsed
        }

        $table = @{}
        foreach ($property in $parsed.PSObject.Properties) {
            $table[$property.Name] = $property.Value
        }

        return $table
    }
    catch {
        return @{}
    }

    return @{}
}

function Convert-InteractionLine {
    param([string]$Line)

    $trimmed = $Line.Trim()
    if ([string]::IsNullOrWhiteSpace($trimmed)) {
        return $null
    }

    if ($trimmed.StartsWith("{")) {
        try {
            $json = $trimmed | ConvertFrom-Json
            $eventName = $json.eventName
            if (-not $eventName) { $eventName = $json.EventName }
            if (-not $eventName) { return $null }

            $sessionId = $json.sessionId
            if (-not $sessionId) { $sessionId = $json.SessionId }
            if (-not $sessionId) { $sessionId = "unknown" }

            $details = $json.details
            if (-not $details) { $details = $json.Details }
            if (-not ($details -is [System.Collections.IDictionary])) {
                $detailsTable = @{}
                if ($details) {
                    foreach ($property in $details.PSObject.Properties) {
                        $detailsTable[$property.Name] = $property.Value
                    }
                }
                $details = $detailsTable
            }

            return [pscustomobject]@{
                EventName = [string]$eventName
                SessionId = [string]$sessionId
                Details = $details
                Raw = $Line
            }
        }
        catch {
            return $null
        }
    }

    $match = [regex]::Match($Line, 'Interaction\s+(?<event>\S+)\s+session=(?<session>\S+)\s+path=(?<path>\S+)\s+details=(?<details>\{.*\})')
    if (-not $match.Success) {
        return $null
    }

    return [pscustomobject]@{
        EventName = $match.Groups["event"].Value
        SessionId = $match.Groups["session"].Value
        Details = Convert-DetailsJson $match.Groups["details"].Value
        Raw = $Line
    }
}

function Get-DetailValue {
    param(
        $Details,
        [string]$Name,
        $Default = $null
    )

    if ($Details -is [System.Collections.IDictionary] -and $Details.Contains($Name)) {
        return $Details[$Name]
    }

    return $Default
}

function Convert-ToBool {
    param($Value)

    if ($Value -is [bool]) {
        return $Value
    }

    if ($null -eq $Value) {
        return $false
    }

    return ([string]$Value).Equals("true", [System.StringComparison]::OrdinalIgnoreCase)
}

function Convert-ToInt {
    param($Value)

    if ($null -eq $Value) {
        return 0
    }

    $out = 0
    if ([int]::TryParse(([string]$Value), [ref]$out)) {
        return $out
    }

    return 0
}

$events = New-Object System.Collections.Generic.List[object]

Get-Content -LiteralPath $Path | ForEach-Object {
    $event = Convert-InteractionLine $_
    if ($null -ne $event) {
        $events.Add($event)
    }
}

if ($events.Count -eq 0) {
    Write-Host "No interaction events found."
    Write-Host "Expected lines containing 'Interaction ... session=... details={...}' or JSONL entries."
    exit 0
}

$sessions = @{}

foreach ($event in $events) {
    $sessionId = if ([string]::IsNullOrWhiteSpace($event.SessionId)) { "unknown" } else { $event.SessionId }
    if (-not $sessions.ContainsKey($sessionId)) {
        $sessions[$sessionId] = [ordered]@{
            SessionId = $sessionId
            Events = 0
            FormStarted = $false
            SubmitAttempted = $false
            SubmitValidated = $false
            AnalysisCompleted = $false
            ReachedResults = $false
            ResultsTimeSeconds = 0
            MaxScrollDepth = 0
            ClickedStartStep1 = $false
            CopiedStep1 = $false
            OpenedFeedback = $false
            SubmittedFeedback = $false
            DownloadedReport = $false
            SharedResults = $false
            AnalyzeAnother = $false
            LeftWithoutStartingStep1 = $false
            Score = 0
            LastEvent = ""
        }
    }

    $session = $sessions[$sessionId]
    $session.Events++
    $session.LastEvent = $event.EventName

    switch -Wildcard ($event.EventName) {
        "client_target_role_focused" { $session.FormStarted = $true }
        "client_resume_text_focused" { $session.FormStarted = $true }
        "client_resume_file_selected" { $session.FormStarted = $true }
        "client_submit_attempted" { $session.SubmitAttempted = $true }
        "client_submit_validated" { $session.SubmitValidated = $true }
        "server_analysis_completed" { $session.AnalysisCompleted = $true }
        "server_results_page_loaded" { $session.ReachedResults = $true }
        "client_results_page_view" { $session.ReachedResults = $true }
        "client_start_step_1_clicked" { $session.ClickedStartStep1 = $true }
        "client_first_step_copied" { $session.CopiedStep1 = $true }
        "client_feedback_opened" { $session.OpenedFeedback = $true }
        "client_feedback_submit_attempted" { $session.SubmittedFeedback = $true }
        "server_feedback_submitted" { $session.SubmittedFeedback = $true }
        "client_download_report_clicked" { $session.DownloadedReport = $true }
        "server_results_downloaded" { $session.DownloadedReport = $true }
        "client_share_results_clicked" { $session.SharedResults = $true }
        "client_analyze_another_clicked" { $session.AnalyzeAnother = $true }
    }

    $secondsOnResults = Convert-ToInt (Get-DetailValue $event.Details "secondsOnResults" 0)
    if ($secondsOnResults -gt $session.ResultsTimeSeconds) {
        $session.ResultsTimeSeconds = $secondsOnResults
    }

    $scrollDepth = Convert-ToInt (Get-DetailValue $event.Details "maxScrollDepth" 0)
    if ($scrollDepth -gt $session.MaxScrollDepth) {
        $session.MaxScrollDepth = $scrollDepth
    }

    $score = Convert-ToInt (Get-DetailValue $event.Details "score" 0)
    if ($score -gt 0) {
        $session.Score = $score
    }

    if ($event.EventName -eq "client_results_page_hidden") {
        $session.LeftWithoutStartingStep1 = Convert-ToBool (Get-DetailValue $event.Details "leftWithoutStartingStep1" $false)
        $finalScrollDepth = Convert-ToInt (Get-DetailValue $event.Details "finalScrollDepth" 0)
        if ($finalScrollDepth -gt $session.MaxScrollDepth) {
            $session.MaxScrollDepth = $finalScrollDepth
        }
    }
}

$rows = $sessions.Values |
    ForEach-Object { [pscustomobject]$_ } |
    Sort-Object SessionId

function Get-Percent {
    param(
        [int]$Part,
        [int]$Total
    )

    if ($Total -eq 0) {
        return "0.0%"
    }

    return "{0:N1}%" -f (($Part / $Total) * 100)
}

function Get-Median {
    param([int[]]$Values)

    if (-not $Values -or $Values.Count -eq 0) {
        return 0
    }

    $sorted = $Values | Sort-Object
    $middle = [math]::Floor($sorted.Count / 2)
    if ($sorted.Count % 2 -eq 1) {
        return $sorted[$middle]
    }

    return [math]::Round(($sorted[$middle - 1] + $sorted[$middle]) / 2, 1)
}

$total = $rows.Count
$reachedResults = @($rows | Where-Object ReachedResults).Count
$clickedStep1 = @($rows | Where-Object ClickedStartStep1).Count
$copiedStep1 = @($rows | Where-Object CopiedStep1).Count
$submittedFeedback = @($rows | Where-Object SubmittedFeedback).Count
$downloaded = @($rows | Where-Object DownloadedReport).Count
$shared = @($rows | Where-Object SharedResults).Count
$leftWithoutStart = @($rows | Where-Object { $_.ReachedResults -and -not $_.ClickedStartStep1 -and $_.LeftWithoutStartingStep1 }).Count
$medianResultsTime = Get-Median @($rows | Where-Object ReachedResults | ForEach-Object { $_.ResultsTimeSeconds })
$medianScroll = Get-Median @($rows | Where-Object ReachedResults | ForEach-Object { $_.MaxScrollDepth })

Write-Host ""
Write-Host "Interaction Summary"
Write-Host "==================="
Write-Host "Sessions:                 $total"
Write-Host "Reached results:          $reachedResults ($(Get-Percent $reachedResults $total))"
Write-Host "Clicked Start Step 1:     $clickedStep1 ($(Get-Percent $clickedStep1 $reachedResults))"
Write-Host "Copied Step 1:            $copiedStep1 ($(Get-Percent $copiedStep1 $reachedResults))"
Write-Host "Left without Step 1:      $leftWithoutStart ($(Get-Percent $leftWithoutStart $reachedResults))"
Write-Host "Submitted feedback:       $submittedFeedback ($(Get-Percent $submittedFeedback $reachedResults))"
Write-Host "Downloaded report:        $downloaded ($(Get-Percent $downloaded $reachedResults))"
Write-Host "Shared results:           $shared ($(Get-Percent $shared $reachedResults))"
Write-Host "Median results time:      ${medianResultsTime}s"
Write-Host "Median scroll depth:      ${medianScroll}%"
Write-Host ""

$rows |
    Select-Object SessionId, ReachedResults, ResultsTimeSeconds, MaxScrollDepth, ClickedStartStep1, CopiedStep1, OpenedFeedback, SubmittedFeedback, DownloadedReport, SharedResults, LeftWithoutStartingStep1, Score, Events |
    Format-Table -AutoSize

if (-not [string]::IsNullOrWhiteSpace($CsvOut)) {
    $rows |
        Select-Object SessionId, ReachedResults, ResultsTimeSeconds, MaxScrollDepth, ClickedStartStep1, CopiedStep1, OpenedFeedback, SubmittedFeedback, DownloadedReport, SharedResults, AnalyzeAnother, LeftWithoutStartingStep1, Score, Events, LastEvent |
        Export-Csv -LiteralPath $CsvOut -NoTypeInformation

    Write-Host ""
    Write-Host "CSV written to: $CsvOut"
}
