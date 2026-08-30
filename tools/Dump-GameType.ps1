param([string[]]$Types, [string]$Filter = "", [switch]$FieldsOnly)
$P="C:\Users\samue\AppData\Roaming\r2modmanPlus-local\Valheim\profiles\QoL\BepInEx"
Add-Type -Path "$P\core\Mono.Cecil.dll"
$managed = "C:\Program Files (x86)\Steam\steamapps\common\Valheim\valheim_Data\Managed"
$rp = New-Object Mono.Cecil.DefaultAssemblyResolver
$rp.AddSearchDirectory($managed)
$pars = New-Object Mono.Cecil.ReaderParameters
$pars.AssemblyResolver = $rp
$mods = @()
foreach ($f in @("assembly_valheim","Assembly-CSharp","assembly_utils","assembly_guiutils")) {
  if (Test-Path "$managed\$f.dll") { $mods += [Mono.Cecil.ModuleDefinition]::ReadModule("$managed\$f.dll", $pars) }
}
function Show($t) {
  Write-Output "==== $($t.FullName)  (base $($t.BaseType)) ===="
  Write-Output "-- fields --"
  foreach ($f in $t.Fields) {
    if ($Filter -and $f.Name -notmatch $Filter) { continue }
    $mod = if ($f.IsStatic) { "static " } else { "" }
    Write-Output ("  {0}{1} {2}" -f $mod, $f.FieldType.Name, $f.Name)
  }
  if ($FieldsOnly) { return }
  Write-Output "-- methods --"
  foreach ($me in $t.Methods) {
    if ($Filter -and $me.Name -notmatch $Filter) { continue }
    $ps = ($me.Parameters | ForEach-Object { "$($_.ParameterType.Name) $($_.Name)" }) -join ", "
    $mod = if ($me.IsStatic) { "static " } else { "" }
    Write-Output ("  {0}{1} {2}({3})" -f $mod, $me.ReturnType.Name, $me.Name, $ps)
  }
}
foreach ($n in $Types) {
  $found = $false
  foreach ($m in $mods) {
    foreach ($t in $m.Types) {
      if ($t.Name -eq $n) { Show $t; $found = $true }
      foreach ($nt in $t.NestedTypes) { if ($nt.Name -eq $n) { Show $nt; $found = $true } }
    }
  }
  if (-not $found) { Write-Output "!! NOT FOUND: $n" }
}
