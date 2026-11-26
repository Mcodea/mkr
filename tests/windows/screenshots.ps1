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
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
$out = Join-Path $env:RUNNER_TEMP 'mkr-captures'
New-Item -ItemType Directory -Force -Path $out | Out-Null
$script:SettleMs = 900
function Capture($window, $label) {
    try {
        # bring the window forward without moving the keyboard focus (UIA SetFocus restores the focus to a navigation button and raises its tooltip)
        try { [void][Mkr.Mouse]::SetForegroundWindow([IntPtr]$window.Current.NativeWindowHandle) } catch { }
        # park the pointer on the title bar, then let any tooltip time out
        try { $tb = $window.Current.BoundingRectangle; [void][Mkr.Mouse]::SetCursorPos([int]($tb.X + $tb.Width / 2), [int]($tb.Y + 14)) } catch { }
        Start-Sleep -Milliseconds $script:SettleMs
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

Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public class MkrDisplay {
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct DEVMODE {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
        public short dmSpecVersion, dmDriverVersion, dmSize, dmDriverExtra;
        public int dmFields, dmPositionX, dmPositionY, dmDisplayOrientation, dmDisplayFixedOutput;
        public short dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
        public short dmLogPixels;
        public int dmBitsPerPel, dmPelsWidth, dmPelsHeight, dmDisplayFlags, dmDisplayFrequency, dmICMMethod, dmICMIntent, dmMediaType, dmDitherType, dmReserved1, dmReserved2, dmPanningWidth, dmPanningHeight;
    }
    [DllImport("user32.dll", CharSet = CharSet.Ansi)] public static extern bool EnumDisplaySettings(string device, int mode, ref DEVMODE dm);
    [DllImport("user32.dll", CharSet = CharSet.Ansi)] public static extern int ChangeDisplaySettings(ref DEVMODE dm, int flags);
    public static int Set(int width, int height) {
        DEVMODE dm = new DEVMODE();
        dm.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
        EnumDisplaySettings(null, -1, ref dm);
        dm.dmPelsWidth = width; dm.dmPelsHeight = height; dm.dmFields = 0x80000 | 0x100000;
        return ChangeDisplaySettings(ref dm, 0);
    }
}
"@
function Set-Resolution($width, $height) {
    # 0 = success. The achieved size is logged: a runner without a display driver may refuse, and then the capture says so instead of pretending.
    $code = [MkrDisplay]::Set($width, $height)
    Start-Sleep -Milliseconds 2500
    $actual = [System.Windows.Forms.SystemInformation]::PrimaryMonitorSize
    Write-Host "##RESOLUTION requested ${width}x${height} code $code actual $($actual.Width)x$($actual.Height)"
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
        $mainHandle = $mainWindow.Current.NativeWindowHandle
        # the main window is identified by its handle: in the workspace its title is the project's, not « Espace projets »
        $others = @($found | Where-Object { $_.Current.Name -like 'ORIAS — *' -and $_.Current.NativeWindowHandle -ne $mainHandle })
        if ($others.Count -eq 0) { return }
        foreach ($other in $others) {
            try { $other.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close() } catch { }
            Start-Sleep -Milliseconds 600
        }
    }
}
$script:failures = @()
function Check($label, [scriptblock]$body) {
    # Unlike Step (captures never fail the run), a Check is an assertion: a failure fails the CI job.
    try {
        if (& $body) { Write-Host "##CHECK PASS $label" } else { $script:failures += $label; Write-Host "##CHECK FAIL $label" }
    } catch { $script:failures += $label; Write-Host "##CHECK FAIL $label : $($_.Exception.Message)" }
}
Add-Type -Name Mouse -Namespace Mkr -MemberDefinition @"
[DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
[DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra);
[DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
"@
function Mouse-Move($x, $y) { [void][Mkr.Mouse]::SetCursorPos([int]$x, [int]$y); Start-Sleep -Milliseconds 250 }
function Mouse-Drag($x1, $y1, $x2, $y2) {
    Mouse-Move $x1 $y1
    [Mkr.Mouse]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero)         # left button down
    foreach ($k in 1..8) { Mouse-Move ($x1 + ($x2 - $x1) * $k / 8) ($y1 + ($y2 - $y1) * $k / 8) }
    [Mkr.Mouse]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)         # left button up
    Start-Sleep -Milliseconds 400
}
function View-Of($chart) { ($chart.Current.ItemStatus -split ' ; ')[0] }
function Save-Through-Dialog($main, $path) {
    # Common « Save as » dialog: file name field (id 1001), Save button (id 1).
    $deadline = [DateTime]::UtcNow.AddSeconds(20)
    $dialog = $null
    do {
        $cond = [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ClassNameProperty, '#32770')
        $dialog = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
        if ($null -eq $dialog) { Start-Sleep -Milliseconds 300 }
    } while ($null -eq $dialog -and [DateTime]::UtcNow -lt $deadline)
    if ($null -eq $dialog) { throw 'boîte « Enregistrer sous » introuvable' }
    $AE = [System.Windows.Automation.AutomationElement]
    $desc = [System.Windows.Automation.TreeScope]::Descendants
    # the name field is the native Edit of id 1001 (the address bar also has an id 1001, but it is a toolbar): select it by class
    $nameField = $null
    $fieldDeadline = [DateTime]::UtcNow.AddSeconds(15)
    do {
        # the dialog fills its tree progressively: look again (and re-find the dialog) until the field appears
        $dialog = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ClassNameProperty, '#32770'))
        if ($null -ne $dialog) {
            foreach ($e in $dialog.FindAll($desc, [System.Windows.Automation.PropertyCondition]::new($AE::AutomationIdProperty, '1001'))) {
                if ($e.Current.ClassName -eq 'Edit') { $nameField = $e; break }
            }
        }
        if ($null -eq $nameField) { Start-Sleep -Milliseconds 400 }
    } while ($null -eq $nameField -and [DateTime]::UtcNow -lt $fieldDeadline)
    if ($null -eq $nameField) { throw 'zone du nom de fichier (Edit 1001) introuvable dans la boîte Enregistrer sous' }
    $vp = $null
    if ($nameField.TryGetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern, [ref]$vp)) { $vp.SetValue($path) }
    else {
        Write-Host '##DIAG saisie clavier du nom de fichier'
        [void][Mkr.Mouse]::SetForegroundWindow([IntPtr]$dialog.Current.NativeWindowHandle)
        Start-Sleep -Milliseconds 500
        # a real click in the name field gives it the keyboard focus whatever the automation tree says
        $fr = $nameField.Current.BoundingRectangle
        Mouse-Move ($fr.X + $fr.Width / 2) ($fr.Y + $fr.Height / 2)
        [Mkr.Mouse]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero); [Mkr.Mouse]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)
        Start-Sleep -Milliseconds 400
        [System.Windows.Forms.SendKeys]::SendWait('^a')
        # paste the path (typing a long path key by key loses characters on a loaded runner)
        try { [System.Windows.Forms.Clipboard]::SetText($path); [System.Windows.Forms.SendKeys]::SendWait('^v') }
        catch { [System.Windows.Forms.SendKeys]::SendWait(($path -replace '([+^%~(){}\[\]])', '{$1}')) }
        Start-Sleep -Milliseconds 400
    }
    $save = $dialog.FindFirst($desc, [System.Windows.Automation.PropertyCondition]::new($AE::AutomationIdProperty, '1'))
    $invoke = $null
    if ($null -ne $save -and $save.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$invoke)) { $invoke.Invoke() }
    else { [System.Windows.Forms.SendKeys]::SendWait('{ENTER}') }
    # an existing file asks for a confirmation: answer « Yes » (« Oui »)
    Start-Sleep -Milliseconds 800
    foreach ($b in $root.FindAll($desc, [System.Windows.Automation.Condition]::TrueCondition)) {
        if ($b.Current.Name -in @('Yes', 'Oui') -and $b.Current.ClassName -eq 'Button') {
            $ip = $null
            if ($b.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$ip)) { $ip.Invoke() } else { [System.Windows.Forms.SendKeys]::SendWait('y') }
            Write-Host '##DIAG remplacement confirme'
            break
        }
    }
    $deadline = [DateTime]::UtcNow.AddSeconds(15)
    while (((-not (Test-Path $path)) -or ((Get-Item $path).Length -eq 0)) -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 300 }
}
function Step($label, [scriptblock]$body) {
    try { & $body } catch {
        Write-Host "##CAPTURE-FAILED $label : $($_.Exception.Message)"
        # a captures step never fails the run, but the interaction checks are assertions: not being able to run them is a failure
        if ($label -like '*interactions*') { $script:failures += "$label : $($_.Exception.Message)" }
        # a dialog left open by the failed step would hide every following capture
        $left = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ClassNameProperty, '#32770'))
        $tries = 0
        while ($null -ne $left -and $tries -lt 4) {
            [System.Windows.Forms.SendKeys]::SendWait('{ESC}'); Start-Sleep -Milliseconds 600
            $left = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ClassNameProperty, '#32770'))
            $tries++
        }
    }
}
$start = New-Object System.Diagnostics.ProcessStartInfo
$start.FileName = (Resolve-Path $Executable).Path
$start.UseShellExecute = $false
$start.EnvironmentVariables['MKR_DATA_DIRECTORY'] = (Resolve-Path $Fixture).Path
$start.EnvironmentVariables['MKR_THEME'] = $Theme
$exportDir = Join-Path $env:RUNNER_TEMP "mkr-exports-$Theme"
Remove-Item -Recurse -Force $exportDir -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $exportDir | Out-Null
$start.EnvironmentVariables['MKR_EXPORT_DIRECTORY'] = $exportDir     # the exports of the plots are written here, without the operating system dialog
$start.EnvironmentVariables['MKR_CULTURE'] = 'fr-FR'      # captures show the French date and number format whatever the runner's regional settings
$process = [System.Diagnostics.Process]::Start($start)
try {
    $root = $elementType::RootElement
    $main = Find $root $name 'ORIAS — Espace projets'
    Capture $main '01-espace-projets'
    $row = Find (Find $main $id 'ProjectList') $type ([System.Windows.Automation.ControlType]::DataItem)
    $row.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    Press $main 'Ouvrir le projet sélectionné'
    $null = Find $main $id 'StudyPicker'
    $project = $main      # single window: the project's data are the « Données » tab of the workspace
    Capture $project '02-projet'
    Press $project 'Préparer les essais sur banc…'
    $catalogue = Find $root $name 'ORIAS — Catalogue des blocs'
    Fill $catalogue 'Filter' 'PIDFF'
    $null = Find $catalogue $name 'Compatibilité à vérifier'
    Capture $catalogue '03-catalogue-blocs'
    Close-Window $catalogue
    Press $project 'Identification…'
    $null = Find $main $id 'StudyList'
    $ident = $main        # single window: the identification is the « Identification » tab
    Show-Page $ident 'PageResults'
    $modelId = (Get-Content -LiteralPath (Join-Path $Fixture 'short-delay-study.txt') -Raw).Trim()
    Select-Option $ident 'StudyList' "*$modelId"
    Capture $ident '04-identification'
    Show-Page $ident 'PageSetup'
    Step '04b-courbes' {
        Select-Option $ident 'TrainingSource' '*'
        Start-Sleep -Milliseconds 500
        Press-Retry $ident 'Afficher les courbes brutes (estimation)…'
        $preview = Find $root $name 'ORIAS — Prévisualisation des signaux'
        Start-Sleep -Milliseconds 900
        Capture $preview '04b-courbes-sources'
        Close-Window $preview
    }
    Step '05-revue' {
        try { Press $ident "Revue de l'étude…" } catch { }
        $review = Find $root $name "ORIAS — Revue de l'étude"
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
        $tuning = Find $root $name "ORIAS — Réglage d'un procédé intégrateur"
        Fill $tuning 'ResponseSeconds' '8'
        Press-Retry $tuning 'Calculer'
        $null = Wait-Value $tuning 'Result' '*SIMC_integrating*'
        Capture $tuning '06-reglage-integrateur'
        Press-Retry $tuning 'Valeurs du bloc PIDFF…'
        $values = Find $root $name 'ORIAS — Valeurs du bloc PIDFF (non qualifiées)'
        $null = Wait-Value $values 'Values' '*NON QUALIFIÉ*'
        Capture $values '07-valeurs-bloc-pidff'
        Close-Window $values
        Close-Window $tuning
    }
    Step '09-acquisition' {
        Go-Tab $main 2
        Press-Retry $project 'Acquisition automate…'
        $acq = Find $root $name 'ORIAS — Acquisition en lecture seule'
        Capture $acq '09-acquisition'
        Close-Window $acq
        Go-Tab $main 4
    }
    Step '11-simulation' {
        Select-Option $ident 'StudyList' "*$modelId"
        Select-Option $ident 'ResponseProfile' 'Personnalisée'
        Fill $ident 'ResponseTarget' '12'
        try { Press $ident 'Comparer Lambda et SIMC' } catch { }
        $null = Wait-Name $ident 'Message' 'Réglages théoriques PI enregistrés.' 60      # the engine call can be slow on a loaded runner (one light-theme run missed the default 15 s)
        try { Press $ident 'Simuler le réglage enregistré…' } catch { }   # a button that opens a modal dialog may report a UIA error although it worked
        $sim = Find $root $name 'ORIAS — Simulation PI'
        foreach ($entry in @{Sample='1';Duration='120';Setpoint='1';Lower='-5';Upper='5';Disturbance='0';DisturbanceAt='0';ManualMv='0';SwitchTime='5'}.GetEnumerator()) { Fill $sim $entry.Key $entry.Value }
        try { Press $sim 'Simuler et enregistrer' } catch { }
        $null = Find $sim $name 'Simulation enregistrée.'
        Start-Sleep -Milliseconds 1000
        Capture $sim '11-simulation'
        Step '12-prototype-pid' {
            try { Press $sim 'Essai PID manuel…' } catch { }
            $proto = Find $root $name 'ORIAS — Laboratoire PID hors ligne'
            Press-Retry $proto 'Comparer PID / PI'
            Start-Sleep -Milliseconds 2500
            Capture $proto '12-prototype-pid'
            Close-Window $proto
        }
        Close-Window $sim
    }
    Step '13-boucle-ouverte' {
        Press-Retry $ident 'Simuler en boucle ouverte…'
        $open = Find $root $name 'ORIAS — Simulation en boucle ouverte'
        foreach ($entry in @{Sample='1';Duration='100';StepAt='0';StepMv='2';Lower='-5';Upper='5'}.GetEnumerator()) { Fill $open $entry.Key $entry.Value }
        Press-Retry $open 'Simuler'
        Start-Sleep -Milliseconds 2000
        Capture $open '13-boucle-ouverte'
        Close-Window $open
    }
    Step '08-audit' {
        Go-Tab $main 2
        Press-Retry $project "Journal d'audit…"
        $audit = Find $root $name "ORIAS — Journal d'audit"
        Start-Sleep -Milliseconds 800
        Capture $audit '08-journal-audit'
        Close-Window $audit
    }
    Step '10-espace-etude' {
        # a dialog left open by an earlier step (the review window) must not hide the main window
        Close-Others $root $main
        Go-Home $main
        Press-Retry $main "Ouvrir l'espace d'étude"
        $null = Find $main $id 'StudyPicker'
        Start-Sleep -Milliseconds 1200
        Capture $main '10-espace-etude'
        $tab = Find $main $id 'Step4'
        $tab.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
        Start-Sleep -Milliseconds 500
        Show-Page $main 'PageResults'
        Capture $main '10b-onglet-identification'
        foreach ($entry in @(@('Step2','10d-onglet-donnees'), @('Step3','10g-onglet-diagnostic'), @('Step5','10e-onglet-validation'), @('Step8','10f-onglet-reglage'))) {
            $other = Find $main $id $entry[0]
            $other.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
            Start-Sleep -Milliseconds 700
            Capture $main $entry[1]
        }
        # the chart commands belong to the first tab only (the other tabs show their own view)
        $first = Find $main $id 'Step1'
        $first.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
        Start-Sleep -Milliseconds 500
        Press-Retry $main 'Agrandir'
        Start-Sleep -Milliseconds 700
        Capture $main '10c-graphique-agrandi'
        Press-Retry $main 'Réduire'
        $step6 = Find $main $id 'Step6'
        $step6.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
        Start-Sleep -Milliseconds 500
        # single window: the guided tuning is shown inside the study workspace (no dialog)
        $null = Find $main $id 'Objective'
        try { Press $main 'Calculer le réglage guidé' } catch { }
        $null = Wait-Name $main 'Status' '*.'
        Start-Sleep -Milliseconds 800
        Capture $main '14-reglage-guide'
        # the rest of the result (comparison table, curves, notes) is below the fold: scroll the page like a user would
        foreach ($page in 1..2) {
            try { $main.SetFocus() } catch { }
            [System.Windows.Forms.SendKeys]::SendWait('{PGDN}')
            Start-Sleep -Milliseconds 700
            Capture $main "14$([char](97 + $page))-reglage-guide-suite$page"
        }
        $step9 = Find $main $id 'Step9'
        $step9.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
        Start-Sleep -Milliseconds 1200
        Capture $main '15-simulation-integree'
    }
    Step '10i-chart-interactions' {
        Close-Others $root $main
        $tab = Find $main $id 'Step4'
        $tab.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
        Show-Page $main 'PageResults'
        $noisyId = (Get-Content -LiteralPath (Join-Path $Fixture 'noisy-demo-study.txt') -Raw).Trim()
        Select-Option $main 'StudyList' "*$noisyId"
        Start-Sleep -Milliseconds 1000
        $apos = [string][char]0x2019
        $main1 = Find $main $name ('Réponse du procédé : mesure observée et modèle identifié, en fonction du temps')
        $cmd = Find $main $name ("Commande de l${apos}essai, en fonction du temps")
        $res = Find $main $name ('Résidus (mesure moins modèle), en fonction du temps')
        $full = View-Of $main1
        Check 'les trois graphiques partagent la même vue au départ' { (View-Of $cmd) -eq $full -and (View-Of $res) -eq $full }
        Press $main 'Zoom avant'
        Start-Sleep -Milliseconds 400
        $zoomed = View-Of $main1
        Check 'zoom avant : la vue se resserre' { $zoomed -ne $full }
        Check 'zoom avant : les trois graphiques restent synchronisés' { (View-Of $cmd) -eq $zoomed -and (View-Of $res) -eq $zoomed }
        foreach ($try in 1..3) {
            Press $main 'Sélectionner une plage A–B'      # gives the keyboard focus to the main chart
            Start-Sleep -Milliseconds 300
            [System.Windows.Forms.SendKeys]::SendWait('{RIGHT}')
            Start-Sleep -Milliseconds 500
            if ((View-Of $main1) -ne $zoomed) { break }   # the key can be lost while the window settles: retry, the check below still requires a real pan
        }
        $panned = View-Of $main1
        $panDeadline = [DateTime]::UtcNow.AddSeconds(4)
        while ((View-Of $cmd) -ne (View-Of $main1) -and [DateTime]::UtcNow -lt $panDeadline) { Start-Sleep -Milliseconds 200 }
        $panned = View-Of $main1
        Check 'déplacement (flèche droite) : la fenêtre glisse' { $panned -ne $zoomed -and (View-Of $cmd) -eq $panned }
        Press $main 'Zoom arrière'; Press $main 'Zoom arrière'; Press $main 'Zoom arrière'
        Start-Sleep -Milliseconds 400
        Write-Host "##DIAG vue complete [$full] apres trois zoom arriere [$(View-Of $main1)] apres deplacement [$panned]"
        Check 'zoom arrière : retour à la vue complète' { (View-Of $main1) -eq $full }
        Press $main 'Zoom avant'
        Press $main "Ajuster à tout l${apos}essai"
        Start-Sleep -Milliseconds 400
        Check 'ajuster : vue complète sur les trois graphiques' { (View-Of $main1) -eq $full -and (View-Of $cmd) -eq $full -and (View-Of $res) -eq $full }
        $r = $main1.Current.BoundingRectangle
        $cx = $r.X + $r.Width * 0.5; $cy = $r.Y + $r.Height * 0.5
        Mouse-Move $cx $cy
        Check 'curseur : la position se reflète sur Commande et Résidus' { ($cmd.Current.ItemStatus -like '*curseur*') -and ($res.Current.ItemStatus -like '*curseur*') }
        Mouse-Drag ($r.X + $r.Width * 0.3) $cy ($r.X + $r.Width * 0.6) $cy
        Mouse-Move ($r.X + 5) ($r.Y - 60)            # leave the chart so that the legend line is shown again
        $legend = Find $main $id 'PlotLegend'
        Check 'sélection A–B : la plage choisie est annoncée' { $legend.Current.Name -like '*plage A–B choisie*' }
        Check 'sélection A–B : les indicateurs sont recalculés sur la plage' { (Find $main $name 'FIT (validation A–B, NRMSE)') -ne $null }
        Press $main 'Effacer la plage A–B'
        Start-Sleep -Milliseconds 500
        Check 'effacer A-B : retour a la plage par defaut de l ajustement' { (Find $main $id 'PlotLegend').Current.Name -like '*intervalle de l*ajustement*' }
        $csv = Join-Path $exportDir 'diagnostic-modele.csv'
        $png = Join-Path $exportDir 'diagnostic-modele.png'
        function Wait-File($path) {
            $until = [DateTime]::UtcNow.AddSeconds(15)
            while (((-not (Test-Path $path)) -or ((Get-Item $path).Length -eq 0)) -and [DateTime]::UtcNow -lt $until) { Start-Sleep -Milliseconds 300 }
        }
        Press $main 'Exporter les données'
        Wait-File $csv
        $csvText = if (Test-Path $csv) { [System.IO.File]::ReadAllText($csv, [System.Text.Encoding]::UTF8) } else { '' }
        Write-Host "##DIAG csv : $($csvText.Length) caracteres ; debut [$($csvText.Substring(0, [Math]::Min(160, $csvText.Length)) -replace "`r|`n", ' / ')]"
        Check 'export des données : CSV écrit avec les trois graphiques' {
            $csvText.StartsWith('piste;courbe;unite;temps_s;valeur') -and $csvText.Contains('Commande') -and $csvText.Contains('Résidus')
        }
        Press $main "Exporter l${apos}image"
        Wait-File $png
        Check 'export de limage : PNG valide' {
            (Test-Path $png) -and ((Get-Item $png).Length -gt 2000) -and ([System.IO.File]::ReadAllBytes($png)[1] -eq 0x50)
        }
    }
    Step '21-screens-resolutions' {
        # The six screens at both resolutions (12 captures), on the study that has a stored PI tuning and simulation.
        Close-Others $root $main
        $modelId = (Get-Content -LiteralPath (Join-Path $Fixture 'short-delay-study.txt') -Raw).Trim()
        Select-Option $main 'StudyPicker' "*$modelId"
        Start-Sleep -Milliseconds 800
        $script:SettleMs = 6000
        $original = [System.Windows.Forms.SystemInformation]::PrimaryMonitorSize
        $screens = @(@('1','acquisition','Step2'), @('2','diagnostic','Step3'), @('3','identification','Step4'), @('4','reglage-pid','Step6'), @('5','simulation','Step9'), @('6','validation','Step5'), @('7','rapport','Step10'))
        foreach ($size in @(@(1920,1080), @(1366,768))) {
            Set-Resolution $size[0] $size[1]
            Start-Sleep -Milliseconds 1500
            foreach ($screen in $screens) {
                $tabElement = Find $main $id $screen[2]
                $tabElement.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
                Start-Sleep -Milliseconds 900
                if ($screen[1] -eq 'identification') { Show-Page $main 'PageResults' }
                if ($screen[1] -eq 'reglage-pid') {
                    try { Press $main 'Calculer le réglage guidé' } catch { }
                    $null = Wait-Name $main 'Status' '*.' 40
                    Start-Sleep -Milliseconds 800
                }
                Capture $main ("21-{0}-{1}-{2}x{3}" -f $screen[0], $screen[1], $size[0], $size[1])
            }
        }
        Set-Resolution $original.Width $original.Height
    }
    Step '10h-identification-resolutions' {
        Close-Others $root $main
        $tab = Find $main $id 'Step4'
        $tab.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
        Show-Page $main 'PageResults'
        $noisyId = (Get-Content -LiteralPath (Join-Path $Fixture 'noisy-demo-study.txt') -Raw).Trim()
        Select-Option $main 'StudyList' "*$noisyId"
        Start-Sleep -Milliseconds 800
        $original = [System.Windows.Forms.SystemInformation]::PrimaryMonitorSize
        foreach ($size in @(@(1920,1080), @(1366,768))) {
            Set-Resolution $size[0] $size[1]
            Start-Sleep -Milliseconds 1500
            Capture $main ("20-identification-{0}x{1}" -f $size[0], $size[1])
        }
        Set-Resolution $original.Width $original.Height
    }
} catch {
    Write-Host "##CAPTURE-ABORTED $($_.Exception.Message)"
} finally {
    if (-not $process.HasExited) { Stop-Process -Id $process.Id }
}
if ($script:failures.Count -gt 0) {
    Write-Host "##CHECKS-FAILED $($script:failures -join ' | ')"
    exit 1
}
exit 0
