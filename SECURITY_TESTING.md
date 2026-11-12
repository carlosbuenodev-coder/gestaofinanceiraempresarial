# Development e Testing

Development utiliza gestao_api_dev@localhost no banco gestao_financeira_empresarial,
porta 5216. Testing utiliza gestao_api_testing@localhost no banco
gestao_financeira_seguranca_testes, porta 5217. Ambas as contas têm somente SELECT,
INSERT e UPDATE nas tabelas empresas, usuarios e administradores_gerais, sem
privilégios globais, de schema ou de concessão, e sem papéis ativos. A API não usa root.

## Arquivos publicados

| Arquivo | Responsabilidade |
|---|---|
| Program.cs | Configuração, endpoints e seleção do cenário de Testing. |
| Security/DatabaseSafety.cs | Confere destino, identidade e permissões; protege gravações EF de Testing. |
| Security/TestingScenario.cs | Fixtures e verificações HTTP no banco isolado. |
| appsettings.Testing.json | Configurações não secretas de Testing. |
| scripts/Start-SecurityEnvironment.ps1 | Inicialização com configurações privadas separadas. |
| README.md, SECURITY_TESTING.md, .gitignore | Documentação e exclusão de arquivos locais. |

## Ferramentas administrativas somente locais

Os scripts mysql_security.py, inspect_mysql.py e open_admin_session.py, da pasta
scripts, são ignorados pelo Git e não acompanham um clone. São ferramentas locais
do operador para provisionamento, inspeção e recuperação administrativa.
A API e o inicializador publicado não importam nem executam esses scripts.

O administrador deve preparar bancos, estrutura, contas restritas e segredos por
procedimento próprio autorizado. Scripts SQL de estrutura e backups não são
publicados. Testing deve receber apenas a estrutura necessária, sem registros,
triggers, views ou referências cruzadas ao banco original. Antes de qualquer
preparação com escrita, confirme DATABASE(), CURRENT_USER() e permissões.
Não use credenciais administrativas para executar a API.

## Dependências privadas do inicializador

O inicializador requer .NET 10, Windows PowerShell, política de execução local
compatível e banco provisionado. Ele não altera essa política. Execute na raiz
do repositório, após `dotnet build`; o executável esperado é
`bin/Debug/net10.0/GestaoFinanceira.dll`.

Development requer ConnectionStrings:GestaoFinanceira e Jwt:Key na configuração
ASP.NET Core. Use User Secrets ou outro mecanismo local seguro. As configurações
locais appsettings.json e appsettings.Development.json são ignoradas pelo Git.

Testing requer o seguinte arquivo externo, não fornecido pelo repositório:

```text
%LOCALAPPDATA%\GestaoFinanceira\security\gestao_api_testing.dpapi
```

O conteúdo deve ser um objeto JSON cifrado com Windows DPAPI, escopo CurrentUser,
acessível pelo mesmo perfil Windows que executa o script. Propriedades esperadas:

| Propriedade | Conteúdo esperado |
|---|---|
| host | localhost |
| port | 3306 |
| schema | gestao_financeira_seguranca_testes |
| user | gestao_api_testing |
| password | Senha exclusiva da conta de Testing. |
| jwt | Chave exclusiva, Base64 representando pelo menos 32 bytes. |

O operador deve preparar esse arquivo por procedimento seguro, fora do repositório.
Não publique versões JSON em texto puro. O script apenas descriptografa em memória,
configura variáveis do processo e inicia a API. Ausência, falha de descriptografia
ou destino inválido interrompem a execução, sem fallback para Development.

Alternativamente, a API pode ser iniciada diretamente, sem o script, com
DOTNET_ENVIRONMENT=Testing, Testing__ConnectionString e Testing__JwtKey fornecidos
por mecanismo seguro no processo. Não digite valores secretos em comandos sujeitos
a histórico. A API fixa a porta 5217 e valida a conexão. O JWT de Testing tem chave,
emissor e público separados de Development.

## Inicialização e retorno a Development

```powershell
.\scripts\Start-SecurityEnvironment.ps1 -Environment Development
.\scripts\Start-SecurityEnvironment.ps1 -Environment Testing
```

Execute uma instância por terminal. O script restaura as variáveis do processo ao
terminar. Para retornar, encerre Testing e inicie Development com sua configuração
restrita. Não é necessário restaurar dados ou utilizar root na API.
Não execute com transcrição, depuração de variáveis ou logs de corpos de requisições.

## Cenário HTTP descartável

```powershell
.\scripts\Start-SecurityEnvironment.ps1 -Environment Testing -RunTests
```

Esse comando exige as três tabelas de Testing vazias, cria duas empresas,
seis usuários empresariais e um administrador geral fictícios e encerra a API
após os testes. Senhas das fixtures existem apenas em memória; hashes são
persistidos exclusivamente no banco de testes.

O cenário verifica autenticação, consultas, PUT/PATCH entre empresas com 404,
proteção de ADMIN com 403, ausência de autenticação com 401, limites dos perfis
e operações permitidas em registros descartáveis. Os campos das três tabelas
são comparados em memória após operações negadas, incluindo hashes sem exibi-los.

Não há limpeza ou exclusão de registros. Uma segunda execução recusa sem reinserir
fixtures. Para nova rodada, planeje explicitamente outro conjunto isolado;
não exclua o banco existente. O interceptor verifica a conexão antes de
SaveChangesAsync e recusa exclusões. Não contorne a proteção com SQL direto.

## Segredos e recuperação administrativa

Arquivos DPAPI, .env, configurações privadas, certificados e backups permanecem
locais e são ignorados pelo Git. DPAPI depende do perfil Windows que protegeu os
dados; não é uma recuperação administrativa independente.

A recuperação é responsabilidade do operador e não é iniciada pelos arquivos
publicados. Antes de rotacionar root, mantenha uma conta administrativa independente
e valide seu acesso. Não remova ou migre o perfil Windows sem recuperação segura.
Rotação e tratamento de falhas de persistência pertencem às ferramentas locais,
não à rotina da API ou dos testes.
