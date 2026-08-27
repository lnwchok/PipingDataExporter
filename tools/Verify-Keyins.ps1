<#
.SYNOPSIS
    Verifies that every KeyinHandler in the built AddIn's embedded Commands.xml actually resolves.

.DESCRIPTION
    MicroStation resolves KeyinHandler Function="Namespace.Class.Method" strings by reflection at
    runtime. A typo, a rename, or a moved class does not fail the build and does not raise an error
    on load - the key-in simply never exists. So does a wrong MdlTaskID or a mis-named embedded
    resource. This script checks all three against the compiled DLL.

.EXAMPLE
    pwsh -File tools/Verify-Keyins.ps1
    pwsh -File tools/Verify-Keyins.ps1 -DllPath bin/x64/Release/net48/PipingDataExporter.dll
#>
[CmdletBinding()]
param(
    [string] $DllPath = "$PSScriptRoot/../bin/Debug/net48/PipingDataExporter.dll",
    [string] $ResourceName = 'CommandTable.xml',
    [string] $BentleyDir = 'C:\Program Files\Bentley\OpenPlant CONNECT Edition\OpenPlantModeler'
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $DllPath)) {
    throw "Assembly not found: $DllPath. Build the project first."
}

# The AddIn's own references (ustation, Bentley.*) live in the product install folder, not next to
# the built DLL, so reflection needs to be pointed at them.
$probeDirs = @(
    (Split-Path (Resolve-Path $DllPath).Path),
    $BentleyDir,
    (Join-Path $BentleyDir 'Assemblies\ECFramework')
) | Where-Object { Test-Path $_ }

$resolver = [ResolveEventHandler] {
    param($sender, $e)
    $simpleName = ($e.Name -split ',')[0]
    foreach ($dir in $probeDirs) {
        $candidate = Join-Path $dir "$simpleName.dll"
        if (Test-Path $candidate) {
            try { return [Reflection.Assembly]::LoadFrom($candidate) } catch { }
        }
    }
    return $null
}
[AppDomain]::CurrentDomain.add_AssemblyResolve($resolver)

try {

$dll = (Resolve-Path $DllPath).Path
$assembly = [Reflection.Assembly]::LoadFrom($dll)
$assemblyName = $assembly.GetName().Name
$failures = New-Object Collections.Generic.List[string]

# 1. The embedded key-in table must be present under the logical name MicroStation looks for.
$resourceNames = $assembly.GetManifestResourceNames()
if ($resourceNames -notcontains $ResourceName) {
    $failures.Add("Embedded resource '$ResourceName' is missing (found: $($resourceNames -join ', ')). Check <LogicalName> in the .csproj.")
    $xml = $null
} else {
    $stream = $assembly.GetManifestResourceStream($ResourceName)
    try {
        $reader = New-Object IO.StreamReader($stream)
        try { $xml = [xml] $reader.ReadToEnd() } finally { $reader.Dispose() }
    } finally { $stream.Dispose() }

    # 2. The KeyinTree namespace is load-bearing: get it wrong and no key-in registers, silently.
    $expectedNs = 'http://www.bentley.com/schemas/1.0/MicroStation/AddIn/KeyinTree.xsd'
    if ($xml.DocumentElement.NamespaceURI -ne $expectedNs) {
        $failures.Add("KeyinTree xmlns is '$($xml.DocumentElement.NamespaceURI)', expected '$expectedNs'.")
    }
}

# 3. MdlTaskID must equal the DLL filename or the AddIn never loads.
#    Read from source rather than by reflection: the AddIn type derives from a type in ustation.dll,
#    which cannot be loaded outside MicroStation, so its attribute is unreachable here.
$sourceDir = Join-Path (Split-Path $PSScriptRoot) 'src'
$taskIds = Select-String -Path (Join-Path $sourceDir '*.cs') -Pattern 'MdlTaskID\s*=\s*"([^"]*)"' -AllMatches |
    ForEach-Object { $_.Matches } |
    ForEach-Object { $_.Groups[1].Value }

if (-not $taskIds) {
    $failures.Add("No [AddIn(MdlTaskID = ...)] found under $sourceDir.")
}
foreach ($taskId in $taskIds) {
    if ($taskId -ne $assemblyName) {
        $failures.Add("MdlTaskID '$taskId' does not match the built assembly name '$assemblyName'.")
    }
}

# 4. Every Function="Namespace.Class.Method" must resolve to a public static method taking a string.
$handlerCount = 0
if ($xml) {
    $ns = New-Object Xml.XmlNamespaceManager($xml.NameTable)
    $ns.AddNamespace('k', $xml.DocumentElement.NamespaceURI)

    foreach ($handler in $xml.SelectNodes('//k:KeyinHandler', $ns)) {
        $handlerCount++
        $keyin = $handler.GetAttribute('Keyin')
        $function = $handler.GetAttribute('Function')

        $lastDot = $function.LastIndexOf('.')
        if ($lastDot -lt 1) {
            $failures.Add("[$keyin] Function '$function' is not a Namespace.Class.Method string.")
            continue
        }

        $typeName = $function.Substring(0, $lastDot)
        $methodName = $function.Substring($lastDot + 1)

        $type = $assembly.GetType($typeName, $false)
        if (-not $type) {
            $failures.Add("[$keyin] type '$typeName' not found in $assemblyName.")
            continue
        }
        if (-not $type.IsPublic) {
            $failures.Add("[$keyin] type '$typeName' is not public.")
        }

        $method = $type.GetMethod($methodName, [type[]] @([string]))
        if (-not $method) {
            $failures.Add("[$keyin] '$typeName' has no method '$methodName(string)'.")
            continue
        }
        if (-not $method.IsStatic -or -not $method.IsPublic) {
            $failures.Add("[$keyin] '$function' must be public static.")
            continue
        }

        Write-Host "  OK  $keyin -> $function"
    }
}

Write-Host ''
if ($failures.Count -gt 0) {
    Write-Host "FAILED ($($failures.Count) problem(s)):" -ForegroundColor Red
    $failures | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
    exit 1
}

Write-Host "All $handlerCount key-in handler(s) resolve against $assemblyName." -ForegroundColor Green
exit 0

} finally {
    [AppDomain]::CurrentDomain.remove_AssemblyResolve($resolver)
}
