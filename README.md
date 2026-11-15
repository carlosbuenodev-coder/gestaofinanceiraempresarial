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
- Baixas parciais/totais, saldo e estorno rastreável exclusivamente em Testing.

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
SELECT/INSERT/UPDATE em lancamentos, SELECT/INSERT em auditoria, SELECT em baixas
e nas quatro tabelas auxiliares. Baixas não recebe permissão de escrita em Development.
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

Development tem SELECT/INSERT/UPDATE em lancamentos, SELECT/INSERT em auditoria
e somente SELECT em baixas e nas tabelas auxiliares. Testing mantém UPDATE somente
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
do lançamento. O estorno de baixa existe somente no módulo Testing descrito abaixo.
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

## Baixas financeiras — somente Testing

O módulo inteiro, incluindo consultas, retorna 403 fora de Testing após autenticação.
As operações anteriores de lançamentos em Development continuam habilitadas; nenhuma
permissão, estrutura ou registro desse banco foi alterado nesta entrega.

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

Prepare somente a estrutura/permissões Testing descritas em SECURITY_TESTING.md e
execute a suíte existente com `-Environment Testing -RunTests`. O repositório não
provisiona bancos ao iniciar e nunca executa migração em Development. O módulo não
calcula saldo bancário, não altera contas bancárias e não oferece encargos novos,
transferências ou estorno de lançamento.
