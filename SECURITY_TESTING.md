# Consultas financeiras e segurança dos ambientes

Development usa gestao_api_dev@localhost em gestao_financeira_empresarial (5216).
Testing usa gestao_api_testing@localhost em gestao_financeira_seguranca_testes (5217).
O servidor permitido é localhost:3306. A API nunca usa root.

## Privilégios exatos

| Tabelas | Development | Testing |
|---|---|---|
| empresas, usuarios, administradores_gerais | SELECT, INSERT, UPDATE | SELECT, INSERT, UPDATE |
| lancamentos | SELECT, INSERT, UPDATE | SELECT, INSERT, UPDATE |
| auditoria | SELECT, INSERT | SELECT, INSERT |
| categorias, centros_custo, clientes, fornecedores | SELECT | SELECT, INSERT |
| baixas | SELECT, INSERT | SELECT, INSERT |
| contas_bancarias, formas_pagamento | SELECT | SELECT, INSERT |
| estornos_baixas | SELECT, INSERT | SELECT, INSERT |

Os INSERT financeiros de Testing atendem fixtures e POST /lancamentos.
Development permite inclusão em lancamentos, auditoria, baixas e estornos_baixas,
com SELECT nas referências e catálogos. UPDATE financeiro existe somente em lancamentos dos dois ambientes.
Não há DELETE, DDL, GRANT OPTION,
privilégios globais, de schema, de coluna ou papéis ativos nas contas da API.
DatabaseSafety exige igualdade entre privilégios reais e autorizados.
TestingWriteGuard verifica a conexão antes de SaveChangesAsync e recusa exclusões.

Testing contém as três tabelas de segurança, auditoria e cinco tabelas financeiras:
categorias, centros_custo, clientes, fornecedores e lancamentos, além de baixas,
contas_bancarias, formas_pagamento e a estrutura complementar estornos_baixas. Foram preservados
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
recusa DELETE e alteração da auditoria antes de abrir conexão; nos ambientes
habilitados aceita somente campos controlados de lancamentos ABERTO. Exige SaveChangesAsync.
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
no Location do ADMIN_GERAL. Edição e cancelamento existem nos dois ambientes, conforme
a seção específica abaixo. O módulo de baixas foi ativado também em Development, conforme a seção final.

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


## Edição e cancelamento em Development e Testing

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
fixtures e UPDATE somente em lancamentos. O módulo novo permite inclusão de baixas
somente dentro da sua transação em ambiente habilitado; UPDATE/DELETE de baixas continuam proibidos.
Auditoria permanece append-only. Na implementação original desta etapa, não houve
alteração em Development. A ativação posterior acrescenta somente as duas permissões
documentadas abaixo; os nove lançamentos e a auditoria continuam preservados.

AlteracaoLancamentoService verifica a política do ambiente antes de acessar o banco, exige a conta
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

Os testes funcionais de escrita usam somente Testing, inclusive para comprovar que
o serviço/interceptor Development rejeita conexão Testing. Cobrem quatro perfis, duas empresas,
JWT inválido e expirado, referências cruzadas, valores/datas inválidos, imutabilidade,
status bloqueados, baixas, duplicidade, auditoria, rollback, concorrência e bloqueios
após revogação/inativação de usuário, empresa e administrador geral. Checkpoints
privados verificam fixtures anteriores e o banco original, sem publicar seus dados.

Resultado da implementação inicial: 59 verificações de consultas, 51 de cadastro/segurança,
86 de edição/cancelamento e 66 de login/regressão: 262 aprovadas, zero reprovadas.
Build --no-restore --nologo: zero erros e zero avisos. O middleware em HTTP local
continua emitindo o aviso de porta HTTPS não determinada; não é aviso de compilação.
As fixtures novas são mantidas e acumulam a cada rodada. Edição de PARCIAL e estorno
de lançamento continuam fora do escopo; o módulo de baixas/estornos existe somente em
Testing. A ativação técnica dos endpoints anteriores de Development está descrita abaixo.

## Histórico: preparação de Development sem ativação

Na versão de preparação, `FinancialUpdatePolicy.DevelopmentEnabled` estava em `false`.
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

## Procedimento de ativação em Development

Procedimento da ativação autorizada. As concessões abaixo são administrativas;
a API nunca recebe credenciais administrativas nem executa GRANT:

1. Obter autorização específica para a ativação e as duas concessões. Confirmar
   destino, identidade, privilégios, backup recuperável e estado dos registros.
2. Preparar uma versão com `FinancialUpdatePolicy.DevelopmentEnabled` em `true`.
   Adaptar os testes de Development para exigir a recusa de conexões Testing no
   serviço e no interceptor. Comprovar regras, auditoria, rollback e concorrência
   em Testing; não executar operações financeiras efetivas no banco original.
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
   não faz parte desta ativação. Se banco e binário não corresponderem à mesma
   matriz, a API deve recusar a conexão, sem relaxar DatabaseSafety.

## Reversão segura da ativação

Esta seção reverte edição/cancelamento de lançamentos. Se o módulo de baixas estiver
ativo, primeiro desative/reverta esse módulo pela seção própria abaixo: UPDATE em
lancamentos e SELECT em baixas são privilégios compartilhados necessários aos pagamentos.

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

## Validação da ativação — 2026-11-14

A ativação autorizada publicou a política em true e aplicou exclusivamente UPDATE
em gestao_financeira_empresarial.lancamentos e SELECT em
gestao_financeira_empresarial.baixas para gestao_api_dev@localhost. Os privilégios
anteriores e a conta Testing permaneceram iguais. Não foram concedidos DELETE,
DDL, privilégios globais, papéis, GRANT OPTION ou acesso cruzado. A inicialização
da API Development confirmou a matriz exata através de DatabaseSafety.

A suíte final em Testing aprovou 266 verificações: 59 de consultas, 51 de cadastro,
90 de edição/cancelamento/segurança e 66 de login/regressão, sem reprovações. Além
das regras financeiras, auditoria, rollback e concorrência anteriores, os testes
confirmam que o serviço e o interceptor Development rejeitam conexão Testing, que
CONSULTA é recusado antes de acessar o banco e que Production/Staging não são liberados.
Durante a adaptação houve uma asserção intermediária incorreta sobre ForbidHttpResult;
ela foi corrigida e a suíte completa foi repetida com sucesso. Não foi uma liberação
indevida de acesso. O build final passou com zero erros e zero avisos; permanece o
aviso de porta HTTPS não determinada na execução HTTP local de Testing.

A API Development iniciou em localhost:5216 e aprovou 18 verificações HTTP: sessão
JWT do ADMIN renovada pelo login normal, GETs, registro das duas rotas no OpenAPI,
401 sem autenticação/JWT inválido, 400 para escopo empresarial indevido, 403 para
consulta de outra empresa, 404 para ID inexistente e 405 para DELETE sem rota.
Todos os PUT/POST de cancelamento e DELETE negativos apontaram exclusivamente para
um ID previamente confirmado inexistente; nenhum lançamento real foi alvo dessas
requisições. Não foram executados cadastro, edição ou cancelamento efetivos em Development.

Development possui apenas ADMIN ativo. Não se criou ou ativou CONSULTA para testes;
não houve teste HTTP com esse perfil nesse banco. Sua recusa foi comprovada em
Testing por HTTP e diretamente no serviço com ambiente Development e contexto
Testing. A execução financeira autorizada em registro real não foi validada, por
restrição expressa desta ativação. Não se presume essa validação como realizada.

A comparação antes/depois confirmou todos os dados e estruturas originais idênticos,
inclusive nove lançamentos e uma auditoria, além das fixtures anteriores de Testing.
Somente novas fixtures fictícias foram inseridas/alteradas nas rodadas Testing.
Uma operação futura em Development exige autorização específica sobre registros e
dados. Estornos e novas regras de saldo continuam fora do escopo.

## Módulo de baixas e estornos em Development e Testing

O DDL real de baixas contém id, lancamento_id, conta_bancaria_id, forma_pagamento_id
opcional, data_pagamento DATE, valor/juros/multa/desconto DECIMAL(15,2) e observacoes
VARCHAR(255). Empresa é derivada do lançamento; não existem colunas de criação,
autoria ou estorno. Autoria e instante de criação são registrados na auditoria.
As views vw_saldos_lancamentos e vw_fluxo_caixa foram inspecionadas no original e
permanecem intocadas; não existem em Testing e não foram copiadas. A primeira soma
encargos em total_baixado, mas calcula o principal pendente com SUM(valor).

O usuário confirmou expressamente o contrato pelo principal: totalBaixado é SUM(valor)
das baixas sem estorno; saldoRestante é valorOriginal menos esse total. Juros, multa
e desconto não são aceitos nos novos contratos e são persistidos como zero. Valores
legados são expostos separadamente, sem alterar os registros. Não há regra nova de
encargos, saldo bancário ou transferências.
O suporte a juros, multa e desconto em novos registros fica para uma etapa futura.

Na implementação inicial, a única alteração estrutural foi a tabela complementar
abaixo em Testing. Na ativação posterior, o usuário autorizou especificamente a mesma
estrutura em Development, como exceção à proibição original de alterações estruturais.
Nenhuma coluna ou registro existente foi modificado pelo provisionamento:

```sql
CREATE TABLE `gestao_financeira_seguranca_testes`.`estornos_baixas` (
  id BIGINT NOT NULL AUTO_INCREMENT,
  baixa_id BIGINT NOT NULL,
  motivo VARCHAR(500) NOT NULL,
  criado_em DATETIME(6) NOT NULL,
  PRIMARY KEY (id),
  UNIQUE KEY uq_estorno_baixa (baixa_id),
  CONSTRAINT fk_estorno_baixa FOREIGN KEY (baixa_id)
    REFERENCES `gestao_financeira_seguranca_testes`.`baixas` (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

GRANT SELECT, INSERT
  ON `gestao_financeira_seguranca_testes`.`estornos_baixas`
  TO 'gestao_api_testing'@'localhost';
```

Esse provisionamento é administrativo, separado da API, e recusa sobrescrever a
estrutura existente. Não é comando de inicialização ou migração automática. Depois
da preparação, DatabaseSafety exige exatamente a matriz da tabela inicial. Não houve
novas concessões em Development na implementação inicial. A ativação descrita abaixo
acrescentou somente as concessões necessárias, sem UPDATE/DELETE em baixas, estornos ou auditoria.
O estorno é append-only; baixa_id único impede duplicidade e a FK é local ao banco de cada ambiente.

Os cinco endpoints, exemplos e DTOs estão no README. FinancialPaymentPolicy permite
explicitamente Development/Testing; fora deles, o serviço retorna 403 antes de acessar o banco. A autenticação
continua obrigatória. ADMIN/FINANCEIRO usam a empresa do JWT; ADMIN_GERAL informa
empresaId explicitamente, no JSON dos POSTs e na query dos GETs. CONSULTA só lê.
Valida-se a situação atual das identidades e da empresa antes de procurar IDs ou
calcular totais. Conta precisa ser ativa e da mesma empresa. Formas de pagamento são
um catálogo global no DDL real, sem vínculo empresarial; o ID informado deve existir.

ABERTO/PARCIAL aceitam baixa até o saldo. QUITADO e CANCELADO não aceitam nova baixa.
Status/saldo inconsistentes recebem 409, sem reparo silencioso ou saldo negativo.
Estorno mantém a baixa original e acrescenta motivo e instante UTC, exige identidade
autorizada, não aceita duplicidade nem lançamento cancelado e recalcula o status.
Ausência de baixas válidas resulta em ABERTO; saldo positivo em PARCIAL; saldo zero
em QUITADO. As antigas regras de edição/cancelamento do lançamento não foram relaxadas:
a existência de qualquer baixa histórica continua bloqueando essas duas operações.

BaixasService usa transação ReadCommitted e bloqueio exclusivo do lançamento antes
de ler a soma e movimentar o principal. Todos os registros e estornos do mesmo
lançamento seguem esse protocolo. Leituras compostas usam RepeatableRead para saldo,
status, contagem e itens consistentes. Não se usa avaliação em memória para filtrar
empresas: filtros, contagens e paginação são executados no banco antes do DTO.

FinancialWriteGuard permite INSERT de baixa/estorno apenas em ambiente habilitado, com transação
ativa e escopo interno do serviço; nunca aceita UPDATE/DELETE desses registros. Nesse
escopo, só o status do lançamento pode mudar, entre ABERTO/PARCIAL/QUITADO. Não há
parâmetro HTTP ou configuração que abra o escopo. Os controles anteriores do cadastro,
edição e cancelamento e os privilégios de Development permanecem preservados.

As auditorias CRIAR_BAIXA e ESTORNAR_BAIXA têm entidade baixas, ID, empresa, autoria
tipada, UTC e DTOs anterior/posterior; a segunda inclui motivo e ID do estorno no DTO.
Quando o status muda, ATUALIZAR_STATUS registra os saldos e status anterior/posterior
com entidade lancamentos. ADMIN_GERAL continua com usuario_id NULL e autorId tipado
nos detalhes. Falha de auditoria após os efeitos financeiros provoca rollback integral.
Conflito de bloqueio/duplicidade recebe 409, sem repetição automática; persistência
falha recebe 500 genérico. Nenhum segredo é registrado na auditoria.

Para testar, prepare o banco/conta Testing conforme a matriz e execute:

```powershell
dotnet build --no-restore --nologo
.\scripts\Start-SecurityEnvironment.ps1 -Environment Testing -RunTests
```

BaixasTestingScenario cria somente novas fixtures fictícias. Cobre parcial/total,
múltiplas baixas, centavos, saldo/status, RECEITA/DESPESA, catálogo de pagamento,
conta inativa/estrangeira, validação, quatro perfis, duas empresas, JWT, auditoria,
rollback após INSERT/estorno/status, concorrência de baixa total e parcial e estorno
duplicado. A suíte principal cobre também usuário/perfil/empresa/ADMIN_GERAL revogados,
em todas as novas rotas. Production/Staging permanecem bloqueados; Development é
testado com contexto Testing e deve rejeitar a conexão incompatível antes de emitir SQL.
O guard também bloqueia
entidades novas/alteradas/excluídas fora da transação autorizada.

Limitações: não há idempotência por chave de requisição ou reenvio automático; consulte
saldo/histórico após resultado inconclusivo. Não há novas operações de baixas em
ambientes não habilitados, encargos novos, baixa/estorno de lançamento cancelado, reparo de legado
inconsistente, edição de PARCIAL ou estorno de lançamento. Escritores externos futuros
devem seguir o mesmo bloqueio do lançamento para manter a concorrência segura.

Resultado da implementação inicial: 448 verificações aprovadas na rodada completa final,
zero reprovadas (59 consultas, 51 cadastro, 90 edição/cancelamento, 157 baixas e
91 de login/regressão, incluindo revogações nas novas rotas). O build final passou
com zero erros e zero avisos. Houve uma compilação inicial bloqueada pela DLL em uso
pela API anterior; o processo identificado foi encerrado para recompilar. Uma consulta
inicial também exigiu ajuste de tradução para o provedor MySQL; a suíte foi repetida
integralmente após a correção. O aviso HTTPS da execução HTTP local permanece.

A comparação final confirmou todos os dados, tabelas, views e privilégios originais
intactos, incluindo nove lançamentos e uma auditoria, e todas as fixtures Testing
anteriores preservadas. Development foi reiniciado sem habilitar o novo módulo.
Nenhum teste de escrita financeira foi executado contra o banco original.

## Ativação técnica do módulo de baixas em Development — 2026-11-14

O estado inicial foi registrado por leitura: nove lançamentos ABERTO, zero baixas,
uma auditoria e nenhuma tabela estornos_baixas. Valores, principal baixado e saldo
por lançamento foram guardados em checkpoint privado cifrado. A ausência da estrutura
de estornos impedia também as consultas e os cálculos do módulo; a ativação foi
interrompida até o usuário autorizar especificamente somente essa tabela complementar.

A autorização adicional permitiu criar a estrutura abaixo em Development, sem dados
copiados, com o mesmo modelo de Testing, FK local e baixa_id único:

```sql
CREATE TABLE `gestao_financeira_empresarial`.`estornos_baixas` (
  id BIGINT NOT NULL AUTO_INCREMENT,
  baixa_id BIGINT NOT NULL,
  motivo VARCHAR(500) NOT NULL,
  criado_em DATETIME(6) NOT NULL,
  PRIMARY KEY (id),
  UNIQUE KEY uq_estorno_baixa (baixa_id),
  CONSTRAINT fk_estorno_baixa FOREIGN KEY (baixa_id)
    REFERENCES `gestao_financeira_empresarial`.`baixas` (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
```

As permissões anteriores foram preservadas. Foram adicionadas somente cinco entradas
de privilégios por tabela, mediante estes comandos administrativos fora da API:

```sql
GRANT INSERT ON `gestao_financeira_empresarial`.`baixas`
  TO 'gestao_api_dev'@'localhost';
GRANT SELECT ON `gestao_financeira_empresarial`.`contas_bancarias`
  TO 'gestao_api_dev'@'localhost';
GRANT SELECT ON `gestao_financeira_empresarial`.`formas_pagamento`
  TO 'gestao_api_dev'@'localhost';
GRANT SELECT, INSERT ON `gestao_financeira_empresarial`.`estornos_baixas`
  TO 'gestao_api_dev'@'localhost';
```

SELECT em baixas, SELECT/UPDATE em lancamentos e SELECT/INSERT em auditoria já existiam
e não foram concedidos novamente. A conta da API não recebeu CREATE, ALTER, DROP,
DELETE, GRANT OPTION, privilégio global, papel ou acesso a Testing. A conta e a
estrutura de Testing permaneceram iguais; só novas fixtures fictícias foram usadas.

FinancialPaymentPolicy.DevelopmentEnabled está em true. O serviço escolhe a conta
e a matriz pelo ambiente, e o guard exige ambiente habilitado, escopo interno e
transação ativa para incluir baixa/estorno ou recalcular status. Nenhum parâmetro
HTTP ou configuração pode ampliar os ambientes. FinancialUpdatePolicy e as regras
anteriores de cadastro, edição e cancelamento não foram alteradas.

O build --no-restore --nologo terminou com zero erros e zero avisos. A nova execução
da suíte inteira em Testing aprovou 450 verificações, zero reprovadas: 59 consultas,
51 cadastro, 90 edição/cancelamento, 159 baixas e 91 de login/regressão. Os testes
adaptados comprovam recusa de conexão Testing no serviço Development, inclusive nos
GETs, e recusa de escrita por CONSULTA antes de acessar o banco. Auditoria, rollback,
concorrência, estorno, principal, ajustes e regressões anteriores passaram novamente.
Permanece somente o aviso de HTTPS dos testes HTTP locais, sem aviso de compilação.

A API foi reiniciada controladamente e aprovou 23 verificações HTTP em Development:
JWT renovado pelo login normal, GETs anteriores, consultas de baixas, saldos da própria
empresa idênticos ao checkpoint, OpenAPI com os cinco endpoints, 401 sem autenticação
ou JWT inválido, 403 para escopo estrangeiro, 400 para parâmetro inválido e 405 para
DELETE. Nenhum POST de baixa/estorno com autorização válida foi enviado, nem mesmo
contra ID inexistente. Os POSTs negativos foram exclusivamente sem JWT ou com JWT
inválido. Development não tem CONSULTA ativo; o teste HTTP desse perfil foi feito
em Testing, complementado pelo teste direto do serviço com ambiente Development
e contexto Testing. Não se criou ou ativou usuário para essa validação.

A comparação final confirmou os nove lançamentos, zero baixas, zero estornos e uma
auditoria, com valores, status e saldos iguais aos iniciais. Todos os registros,
tabelas e views preexistentes permaneceram intactos; a única inclusão estrutural foi
a tabela complementar autorizada, vazia. As fixtures Testing anteriores foram
preservadas. Não houve execução financeira real em Development nesta ativação.

### Reversão segura somente do módulo de baixas

Interromper a API e aguardar o término das transações. Publicar a política de pagamentos
com DevelopmentEnabled em false, sem alterar FinancialUpdatePolicy. Revogar somente
as concessões acrescentadas nesta ativação:

```sql
REVOKE INSERT ON `gestao_financeira_empresarial`.`baixas`
  FROM 'gestao_api_dev'@'localhost';
REVOKE SELECT ON `gestao_financeira_empresarial`.`contas_bancarias`
  FROM 'gestao_api_dev'@'localhost';
REVOKE SELECT ON `gestao_financeira_empresarial`.`formas_pagamento`
  FROM 'gestao_api_dev'@'localhost';
REVOKE SELECT, INSERT ON `gestao_financeira_empresarial`.`estornos_baixas`
  FROM 'gestao_api_dev'@'localhost';
```

Não revogar SELECT em baixas nem os privilégios anteriores de lançamentos/auditoria.
Manter a tabela complementar e todos os registros, sem DROP, DELETE ou correções
financeiras automáticas. Conferir a matriz anterior e reiniciar a API com o módulo
de baixas bloqueado e as funcionalidades anteriores preservadas. DatabaseSafety
recusa qualquer combinação de política e privilégios incompatíveis. A tabela nova
é preservada mesmo se a ativação for revertida; sua exclusão não está autorizada.

As limitações anteriores permanecem: sem encargos novos ou chave de idempotência,
sem estorno de lançamento cancelado, sem reparo automático de legado inconsistente.
A ativação técnica não comprova execução de baixa/estorno real em Development:
qualquer operação financeira concreta depende de autorização posterior própria.
