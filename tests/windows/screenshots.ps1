param([Parameter(Mandatory)][string]$Executable, [Parameter(Mandatory)][string]$Fixture, [string]$Theme = 'light')
# Captures of the real windows of the packaged application (for people without a Windows PC). Never fails the build.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
$scope = [System.Windows.Automation.TreeScope]::Descendants
$elementType = [System.Windows.Automation.AutomationElement]
$name = $elementType::NameProperty
$id = $elementType::AutomationIdProperty
$type = $elementType::ControlTypeProperty
function Find($parent, $property, $value) {
    $condition = [System.Windows.Automation.PropertyCondition]::new($property, $value)
    $until = [DateTime]::UtcNow.AddSeconds(15)
    do {
        $found = $parent.FindFirst($scope, $condition)
        if ($found) { return $found }
        Start-Sleep -Milliseconds 150
    } while ([DateTime]::UtcNow -lt $until)
    throw "Élément introuvable : $value"
}
function Press($parent, $caption) {
    $captionCondition = [System.Windows.Automation.PropertyCondition]::new($name, $caption)
    $buttonType = [System.Windows.Automation.PropertyCondition]::new($type, [System.Windows.Automation.ControlType]::Button)
    $button = $parent.FindFirst($scope, [System.Windows.Automation.AndCondition]::new($captionCondition, $buttonType))
    if (!$button) { throw "Bouton introuvable : $caption" }
    $button.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
}
function Fill($parent, $field, $value) {
    (Find $parent $id $field).GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($value)
}
function Select-Option($parent, $field, $pattern) {
    $combo = Find $parent $id $field
    $expand = $combo.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern)
    $expand.Expand()
    $items = $combo.GetCurrentPattern([System.Windows.Automation.ItemContainerPattern]::Pattern)
    $item = $null
    do {
        $item = $items.FindItemByProperty($item, $null, $null)
        if (!$item) { throw "Option absente dans $field : $pattern" }
    } until ($item.Current.Name -like $pattern)
    $item.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    $expand.Collapse()
}
function Wait-Value($parent, $field, $pattern, $seconds = 25) {
    $element = Find $parent $id $field
    $until = [DateTime]::UtcNow.AddSeconds($seconds)
    do {
        $text = $element.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).Current.Value
        if ($text -like $pattern) { return $text }
        Start-Sleep -Milliseconds 200
    } while ([DateTime]::UtcNow -lt $until)
    throw "Texte attendu dans ${field} : $pattern. Obtenu : $text"
}
function Wait-Name($parent, $field, $pattern, $seconds = 25) {
    $element = Find $parent $id $field
    $until = [DateTime]::UtcNow.AddSeconds($seconds)
    do {
        $text = $element.Current.Name
        if ($text -like $pattern) { return $text }
        Start-Sleep -Milliseconds 200
    } while ([DateTime]::UtcNow -lt $until)
    throw "Texte attendu dans ${field} : $pattern. Obtenu : $text"
}
function Texts($window) {
    # A native message box exposes its text under a control type that varies with the Windows build: read every descendant name.
    return (@($window.FindAll($scope, [System.Windows.Automation.Condition]::TrueCondition) | ForEach-Object { $_.Current.Name }) -join "`n")
}
function Close-Window($window) { $window.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close() }

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
$out = Join-Path $env:RUNNER_TEMP 'mkr-captures'
New-Item -ItemType Directory -Force -Path $out | Out-Null
function Capture($window, $label) {
    try {
        Start-Sleep -Milliseconds 700
        try { $window.SetFocus() } catch { }
        Start-Sleep -Milliseconds 300
        $r = $window.Current.BoundingRectangle
        $x = [int][Math]::Max(0, $r.X); $y = [int][Math]::Max(0, $r.Y)
        $w = [int][Math]::Min($r.Width, [System.Windows.Forms.SystemInformation]::VirtualScreen.Width - $x)
        $h = [int][Math]::Min($r.Height, [System.Windows.Forms.SystemInformation]::VirtualScreen.Height - $y)
        $bmp = New-Object System.Drawing.Bitmap $w, $h
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.CopyFromScreen($x, $y, 0, 0, (New-Object System.Drawing.Size $w, $h))
        $g.Dispose()
        $label = "$Theme-$label"
        $file = Join-Path $out "$label.png"
        $bmp.Save($file, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
        $b64 = [Convert]::ToBase64String([IO.File]::ReadAllBytes($file))
        Write-Host "##CAPTURE-BEGIN $label $w x $h"
        for ($i = 0; $i -lt $b64.Length; $i += 3000) { Write-Host ("##C " + $b64.Substring($i, [Math]::Min(3000, $b64.Length - $i))) }
        Write-Host "##CAPTURE-END $label"
    } catch { Write-Host "##CAPTURE-FAILED $label : $($_.Exception.Message)" }
}
function Press-Retry($parent, $caption) {
    for ($attempt = 1; $attempt -le 4; $attempt++) {
        try { Press $parent $caption; return } catch { Start-Sleep -Milliseconds 800 }
    }
    Press $parent $caption
}
function Close-Others($rootElement, $mainWindow) {
    # Closes every MKR window except the main one (dialogs left open by an earlier step), innermost first, several passes.
    # Owned dialogs appear under their owner in the UI Automation tree, not under the desktop: look in both places.
    $windowType = [System.Windows.Automation.PropertyCondition]::new($type, [System.Windows.Automation.ControlType]::Window)
    for ($pass = 0; $pass -lt 6; $pass++) {
        $found = @($rootElement.FindAll([System.Windows.Automation.TreeScope]::Children, $windowType)) +
                 @($mainWindow.FindAll([System.Windows.Automation.TreeScope]::Descendants, $windowType))
        $others = @($found | Where-Object { $_.Current.Name -like 'MKR — *' -and $_.Current.Name -ne 'MKR — Espace projets' })
        if ($others.Count -eq 0) { return }
        foreach ($other in $others) {
            try { $other.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close() } catch { }
            Start-Sleep -Milliseconds 600
        }
    }
}
function Step($label, [scriptblock]$body) {
    try { & $body } catch { Write-Host "##CAPTURE-FAILED $label : $($_.Exception.Message)" }
}
$start = New-Object System.Diagnostics.ProcessStartInfo
$start.FileName = (Resolve-Path $Executable).Path
$start.UseShellExecute = $false
$start.EnvironmentVariables['MKR_DATA_DIRECTORY'] = (Resolve-Path $Fixture).Path
$start.EnvironmentVariables['MKR_THEME'] = $Theme
$process = [System.Diagnostics.Process]::Start($start)
try {
    $root = $elementType::RootElement
    $main = Find $root $name 'MKR — Espace projets'
    Capture $main '01-espace-projets'
    $row = Find (Find $main $id 'ProjectList') $type ([System.Windows.Automation.ControlType]::DataItem)
    $row.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    Press $main 'Ouvrir le projet sélectionné'
    $project = Find $root $name 'MKR — Projet initial'
    Capture $project '02-projet'
    Press $project 'Préparer les essais sur banc…'
    $catalogue = Find $root $name 'MKR — Catalogue des blocs'
    Fill $catalogue 'Filter' 'PIDFF'
    $null = Find $catalogue $name 'Compatibilité à vérifier'
    Capture $catalogue '03-catalogue-blocs'
    Close-Window $catalogue
    Press $project 'Identification…'
    $ident = Find $root $name 'MKR — Identification'
    $modelId = (Get-Content -LiteralPath (Join-Path $Fixture 'short-delay-study.txt') -Raw).Trim()
    Select-Option $ident 'StudyList' "*$modelId"
    Capture $ident '04-identification'
    Step '04b-courbes' {
        Select-Option $ident 'TrainingSource' '*'
        Start-Sleep -Milliseconds 500
        Press-Retry $ident 'Afficher les courbes brutes (estimation)…'
        $preview = Find $root $name 'MKR — Prévisualisation des signaux'
        Start-Sleep -Milliseconds 900
        Capture $preview '04b-courbes-sources'
        Close-Window $preview
    }
    Step '05-revue' {
        try { Press $ident "Revue de l'étude…" } catch { }
        $review = Find $root $name "MKR — Revue de l'étude"
        Fill $review 'Reason' 'Identification terminée'
        Fill $review 'Evidence' 'essai-7 / essai-8'
        # the click is applied even when UI Automation reports 'Unrecognized error' (the window is busy); the state line is the proof
        try { Press $review 'Marquer comme calculé' } catch { }
        $null = Wait-Name $review 'StateLine' '*:*é'
        Capture $review '05-revue'
        Close-Window $review
    }
    Step '06-integrateur' {
        $integratorId = (Get-Content -LiteralPath (Join-Path $Fixture 'integrator-study.txt') -Raw).Trim()
        Select-Option $ident 'StudyList' "*$integratorId"
        Press-Retry $ident 'Régler le procédé intégrateur…'
        $tuning = Find $root $name "MKR — Réglage d'un procédé intégrateur"
        Fill $tuning 'ResponseSeconds' '8'
        Press-Retry $tuning 'Calculer'
        $null = Wait-Value $tuning 'Result' '*SIMC_integrating*'
        Capture $tuning '06-reglage-integrateur'
        Press-Retry $tuning 'Valeurs du bloc PIDFF…'
        $values = Find $root $name 'MKR — Valeurs du bloc PIDFF (non qualifiées)'
        $null = Wait-Value $values 'Values' '*NON QUALIFIÉ*'
        Capture $values '07-valeurs-bloc-pidff'
        Close-Window $values
        Close-Window $tuning
    }
    Step '09-acquisition' {
        Press-Retry $project 'Acquisition automate…'
        $acq = Find $root $name 'MKR — Acquisition en lecture seule'
        Capture $acq '09-acquisition'
        Close-Window $acq
    }
    Step '08-audit' {
        Close-Window $ident
        Press-Retry $project "Journal d'audit…"
        $audit = Find $root $name "MKR — Journal d'audit"
        Start-Sleep -Milliseconds 800
        Capture $audit '08-journal-audit'
        Close-Window $audit
    }
    Step '10-espace-etude' {
        # a dialog left open by an earlier step (the review window) must not hide the main window
        Close-Others $root $main
        Press-Retry $main "Ouvrir l'espace d'étude"
        $null = Find $main $id 'StudyGrid'
        Start-Sleep -Milliseconds 1200
        Capture $main '10-espace-etude'
        $tab = Find $main $id 'Step4'
        $tab.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
        Start-Sleep -Milliseconds 500
        Capture $main '10b-onglet-identification'
        Press-Retry $main 'Agrandir'
        Start-Sleep -Milliseconds 700
        Capture $main '10c-graphique-agrandi'
    }
} catch {
    Write-Host "##CAPTURE-ABORTED $($_.Exception.Message)"
} finally {
    if (-not $process.HasExited) { Stop-Process -Id $process.Id }
}
exit 0
