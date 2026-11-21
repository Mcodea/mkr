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

$exe = Resolve-Path 'src/Mkr.Desktop/bin/Release/net10.0-windows/Mkr.Desktop.exe'
$process = Start-Process -FilePath $exe.Path -PassThru
try {
    $nameProperty = [System.Windows.Automation.AutomationElement]::NameProperty
    $idProperty = [System.Windows.Automation.AutomationElement]::AutomationIdProperty
    $main = Wait-Element $root $nameProperty 'MKR — Espace projets'
    $button = Wait-Element $main $nameProperty 'Ouvrir le catalogue des blocs'
    $button.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    $catalogue = Wait-Element $root $nameProperty 'MKR — Catalogue des blocs'
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
    Write-Output 'Accueil, ouverture du catalogue, filtre PIDFF et statut vérifiés.'
} finally {
    if (-not $process.HasExited) { Stop-Process -Id $process.Id }
}
