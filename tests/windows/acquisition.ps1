param([Parameter(Mandatory)][string]$Executable, [Parameter(Mandatory)][string]$Fixture, [string]$Python = 'python')
# Drives the read-only acquisition window against independent reference servers
# (pymodbus for Schneider Modbus TCP, the Snap7 library for Siemens S7comm).
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
    $until = [DateTime]::UtcNow.AddSeconds(20)
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
function Wait-Text($parent, $field, $pattern, $seconds = 30) {
    $element = Find $parent $id $field
    $until = [DateTime]::UtcNow.AddSeconds($seconds)
    do {
        $text = $element.Current.Name
        if ($text -like $pattern) { return $text }
        Start-Sleep -Milliseconds 200
    } while ([DateTime]::UtcNow -lt $until)
    throw "Texte attendu dans ${field} : $pattern. Obtenu : $text"
}
function Open-Acquisition($root, $project) {
    Press $project 'Acquisition automate…'
    return Find $root $name 'ORIAS — Acquisition en lecture seule'
}

$manifest = Join-Path $env:RUNNER_TEMP 'mkr-ui-interop.json'
if (Test-Path $manifest) { Remove-Item $manifest }
$servers = Start-Process -FilePath $Python -ArgumentList @('-I', (Resolve-Path 'tests/interop/plc_servers.py').Path, $manifest, '15020', '15102') -PassThru -WindowStyle Hidden
function Go-Tab($window, $number) {
    # Single window: the steps of the study are the tabs of the workspace (Step1 ... Step10).
    $tab = Find $window $id "Step$number"
    $tab.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    Start-Sleep -Milliseconds 700
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
$process = $null
try {
    $until = [DateTime]::UtcNow.AddSeconds(30)
    while (!(Test-Path $manifest)) {
        if ($servers.HasExited -or [DateTime]::UtcNow -gt $until) { throw 'Serveurs de référence non démarrés.' }
        Start-Sleep -Milliseconds 200
    }
    Start-Sleep -Milliseconds 500
    $process = [System.Diagnostics.Process]::Start($start)
    $root = $elementType::RootElement
    $main = Find $root $name 'ORIAS — Espace projets'
    $grid = Find $main $id 'ProjectList'
    $row = Find $grid $type ([System.Windows.Automation.ControlType]::DataItem)
    $row.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    Press $main 'Ouvrir le projet sélectionné'
    $null = Find $main $id 'StudyPicker'
    $project = $main      # single window: the project's data are the « Données » tab of the workspace

    # Siemens S7comm: DB10.DBD0/4/8 REAL served by Snap7.
    $window = Open-Acquisition $root $project
    Select-Option $window 'Protocol' 'Siemens*'
    Fill $window 'Host' '127.0.0.1'
    Fill $window 'Port' '15102'
    Fill $window 'DataBlock' '10'
    Fill $window 'SPOffset' '0'
    Fill $window 'PVOffset' '4'
    Fill $window 'MVOffset' '8'
    Fill $window 'ProcessUnit' 'bar'
    Fill $window 'CommandUnit' '%'
    Press $window 'Tester la lecture'
    $null = Wait-Text $window 'Status' "Lecture d’essai réussie*"
    $latest = (Find $window $id 'Latest').Current.Name
    foreach ($part in @('Consigne 52.5 bar', 'Mesure 48.25 bar', 'Sortie de commande -12.75 %')) {
        if (!$latest.Contains($part)) { throw "Valeur S7 absente : $part. Affichage : $latest" }
    }
    Fill $window 'Period' '200'
    Fill $window 'Count' '5'
    $plan = (Find $window $id 'PlanSummary').Current.Name
    if (!$plan.StartsWith('Durée prévue : 00:00 (5 mesures toutes les 200 ms)')) { throw "Durée prévue absente : $plan" }
    Press $window "Démarrer l’acquisition"
    $null = Wait-Text $window 'Status' 'Nombre de mesures atteint.*'
    $progress = (Find $window $id 'ProgressText').Current.Name
    if (!$progress.StartsWith('5 / 5 mesures · 0 manquée')) { throw "Avancement inattendu : $progress" }
    $bar = (Find $window $id 'Progress').GetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern).Current
    if ($bar.Value -ne 5 -or $bar.Maximum -ne 5) { throw "Barre de progression incomplète : $($bar.Value)/$($bar.Maximum)" }
    # End state of a direct acquisition, shown in the state panel (the light and the text agree with the real outcome).
    $finished = Wait-Text $window 'ConnectionState' 'Acquisition terminée*'
    Write-Output "##STATE terminée : $finished"
    Press $window 'Enregistrer dans le projet'
    $report = Wait-Text $project 'Report' '*S7 127.0.0.1:15102*DB10.DBD4*5/5 mesures*'

    # Schneider Modbus TCP: %MW100/102/104 REAL low word first, encoded by pymodbus.
    $window = Open-Acquisition $root $project
    Fill $window 'Host' '127.0.0.1'
    Fill $window 'Port' '15020'
    Fill $window 'UnitId' '7'
    Press $window 'Tester la lecture'
    $null = Wait-Text $window 'Status' '*ordre des octets*'
    Select-Option $window 'Order' 'CDAB*'
    Fill $window 'SPOffset' '100'
    Fill $window 'PVOffset' '102'
    Fill $window 'MVOffset' '104'
    Fill $window 'ProcessUnit' 'bar'
    Fill $window 'CommandUnit' '%'
    Press $window 'Tester la lecture'
    $null = Wait-Text $window 'Status' "Lecture d’essai réussie*"
    $latest = (Find $window $id 'Latest').Current.Name
    foreach ($part in @('Consigne 45.25 bar', 'Mesure -3.5 bar', 'Sortie de commande 61.875 %')) {
        if (!$latest.Contains($part)) { throw "Valeur Modbus absente : $part. Affichage : $latest" }
    }
    # Controller mode: address and "manual" value are entered by the user; nothing is assumed.
    $toggle = (Find $window $id 'ModeEnabled').GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)
    $toggle.Toggle()
    Press $window 'Tester la lecture'
    $null = Wait-Text $window 'Status' "*aucune convention n’est supposée*"
    Fill $window 'ModeOffset' '106'
    Fill $window 'ManualValue' '0'
    Press $window 'Tester la lecture'
    $null = Wait-Text $window 'Latest' '*Mode brut 0 → manuel*'
    Fill $window 'ManualValue' '5'
    Press $window 'Tester la lecture'
    $null = Wait-Text $window 'Latest' '*Mode brut 0 → PAS en manuel*'
    $toggle.Toggle()
    # An address outside the server table is refused explicitly (exception 02).
    Fill $window 'MVOffset' '5000'
    Press $window 'Tester la lecture'
    $null = Wait-Text $window 'Status' '*Exception Modbus 02*'
    # Nobody listens on port 1: the acquisition must end in an explicit failure state, never in « terminée » or « en cours ».
    Fill $window 'MVOffset' '104'
    Fill $window 'Port' '1'
    Fill $window 'Period' '200'
    Fill $window 'Count' '5'
    Press $window "Démarrer l’acquisition"
    # The only legitimate outcome is a refused connection: « Déconnecté » is the state left by the previous probe, so it is not accepted here.
    $failure = Wait-Text $window 'ConnectionState' 'Connexion impossible*' 40
    Write-Output "##STATE échec : $failure"
    $window.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close()

    # Continuous recording: about 3.6 s at 100 ms, cut into segments of 10 readings that overlap by 2.
    $window = Open-Acquisition $root $project
    Fill $window 'Host' '127.0.0.1'
    Fill $window 'Port' '15020'
    Fill $window 'UnitId' '7'
    Select-Option $window 'Order' 'CDAB*'
    Fill $window 'SPOffset' '100'
    Fill $window 'PVOffset' '102'
    Fill $window 'MVOffset' '104'
    Fill $window 'ProcessUnit' 'bar'
    Fill $window 'CommandUnit' '%'
    (Find $window $id 'Continuous').GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle()
    Fill $window 'Period' '100'
    Fill $window 'Hours' '0.001'
    Fill $window 'SegmentSize' '10'
    Fill $window 'Overlap' '2'
    $plan = Wait-Text $window 'PlanSummary' '*segment(s)*'
    if (!$plan.Contains('37 mesures')) { throw "Plan d’enregistrement continu inattendu : $plan" }
    Add-Type -Namespace MkrUiTest -Name NativeDialog -MemberDefinition @'
[System.Runtime.InteropServices.DllImport("user32.dll")]
public static extern System.IntPtr GetDlgItem(System.IntPtr dialog, int id);
[System.Runtime.InteropServices.DllImport("user32.dll")]
public static extern bool PostMessage(System.IntPtr target, uint msg, System.IntPtr wParam, System.IntPtr lParam);
'@
    Press $window "Démarrer l’enregistrement continu"
    $dialog = Find $root $name 'ORIAS — Enregistrement continu terminé'
    if ($dialog.Current.ProcessId -ne $process.Id) { throw 'Dialogue hors du processus MKR.' }
    $summaryText = (@($dialog.FindAll($scope, [System.Windows.Automation.Condition]::TrueCondition) | ForEach-Object { $_.Current.Name }) -join ' ')
    if (!$summaryText.Contains('Durée prévue atteinte.') -or !$summaryText.Contains('segment(s) enregistré(s)') -or !$summaryText.Contains('37 mesures')) {
        throw "Résumé de l’enregistrement continu inattendu : $summaryText"
    }
    # A native message box: try the Win32 button first, then UI Automation; on failure list what the dialog contains.
    $dismissed = $false
    $until = [DateTime]::UtcNow.AddSeconds(15)
    while (!$dismissed -and [DateTime]::UtcNow -lt $until) {
        $handle = [IntPtr]$dialog.Current.NativeWindowHandle
        $ok = [MkrUiTest.NativeDialog]::GetDlgItem($handle, 1)
        if ($ok -ne [IntPtr]::Zero -and [MkrUiTest.NativeDialog]::PostMessage($ok, 0x00F5, [IntPtr]::Zero, [IntPtr]::Zero)) { $dismissed = $true; break }
        $buttons = $dialog.FindAll($scope, [System.Windows.Automation.PropertyCondition]::new($type, [System.Windows.Automation.ControlType]::Button))
        foreach ($button in $buttons) {
            if ($button.Current.Name -in @("OK", "Ok", "D’accord")) {
                $button.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); $dismissed = $true; break
            }
        }
        Start-Sleep -Milliseconds 300
    }
    if (!$dismissed) {
        # This Windows build exposes the OK button as a pane: close the box as its window frame would (returns OK for an OK-only box).
        $closer = $null
        if ($dialog.TryGetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern, [ref]$closer)) {
            $closer.Close(); $dismissed = $true
        }
    }
    if (!$dismissed) {
        $content = @($dialog.FindAll($scope, [System.Windows.Automation.Condition]::TrueCondition) | ForEach-Object { "$($_.Current.ControlType.ProgrammaticName):$($_.Current.Name)" })
        throw "Bouton OK inaccessible. Handle $($dialog.Current.NativeWindowHandle). Contenu : $($content -join ' | ')"
    }
    $combo = Find $project $id 'Imports'
    $items = $combo.GetCurrentPattern([System.Windows.Automation.ItemContainerPattern]::Pattern)
    $labels = @(); $item = $null
    while ($item = $items.FindItemByProperty($item, $null, $null)) { $labels += $item.Current.Name }
    $segments = @($labels | Where-Object { $_ -match 'FileName = historique-[0-9a-f]{8}-[0-9]{3}\.csv' })
    if ($segments.Count -ne 5) { throw "Segments d’historique attendus dans le projet : $($labels -join ' | ')" }
    Write-Output "Enregistrement continu vérifié : $($segments.Count) segments archivés."
    Write-Output "Acquisition S7 et Modbus vérifiée : lecture d’essai, progression, enregistrement et refus d’adresse."
} finally {
    if ($process -and -not $process.HasExited) { Stop-Process -Id $process.Id }
    if (-not $servers.HasExited) { Stop-Process -Id $servers.Id -Force }
}
