# Sistema de Gestão Financeira Empresarial

API REST em C#/.NET 10, ASP.NET Core, Entity Framework Core e MySQL, com
controle de acesso por perfil e empresa. O projeto está em desenvolvimento.

## Funcionalidades implementadas

- Autenticação JWT separada para administrador geral e usuários de empresas.
- Validação de assinatura, emissor, público e validade do JWT.
- Senhas protegidas por PasswordHasher, sem senha ou hash nos DTOs de resposta.
- Cadastro, consulta e atualização de empresas pelo ADMIN_GERAL.
- Cadastro, consulta, atualização e desativação lógica de usuários conforme autorização.
- Consulta do próprio cadastro e listagem da própria empresa para ADMIN.
- Isolamento por empresa do JWT e verificação do acesso atual do ADMIN no banco.
- Development e Testing com contas MySQL, conexões e JWTs separados.

## Perfis e permissões atuais

| Perfil | Permissões |
|---|---|
| ADMIN_GERAL | Administra empresas e usuários empresariais de qualquer empresa; identidade armazenada separadamente. |
| ADMIN | Consulta usuários da própria empresa; cria e administra FINANCEIRO e CONSULTA dessa empresa. Não altera nem desativa ADMIN, inclusive a si mesmo. |
| FINANCEIRO | Consulta o próprio cadastro; não administra usuários. |
| CONSULTA | Consulta o próprio cadastro; não administra usuários. |

FINANCEIRO e CONSULTA ainda têm as mesmas permissões nos endpoints existentes.
Os módulos financeiros e suas permissões específicas não foram implementados.
ADMIN_GERAL utiliza endpoints gerais, não os exclusivos de usuários empresariais.

Nos endpoints PUT e PATCH de usuários, ADMIN recebe 404 para IDs inexistentes e
usuários de outras empresas. ADMIN da própria empresa permanece protegido com
403. Requisições sem autenticação recebem 401.

## Endpoints principais

| Método | Rota | Finalidade |
|---|---|---|
| POST | `/auth/admin/login` | Login do ADMIN_GERAL. |
| POST | `/auth/usuarios/login` | Login empresarial com EmpresaId, e-mail e senha. |
| GET | `/usuarios/me` | Próprio cadastro. |
| GET / POST | `/usuarios/minha-empresa` | Listagem ou cadastro pelo ADMIN empresarial. |
| POST | `/usuarios` | Cadastro pelo ADMIN_GERAL. |
| GET / PUT | `/usuarios/{id}` | Consulta ou atualização conforme autorização. |
| PATCH | `/usuarios/{id}/desativar` | Desativação lógica conforme autorização. |
| GET / POST | `/empresas` | Listagem ou cadastro pelo ADMIN_GERAL. |
| GET / PUT | `/empresas/{id}` | Consulta ou atualização pelo ADMIN_GERAL. |

## Ambientes e execução

Pré-requisitos: .NET SDK 10, MySQL com estrutura e contas previamente provisionadas
e, para o inicializador publicado, Windows PowerShell com execução de scripts autorizada.

| Ambiente | Endereço | Banco | Conta MySQL |
|---|---|---|---|
| Development | `http://localhost:5216` | `gestao_financeira_empresarial` | `gestao_api_dev@localhost` |
| Testing | `http://localhost:5217` | `gestao_financeira_seguranca_testes` | `gestao_api_testing@localhost` |

A configuração atual exige MySQL em localhost:3306 e contas com somente SELECT,
INSERT e UPDATE nas tabelas empresas, usuarios e administradores_gerais, sem
privilégios globais, de schema ou de concessão, e sem papéis ativos. A API rejeita
destino, identidade ou permissões incompatíveis. Root não é utilizado pela API.

Development lê `ConnectionStrings:GestaoFinanceira` e `Jwt:Key` dos mecanismos
ASP.NET Core, preferencialmente User Secrets locais. A chave deve ser Base64 e
representar pelo menos 32 bytes. Não exponha segredos em arquivos ou histórico.

Testing exige `Testing__ConnectionString` e `Testing__JwtKey`, sem fallback para
Development. O inicializador carrega esses valores de um arquivo DPAPI externo,
previamente preparado pelo operador. Um clone não fornece esse arquivo, bancos,
contas ou estrutura SQL. Consulte [SECURITY_TESTING.md](SECURITY_TESTING.md).

Na raiz do repositório:

```powershell
dotnet build
.\scripts\Start-SecurityEnvironment.ps1 -Environment Development
```

Em outro terminal, após preparar as dependências isoladas:

```powershell
.\scripts\Start-SecurityEnvironment.ps1 -Environment Testing
```

O inicializador não cria bancos, tabelas ou contas e não executa migrações.

## Validações realizadas

O cenário HTTP em Security/TestingScenario.cs foi executado em banco exclusivo
com empresas e usuários fictícios. Foram confirmados login dos quatro perfis,
consultas autorizadas, PUT/PATCH entre empresas e em ID inexistente com 404,
proteção de ADMIN com 403 e ausência de autenticação com 401.

Também foram validados bloqueio de administração por FINANCEIRO e CONSULTA,
bloqueio de promoção para ADMIN, preservação dos registros após operações negadas,
operações permitidas com usuários descartáveis e bloqueio após desativação ou
mudança de perfil. Development teve login e consultas do ADMIN empresarial validados.
Esses resultados não representam cobertura completa de todos os endpoints.

O cenário exige tabelas vazias e grava somente no banco de Testing. Não exclui
registros; uma segunda execução é recusada. Consulte as instruções específicas
antes de usar `-RunTests`.

## Estrutura e próximos passos

- Program.cs: configuração, autenticação e endpoints.
- Models/: entidades, DTOs e contexto EF Core.
- Security/: validação das conexões e cenário HTTP isolado.
- scripts/Start-SecurityEnvironment.ps1: inicializador sem credenciais embutidas.

Próximos passos: ampliar a cobertura automatizada; desenvolver clientes,
fornecedores, contas bancárias, lançamentos e fluxo de caixa; e preparar configuração
específica para implantação. A configuração atual destina-se aos ambientes locais descritos.

## Autor

Carlos Bueno — [carlosbuenodev-coder](https://github.com/carlosbuenodev-coder)
