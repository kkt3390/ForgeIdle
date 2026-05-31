$ErrorActionPreference = "Stop"

$clientId = Read-Host "Kakao REST API key"
$clientSecret = Read-Host "Kakao client secret"

dotnet user-secrets set "Authentication:Kakao:ClientId" $clientId
dotnet user-secrets set "Authentication:Kakao:ClientSecret" $clientSecret

Write-Host "Kakao development secrets saved outside the project."
