# Consultas financeiras e segurança dos ambientes

Development usa gestao_api_dev@localhost em gestao_financeira_empresarial (5216).
Testing usa gestao_api_testing@localhost em gestao_financeira_seguranca_testes (5217).
O servidor permitido é localhost:3306. A API nunca usa root.

## Privilégios exatos

| Tabelas | Development | Testing |
|---|---|---|
| empresas, usuarios, administradores_gerais | SELECT, INSERT, UPDATE | SELECT, INSERT, UPDATE |
| lancamentos | SELECT, INSERT | SELECT, INSERT, UPDATE |
| auditoria | SELECT, INSERT | SELECT, INSERT |
| categorias, centros_custo, clientes, fornecedores | SELECT | SELECT, INSERT |
| baixas, contas_bancarias, formas_pagamento | nenhum | SELECT, INSERT |

Os INSERT financeiros de Testing atendem fixtures e POST /lancamentos.
Development permite inclusão em lancamentos e auditoria, com SELECT nas referências.
UPDATE financeiro existe somente em lancamentos de Testing. Não há DELETE, DDL, GRANT OPTION,
privilégios globais, de schema, de coluna ou papéis ativos nas contas da API.
DatabaseSafety exige igualdade entre privilégios reais e autorizados.
TestingWriteGuard verifica a conexão antes de SaveChangesAsync e recusa exclusões.

Testing contém as três tabelas de segurança, auditoria e cinco tabelas financeiras:
categorias, centros_custo, clientes, fornecedores e lancamentos, além de baixas,
contas_bancarias e formas_pagamento adicionadas nesta etapa. Foram preservados
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
recusa UPDATE financeiro em Development e DELETE em ambos antes de abrir conexão;
em Testing aceita somente os campos controlados de lancamentos ABERTO. Exige SaveChangesAsync.
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
no Location do ADMIN_GERAL. Edição e cancelamento existem somente em Testing, conforme
a seção específica abaixo. Não há operação de baixas pela API.

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
em Development e Testing, incluindo mudanças de campos imutáveis. O interceptor recusa
nessas verificações os estados proibidos antes de abrir conexão,
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


## Edição e cancelamento em Testing

PUT /lancamentos/{id} usa AtualizarLancamentoRequest. POST
/lancamentos/{id}/cancelamento usa CancelarLancamentoRequest (corpo `{}` para usuário
empresarial). Exemplos completos e limites estão no README. ADMIN e FINANCEIRO só
operam na própria empresa; ADMIN_GERAL informa empresaId no JSON. CONSULTA recebe
403. JWT inválido/expirado recebe 401, identidade ou empresa inativa/perfil revogado
recebe 403. O filtro empresarial precede a busca e os bloqueios do lançamento;
ID inexistente ou de outra empresa retorna 404 dentro do escopo autorizado.

Somente ABERTO sem baixas pode mudar. PARCIAL não possui regra de alteração de saldo
definida e fica bloqueado, assim como QUITADO e CANCELADO (409). Qualquer baixa bloqueia
ambas as operações, mesmo se o status ainda for ABERTO. Não há estorno, DELETE, mudança
de tipo ou de empresa. Transferências não possuem FK para lançamentos; são vinculadas
a contas bancárias e não são modificadas. Nenhuma view original foi copiada.

A autorização adicional de preparação abrangeu somente as estruturas Testing de
contas_bancarias, formas_pagamento e baixas, compatíveis com o DDL original e sem
registros originais. A conta Testing recebeu SELECT/INSERT nessas tabelas para as
fixtures e UPDATE somente em lancamentos. A aplicação apenas lê baixas, e o interceptor
recusa sua escrita via EF; as fixtures inserem somente novos registros fictícios.
Auditoria permanece append-only. Não houve alteração de dados, estruturas ou permissões
em Development; seus nove lançamentos e sua auditoria anterior foram preservados.

AlteracaoLancamentoService verifica Testing antes de acessar o banco, exige a conta
e o destino exatos e usa transação Serializable e SELECT FOR UPDATE filtrado por
empresa e ID. A leitura de baixas e das referências ocorre dentro da transação.
Operações concorrentes sobre o mesmo lançamento são serializadas; cancelamento
repetido recebe 409 e não duplica auditoria. Não existe edição de baixas concorrente
pela API nesta entrega; escritores futuros devem preservar o protocolo transacional.

ALTERAR e CANCELAR registram empresa, registro, usuário, autorId/autorTipo/perfil,
instante UTC e DTOs anterior/posterior. Para ADMIN_GERAL, usuario_id continua NULL.
O UPDATE e a auditoria pertencem à mesma transação. A simulação de falha ocorre após
o UPDATE e confirma rollback integral para edição e cancelamento. Não há JWTs ou
segredos nos detalhes. Cancelamento mantém registro, referências e histórico.

Os testes usam somente Testing, inclusive para simular o serviço/interceptor em
Development sem conexão ao banco original. Cobrem quatro perfis, duas empresas,
JWT inválido e expirado, referências cruzadas, valores/datas inválidos, imutabilidade,
status bloqueados, baixas, duplicidade, auditoria, rollback, concorrência e bloqueios
após revogação/inativação de usuário, empresa e administrador geral. Checkpoints
privados verificam fixtures anteriores e o banco original, sem publicar seus dados.

Resultado desta etapa: 59 verificações de consultas, 51 de cadastro/segurança,
86 de edição/cancelamento e 66 de login/regressão: 262 aprovadas, zero reprovadas.
Build --no-restore --nologo: zero erros e zero avisos. O middleware em HTTP local
continua emitindo o aviso de porta HTTPS não determinada; não é aviso de compilação.
As fixtures novas são mantidas e acumulam a cada rodada. PARCIAL, estornos, baixas pela
API e habilitação de edição/cancelamento em Development permanecem fora do escopo.

## Preparação de Development sem ativação

`FinancialUpdatePolicy.DevelopmentEnabled` está fixado em `false` nesta versão.
O serviço e o interceptor usam a mesma política; não há parâmetro HTTP, chave de
configuração ou variável de ambiente que permita habilitá-la. Production e ambientes
desconhecidos permanecem bloqueados. Endpoints, autorização, regras financeiras,
transação, bloqueios e auditoria de Testing foram preservados.

O serviço de alteração passa a escolher a verificação de conexão pelo ambiente,
sem fallback de Testing para Development. O ramo futuro de DatabaseSafety exige
as duas permissões adicionais exatas abaixo somente quando a ativação for publicada.
Nesta preparação, a matriz efetiva permanece a da tabela inicial: Development não
tem UPDATE em lancamentos nem SELECT em baixas, e concessões extras impedem a
inicialização. As permissões antigas de empresas, usuários e auditoria não mudam.

Não foram executados PUT, cancelamento, migrações, mudanças de permissões, estruturas
ou registros em Development. Os nove lançamentos, a auditoria e as demais tabelas e
views são preservados. A regressão usa somente Testing e novas fixtures fictícias;
os testes de bloqueio de Development usam contexto Testing e confirmam recusa antes
de emitir SQL, sem testar escrita contra o banco original.

A regressão desta preparação aprovou novamente 262 verificações: 59 de consultas,
51 de cadastro/segurança, 86 de edição/cancelamento e 66 de login/regressão, sem
reprovações. O build --no-restore --nologo passou sem erros ou avisos. A comparação
antes/depois confirmou os dados, estruturas e privilégios de Development intactos,
assim como os privilégios de Testing e suas fixtures anteriores. O aviso de porta
HTTPS em HTTP local permanece; não é aviso do build.

## Ativação futura em Development

Procedimento documentado para execução posterior, **não executado nesta etapa**:

1. Obter autorização específica para a ativação e as duas concessões. Confirmar
   destino, identidade, privilégios, backup recuperável e estado dos registros.
2. Preparar uma versão com `FinancialUpdatePolicy.DevelopmentEnabled` em `true`.
   Adaptar os testes que hoje exigem Development bloqueado e comprovar o caminho
   habilitado, isolamento de conexões, regras, auditoria e rollback em ambiente
   isolado. Não reutilizar esta preparação como evidência de ativação já testada.
3. Interromper a API Development e aguardar o término das operações em andamento.
   Com credencial administrativa externa à API, executar somente as concessões:

   ```sql
   GRANT UPDATE ON `gestao_financeira_empresarial`.`lancamentos`
     TO 'gestao_api_dev'@'localhost';
   GRANT SELECT ON `gestao_financeira_empresarial`.`baixas`
     TO 'gestao_api_dev'@'localhost';
   ```

4. Conferir a matriz exata e publicar/iniciar a versão habilitada. Não conceder
   INSERT/UPDATE/DELETE em baixas, UPDATE em outras tabelas, DELETE, privilégios
   globais, administrativos ou acesso a outros bancos. Preservar os privilégios
   anteriores, inclusive SELECT/INSERT em auditoria e o cadastro financeiro.
5. Validar autenticação e consultas. Qualquer PUT ou cancelamento controlado em
   Development dependerá da autorização posterior para registros e dados específicos;
   não é parte desta preparação. Se banco e binário não corresponderem à mesma
   matriz, a API deve recusar a conexão, sem relaxar DatabaseSafety.

## Reversão segura da ativação futura

Com a API parada e sem operações em andamento, publicar novamente a política com
`DevelopmentEnabled` em `false` e remover **somente** as duas concessões novas:

```sql
REVOKE UPDATE ON `gestao_financeira_empresarial`.`lancamentos`
  FROM 'gestao_api_dev'@'localhost';
REVOKE SELECT ON `gestao_financeira_empresarial`.`baixas`
  FROM 'gestao_api_dev'@'localhost';
```

Confirmar a matriz original, reiniciar com o bloqueio ativo e validar GET/cadastro
pelos procedimentos autorizados. Não revogar privilégios preexistentes nem alterar
Testing. Não apagar lançamentos ou auditorias e não tentar desfazer operações já
confirmadas por SQL: qualquer correção financeira exige uma autorização própria.
A reversão acima limita a capacidade futura de alteração; preserva o histórico.
