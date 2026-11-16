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
- Baixas parciais/totais, saldo e estorno rastreável em Development e Testing.

## Perfis e permissões atuais

| Perfil | Permissões |
|---|---|
| ADMIN_GERAL | Administra empresas e usuários empresariais. Consulta e cadastra lançamentos de empresas ativas com empresaId explícito, respeitando o escopo empresarial. Sua identidade é armazenada separadamente. |
| ADMIN | Consulta e cadastra lançamentos na própria empresa. Consulta usuários dessa empresa e cria e administra FINANCEIRO e CONSULTA. Não altera nem desativa ADMIN, inclusive a si mesmo. |
| FINANCEIRO | Consulta e cadastra lançamentos na própria empresa. Consulta o próprio cadastro; não administra usuários. |
| CONSULTA | Somente consulta lançamentos da própria empresa; não pode cadastrar lançamentos. Consulta o próprio cadastro; não administra usuários. |

O cadastro financeiro está habilitado em Development e Testing, com contas, bancos
e chaves JWT separados. O serviço e o interceptor validam a conexão correspondente
ao ambiente. Edição e cancelamento estão habilitados em Development e Testing.
DELETE financeiro permanece bloqueado em ambos. As regras anteriores
de administração de empresas e usuários
permanecem preservadas. ADMIN_GERAL informa empresaId na query dos GETs e no corpo JSON
do POST; usuários empresariais usam a empresa do JWT e não podem informar empresaId no POST.
ADMIN_GERAL não utiliza os endpoints exclusivos de usuários empresariais.

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

A configuracao atual exige MySQL em localhost:3306 e privilegios exatos por tabela.
As tres tabelas de seguranca preservam SELECT, INSERT e UPDATE. Development tem
SELECT/INSERT/UPDATE em lancamentos, SELECT/INSERT em auditoria, baixas e estornos_baixas,
e SELECT nas tabelas auxiliares, contas_bancarias e formas_pagamento. Baixas e estornos
não recebem UPDATE ou DELETE.
Testing acrescenta UPDATE somente em lancamentos e SELECT/INSERT em baixas,
contas_bancarias e formas_pagamento para consultas e novas fixtures fictícias.
Nao ha privilegios globais, de schema ou concessao, nem papeis ativos.
A API rejeita destino, identidade ou permissoes incompativeis. Root nao e usado pela API.
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

O cenario cria novas fixtures identificadas por execucao somente em Testing.
Nao exclui registros nem altera fixtures anteriores. Consulte SECURITY_TESTING.md
antes de usar `-RunTests`.

## Estrutura e próximos passos

- Program.cs: configuração, autenticação e endpoints.
- Models/: entidades, DTOs e contexto EF Core.
- Security/: validação das conexões e cenário HTTP isolado.
- scripts/Start-SecurityEnvironment.ps1: inicializador sem credenciais embutidas.

Próximos passos: ampliar a cobertura automatizada; desenvolver clientes,
fornecedores, contas bancarias, escrita financeira e fluxo de caixa; e preparar configuracao
específica para implantação. A configuração atual destina-se aos ambientes locais descritos.

## Autor

Carlos Bueno — [carlosbuenodev-coder](https://github.com/carlosbuenodev-coder)

## Consultas de lancamentos

GET /lancamentos e GET /lancamentos/{id} exigem JWT e identidade atualmente ativa.
ADMIN, FINANCEIRO e CONSULTA consultam a propria empresa. ADMIN_GERAL precisa
informar empresaId explicitamente em ambas as rotas. IDs de outra empresa ou
ausentes retornam 404 dentro do escopo. Tentativa de trocar o escopo empresarial
retorna 403. Permissoes revogadas, usuario/empresa ou administrador inativos recebem 403.

Filtros opcionais: tipo (RECEITA/DESPESA), status (ABERTO/PARCIAL/QUITADO/CANCELADO),
categoriaId, clienteId, fornecedorId, centroCustoId, vencimentoInicio e vencimentoFim
(datas ISO yyyy-MM-dd, limites inclusivos). IDs devem ser positivos.

Paginacao: pagina=1, tamanhoPagina=20 (1 a 100). Ordenacao: ordenarPor aceita
id, dataVencimento (padrao), dataEmissao ou valor; direcao aceita asc (padrao) ou desc.
O desempate usa ID ascendente. Parametros invalidos retornam 400.
A resposta possui itens, total, pagina, tamanhoPagina e totalPaginas; nao calcula
saldo ou baixas. O detalhe devolve um DTO sem dados de autenticacao.

Exemplo: `/lancamentos?tipo=RECEITA&status=ABERTO&vencimentoInicio=2026-01-01&pagina=1&tamanhoPagina=20`.
Para ADMIN_GERAL, acrescente `empresaId` em lista e detalhe.

Development tem SELECT/INSERT/UPDATE em lancamentos, SELECT/INSERT em auditoria,
baixas e estornos_baixas e SELECT nas referências e catálogos. Testing mantém UPDATE somente
em lancamentos; auditoria continua somente SELECT/INSERT.
Consulte a matriz exata e o procedimento
seguro de execucao em [SECURITY_TESTING.md](SECURITY_TESTING.md).

## Cadastro financeiro em Development e Testing

`POST /lancamentos` retorna 201 com DTO e Location. ADMIN e FINANCEIRO usam a empresa
do JWT; ADMIN_GERAL exige empresaId no JSON; CONSULTA não pode cadastrar.
Os cadastros validam tipo, descrição, datas, valor, categoria compatível e vínculos
empresariais. Cliente, fornecedor e centro de custo são opcionais. O status inicial
é sempre ABERTO; o cliente não pode fornecer status. Lançamento e auditoria são
atômicos, com autoria tipada para o administrador geral.

```json
{
  "tipo": "RECEITA",
  "descricao": "Receita fictícia",
  "categoriaId": 1,
  "dataEmissao": "2026-11-01",
  "dataVencimento": "2026-11-30",
  "valor": 123.45
}
```

O exemplo depende de categoria fictícia existente da mesma empresa e tipo em Testing.
Os testes funcionais gravam somente novas fixtures em Testing. Em 2026-11-14,
um único cadastro fictício expressamente autorizado em Development retornou 201
(lançamento 9), com GET 200 e auditoria 1. Os oito lançamentos anteriores e os demais
registros e estruturas foram preservados.
UPDATE financeiro está restrito a lancamentos; DELETE permanece bloqueado em ambos.
As regras e testes estao descritos
em [SECURITY_TESTING.md](SECURITY_TESTING.md).


## Edição e cancelamento em Development e Testing

`PUT /lancamentos/{id}` substitui os campos editáveis e retorna 200 com DTO.
`POST /lancamentos/{id}/cancelamento` retorna 200 com o registro preservado e status
CANCELADO. Ambos exigem JWT válido, perfil atual ADMIN/FINANCEIRO da própria empresa
ou ADMIN_GERAL ativo com empresaId explícito no corpo JSON. CONSULTA recebe 403.
Empresa e usuário devem estar ativos. O escopo não é aceito na query dessas operações.
Em outros ambientes, o serviço retorna 403 antes de acessar o banco.
GETs e POST de cadastro permanecem disponíveis nos ambientes anteriores.

Exemplo de PUT em Testing (IDs devem ser de fixtures da mesma empresa):

```json
{
  "descricao": "Receita fictícia revisada",
  "categoriaId": 1,
  "clienteId": null,
  "fornecedorId": null,
  "centroCustoId": null,
  "dataEmissao": "2026-11-01",
  "dataVencimento": "2026-11-30",
  "valor": 150.00,
  "observacoes": "Revisão fictícia"
}
```

Para cancelar como usuário empresarial, envie `{}`. Para ADMIN_GERAL, acrescente
`"empresaId": 1` ao PUT ou envie `{"empresaId": 1}` no cancelamento. EmpresaId é
somente escopo do administrador geral; nunca muda a empresa do lançamento.
PUT exige descrição, categoria, datas e valor; campos opcionais omitidos ficam nulos.
ID, tipo, criadoEm e status não são aceitos no corpo. Campos desconhecidos recebem 400.

Somente ABERTO sem qualquer baixa pode ser editado ou cancelado. PARCIAL é bloqueado
até definição das regras de saldo; QUITADO, CANCELADO e cancelamento repetido recebem
409. Referências de outra empresa, categoria incompatível, datas ou valores inválidos
recebem 400; IDs ausentes ou fora da empresa recebem 404. Não há exclusão ou estorno
do lançamento. O estorno de baixa existe no módulo descrito abaixo.
As validações de texto, datas e decimal(15,2) são as mesmas do cadastro.

A transação serializável bloqueia o lançamento durante a operação e registra auditoria
ALTERAR/CANCELAR com autoria, estado anterior e posterior e horário UTC. Falha na
auditoria reverte o UPDATE. Os endpoints não inserem nem alteram baixas.

Execute o build e a suíte somente com as dependências Testing previamente preparadas:

```powershell
dotnet build --no-restore --nologo
.\scripts\Start-SecurityEnvironment.ps1 -Environment Testing -RunTests
```

A rodada da implementação inicial aprovou 262 verificações (59 consultas, 51 cadastro/segurança,
86 edição/cancelamento e 66 de login e regressão), sem reprovações. O build passou sem
avisos; o aviso de HTTPS em HTTP local permanece documentado. Consulte
[SECURITY_TESTING.md](SECURITY_TESTING.md) para permissões, preparação e limitações.

## Ativação técnica de Development

`FinancialUpdatePolicy.DevelopmentEnabled` está em `true`. Serviço, interceptor e
DatabaseSafety exigem ambiente, conta, banco e matriz exata correspondentes. A
ativação acrescenta somente UPDATE em lancamentos e SELECT em baixas para
gestao_api_dev; não concede DELETE, escrita em baixas ou acesso a Testing.

Os testes funcionais de escrita continuam exclusivos de Testing. Esta ativação
não autoriza editar ou cancelar nenhum dos nove lançamentos de Development, nem
criar fixtures nesse banco. Sua auditoria e todos os registros existentes devem
permanecer preservados. A execução de uma operação real exige autorização própria.

O procedimento de ativação, as validações e a reversão segura estão em
[SECURITY_TESTING.md](SECURITY_TESTING.md#procedimento-de-ativação-em-development).

A suíte final desta ativação aprovou 266 verificações em Testing, sem reprovações.
Development iniciou com a matriz autorizada e passou por 18 verificações HTTP de
consulta, autenticação, endpoints e bloqueios seguros, sem escrita financeira.
Os nove lançamentos, a auditoria e as demais estruturas e registros foram preservados.
Não há CONSULTA ativo em Development: o bloqueio desse perfil foi comprovado por HTTP
em Testing e no serviço Development usando contexto Testing, sem criar usuários reais.

## Baixas financeiras — Development e Testing

O módulo inteiro, incluindo consultas, retorna 403 fora de Development/Testing após
autenticação. A política FinancialPaymentPolicy habilita os dois ambientes explicitamente;
as políticas anteriores de cadastro, edição e cancelamento permanecem preservadas.
Para a ativação, foi autorizada especificamente a tabela complementar estornos_baixas
em Development, sem copiar registros nem alterar as tabelas e views existentes.

| Método | Rota | Resposta |
|---|---|---|
| POST | `/lancamentos/{id}/baixas` | 201, baixa e saldo; Location do detalhe. |
| GET | `/lancamentos/{id}/baixas` | 200, itens, total, pagina e tamanhoPagina. |
| GET | `/lancamentos/{id}/saldo` | 200, valorOriginal, totalBaixado, saldoRestante e status. |
| GET | `/baixas/{id}` | 200, DTO da baixa, incluindo estorno, motivo e data se houver. |
| POST | `/baixas/{id}/estorno` | 200, baixa preservada marcada no DTO como estornada e saldo recalculado. |

ADMIN e FINANCEIRO registram/estornam na própria empresa. ADMIN_GERAL exige empresaId
explícito no JSON dos POSTs e na query dos GETs. CONSULTA só consulta a própria empresa.
JWT, identidade, perfil e empresa atuais são conferidos. IDs fora do escopo recebem
404; escopo indevido recebe 400/403. O escopo empresarial precede IDs, contagens e saldo.
O cliente não pode mudar tipo, empresa ou status por meio desses contratos.

Exemplo fictício de registro em Testing (conta e forma devem existir):

```json
{
  "contaBancariaId": 1,
  "formaPagamentoId": 1,
  "dataPagamento": "2026-11-14",
  "valor": 30.00,
  "observacoes": "Recebimento fictício"
}
```

Conta e data são obrigatórias; a conta deve estar ativa e pertencer à empresa.
Forma de pagamento e observações são opcionais. A forma, quando enviada, precisa
existir; seu catálogo é global, conforme o DDL real, sem coluna empresa_id.
Observações têm até 255 caracteres. Datas usam yyyy-MM-dd (anos 1000–9999).
Valor positivo deve caber em decimal(15,2), sem arredondamento silencioso.
Campos desconhecidos, inclusive juros/multa/desconto ou status, recebem 400.

Conforme a regra confirmada nesta entrega, totalBaixado soma **somente valor principal**
das baixas não estornadas; saldoRestante = valorOriginal - totalBaixado. Novas baixas
têm juros, multa e desconto zero. Os ajustes legados são expostos separadamente no
DTO; não são somados ao principal nem alterados. As views originais não são usadas
ou copiadas: sua fórmula de total com encargos não representa este contrato.
Juros, multa e desconto para novos registros serão implementados em uma etapa futura.

Registro aceita ABERTO/PARCIAL e nunca ultrapassa o saldo. QUITADO/CANCELADO ou
inconsistência entre principal, baixas e status recebem 409, sem correção automática.
Sem baixas válidas o status é ABERTO; com saldo positivo, PARCIAL; saldo zero, QUITADO.
CANCELADO não é recalculado automaticamente.

Estorno exige um motivo com até 500 caracteres:

```json
{ "motivo": "Pagamento fictício registrado por engano" }
```

O estorno acrescenta um registro em estornos_baixas; não atualiza nem apaga a baixa.
Não permite duplicidade, acesso entre empresas ou lançamento cancelado. Recalcula o
saldo e o status, inclusive reabrindo um lançamento quitado. A listagem mantém todo
o histórico, inclusive baixas estornadas. Edição/cancelamento do lançamento continuam
conservadoramente bloqueados quando existe qualquer baixa, mesmo após seu estorno.

Listagem aceita pagina (padrão 1) e tamanhoPagina (padrão 20, máximo 100), com ID
ascendente e total da empresa antes da paginação. Parâmetros inválidos recebem 400.

A transação bloqueia o lançamento com SELECT FOR UPDATE antes de ler o saldo e
movimentá-lo. Baixa/estorno, status e auditorias pertencem à mesma transação; falha
na auditoria desfaz todos os efeitos. A tabela complementar tem baixa_id único.
Conflitos concorrentes recebem 409; erros de persistência, 500 genérico. Não existe
chave de idempotência para reenvios: após timeout ou resultado inconclusivo, consulte
o histórico e o saldo antes de decidir reenviar; a API não repete operações sozinha.

Prepare a estrutura/permissões do ambiente descritas em SECURITY_TESTING.md e
execute a suíte de escrita somente com `-Environment Testing -RunTests`. O repositório não
provisiona bancos ao iniciar e nunca executa migração em Development. O módulo não
calcula saldo bancário, não altera contas bancárias e não oferece encargos novos,
transferências ou estorno de lançamento.

## Validação da ativação de baixas em Development

A ativação acrescentou somente INSERT em baixas, SELECT em contas_bancarias e
formas_pagamento e SELECT/INSERT em estornos_baixas para gestao_api_dev@localhost.
Os privilégios anteriores foram preservados; não há DELETE, UPDATE de baixa/estorno,
DDL, privilégios globais ou acesso cruzado nas contas da API. Testing não recebeu
alteração de estrutura ou privilégios nesta ativação.

A suíte completa em Testing aprovou 450 verificações, sem reprovações (159 do módulo
de baixas e 291 de consultas, cadastro, edição/cancelamento, login e regressão).
O build passou sem erros ou avisos. Development passou por 23 verificações HTTP de
autenticação, GETs, saldos, registro dos endpoints e bloqueios seguros, sem qualquer
POST com autorização válida para baixa ou estorno. Não há CONSULTA ativo nesse banco;
sua recusa foi comprovada por HTTP em Testing e no serviço Development com contexto Testing.

Os nove lançamentos ABERTO, zero baixas e uma auditoria permaneceram intactos, assim
como valores, saldos, tabelas e views existentes. A nova tabela de estornos ficou
vazia. Os exemplos de escrita continuam destinados somente a Testing; esta ativação
não autoriza registrar ou estornar baixas em Development durante a validação.
O procedimento de reversão está em SECURITY_TESTING.md; ele preserva os registros
e a tabela complementar, bloqueando a política e removendo só as concessões novas.

## Consultas de fluxo de caixa (somente leitura)

Todos os endpoints exigem JWT e usuário/empresa ativos. ADMIN, FINANCEIRO e CONSULTA
consultam somente a própria empresa. ADMIN_GERAL precisa informar `empresaId`
explicitamente, com cadastro geral ativo; nunca há consulta global implícita.
O escopo é validado no banco, não apenas pelas claims ou pelo parâmetro recebido.

| GET | Resposta |
| --- | --- |
| `/fluxo-caixa/resumo` | Totais previstos e realizados separados, pendências e contagens |
| `/fluxo-caixa/previsto` | Lista paginada dos principais ainda pendentes, por vencimento |
| `/fluxo-caixa/realizado` | Lista paginada das baixas efetivas, por pagamento/recebimento |
| `/fluxo-caixa/por-conta` | Realizado agrupado por conta bancária, paginado por ID |
| `/fluxo-caixa/por-periodo` | Previsão e realização agrupadas por dia, em colunas separadas |
| `/fluxo-caixa/dashboard` | Resumo completo e páginas dos agrupamentos diário e por conta |

Parâmetros comuns: `dataInicial` e `dataFinal` obrigatórios (`yyyy-MM-dd`, inclusivos),
`empresaId`, `categoriaId`, `centroCustoId`, `tipo` (`RECEITA` ou `DESPESA`),
`pagina` (padrão 1) e `tamanhoPagina` (padrão 20, entre 1 e 100).
O deslocamento máximo é Int32.MaxValue. Paginação limita listas e agrupamentos;
o resumo sempre considera todos os registros filtrados. No dashboard, cada agrupamento
aplica a mesma página/tamanho independentemente. Dias e contas sem movimento não são
preenchidos artificialmente. A ordem das listas é data, lançamento e baixa, com desempate
por ID; agrupamentos seguem data ou ID da conta em ordem crescente.

`contaBancariaId` é aceito em `realizado` e `por-conta`. O lançamento não possui conta
prevista; atribuí-la pela última baixa inventaria uma relação. Por isso, o parâmetro
é rejeitado com 400 em `previsto`, `resumo`, `por-periodo` e `dashboard`. Use as consultas
de realizado para filtrar uma conta. Contas inativas continuam consultáveis em seu histórico.
IDs estrangeiros e inexistentes recebem a mesma validação 400, sem expor sua existência;
empresa fora do escopo recebe 403. Categoria e tipo incompatíveis recebem 400.
Datas inválidas, invertidas ou ausentes e paginação inválida recebem 400; JWT ausente,
inválido ou expirado recebe 401. Usuário ou empresa inativos recebem 403.

O previsto usa `valorOriginal - SUM(baixas.valor sem estorno)` e inclui somente resultados
positivos de lançamentos não cancelados com vencimento no intervalo. O abatimento usa
**todo o histórico atual** de baixas válidas, mesmo fora do intervalo solicitado.
Assim, trata-se da pendência atual organizada por vencimento, não de uma reconstrução
histórica do saldo na data final. O status QUITADO sozinho não substitui o cálculo.
Um principal baixado superior ao original interrompe consultas com previsto e retorna 409;
não há ajuste automático ou ocultação de saldo negativo.

O realizado usa apenas baixas sem estorno, por `data_pagamento` no intervalo, relacionadas
a lançamento e conta da empresa autorizada. Recebimentos e pagamentos históricos válidos
continuam realizados mesmo se um lançamento legado tiver status CANCELADO; cancelamento
exclui somente a previsão e não equivale a estorno. Baixas estornadas são excluídas
integralmente do realizado e deixam de abater a pendência. Estornos são considerados pelo
estado atual, sem criar uma movimentação negativa fictícia na data de estorno.

Os totais são de **principal**. Juros, multa e desconto históricos aparecem separadamente
na lista realizada e não entram no principal, no saldo previsto ou no saldo realizado.
Não são criadas novas regras de encargos. Uma parcela baixada aparece no realizado e
somente seu restante aparece no previsto, sem duplicar o valor original.

`saldoPrevisto` e `saldoRealizado` são receitas menos despesas de cada modalidade no
período. `valoresPendentes` soma receitas e despesas ainda pendentes (valor bruto, não
líquido). `quantidadeLancamentos` conta IDs distintos com pendência no intervalo ou baixa
válida no intervalo; `quantidadeBaixasValidas` conta as baixas da parte realizada.
As datas do resumo registram o filtro aplicado. Todos os valores e contagens respeitam
categoria, centro de custo e tipo quando informados.

Existe `contas_bancarias.saldo_inicial`, mas não há data de referência nem garantia de
um histórico bancário completo. `saldoBancarioAcumulado` retorna `null`. O fluxo líquido
apurado por conta não representa saldo bancário disponível ou acumulado. Nenhuma conta
ou saldo inicial é criado ou alterado.

Exemplo fictício de consulta:

```http
GET /fluxo-caixa/resumo?dataInicial=2030-01-01&dataFinal=2030-01-31&empresaId=123
Authorization: Bearer <JWT>
```

```json
{
  "empresaId": 123,
  "dataInicial": "2030-01-01",
  "dataFinal": "2030-01-31",
  "receitasPrevistas": 680,
  "despesasPrevistas": 410,
  "receitasRealizadas": 470,
  "despesasRealizadas": 150,
  "saldoPrevisto": 270,
  "saldoRealizado": 320,
  "valoresPendentes": 1090,
  "quantidadeLancamentos": 7,
  "quantidadeBaixasValidas": 5
}
```

Exemplo fictício da lista realizada (página com um registro):

```json
{
  "empresaId": 123,
  "pagina": 1,
  "tamanhoPagina": 1,
  "total": 5,
  "itens": [{
    "lancamentoId": 10, "baixaId": 20, "contaBancariaId": 30,
    "tipo": "RECEITA", "descricao": "Fluxo ficticio", "categoriaId": 40,
    "centroCustoId": 50, "data": "2030-01-05T00:00:00",
    "principal": 200, "juros": 3, "multa": 2, "desconto": 1
  }]
}
```

No previsto, `baixaId` e `contaBancariaId` são `null`, a data é o vencimento e
`principal` é o restante. Agrupamentos usam o mesmo envelope de paginação. Por conta,
cada item contém `contaBancariaId`, `receitasRealizadas`, `despesasRealizadas`,
`saldoRealizado` e `saldoBancarioAcumulado: null`. Por dia, contém `data` e os seis
campos monetários de previsão/realização e saldos. O dashboard contém `resumo`,
`porPeriodo` e `porConta` com esses mesmos contratos.

As views originais foram inspecionadas e preservadas. `vw_fluxo_caixa` inclui encargos
e não exclui estornos; `vw_saldos_lancamentos` também soma baixas estornadas. Portanto,
os novos endpoints consultam as tabelas com filtros explícitos, sem depender dessas views
ou exigir novos privilégios SELECT. Não há migrations, DDL, novos índices ou SaveChanges
no serviço. Consultas e agregações são assíncronas, filtradas no MySQL, sem rastreamento;
contagens e páginas compartilham uma transação RepeatableRead.

Para executar a regressão, compile com `dotnet build --no-restore --nologo` e execute
`./scripts/Start-SecurityEnvironment.ps1 -Environment Testing -RunTests` no PowerShell.
Credenciais Testing permanecem em DPAPI. A suíte só aceita o banco exclusivo Testing;
não use dados reais, não execute provisionamento nem amplie permissões. Detalhes e
resultados da validação estão em SECURITY_TESTING.md.

## Frontend React + TypeScript

A interface inicial fica em `frontend/`, com React 19.3.1, TypeScript 7.0.2,
Vite 8.3.8 e React Router 7.18.5. As versões exatas e dependências transitivas
estão em `frontend/package.json` e `frontend/package-lock.json`.
Pré-requisito: Node.js 24 ou superior com npm. Ambiente validado: Node.js
24.21.0 e npm 11.19.0. No Windows, use `npm.cmd` se a política do PowerShell
impedir a execução de `npm.ps1`, sem precisar mudar a política do sistema.

```text
frontend/
  .env.example           Exemplo público de configuração
  index.html             Documento em português brasileiro
  src/
    components/          Marca própria, ícones, alertas e layout responsivo
    contexts/            Estado da autenticação e ciclo de sessão em memória
    hooks/               useAuth
    pages/               Login, início autenticado e páginas de estado
    routes/              React Router e proteção da área autenticada
    services/            Cliente HTTP e tratamento de respostas
    styles/              Estilos responsivos e foco de teclado
    test/                Testes de interface com respostas simuladas
    types/               Contratos e validação da resposta da API
```

Instalação e execução, a partir da raiz do repositório:

```powershell
cd frontend
npm.cmd install
Copy-Item .env.example .env.local
npm.cmd run dev
```

O frontend fica em `http://localhost:5173/login`. A porta é fixa (`strictPort`):
se estiver ocupada, encerre o processo que a utiliza. Não há troca silenciosa de
porta porque a origem precisa corresponder ao CORS autorizado. O Vite escuta
somente em localhost. Para instalação reproduzível após clonar, pode-se usar
`npm.cmd ci` com o lockfile publicado.

O arquivo `.env.local` pode conter:

```dotenv
VITE_API_BASE_URL=http://localhost:5216
```

Essa é também a URL padrão. Reinicie o Vite após editar a configuração. Variáveis
`VITE_` são públicas e incorporadas ao build; nunca coloque senhas, chaves JWT ou
credenciais nelas. Todos os `.env` locais continuam ignorados; apenas
`frontend/.env.example`, sem segredos, é publicado. `node_modules`, `dist`, logs,
cobertura e artefatos locais de QA também ficam fora do Git.

Para iniciar a API em outro terminal, na raiz do repositório:

```powershell
dotnet build --no-restore --nologo
dotnet run --no-build --launch-profile http
```

A API utiliza a configuração local existente em User Secrets e as verificações
atuais de ambiente/privilégios. Não execute provisionamento, migrations ou
`--criar-admin-geral` para usar o frontend. API HTTP local: `http://localhost:5216`.
O perfil HTTPS existente usa 7272; para este frontend local, use o perfil `http`
para manter a origem de desenvolvimento documentada. Se o PowerShell restringir
scripts, os comandos acima não exigem liberar execução de arquivos `.ps1`.

O único ajuste HTTP no backend é CORS, somente em Development:
origem exata `http://localhost:5173`, métodos GET/POST e cabeçalhos Content-Type e
Authorization. Não há wildcard nem cookies cross-origin. `127.0.0.1:5173`, outra
porta e origens externas não são autorizados. Outros ambientes mantêm o
comportamento anterior e precisam de configuração própria antes de uma implantação
externa. O CORS não substitui JWT, perfis ou isolamento empresarial.
O `.csproj` exclui `frontend/**` dos itens .NET para evitar copiar dependências,
artefatos ou `.env` para a saída do backend. O frontend é compilado separadamente.
Nenhum endpoint ou contrato financeiro foi modificado.

### Login e sessão

O formulário solicita código da empresa, e-mail e senha. O código é informado
manualmente pelo usuário autorizado, sem consultar ou listar empresas de terceiros.
IDs precisam ser inteiros positivos representáveis com precisão em JavaScript;
valores acima de Number.MAX_SAFE_INTEGER são rejeitados, nunca arredondados.

Contrato real enviado, sem campos adicionais:

```http
POST /auth/usuarios/login
Content-Type: application/json
```

```json
{ "empresaId": 7, "email": "usuario@empresa.invalid", "senha": "<informada-localmente>" }
```

A API retorna `token`, `tipo` (`Bearer`), `expiraEmUtc` e `usuario`, que contém
`id`, `empresaId`, `nome`, `email`, `perfil` e `ativo`. Antes de liberar `/app`,
o frontend confirma a identidade com:

```http
GET /usuarios/me
Authorization: Bearer <token-em-memoria>
```

A identidade exibida vem dessa confirmação. Os IDs do usuário e da empresa são
conferidos contra o login; contratos inválidos ou identidade de outra empresa
impedem a liberação. O formulário permanece bloqueado durante a verificação.
Nenhum token aparece em URLs, logs, localStorage ou sessionStorage. Senhas ficam
somente no estado temporário do formulário e não são persistidas. A opção mostrar
senha é explícita; a senha não é alterada por trim. E-mail é enviado sem espaços
externos. Dados de empresa/e-mail são preservados após falhas; senha é limpa quando
há recusa de credenciais ou acesso, ou após sucesso, e preservada em falhas de rede
recuperáveis enquanto o formulário estiver aberto.

O logout descarta o token, limpa a identidade e impede respostas tardias de
restaurarem uma sessão encerrada. Ao recarregar a página, é necessário novo login.
A sessão expira pelo horário informado pela API; 401 em consulta protegida remove
o acesso e solicita novo login. 403 remove a sessão e leva à página não autorizada.
Não há refresh token nem endpoint fictício de logout. Como o JWT é stateless,
o logout local não revoga cópias externas de um token: sua validade no servidor
continua seguindo as regras já existentes do backend.

O botão **Verificar acesso**, o retorno à janela e a reativação da aba consultam
`/usuarios/me` novamente. Falhas nessa confirmação removem dados protegidos e
solicitam nova autenticação. Há timeout de 15 segundos para solicitações. Erros de
rede, indisponibilidade, HTTP 500 e respostas inesperadas recebem mensagens amigáveis;
corpos técnicos e stack traces da API nunca são apresentados ao usuário.

Este login atende ADMIN, FINANCEIRO e CONSULTA. ADMIN_GERAL continua preservado
em `/auth/admin/login`, mas não possui tela nesta etapa: `/usuarios/me` exige a
política empresarial UsuarioEmpresa e não aceita essa identidade geral. Não foi
criado um contrato alternativo nem alterada a segurança para contornar a separação.
A interface não permite troca de empresa, e o backend continua responsável pela
autorização de cada solicitação.

### Rotas e escopo atual

- `/login`: formulário empresarial; usuários autenticados seguem para `/app`.
- `/app`: início protegido, identificação do usuário, empresa por ID e perfil.
- `/nao-autorizado`: explicação de acesso restrito.
- `/nao-encontrado`: página para caminhos desconhecidos.

A raiz encaminha para a área protegida. O layout inclui cabeçalho, menu lateral,
logout e menu móvel com foco de teclado, Escape e fundo modal. O nome empresarial
não é inventado: como `/usuarios/me` fornece somente `empresaId`, a tela mostra
`Empresa #ID`. Dashboard, lançamentos, contas a pagar/receber, baixas, fluxo de caixa,
relatórios e configurações aparecem desabilitados e identificados como em
desenvolvimento. Nenhum valor financeiro fictício é exibido. A ilustração e os
ícones são locais, sem bibliotecas de UI, fontes externas ou logotipos de terceiros.

### Validação do frontend

Na pasta `frontend`:

```powershell
npm.cmd run typecheck
npm.cmd run build
npm.cmd test
```

`build` verifica TypeScript e gera `frontend/dist`. `test` usa Vitest, jsdom e
Testing Library com mocks de fetch; não usa usuários reais nem acessa MySQL.
Não foi adicionado ESLint: `typecheck` é a verificação estática disponível,
e `test:watch` permite desenvolvimento dos testes. Dependências de execução são
somente React, React DOM e React Router; as demais são de compilação ou testes.
Para visualizar o build, `npm.cmd run preview` usa `http://localhost:4173`.
Essa origem não está autorizada no CORS local: use `npm.cmd run dev` em 5173 para
validar login com a API, sem ampliar origens automaticamente.

Validação desta entrega em 16/11/2026:

- Instalação concluída; auditoria npm inicial sem vulnerabilidades reportadas.
- Builds React/TypeScript/Vite e .NET aprovados; backend sem avisos ou erros.
- 33 testes automatizados aprovados: renderização, campos, login, confirmação de
  identidade, erros, isolamento de empresa, expiração, rotas, logout, recarga,
  resposta tardia, módulos futuros e menu móvel.
- Nove verificações em Edge real com autenticação simulada, incluindo desktop
  1440×1000, móvel 390×844, ausência de overflow, menu e logout.
- Sete verificações reais de CORS, somente OPTIONS e GET `/usuarios/me` sem token:
  origem permitida, métodos/cabeçalhos limitados, origens recusadas e HTTP 401.
- Nenhum login real foi executado e nenhuma credencial foi solicitada pelo chat.
  A validação com um usuário real permanece como teste manual local, exclusivamente
  com credenciais fornecidas pelo usuário no próprio formulário.
- Nenhuma operação financeira ou escrita, DDL ou mudança de privilégios MySQL foi
  executada. A inicialização da API apenas realizou suas verificações existentes.
  A suíte financeira anterior não foi executada nesta etapa, pois cria operações
  financeiras fictícias; os novos testes são de interface e autenticação com mocks.

Para uma implantação futura, configure a URL HTTPS real da API no build e o
servidor estático para responder com `index.html` nas rotas da SPA (inclusive acesso
direto a `/app`). Publicação em hospedagem externa não faz parte desta etapa.
Próximas funcionalidades: telas financeiras integradas aos contratos já existentes,
dashboard real e, mediante escopo específico, acesso administrativo geral.
