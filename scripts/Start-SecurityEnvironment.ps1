param(
    [ValidateSet('Development', 'Testing')]
    [string]$Environment = 'Testing',
    [switch]$RunTests
)
$ErrorActionPreference = 'Stop'
if ($RunTests -and $Environment -ne 'Testing') { throw 'Testes exigem Testing.' }
Add-Type -AssemblyName System.Security
$names = @('DOTNET_ENVIRONMENT','ASPNETCORE_ENVIRONMENT','ASPNETCORE_URLS',
           'Testing__ConnectionString','Testing__JwtKey')
$previous = @{}
foreach ($name in $names) { $previous[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
try {
    $env:DOTNET_ENVIRONMENT = $Environment
    $env:ASPNETCORE_ENVIRONMENT = $Environment
    if ($Environment -eq 'Testing') {
        $path = Join-Path $env:LOCALAPPDATA 'GestaoFinanceira\security\gestao_api_testing.dpapi'
        $bytes = [Security.Cryptography.ProtectedData]::Unprotect(
            [IO.File]::ReadAllBytes($path), $null,
            [Security.Cryptography.DataProtectionScope]::CurrentUser)
        $config = [Text.Encoding]::UTF8.GetString($bytes) | ConvertFrom-Json
        $builder = New-Object System.Data.Common.DbConnectionStringBuilder
        $builder['Server'] = 'localhost'
        $builder['Port'] = [int]$config.port
        $builder['Database'] = $config.schema
        $builder['User ID'] = $config.user
        $builder['Password'] = $config.password
        if ($config.schema -cne 'gestao_financeira_seguranca_testes' -or
            $config.user -cne 'gestao_api_testing' -or $config.host -cne 'localhost') {
            throw 'Destino Testing inválido.'
        }
        $env:Testing__ConnectionString = $builder.ConnectionString
        $env:Testing__JwtKey = $config.jwt
        $env:ASPNETCORE_URLS = 'http://localhost:5217'
        [Array]::Clear($bytes, 0, $bytes.Length)
        $config = $null
        $builder = $null
    } else {
        $env:ASPNETCORE_URLS = 'http://localhost:5216'
        $env:Testing__ConnectionString = $null
        $env:Testing__JwtKey = $null
    }
    $dll = Join-Path $PSScriptRoot '..\bin\Debug\net10.0\GestaoFinanceira.dll'
    if ($RunTests) { & dotnet $dll --security-tests }
    else { & dotnet $dll }
    if ($LASTEXITCODE -ne 0) { throw 'API ou testes falharam; detalhes sensíveis omitidos.' }
} finally {
    foreach ($name in $names) { [Environment]::SetEnvironmentVariable($name, $previous[$name], 'Process') }
    $config = $null
    $builder = $null
}
