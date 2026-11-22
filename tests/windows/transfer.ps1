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
function Study-Labels($window) {
    $combo = Find $window $id 'StudyList'
    $items = $combo.GetCurrentPattern([System.Windows.Automation.ItemContainerPattern]::Pattern)
    $item = $null
    while ($item = $items.FindItemByProperty($item, $null, $null)) { $item.Current.Name }
}
function Verify-Comparison($window) {
    $report = (Find $window $id 'FitReport').Current.Name
    foreach ($part in @('Comparaison enregistrée','modèle retenu FOPDT','RMSE ≤ 0','FOPDT · RMSE','SOPDT · RMSE')) {
        if (!$report.Contains($part)) { throw "Information de comparaison absente : $part. Rapport : $report" }
    }
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
    Select-Option $ident 'TrainingSource' 'essai-1.csv*'
    Select-Option $ident 'ValidationSource' 'essai-2.csv*'
    foreach ($prefix in @('Training', 'Validation')) {
        Select-Option $ident ($prefix + 'Time') 'timestamp'
        Select-Option $ident ($prefix + 'Mv') 'mv'
        Select-Option $ident ($prefix + 'Pv') 'pv'
    }
    foreach ($entry in @{MvUnit='%';PvUnit='bar';MvTolerance='1e-2';PvTolerance='1e-2';MaximumRmse='1e-3';MaximumLag='1'}.GetEnumerator()) {
        Fill $ident $entry.Key $entry.Value
    }
    $confirm = Find $ident $id 'Confirmed'
    $confirm.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle()
    Press $ident 'Comparer FOPDT et SOPDT'
    $dialog = Find $root $name 'Comparaison sur essai indépendant'
    # WPF MessageBox is a native Win32 dialog. Activate its IDYES button.
    Add-Type -Namespace MkrUiTest -Name NativeDialog -MemberDefinition @'
[System.Runtime.InteropServices.DllImport("user32.dll")]
public static extern System.IntPtr GetDlgItem(System.IntPtr dialog, int id);
[System.Runtime.InteropServices.DllImport("user32.dll")]
public static extern bool PostMessage(System.IntPtr target, uint msg, System.IntPtr wParam, System.IntPtr lParam);
'@
    if ($dialog.Current.ProcessId -ne $process.Id) { throw 'Dialogue hors du processus MKR.' }
    $yes = [MkrUiTest.NativeDialog]::GetDlgItem([IntPtr]$dialog.Current.NativeWindowHandle, 6)
    if ($yes -eq [IntPtr]::Zero -or ![MkrUiTest.NativeDialog]::PostMessage($yes, 0x00F5, [IntPtr]::Zero, [IntPtr]::Zero)) {
        throw 'Confirmation Oui inaccessible.'
    }
    $null = Find $ident $name 'Étude exploratoire enregistrée.'
    Verify-Comparison $ident
    $combo = Find $ident $id 'StudyList'
    $selected = $combo.GetCurrentPattern([System.Windows.Automation.SelectionPattern]::Pattern).Current.GetSelection()
    $savedLabel = $selected[0].Current.Name
    if ($savedLabel -notmatch 'FOPDT.*[0-9a-f]{8}$' -or $savedLabel.EndsWith($study)) { throw "Nouvelle étude attendue : $savedLabel" }
    $before = @(Study-Labels $ident)
    # The holdout now follows an integrating process, unlike the training trace.
    Select-Option $ident 'ValidationSource' 'essai-6.csv*'
    Select-Option $ident 'ValidationTime' 'timestamp'
    Select-Option $ident 'ValidationMv' 'mv'
    Select-Option $ident 'ValidationPv' 'pv'
    $confirm.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle()
    Press $ident 'Comparer FOPDT et SOPDT'
    $status = Find $ident $id 'Message'
    $until = [DateTime]::UtcNow.AddSeconds(25)
    do {
        $details = $status.Current.Name
        if ($details.Contains('Aucun des deux modèles ne satisfait les critères.')) { break }
        Start-Sleep -Milliseconds 200
    } while ([DateTime]::UtcNow -lt $until)
    foreach ($part in @('essai-6.csv','FOPDT · RMSE','SOPDT · RMSE','erreur de validation excessive','Aucun des deux modèles')) {
        if (!$details.Contains($part)) { throw "Explication du refus absente : $part. $details" }
    }
    $ident.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close()
    Press $project 'Identification…'
    $ident = Find $root $name 'MKR — Identification'
    $after = @(Study-Labels $ident)
    if ($before.Count -ne $after.Count -or (Compare-Object $before $after)) {
        throw 'Les études enregistrées ont changé après le refus.'
    }
    Select-Option $ident 'StudyList' $savedLabel
    Verify-Comparison $ident
    Write-Output 'Comparaison FOPDT/SOPDT acceptée et relue depuis une nouvelle fenêtre.'
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
