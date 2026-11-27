param([string]$Executable = "src/Mkr.Desktop/bin/Release/net10.0-windows/Mkr.Desktop.exe")
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
$root = [System.Windows.Automation.AutomationElement]::RootElement
$scope = [System.Windows.Automation.TreeScope]::Descendants

function Wait-Element($parent, $property, $value) {
    $condition = [System.Windows.Automation.PropertyCondition]::new($property, $value)
    $deadline = [DateTime]::UtcNow.AddSeconds(20)
    do {
        $element = $parent.FindFirst($scope, $condition)
        if ($null -ne $element) { return $element }
        Start-Sleep -Milliseconds 200
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "Élément WPF introuvable : $value"
}

$exe = Resolve-Path $Executable
$process = Start-Process -FilePath $exe.Path -PassThru
try {
    $nameProperty = [System.Windows.Automation.AutomationElement]::NameProperty
    $idProperty = [System.Windows.Automation.AutomationElement]::AutomationIdProperty
    try { $main = Wait-Element $root $nameProperty 'ORIAS — Espace projets' }
    catch {
        # Diagnostic only: say whether the application is still alive and what the runtime reported, instead of a bare « introuvable ».
        Write-Host "##DIAG exited=$($process.HasExited) exitcode=$(if ($process.HasExited) { $process.ExitCode })"
        try { Get-WinEvent -FilterHashtable @{ LogName = 'Application'; Level = 2 } -MaxEvents 4 | ForEach-Object { $m = ($_.Message -replace "`r?`n", ' | '); Write-Host ("##DIAG " + $_.ProviderName + ' ' + $m.Substring(0, [Math]::Min(1500, $m.Length))) } } catch { Write-Host '##DIAG no event log' }
        $log = Join-Path $env:LOCALAPPDATA 'MKR\mkr.log'
        if (Test-Path $log) {
            $lines = @(Get-Content -LiteralPath $log)
            $at = [Array]::FindIndex($lines, [Predicate[string]]{ param($l) $l -like '*startup detail*' })
            if ($at -ge 0) { $lines[$at..([Math]::Min($at + 14, $lines.Count - 1))] | ForEach-Object { Write-Host "##DIAG mkr.log $_" } } else { $lines | Select-Object -Last 15 | ForEach-Object { Write-Host "##DIAG mkr.log $_" } }
        } else { Write-Host '##DIAG no mkr.log' }
        throw
    }
    $button = Wait-Element $main $nameProperty 'Ouvrir le catalogue des blocs'
    $button.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    $catalogue = Wait-Element $root $nameProperty 'ORIAS — Catalogue des blocs'
    $filter = Wait-Element $catalogue $idProperty 'Filter'
    $filter.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue('PIDFF')
    $list = Wait-Element $catalogue $idProperty 'Blocks'
    $itemType = [System.Windows.Automation.PropertyCondition]::new(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::ListItem)
    $deadline = [DateTime]::UtcNow.AddSeconds(10)
    do {
        $items = $list.FindAll($scope, $itemType)
        if ($items.Count -eq 1) { break }
        Start-Sleep -Milliseconds 200
    } while ([DateTime]::UtcNow -lt $deadline)
    if ($items.Count -ne 1) { throw 'La recherche PIDFF doit afficher un seul bloc.' }
    $null = Wait-Element $catalogue $nameProperty 'Compatibilité à vérifier'
    $evidence = Wait-Element $catalogue $idProperty 'Evidence'
    if ($evidence.Current.Name -notlike '*ti TIME*' -or $evidence.Current.Name -notlike '*33002535K01000*') {
        throw 'Les paramètres typés et la référence de la fiche PIDFF doivent être visibles.'
    }
    $copy = Wait-Element $catalogue $idProperty 'CopySheet'
    $copy.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    $null = Wait-Element $catalogue $nameProperty 'Relevé PIDFF copié.'
    $sheet = Get-Clipboard -Raw
    foreach ($required in @('Relevé de banc · PIDFF', 'Version firmware', 'mix_par [33002535K01000',
                             'en_rcpy [33002535K01000', 'tolérance fixée avant essai', 'trace de mesure')) {
        if (!$sheet.Contains($required)) { throw "Champ du relevé absent : $required" }
    }
    # Bloc sans propriété documentée : « PIFINT » (erreur de nomenclature du client, c'est PID_INT) a été retiré le 2026-11-27 ; PI_B n'a pas encore
    # de paramètres dans le catalogue. Quand ils y seront, choisir une autre entrée sans propriété ou adapter ce contrôle (état vide + relevé propre au bloc).
    $filter.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue('PI_B')
    $null = Wait-Element $catalogue $nameProperty 'Aucune propriété documentée dans cette édition du catalogue.'
    $copy.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    $null = Wait-Element $catalogue $nameProperty 'Relevé PI_B copié.'
    $other = Get-Clipboard -Raw
    if (!$other.Contains('Relevé de banc · PI_B') -or $other.Contains('mix_par')) {
        throw 'Le relevé doit correspondre au bloc sélectionné.'
    }
    Write-Output 'Accueil, ouverture du catalogue, filtre PIDFF et statut vérifiés.'
} finally {
    if (-not $process.HasExited) { Stop-Process -Id $process.Id }
}
