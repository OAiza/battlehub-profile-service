param(
    [Parameter(Mandatory = $true)][string]$Domain,
    [Parameter(Mandatory = $true)][string]$Audience
)

$env:Auth0__Domain = $Domain
$env:Auth0__Audience = $Audience
$env:Cors__Origins__0 = 'http://localhost:4000'

Push-Location (Join-Path $PSScriptRoot '..')
try {
    dotnet run --project src/BattleHub.ProfileService.Api --launch-profile http
}
finally {
    Pop-Location
}
