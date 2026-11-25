param([Parameter(Mandatory)][string]$Executable, [Parameter(Mandatory)][string]$Fixture)
# History mode on a stored recording: find the steps, take the proposed pair, estimate and validate on its two ranges.
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
    # The button may be momentarily absent from the UI Automation tree while the window is busy: look for it for up to 10 s.
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
    Write-Host "Saisie : $field"
    $box = Find $parent $id $field
    $box.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($value)
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

function Wait-Text($parent, $field, $pattern, $seconds = 40) {
    $element = Find $parent $id $field
    $until = [DateTime]::UtcNow.AddSeconds($seconds)
    do {
        $text = $element.Current.Name
        if ($text -like $pattern) { return $text }
        Start-Sleep -Milliseconds 200
    } while ([DateTime]::UtcNow -lt $until)
    throw "Texte attendu dans ${field} : $pattern. Obtenu : $text"
}
$start = New-Object System.Diagnostics.ProcessStartInfo
$start.FileName = (Resolve-Path $Executable).Path
$start.UseShellExecute = $false
$start.EnvironmentVariables['MKR_DATA_DIRECTORY'] = (Resolve-Path $Fixture).Path
$process = [System.Diagnostics.Process]::Start($start)
try {
    $root = $elementType::RootElement
    $main = Find $root $name 'MKR — Espace projets'
    $grid = Find $main $id 'ProjectList'
    $row = Find $grid $type ([System.Windows.Automation.ControlType]::DataItem)
    $row.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    Press $main 'Ouvrir le projet sélectionné'
    $project = Find $root $name 'MKR — Projet initial'
    Press $project 'Identification…'
    $ident = Find $root $name 'MKR — Identification'

    Select-Option $ident 'TrainingSource' 'historique-long.csv*'
    Select-Option $ident 'TrainingMv' 'mv'
    foreach ($entry in @{MvUnit='%';MvTolerance='0.01';PvTolerance='0.01'}.GetEnumerator()) { Fill $ident $entry.Key $entry.Value }

    # Without the two ranges, one recording cannot be both trials.
    Select-Option $ident 'ValidationSource' 'historique-long.csv*'
    Select-Option $ident 'ValidationMv' 'mv'
    $confirm = Find $ident $id 'Confirmed'
    $confirm.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle()
    Press $ident 'Estimer et enregistrer'
    $null = Wait-Text $ident 'Message' '*plages distinctes du même enregistrement*'

    Press $ident 'Rechercher les essais exploitables dans cet enregistrement…'
    $search = Find $root $name 'MKR — Essais détectés'
    $summary = Wait-Text $search 'Summary' '*2 échelon(s) exploitable(s)*'
    $pairs = Find $search $id 'Pairs'
    $pair = Find $pairs $type ([System.Windows.Automation.ControlType]::DataItem)
    $pair.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    $use = Find $search $id 'UsePair'
    $until = [DateTime]::UtcNow.AddSeconds(10)
    while (!$use.Current.IsEnabled -and [DateTime]::UtcNow -lt $until) { Start-Sleep -Milliseconds 150 }
    if (!$use.Current.IsEnabled) { throw 'La paire sélectionnée doit activer « Utiliser cette paire ».' }
    Press $search 'Utiliser cette paire'

    $trainingRange = (Find $ident $id 'TrainingRangeInfo').Current.Name
    $validationRange = (Find $ident $id 'ValidationRangeInfo').Current.Name
    foreach ($text in @($trainingRange, $validationRange)) {
        if (!$text.Contains('indices source') -or !$text.Contains('échelon n°')) { throw "Plage de la paire non retenue : $text" }
    }
    $confirm = Find $ident $id 'Confirmed'
    if ($confirm.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Current.ToggleState -ne 'Off') {
        throw "Les conditions d’essai doivent être reconfirmées après le choix des plages."
    }
    Press $ident 'Proposer les critères'
    $null = Wait-Text $ident 'Message' '*Bruit mesuré sur les plateaux avant échelon*'
    $confirm.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle()
    Press $ident 'Estimer et enregistrer'
    $null = Wait-Text $ident 'Message' 'Étude exploratoire enregistrée.'
    $report = (Find $ident $id 'FitReport').Current.Name
    foreach ($part in @('Validation indépendante', 'modèle accepté', 'historique-long.csv')) {
        if (!$report.Contains($part)) { throw "Rapport de l’étude absent : $part. $report" }
    }
    # The study of one recording stays tunable: the PI panel is offered for the accepted model.
    $null = Find $ident $id 'ComparePiButton'
    Write-Output 'Historique : échelons détectés, paire retenue, estimation et validation sur deux plages du même enregistrement.'
} finally {
    if ($process -and -not $process.HasExited) { Stop-Process -Id $process.Id }
}
