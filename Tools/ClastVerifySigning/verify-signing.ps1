# Fails if any assembly in the given Clast.*.nupkg files is not strong-named with Clast.snk.
#
# Usage: pwsh Tools/ClastVerifySigning/verify-signing.ps1 <dir-with-nupkgs> [expected-token]
#
# Why: an unconditional upstream <AssemblyOriginatorKeyFile> placed after the Clast override in a csproj
# silently wins (last assignment), and the package still builds, packs and publishes fine. That shipped
# Clast.Google.Apis.Storage.v1 and Clast.Google.Apis.Bigquery.v2 signed with google.apis.snk up to
# 1.78.0.4332 / 1.78.0.4328. Nothing short of reading the token off the packed DLL catches it.
param(
    [Parameter(Mandatory = $true)][string] $PackageDir,
    # Public key token of Clast.snk (google-api-dotnet-client/Clast.snk).
    [string] $ExpectedToken = 'b8c3b6fbeeefa129'
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem

$packages = @(Get-ChildItem -Path $PackageDir -Filter '*.nupkg')
if ($packages.Count -eq 0) { throw "No .nupkg files found in $PackageDir" }

$work = Join-Path ([System.IO.Path]::GetTempPath()) ("clast-verify-" + [guid]::NewGuid())
$bad = 0
$checked = 0
try {
    foreach ($pkg in $packages) {
        $dest = Join-Path $work $pkg.BaseName
        [System.IO.Compression.ZipFile]::ExtractToDirectory($pkg.FullName, $dest)
        $dlls = @(Get-ChildItem -Path (Join-Path $dest 'lib') -Recurse -Filter '*.dll' -ErrorAction SilentlyContinue)
        if ($dlls.Count -eq 0) {
            Write-Host "::error::$($pkg.Name) contains no lib/**/*.dll"
            $bad++
            continue
        }
        foreach ($dll in $dlls) {
            $name = [System.Reflection.AssemblyName]::GetAssemblyName($dll.FullName)
            $bytes = $name.GetPublicKeyToken()
            $token = if ($bytes) { -join ($bytes | ForEach-Object { $_.ToString('x2') }) } else { '(unsigned)' }
            $rel = $dll.FullName.Substring($dest.Length + 1)
            $checked++
            if ($token -ne $ExpectedToken) {
                Write-Host "::error::$($pkg.Name) $rel is signed with $token, expected Clast.snk ($ExpectedToken)"
                $bad++
            } else {
                Write-Host "ok  $($pkg.Name)  $rel  $token"
            }
        }
    }
} finally {
    Remove-Item -Recurse -Force $work -ErrorAction SilentlyContinue
}

if ($bad -gt 0) { throw "$bad assembly/package signing problem(s) found." }
Write-Host "All $checked assemblies in $($packages.Count) packages are signed with Clast.snk ($ExpectedToken)."
