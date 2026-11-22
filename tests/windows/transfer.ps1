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
    Select-Option $ident 'ResponseProfile' 'Personnalisée'
    Fill $ident 'ResponseTarget' ''
    if ((Find $ident $id 'ComparePiButton').Current.IsEnabled) { throw 'Comparaison PI active sans cible.' }
    Fill $ident 'ResponseTarget' '2'
    if (!(Find $ident $id 'ComparePiButton').Current.IsEnabled) { throw 'Comparaison PI indisponible avec une cible valide.' }
    $transportId = (Get-Content -LiteralPath (Join-Path $Fixture 'transport-study.txt') -Raw).Trim()
    Select-Option $ident 'StudyList' "*$transportId"
    $transportTarget = (Find $ident $id 'ResponseTarget').GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
    if (!$transportTarget.Current.IsReadOnly -or [Math]::Abs([double]::Parse($transportTarget.Current.Value) - 2040) -ge 0.01) {
        throw 'Cible SIMC attendue proche de 2040 secondes pour le retard de transport de 34 minutes.'
    }
    Press $ident 'Comparer IMC/Lambda et SIMC'
    $null = Find $ident $name 'Réglages théoriques PI enregistrés.'
    if (!(Find $ident $id 'PiReport').Current.Name.Contains('2040 s')) { throw 'Cible absente du résultat PI enregistré.' }
    Select-Option $ident 'ResponseProfile' 'Personnalisée'
    Fill $ident 'ResponseTarget' '3000'
    Press $ident 'Simuler le réglage enregistré…'
    $savedSimulation = Find $root $name 'MKR — Simulation PI'
    $units = (Find $savedSimulation $id 'Units').Current.Name
    if (!$units.Contains('2040 s') -or $units.Contains('3000 s')) {
        throw "La simulation doit reprendre le réglage enregistré. $units"
    }
    $savedSimulation.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close()
    $shortId = (Get-Content -LiteralPath (Join-Path $Fixture 'short-delay-study.txt') -Raw).Trim()
    Select-Option $ident 'StudyList' "*$shortId"
    $target = (Find $ident $id 'ResponseTarget').GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
    if ((Find $ident $id 'SimulateSavedPi').Current.IsEnabled) { throw 'Simulation active sans réglage enregistré.' }
    $help = (Find $ident $id 'TargetHelp').Current.Name
    if (!$target.Current.IsReadOnly -or $target.Current.Value -ne '' -or
        (Find $ident $id 'ComparePiButton').Current.IsEnabled -or !$help.Contains('Retard nul ou mal résolu')) {
        throw "Recommandation incohérente pour un retard inférieur au pas de mesure. $help"
    }
    Select-Option $ident 'ResponseProfile' 'Personnalisée'
    Fill $ident 'ResponseTarget' '3'
    if (!(Find $ident $id 'ComparePiButton').Current.IsEnabled) { throw 'Cible documentée inaccessible.' }
    Select-Option $ident 'ResponseProfile' 'SIMC*'
    if ($target.Current.Value -ne '' -or (Find $ident $id 'ComparePiButton').Current.IsEnabled) {
        throw 'La cible personnalisée a été conservée comme recommandation SIMC.'
    }
    Write-Output 'Retard positif inférieur au pas de mesure : recommandation SIMC indisponible.'
    Write-Output 'Comparaison FOPDT/SOPDT acceptée et relue depuis une nouvelle fenêtre.'
    Select-Option $ident 'StudyList' 'SOPDT*'
    $target = (Find $ident $id 'ResponseTarget').GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
    $initial = [double]::Parse($target.Current.Value)
    if (!$target.Current.IsReadOnly -or $initial -le 0) { throw 'Cible initiale invalide.' }
    Press $ident 'Calculer le PID SIMC'
    $null = Find $ident $name 'Réglage PID théorique affiché.'
    $fit = (Find $ident $id 'FitReport').Current.Name
    $delayLabel = 'θ = ' + $initial.ToString('G5') + ' s'
    if (!$fit.Contains($delayLabel)) { throw "La cible SIMC diffère du retard identifié. $fit" }
    Select-Option $ident 'ResponseProfile' 'Personnalisée'
    foreach ($invalid in @('', '0', '-1', 'abc')) {
        Fill $ident 'ResponseTarget' $invalid
        if ((Find $ident $id 'CalculatePidButton').Current.IsEnabled) { throw "Calcul PID actif avec la cible '$invalid'." }
        if ((Find $ident $id 'SopdtReport').Current.Name) { throw 'Ancien résultat PID encore affiché.' }
    }
    Fill $ident 'ResponseTarget' '2'
    Press $ident 'Calculer le PID SIMC'
    $null = Find $ident $name 'Réglage PID théorique affiché.'
    $report = (Find $ident $id 'SopdtReport').Current.Name
    foreach ($part in @('Cible 2 s','Forme série : Kc -','Forme idéale : Kp -','%/bar','Filtre dérivé à définir')) {
        if (!$report.Contains($part)) { throw "Sortie PID manquante : $part. $report" }
    }
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
    Press $ident 'Simuler le réglage enregistré…'
    $sim = Find $root $name 'MKR — Simulation PI'
    foreach ($entry in @{Sample='1';Duration='10';Setpoint='1';Lower='-2';Upper='2';Disturbance='0';DisturbanceAt='0';Rate='';ManualMv='9';SwitchTime='4'}.GetEnumerator()) {
        Fill $sim $entry.Key $entry.Value
    }
    Fill $sim 'Rate' '0.5'
    Press $sim 'Simuler et enregistrer'
    $null = Find $sim $name 'Simulation enregistrée.'
    Fill $sim 'Setpoint' '3'
    $report = (Find $sim $id 'Report').Current.Name
    if (!$report.Contains('ΔSP 1 bar') -or !$report.Contains('durée 10 s')) { throw "Scénario enregistré ambigu. $report" }
    Press $sim 'Exporter les séries enregistrées…'
    $dialog = Find $root $name 'Exporter les séries enregistrées'
    if ($dialog.Current.ProcessId -ne $process.Id) { throw 'Dialogue export hors du processus MKR.' }
    if ($report -notmatch 'Simulation enregistrée ([0-9a-f]{8})') { throw 'Identifiant de simulation absent.' }
    $mask = "MKR-$($Matches[1])*.csv"
    $save = [MkrUiTest.NativeDialog]::GetDlgItem([IntPtr]$dialog.Current.NativeWindowHandle, 1)
    if ($save -eq [IntPtr]::Zero -or ![MkrUiTest.NativeDialog]::PostMessage($save, 0x00F5, [IntPtr]::Zero, [IntPtr]::Zero)) {
        throw 'Bouton Enregistrer inaccessible.'
    }
    $null = Find $sim $name 'Séries simulées exportées.'
    $folders = @([Environment]::GetFolderPath('MyDocuments'), $Fixture, (Get-Location).Path,
        (Split-Path -Parent $start.FileName)) | Select-Object -Unique
    $files = @(foreach ($folder in $folders) {
        Get-ChildItem -LiteralPath $folder -Filter $mask -File
    })
    if ($files.Count -ne 1) { throw "Export introuvable ou ambigu : $mask" }
    $series = @(Import-Csv -LiteralPath $files[0].FullName)
    if ($series.Count -lt 2 -or @($series | Where-Object { $_.sp_delta -ne '1' }).Count -gt 0) {
        throw 'Les séries exportées doivent reprendre la consigne du scénario enregistré.'
    }
    Fill $sim 'Rate' '2'
    Press $sim 'Fiche de mise au point (PDF)…'
    $dialog = Find $root $name 'Exporter la fiche enregistrée'
    if ($dialog.Current.ProcessId -ne $process.Id) { throw 'Dialogue PDF hors du processus MKR.' }
    $save = [MkrUiTest.NativeDialog]::GetDlgItem([IntPtr]$dialog.Current.NativeWindowHandle, 1)
    if ($save -eq [IntPtr]::Zero -or ![MkrUiTest.NativeDialog]::PostMessage($save, 0x00F5, [IntPtr]::Zero, [IntPtr]::Zero)) {
        throw 'Bouton Enregistrer le PDF inaccessible.'
    }
    $null = Find $sim $name 'Fiche de mise au point exportée.'
    $pdfPath = [IO.Path]::ChangeExtension($files[0].FullName, '.pdf')
    $OutputEncoding = New-Object System.Text.UTF8Encoding($false)
    @'
import sys
from pathlib import Path
from uuid import UUID
from pypdf import PdfReader

path = Path(sys.argv[1])
pages = PdfReader(path).pages
simulation = str(UUID(path.stem.removeprefix("MKR-")))
body = " ".join(" ".join(page.extract_text().split()) for page in pages[:-1])
for expected in ("Consigne en écart 1 bar", "Vitesse maximale de commande 0,5 %/s."):
    if expected not in body:
        raise ValueError(f"Missing saved condition: {expected}")
if simulation not in body or simulation not in pages[-1].extract_text():
    raise ValueError("The report, plot and CSV must identify the same simulation")
'@ | python -X utf8 - $pdfPath
    if ($LASTEXITCODE -ne 0) { throw 'La relecture du PDF a échoué.' }
    Fill $sim 'Rate' ''
    Fill $sim 'Setpoint' '1'
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
