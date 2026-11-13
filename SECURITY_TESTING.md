# Consultas financeiras e segurança dos ambientes

Development usa gestao_api_dev@localhost em gestao_financeira_empresarial (5216).
Testing usa gestao_api_testing@localhost em gestao_financeira_seguranca_testes (5217).
O servidor permitido é localhost:3306. A API nunca usa root.

## Privilégios exatos

| Tabelas | Development | Testing |
|---|---|---|
| empresas, usuarios, administradores_gerais | SELECT, INSERT, UPDATE | SELECT, INSERT, UPDATE |
| lancamentos | SELECT | SELECT, INSERT |
| auditoria | nenhum | SELECT, INSERT |
| categorias, centros_custo, clientes, fornecedores | nenhum | SELECT, INSERT |

Os INSERT financeiros de Testing atendem fixtures e POST /lancamentos.
Development permanece sem escrita financeira. Não há DELETE, DDL, GRANT OPTION,
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

Não execute migrações ou provisionamento no banco original. Não exponha segredos,
hashes ou tokens em logs. Backups, configuração privada e recuperação administrativa
permanecem sob responsabilidade do operador, fora do repositório.

## Cadastro de lançamentos (segunda entrega)

`POST /lancamentos` está habilitado exclusivamente em Testing. Fora de Testing,
o serviço devolve 403 antes de consultar ou gravar. O interceptor financeiro também
bloqueia gravações EF fora de Testing e verifica destino, conta e permissões antes
de cada SaveChangesAsync financeiro. Não houve concessão nova em Development.

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
O bloqueio Development é exercitado no serviço e no interceptor usando conexão
Testing, sem tentar gravações no banco original.

Para habilitar cadastro em Development futuramente, será necessária uma entrega
separada que autorize as mudanças no serviço/interceptor, nas permissões de leitura
das referências e INSERT de lancamentos/auditoria e na matriz exata de DatabaseSafety,
seguida de validação e plano operacional. Nada disso está habilitado nesta entrega.
