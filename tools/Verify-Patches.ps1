param([string]$Dll = "C:\Users\samue\Documents\Code\valheim\gungnir-staff\GungnirStaff\bin\Debug\GungnirStaff.dll")

$P = "C:\Users\samue\AppData\Roaming\r2modmanPlus-local\Valheim\profiles\QoL\BepInEx"
Add-Type -Path "$P\core\Mono.Cecil.dll"
$managed = "C:\Program Files (x86)\Steam\steamapps\common\Valheim\valheim_Data\Managed"

$rp = New-Object Mono.Cecil.DefaultAssemblyResolver
$rp.AddSearchDirectory($managed)
$rp.AddSearchDirectory("$P\core")
$rp.AddSearchDirectory("$P\plugins\ValheimModding-Jotunn")
$pars = New-Object Mono.Cecil.ReaderParameters
$pars.AssemblyResolver = $rp

# Index every type in the game assemblies (including nested).
#
# Keyed on the FULL name ("Piece/Requirement") as well as the short one, because
# short names collide: Incinerator also has a nested Requirement, and whichever
# one happened to be indexed first used to shadow the other - reporting a
# perfectly valid patch target as NOT FOUND.
$gameTypes = @{}
function Index($t) {
  $gameTypes[$t.FullName] = $t
  if (-not $gameTypes.ContainsKey($t.Name)) { $gameTypes[$t.Name] = $t }
  foreach ($n in $t.NestedTypes) { Index $n }
}
foreach ($f in @("assembly_valheim", "Assembly-CSharp", "assembly_utils", "assembly_guiutils")) {
  if (Test-Path "$managed\$f.dll") {
    $gm = [Mono.Cecil.ModuleDefinition]::ReadModule("$managed\$f.dll", $pars)
    foreach ($t in $gm.Types) { Index $t }
  }
}

$mod = [Mono.Cecil.ModuleDefinition]::ReadModule($Dll, $pars)
$fail = 0
$ok = 0

# Patch classes can be nested inside another type. Harmony's PatchAll finds those
# through Assembly.GetTypes(); this script used to walk only the top level, so a
# nested patch was invisible here - reported as neither OK nor FAILED, which is the
# worst outcome for a tool whose job is to prove nothing is missing.
$modTypes = New-Object System.Collections.ArrayList
function CollectTypes($t) {
  [void]$modTypes.Add($t)
  foreach ($n in $t.NestedTypes) { CollectTypes $n }
}
foreach ($t in $mod.Types) { CollectTypes $t }

foreach ($t in $modTypes) {
  # Class-level [HarmonyPatch(...)]
  $classType = $null
  $classMethod = $null
  foreach ($ca in $t.CustomAttributes) {
    if ($ca.AttributeType.Name -ne "HarmonyPatch") { continue }
    foreach ($a in $ca.ConstructorArguments) {
      if ($a.Type.Name -eq "Type") { $classType = $a.Value.FullName }
      elseif ($a.Type.Name -eq "String") { $classMethod = $a.Value }
    }
  }
  if (-not $classType) { continue }

  foreach ($me in $t.Methods) {
    $mName = $classMethod
    $hasPatchAttr = $false
    foreach ($ca in $me.CustomAttributes) {
      if ($ca.AttributeType.Name -eq "HarmonyPatch") {
        $hasPatchAttr = $true
        foreach ($a in $ca.ConstructorArguments) {
          if ($a.Type.Name -eq "String") { $mName = $a.Value }
        }
      }
      if ($ca.AttributeType.Name -match "^Harmony(Prefix|Postfix|Transpiler|Finalizer)$") { $hasPatchAttr = $true }
    }
    # Harmony also recognises patch methods by name alone, with no attribute.
    if ($me.Name -match "^(Prefix|Postfix|Transpiler|Finalizer)$") { $hasPatchAttr = $true }
    if (-not $hasPatchAttr -or -not $mName) { continue }

    # Full name first ("Piece/Requirement"), short name as the fallback.
    $target = $gameTypes[$classType]
    if (-not $target) { $target = $gameTypes[($classType -split "/")[-1]] }
    if (-not $target) {
      Write-Output "  FAIL  $($t.Name).$($me.Name) -> type '$classType' NOT FOUND"
      $fail++
      continue
    }
    $hit = $target.Methods | Where-Object { $_.Name -eq $mName }
    if (-not $hit) {
      Write-Output "  FAIL  $($t.Name).$($me.Name) -> $classType.$mName NOT FOUND"
      $fail++
      continue
    }
    $sig = ($hit[0].Parameters | ForEach-Object { $_.ParameterType.Name }) -join ", "
    Write-Output "  OK    $($t.Name).$($me.Name) -> $classType.$mName($sig)"
    $ok++
  }
}

Write-Output ""
Write-Output "Harmony targets resolved: $ok OK, $fail FAILED"
if ($fail -gt 0) { exit 1 }
