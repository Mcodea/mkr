param([Parameter(Mandatory)][string]$Executable, [Parameter(Mandatory)][string]$Fixture)
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
    $condition = [System.Windows.Automation.AndCondition]::new($captionCondition, $buttonType)
    $button = $parent.FindFirst($scope, $condition)
    if (!$button) { throw "Bouton introuvable : $caption" }
    $button.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
}
function Fill($parent, $field, $value) {
    Write-Host "Saisie : $field"
    $box = Find $parent $id $field
    $box.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($value)
}
$study = (Get-Content -LiteralPath (Join-Path $Fixture 'study.txt') -Raw).Trim()
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
    $project = Find $root $name "MKR — Projet initial"
    Press $project 'Identification…'
    $ident = Find $root $name 'MKR — Identification'
    $studies = Find $ident $id 'StudyList'
    $studies.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand()
    $container = $studies.GetCurrentPattern([System.Windows.Automation.ItemContainerPattern]::Pattern)
    $option = $null
    do {
        $option = $container.FindItemByProperty($option, $null, $null)
        if (!$option) { throw "Étude de référence absente du sélecteur : $study" }
    } until ($option.Current.Name -like "*$study" -or $option.Current.AutomationId -like "$study*")
    $option.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    $studies.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Collapse()
    Press $ident 'Simuler en boucle fermée…'
    $sim = Find $root $name 'MKR — Simulation PI'
    foreach ($entry in @{Sample='1';Duration='10';Setpoint='1';Lower='-2';Upper='2';Disturbance='0';DisturbanceAt='0';Rate='';ManualMv='9';SwitchTime='4'}.GetEnumerator()) {
        Fill $sim $entry.Key $entry.Value
    }
    Press $sim 'Tester le transfert'
    $status = Find $sim $id 'Message'
    $until = [DateTime]::UtcNow.AddSeconds(25)
    do {
        $message = $status.Current.Name
        if ($message -like '*2 → 2 %*' -and $message -like '*saut de commande : 0 %*' -and $message -like '*IMC / Lambda*') { 
            Write-Output 'Transfert manuel 9 %, sortie bornée 2 %, première commande automatique 2 %.'
            return
        }
        Start-Sleep -Milliseconds 200
    } while ([DateTime]::UtcNow -lt $until)
    throw "Résultat de transfert inattendu : $message"
}
catch {
    Write-Host $_.ScriptStackTrace
    throw
}
finally {
    if (!$process.HasExited) { Stop-Process -Id $process.Id }
}
