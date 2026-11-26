param([Parameter(Mandatory)][string]$Executable, [Parameter(Mandatory)][string]$Fixture)
# Integrator tuning window and model drift check, driven through UI Automation (fixtures written by tests/Mkr.Smoke).
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
    # The button may appear a moment after a window closes or opens: look for it for up to 10 s before giving up.
    $captionCondition = [System.Windows.Automation.PropertyCondition]::new($name, $caption)
    $buttonType = [System.Windows.Automation.PropertyCondition]::new($type, [System.Windows.Automation.ControlType]::Button)
    $condition = [System.Windows.Automation.AndCondition]::new($captionCondition, $buttonType)
    $until = [DateTime]::UtcNow.AddSeconds(10)
    do {
        $button = $parent.FindFirst($scope, $condition)
        if ($button) { break }
        Start-Sleep -Milliseconds 250
    } while ([DateTime]::UtcNow -lt $until)
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
function Audit-Rows($window, $filter) {
    # The audit grid is virtualised: filter first, then read the (few) remaining rows as text.
    Fill $window 'Filter' $filter
    Start-Sleep -Milliseconds 500
    $grid = Find $window $id 'Entries'
    return (@($grid.FindAll($scope, [System.Windows.Automation.Condition]::TrueCondition) | ForEach-Object { $_.Current.Name }) -join "`n")
}
function Close-Window($window) { $window.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close() }

function Go-Tab($window, $number) {
    # Single window: the steps of the study are the tabs of the workspace (Step1 ... Step10).
    $tab = Find $window $id "Step$number"
    $tab.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    Start-Sleep -Milliseconds 700
}
function Show-Page($window, $pageId) {
    # The identification workspace has two right-hand pages: results (PageResults) and trials/actions (PageSetup); only the selected one is in the UI tree.
    $radio = Find $window $id $pageId
    $radio.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    Start-Sleep -Milliseconds 500
}
function Go-Home($window) {
    Go-Tab $window 1
    Press $window 'Accueil'
    $null = Find $window $id 'ProjectList'
}
$start = New-Object System.Diagnostics.ProcessStartInfo
$start.FileName = (Resolve-Path $Executable).Path
$start.UseShellExecute = $false
$start.EnvironmentVariables['MKR_DATA_DIRECTORY'] = (Resolve-Path $Fixture).Path
$process = [System.Diagnostics.Process]::Start($start)
try {
    $root = $elementType::RootElement
    $main = Find $root $name 'ORIAS — Espace projets'
    $row = Find (Find $main $id 'ProjectList') $type ([System.Windows.Automation.ControlType]::DataItem)
    $row.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    Press $main 'Ouvrir le projet sélectionné'
    $null = Find $main $id 'StudyPicker'
    $project = $main      # single window: the project's data are the « Données » tab of the workspace
    Press $project 'Identification…'
    $null = Find $main $id 'StudyList'
    $ident = $main        # single window: the identification is the « Identification » tab
    Show-Page $ident 'PageSetup'

    # ---- Integrator tuning window ----
    $integratorId = (Get-Content -LiteralPath (Join-Path $Fixture 'integrator-study.txt') -Raw).Trim()
    Select-Option $ident 'StudyList' "*$integratorId"
    Press $ident 'Régler le procédé intégrateur…'
    $tuning = Find $root $name "ORIAS — Réglage d'un procédé intégrateur"
    if ((Find $tuning $id 'Compute').Current.IsEnabled -or (Find $tuning $id 'BlockValues').Current.IsEnabled) {
        throw 'Aucun calcul ne doit être possible sans durée de réponse saisie.'
    }
    Fill $tuning 'ResponseSeconds' '8'
    if (!(Find $tuning $id 'Compute').Current.IsEnabled) { throw 'Le calcul doit être possible avec une durée valide.' }
    Press $tuning 'Calculer'
    $text = Wait-Value $tuning 'Result' '*SIMC_integrating*'
    foreach ($part in @('Kp = -', 'Ti = 42 s', 'Marge de gain', 'sensibilité maximale', 'Réserves')) {
        if (!$text.Contains($part)) { throw "Réglage intégrateur : $part absent. $text" }      # slope -0.3, delay 2.5 s, tau_c 8 s: Ti = 4 x 10.5 = 42 s
    }
    Press $tuning 'Valeurs du bloc PIDFF…'
    $values = Find $root $name 'ORIAS — Valeurs du bloc PIDFF (non qualifiées)'
    $block = Wait-Value $values 'Values' '*NON QUALIFIÉ*'
    if (!$block.Contains('rev_dir = 1') -or !$block.Contains('ti = T#42000ms')) { throw "Valeurs PIDFF de l'intégrateur inattendues. $block" }
    Close-Window $values
    Fill $tuning 'ResponseSeconds' ''
    if ((Find $tuning $id 'Compute').Current.IsEnabled -or (Find $tuning $id 'BlockValues').Current.IsEnabled) {
        throw 'Un résultat périmé ne doit pas rester utilisable après modification de la saisie.'
    }
    Close-Window $tuning
    Write-Output 'Réglage intégrateur : saisie obligatoire, règle SIMC, valeurs PIDFF non qualifiées, résultat périmé retiré.'

    # ---- Model drift check ----
    $modelId = (Get-Content -LiteralPath (Join-Path $Fixture 'short-delay-study.txt') -Raw).Trim()
    Select-Option $ident 'StudyList' "*$modelId"
    Press $ident 'Vérifier le modèle sur un nouvel essai…'
    if (!(Wait-Name $ident 'Message' '*Choisissez le nouvel essai*')) { throw 'Message attendu sans essai choisi.' }
    Select-Option $ident 'ValidationSource' 'essai-11.csv*'
    Select-Option $ident 'ValidationTime' 'timestamp'
    Select-Option $ident 'ValidationMv' 'mv'
    Select-Option $ident 'ValidationPv' 'pv'
    Fill $ident 'MvUnit' '%'
    Fill $ident 'PvUnit' 'bar'
    Fill $ident 'MvTolerance' '0.01'
    Fill $ident 'PvTolerance' '0.01'
    Press $ident 'Vérifier le modèle sur un nouvel essai…'
    $verdict = Find $root $name 'ORIAS — Contrôle de dérive du modèle'
    $report = Texts $verdict
    if (!$report.Contains('reste cohérent') -or !$report.Contains('constante de temps')) { throw "Verdict de dérive inattendu. $report" }
    Close-Window $verdict
    Write-Output 'Contrôle de dérive : essai indépendant du même procédé jugé cohérent.'

    # The trial that built the model is refused, never judged.
    Select-Option $ident 'ValidationSource' 'essai-7.csv*'
    Select-Option $ident 'ValidationTime' 'timestamp'
    Select-Option $ident 'ValidationMv' 'mv'
    Select-Option $ident 'ValidationPv' 'pv'
    Press $ident 'Vérifier le modèle sur un nouvel essai…'
    $until = [DateTime]::UtcNow.AddSeconds(15)
    do {
        $message = (Find $ident $id 'Message').Current.Name
        if ($message -like '*même*' -or $message -like '*plage*' -or $message -like '*octets*') { break }
        Start-Sleep -Milliseconds 200
    } while ([DateTime]::UtcNow -lt $until)
    if ($message -like '*terminé*' -or [string]::IsNullOrWhiteSpace($message)) { throw "L'essai déjà utilisé doit être refusé. $message" }
    Write-Output "Essai déjà utilisé refusé : $message"

    # ---- Review of a study: session identity, four-eyes rule, declared self-review ----
    Press $ident "Revue de l'étude…"
    $review = Find $root $name "ORIAS — Revue de l'étude"
    if (!(Find $review $id 'Identity').Current.Name.Contains('[S-1-')) { throw "L'identité de session (SID) doit figurer dans la revue : $((Find $review $id 'Identity').Current.Name)" }
    $null = Wait-Name $review 'StateLine' '*Aucune revue*'
    if ((Find $review $id 'Verify').Current.IsEnabled -or !(Find $review $id 'Calculated').Current.IsEnabled) { throw 'Seul « calculé » doit être possible sans revue.' }
    Press $review 'Marquer comme calculé'
    $null = Wait-Name $review 'Status' '*Renseignez le motif*'
    Fill $review 'Reason' 'Identification terminée'
    Fill $review 'Evidence' 'essai-7 / essai-8'
    Press $review 'Marquer comme calculé'
    $null = Wait-Name $review 'StateLine' '*Calculé*'
    Fill $review 'Reason' 'Relecture'
    Press $review 'Vérifier'
    $null = Wait-Name $review 'Status' '*même compte*'
    (Find $review $id 'SelfReview').GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle()
    Press $review 'Vérifier'
    $null = Wait-Name $review 'StateLine' '*Vérifié*'
    $historyText = Wait-Value $review 'History' '*auto-revue déclarée*'
    if (!$historyText.Contains('Calculé') -or !$historyText.Contains('Vérifié')) { throw "Historique de revue incomplet. $historyText" }
    Close-Window $review
    Write-Output 'Revue : identité de session, refus sans déclaration, auto-revue inscrite comme telle.'

    # ---- Audit log: the actions above are traced and the chain is intact ----
    Go-Tab $main 2
    Press $project "Journal d'audit…"
    $audit = Find $root $name "ORIAS — Journal d'audit"
    foreach ($part in @('integrator.pi.calculated', 'block.values.shown', 'model.drift.checked', 'review.recorded')) {
        $rows = Audit-Rows $audit $part
        if (!$rows.Contains($part)) { throw "Action absente du journal : $part. $rows" }
    }
    $rows = Audit-Rows $audit 'block.values.shown'
    if (!$rows.Contains('NON QUALIFIÉES')) { throw "Une valeur de bloc affichée doit être tracée comme non qualifiée. $rows" }
    $null = Audit-Rows $audit ''
    Press $audit "Vérifier l'intégrité"
    $null = Wait-Name $audit 'VerifyStatus' 'Chaîne intacte*'
    Close-Window $audit
    Write-Output "Journal d'audit : actions tracées, chaîne intacte."
} finally {
    if (-not $process.HasExited) { Stop-Process -Id $process.Id }
}
