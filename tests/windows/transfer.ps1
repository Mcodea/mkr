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
    (Find $project $name 'Procédé').GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    Fill $project 'LoopTag' 'PIC-202'
    Fill $project 'LoopControlledQuantity' 'Pression de refoulement'
    Fill $project 'LoopOperatingPoint' '5 bar'
    Fill $project 'LoopObjective' 'Maintenir la pression aval'
    Press $project 'Enregistrer la description'
    $null = Find $project $name 'Description enregistrée.'
    $project.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close()
    Press $main 'Ouvrir le projet sélectionné'
    $project = Find $root $name 'MKR — Projet initial'
    (Find $project $name 'Procédé').GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    foreach ($entry in @{LoopTag='PIC-202'; LoopControlledQuantity='Pression de refoulement'; LoopOperatingPoint='5 bar'; LoopObjective='Maintenir la pression aval'; Description="Procédé d’essai"}.GetEnumerator()) {
        $value = (Find $project $id $entry.Key).GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).Current.Value
        if ($value -ne $entry.Value) { throw "Fiche de boucle incorrecte après réouverture : $($entry.Key)" }
    }
    Press $project 'Préparer les essais sur banc…'
    $bench = Find $root $name 'MKR — Catalogue des blocs'
    Fill $bench 'Filter' 'PIDFF'
    $null = Find $bench $name 'Compatibilité à vérifier'
    $context = (Find $bench $id 'LoopContext').Current.Name
    Press $bench 'Copier le relevé de banc'
    $null = Find $bench $name 'Relevé PIDFF copié.'
    $sheet = Get-Clipboard -Raw
    foreach ($field in @('Repère : PIC-202', 'Grandeur régulée : Pression de refoulement', 'Point de fonctionnement : 5 bar', 'Objectif : Maintenir la pression aval', "Description : Procédé d’essai")) {
        if (!$context.Contains($field) -or !$sheet.Contains($field)) { throw "Champ de boucle absent du relevé : $field" }
    }
    $bench.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close()
    Press $project 'Identification…'
    $ident = Find $root $name 'MKR — Identification'
    Select-Option $ident 'TrainingSource' 'essai-1.csv*'
    Select-Option $ident 'ValidationSource' 'essai-2.csv*'
    foreach ($prefix in @('Training', 'Validation')) {
        foreach ($field in @{Time='timestamp'; Pv='pv'}.GetEnumerator()) {
            $box = Find $ident $id ($prefix + $field.Key)
            $items = $box.GetCurrentPattern([System.Windows.Automation.SelectionPattern]::Pattern).Current.GetSelection()
            if ($items.Count -ne 1 -or $items[0].Current.Name -ne $field.Value) { throw "Affectation enregistrée perdue : $prefix $($field.Key)" }
        }
        $mv = Find $ident $id ($prefix + 'Mv')
        if ($mv.GetCurrentPattern([System.Windows.Automation.SelectionPattern]::Pattern).Current.GetSelection().Count -ne 0) {
            throw "MV déduite sans affectation enregistrée."
        }
        Select-Option $ident ($prefix + 'Mv') 'mv'

    }
    if ((Find $ident $id 'PvUnit').GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).Current.Value -ne 'bar') { throw 'Unité PV enregistrée perdue.' }
    foreach ($entry in @{MvUnit='%';MvTolerance='1e-2';PvTolerance='1e-2';MaximumRmse='1e-3';MaximumLag='1'}.GetEnumerator()) {
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
    # NC-03: tight SIMC on the conservative delay bound, 2040 s estimated + 90 s onset interval.
    if (!$transportTarget.Current.IsReadOnly -or [Math]::Abs([double]::Parse($transportTarget.Current.Value) - 2130) -ge 0.01) {
        throw "Cible SIMC attendue proche de 2130 secondes (retard de 34 minutes + intervalle d’échelon de 90 s)."
    }
    Press $ident 'Comparer Lambda et SIMC'
    $null = Find $ident $name 'Réglages théoriques PI enregistrés.'
    if (!(Find $ident $id 'PiReport').Current.Name.Contains('2130 s')) { throw 'Cible absente du résultat PI enregistré.' }
    # PID_INT and PID_Compact documentary values: nothing is pre-filled, nothing is qualified.
    Press $ident 'Blocs PID_INT et PID_Compact…'
    $blocks = Find $root $name 'MKR — Valeurs des blocs PID_INT et PID_Compact (non qualifiées)'
    if ((Find $blocks $id 'Compute').Current.IsEnabled) { throw 'Calcul actif sans bloc choisi.' }
    function Wait-Values($window, $pattern) {
        $until = [DateTime]::UtcNow.AddSeconds(25)
        do {
            $text = (Find $window $id 'Values').GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).Current.Value
            if ($text -like $pattern) { return $text }
            Start-Sleep -Milliseconds 200
        } while ([DateTime]::UtcNow -lt $until)
        throw "Valeurs de bloc inattendues (attendu $pattern) : $text"
    }
    Select-Option $blocks 'BlockChoice' 'PID_INT'
    Press $blocks 'Calculer'
    if (!(Find $blocks $id 'Status').Current.Name.Contains('Renseignez')) { throw 'Un PID_INT sans valeurs d installation doit être refusé.' }
    Fill $blocks 'PvSpan' '100'
    Fill $blocks 'MvSpan' '100'
    Fill $blocks 'Ts' '2'
    Press $blocks 'Calculer'
    $text = Wait-Values $blocks '*SIMC*'
    if (!$text.Contains('Lambda (Dahlin)') -or !($text.Contains('NON QUALIFIÉ') -or $text.Contains('plage documentée'))) {
        throw "PID_INT : valeurs ou refus documenté attendus. $text"
    }
    Select-Option $blocks 'BlockChoice' 'PID_Compact'
    if ((Find $blocks $id 'Values').GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).Current.Value) {
        throw 'Les valeurs du bloc précédent doivent disparaître au changement de bloc.'
    }
    Select-Option $blocks 'Version' 'V3.0'
    Fill $blocks 'PercentPerUnit' '1'
    Select-Option $blocks 'Cpu' 'S7-1500'
    Fill $blocks 'Firmware' '3.0'
    Press $blocks 'Calculer'
    $text = Wait-Values $blocks '*SIMC*'
    if (!$text.Contains('pas listée') -or $text.Contains('InvertControl')) { throw "V3.0 doit être refusée sur S7-1500 3.0. $text" }
    Fill $blocks 'Firmware' '3.1'
    Press $blocks 'Calculer'
    $text = Wait-Values $blocks '*InvertControl*'
    if (!$text.Contains('NON QUALIFIÉ') -or !$text.Contains('DeadZone = 0') -or $text.Contains('pas listée')) {
        throw "PID_Compact V3.0 sur S7-1500 3.1 : valeurs attendues. $text"
    }
    $blocks.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close()
    Write-Output 'Blocs PID_INT et PID_Compact : saisies obligatoires, refus de compatibilité, valeurs non qualifiées.'
    Select-Option $ident 'ResponseProfile' 'Personnalisée'
    Fill $ident 'ResponseTarget' '3000'
    Press $ident 'Simuler le réglage enregistré…'
    $savedSimulation = Find $root $name 'MKR — Simulation PI'
    $units = (Find $savedSimulation $id 'Units').Current.Name
    if (!$units.Contains('2130 s') -or $units.Contains('3000 s')) {
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
    # NC-03: the guided SIMC target is the conservative upper bound of the published delay interval.
    $upperBound = ' – ' + $initial.ToString('G5') + ' s)'
    if (!$fit.Contains('(intervalle ') -or !$fit.Contains($upperBound)) { throw "La cible SIMC doit être la borne haute de l intervalle du retard. $fit" }
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
    if (!$report.Contains('Variation de consigne 1 bar') -or !$report.Contains('durée 10 s')) { throw "Scénario enregistré ambigu. $report" }
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
    python -X utf8 (Join-Path $PSScriptRoot 'verify_study_pdf.py') $pdfPath
    if ($LASTEXITCODE -ne 0) { throw 'La relecture du PDF a échoué.' }
    Fill $sim 'Rate' ''
    Fill $sim 'Setpoint' '1'
    Press $sim 'Tester le transfert'
    $status = Find $sim $id 'Message'
    $until = [DateTime]::UtcNow.AddSeconds(25)
    do {
        $message = $status.Current.Name
        if ($message -like '*2 → 2 %*' -and $message -like '*saut de commande : 0 %*' -and $message -like '*Lambda (Dahlin)*') { 
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
