# Consultas financeiras e segurança dos ambientes

Development usa gestao_api_dev@localhost em gestao_financeira_empresarial (5216).
Testing usa gestao_api_testing@localhost em gestao_financeira_seguranca_testes (5217).
O servidor permitido é localhost:3306. A API nunca usa root.

## Privilégios exatos

| Tabelas | Development | Testing |
|---|---|---|
| empresas, usuarios, administradores_gerais | SELECT, INSERT, UPDATE | SELECT, INSERT, UPDATE |
| lancamentos | SELECT, INSERT | SELECT, INSERT |
| auditoria | SELECT, INSERT | SELECT, INSERT |
| categorias, centros_custo, clientes, fornecedores | SELECT | SELECT, INSERT |

Os INSERT financeiros de Testing atendem fixtures e POST /lancamentos.
Development permite inclusão em lancamentos e auditoria, com SELECT nas referências.
Não há UPDATE financeiro, DELETE, DDL, GRANT OPTION,
privilégios globais, de schema, de coluna ou papéis ativos nas contas da API.
DatabaseSafety exige igualdade entre privilégios reais e autorizados.
TestingWriteGuard verifica a conexão antes de SaveChangesAsync e recusa exclusões.

Testing contém as três tabelas de segurança, auditoria e cinco tabelas financeiras:
categorias, centros_custo, clientes, fornecedores e lancamentos. Foram preservados
tipos, índices e restrições do DDL financeiro; os contadores AUTO_INCREMENT originais
não foram copiados. Todas as chaves estrangeiras apontam para Testing. Nenhuma view,
trigger, registro original ou referência ao banco original foi copiada.

## Segredos e execução

Development lê ConnectionStrings:GestaoFinanceira e Jwt:Key da configuração ASP.NET
Core, preferencialmente User Secrets. Testing exige Testing__ConnectionString e
Testing__JwtKey sem fallback. Emissores/públicos JWT e chaves são separados.

O inicializador scripts/Start-SecurityEnvironment.ps1 carrega o arquivo externo
%LOCALAPPDATA%\GestaoFinanceira\security\gestao_api_testing.dpapi, cifrado com Windows
DPAPI CurrentUser. O objeto contém host, port, schema, user, password e jwt.
Esse arquivo, os bancos e as contas não são fornecidos pelo repositório.
O inicializador requer .NET 10 e política PowerShell que permita scripts; ele não
altera a política. A API também pode ser iniciada diretamente com as variáveis
Testing fornecidas por mecanismo seguro no processo. Não digite segredos no histórico.

```powershell
dotnet build
.\scripts\Start-SecurityEnvironment.ps1 -Environment Testing -RunTests
.\scripts\Start-SecurityEnvironment.ps1 -Environment Development
```

Os testes usam somente a API Testing em http://localhost:5217. Sem configuração de
porta HTTPS, o middleware emite um aviso de redirecionamento; não é aviso do build.
No ambiente atual, o inicializador foi executado como scriptblock em memória com
caminho do DLL resolvido, sem alterar a política de execução. DPAPI exigiu o contexto
do perfil Windows fora do sandbox. As variáveis são restauradas ao terminar.

## Fixtures e cobertura

Cada execução cria duas empresas, seis usuários e um administrador geral fictícios,
com e-mails únicos por GUID e senhas aleatórias somente em memória. O cenário
financeiro acrescenta duas categorias, cliente, fornecedor, centro de custo e quatro
lançamentos fictícios. Não exige tabelas vazias, não reutiliza nem altera fixtures
anteriores e não exclui registros, inclusive após falhas. Novas rodadas acumulam dados.

FinanceTestingScenario verifica conteúdo e contagens, isolamento, IDs estrangeiros
e ausentes, todos os filtros, combinação de filtros, datas inclusivas, precisão
monetária, paginação, desempate, ordenação e parâmetros inválidos. TestingScenario
executa também a regressão de autenticação, empresas e usuários, inclusive PUT/PATCH
permitidos e negados. Verifica bloqueios financeiros após mudança de perfil,
desativação do usuário, da empresa e do administrador geral, em lista e detalhe.
A cobertura não é exaustiva de todos os endpoints anteriores.

## Administração local

mysql_security.py, inspect_mysql.py, open_admin_session.py e os arquivos
inspect_finance_local.py, provision_finance_local.py, verify_finance_local.py, inspect_post_local.py e
provision_post_local.py são
ferramentas locais ignoradas pelo Git, sem dependência da API sobre elas.
O provisionamento foi administrativo e separado da execução da API. A verificação
final comparou dados e estruturas originais com o baseline privado sem exibir hashes,
validou permissões exatas e negação de SELECT entre bancos e comparou as chaves JWT
em memória. Nada dessa verificação transfere registros originais para Testing.

Não execute migrações ou alterações estruturais no banco original. Concessões de
permissões exigem autorização e devem corresponder à matriz exata. Não exponha segredos,
hashes ou tokens em logs. Backups, configuração privada e recuperação administrativa
permanecem sob responsabilidade do operador, fora do repositório.

## Cadastro de lançamentos (segunda entrega)

`POST /lancamentos` está habilitado em Development e Testing. Nos demais ambientes,
o serviço e o interceptor recusam gravação financeira. Cada ambiente exige sua
conexão, identidade MySQL e privilégios exatos, sem acesso cruzado. O interceptor
recusa UPDATE/DELETE financeiros antes de abrir conexão e exige SaveChangesAsync.
Development recebeu exclusivamente INSERT em lancamentos, SELECT/INSERT em auditoria
e SELECT nas quatro tabelas auxiliares; as permissões anteriores foram preservadas.

ADMIN e FINANCEIRO usam a empresa do JWT e não podem enviar empresaId.
ADMIN_GERAL deve enviar empresaId no corpo JSON; CONSULTA recebe 403.
O escopo não é aceito na query do POST. Usuário, perfil, administrador geral e empresa
são conferidos no banco. Para os GETs, as regras anteriores de escopo permanecem.

O corpo aceita tipo, descricao, categoriaId, clienteId, fornecedorId, centroCustoId,
dataEmissao, dataVencimento, valor e observacoes. Cliente, fornecedor e centro de custo
continuam opcionais. Datas usam yyyy-MM-dd, dentro do intervalo DATE do MySQL.
Valor deve ser positivo e caber em decimal(15,2), sem arredondamento silencioso.
A descrição é aparada e deve ter entre 1 e 255 caracteres; observações respeitam
os 65535 bytes UTF-8 de TEXT. Referências inexistentes e de outras empresas recebem
a mesma validação 400. Categoria precisa ser da empresa e compatível com o tipo.
Campos desconhecidos, inclusive status, são recusados. Status inicial é ABERTO.

O sucesso retorna 201, DTO e Location consultável pelo autor, incluindo empresaId
no Location do ADMIN_GERAL. Não foram implementadas alterações, cancelamentos ou baixas.

Testing recebeu a estrutura auditoria, sem dados originais, e somente SELECT/INSERT
nessa tabela para gestao_api_testing. Ela registra empresa, entidade lancamentos,
registro_id, ação CRIAR, instante UTC e detalhes JSON com autorId, autorTipo e perfil.
Para ADMIN_GERAL, usuario_id permanece NULL; para usuário empresarial, referencia
seu ID em usuarios. Cadastro e auditoria usam uma transação explícita; falhas de
persistência retornam erro genérico e revertem a transação.

O modo explícito --security-tests registra um interceptor de falha de auditoria,
acionável somente pelo código do cenário, sem parâmetro HTTP. O teste provoca a falha
após o INSERT do lançamento e confirma a preservação integral dos registros nas duas
tabelas. Fora desse modo o interceptor de falha não é registrado.

O cenário POST cobre cadastros pelos perfis autorizados, escopo de duas empresas,
referências e parâmetros inválidos, campos opcionais, status, consulta pelo Location,
autoria empresarial e geral e rollback. O cenário principal verifica também o POST
após revogação de perfil, inativação de usuário, empresa e administrador geral.
Os testes recusam uma conexão Testing apresentada como Development, bloqueiam escrita
em Production e simulam estados Modified/Deleted no EF para ambas as entidades,
em Development e Testing. O interceptor recusa esses estados antes de abrir conexão,
sem emitir UPDATE ou DELETE. Nenhuma dessas verificações tenta gravar no banco original.

## Histórico da habilitação controlada em Development

A preparação confirmou tipos, índices, chaves estrangeiras, categorias compatíveis e
ausência de vínculos financeiros entre empresas diferentes. Não foi necessário alterar
estruturas. O baseline privado original permaneceu idêntico, incluindo seus 8 lançamentos
e nenhuma auditoria. Nenhuma permissão global, de schema ou concessão foi adicionada.

A rodada funcional em Testing aprovou 59 verificações de consultas, 51 casos de
cadastro/segurança e a regressão de autenticação, empresas e usuários. As fixtures
anteriores foram verificadas por checkpoint privado, sem exibir hashes ou dados.
Development iniciou com gestao_api_dev e passou por 9 verificações GET de conexão,
consulta, isolamento, autenticação e OpenAPI. O POST está registrado e habilitado,
mas não foi executado em Development, por restrição explícita desta entrega.

A inicialização normal não grava dados, cria tabelas ou aplica migrações. As gravações
de fixtures continuam restritas ao modo explícito --security-tests em Testing.
O comando anterior --criar-admin-geral é uma operação separada, explícita, e não foi
executado nesta habilitação. Não houve implantação pública, commit ou push.

## Validação do cadastro autorizado em Development — 2026-11-14

A retomada confirmou as permissões exatas das duas contas, a negação de acesso cruzado,
as chaves JWT distintas e a preservação do baseline original antes da inclusão. Nenhuma
estrutura ou permissão foi alterada. O ADMIN ativo da empresa 1 autenticou pelo fluxo
normal; a sessão protegida foi validada por GET /usuarios/me, sem exposição do JWT.

Após confirmar empresa ativa, categoria 4 da mesma empresa e tipo DESPESA e ausência
de duplicidade, foi executado exatamente um POST /lancamentos em Development. O
cadastro fictício autorizado retornou HTTP 201, ID 9, e GET /lancamentos/9 retornou
HTTP 200 com os dados esperados e status ABERTO. A auditoria ID 1 confirmou empresa 1,
usuário 1, entidade lancamentos, registro 9 e operação CRIAR.

A comparação de todos os registros e estruturas antes/depois confirmou os oito
lançamentos anteriores inalterados e somente duas novas linhas em Development:
o lançamento e sua auditoria. O lançamento de teste foi mantido.

A regressão desta retomada executou em Testing 59 verificações HTTP de consultas,
51 casos de cadastro/segurança e 56 verificações HTTP de autenticação e endpoints
anteriores: 166 verificações aprovadas, nenhuma reprovada. Os checkpoints confirmaram
a preservação das fixtures anteriores. O build passou sem erros ou avisos; a execução
HTTP local emitiu o aviso de porta HTTPS não determinada.

Testes de carga e implantação com HTTPS exigem preparação própria. As ferramentas
privadas, credenciais, JWTs e checkpoints cifrados permanecem fora dos arquivos publicados.
