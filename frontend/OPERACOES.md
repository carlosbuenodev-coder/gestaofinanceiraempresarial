# Telas de lançamentos e contas

As telas operacionais usam os endpoints financeiros existentes e um GET de
cadastros para preencher os seletores do formulário.

## Rotas e componentes

- `/login` e `/app`: login empresarial e início protegido.
- `/app/dashboard`: indicadores, gráficos e movimentações financeiras do período.
- `/app/lancamentos`: listagem de receitas e despesas.
- `/app/contas-a-pagar`: mesma consulta, sempre com `tipo=DESPESA`.
- `/app/contas-a-receber`: mesma consulta, sempre com `tipo=RECEITA`.
- `/app/lancamentos/novo`: cadastro por ADMIN e FINANCEIRO.
- `/app/lancamentos/:id`: detalhes para todos os perfis empresariais.
- `/app/lancamentos/:id/editar`: edição por ADMIN e FINANCEIRO, somente ABERTO.
- `/app/fluxo-caixa`: resumo de caixa para ADMIN, FINANCEIRO e CONSULTA.

`EntriesPage` reutiliza `EntryFilters` e `EntryBalancePanel`; `EntryPage` reúne
leitura dos detalhes, edição e confirmação de cancelamento, reutilizando
`EntryForm`. Os contratos e seus validadores ficam em `types/entries.ts`; o
serviço em `services/entries.ts`; validação de formulário em `utils/entryForm.ts`.
O menu disponibiliza Início, Dashboard, Lançamentos, Contas a pagar, Contas a
receber e Fluxo de caixa. Baixas, Relatórios e Configurações continuam marcados
como módulos futuros. A interface ainda não oferece telas de baixa, estorno,
administração geral ou manutenção de cadastros auxiliares.

## Contratos reais utilizados

| Método e endpoint | Uso |
| --- | --- |
| GET `/lancamentos` | Lista paginada com `itens`, `total`, `pagina`, `tamanhoPagina`, `totalPaginas`. |
| GET `/lancamentos/{id}` | Detalhes, inclusive tipo, referências, datas, valor, status e observações. |
| GET `/lancamentos/{id}/saldo` | Valor original, total de principal baixado e saldo restante oficiais. |
| GET `/cadastros-financeiros` | Catálogos da empresa autenticada com ID, nome, tipo das categorias e situação de clientes e fornecedores. |
| GET `/fluxo-caixa/resumo` | Receitas, despesas e resultados líquidos previstos e realizados, por período e tipo de movimentação. |
| GET `/fluxo-caixa/por-conta` | Receitas, despesas e líquido realizado por conta. A tela dedicada consulta este endpoint quando o resumo retorna 409. |
| POST `/lancamentos` | Cadastro de receita ou despesa. |
| PUT `/lancamentos/{id}` | Atualização completa dos campos editáveis, sem enviar tipo. |
| POST `/lancamentos/{id}/cancelamento` | Cancelamento com confirmação e corpo `{}`. |

Os filtros são os existentes no backend: tipo, situação (`ABERTO`, `PARCIAL`,
`QUITADO`, `CANCELADO`), intervalo de vencimento e IDs de categoria, cliente,
fornecedor e centro de custo. A ordenação aceita ID, vencimento, emissão ou valor,
em ordem crescente/decrescente. Paginação de 10, 20, 50 ou 100 itens. Os filtros
aplicados e a página ficam na URL, preservando o retorno da tela de detalhes.
Editar filtros não consulta a API até clicar em Aplicar filtros. Não há polling.

O fluxo de caixa abre no mês atual. O previsto considera o principal pendente
por vencimento e o realizado considera o principal das baixas válidas por data
de pagamento. Com HTTP 200, a tela apresenta os dois resultados líquidos
fornecidos pelo resumo. Com HTTP 409, consulta o realizado por conta com as
mesmas datas e o mesmo tipo de movimentação. A tabela parcial exibe receitas,
despesas e líquido realizado por conta, e indica que o previsto e o total
consolidado estão indisponíveis, sem apresentar zeros em seu lugar.
Se a segunda consulta falhar, o aviso sobre o previsto permanece e a tela
oferece nova tentativa. Respostas de filtros anteriores são descartadas.
O contrato não fornece saldo bancário acumulado nem um líquido combinado
de previsto e realizado.

## Apresentação e regras

- Valores monetários em BRL e datas civis em formato brasileiro, sem converter
  datas de emissão ou vencimento para UTC no envio.
- Cadastro solicita tipo, descrição, categoria, emissão, vencimento e valor;
  cliente, fornecedor, centro de custo e observações são opcionais conforme a API.
- O formulário lista os cadastros pelo nome e envia os respectivos IDs. Mudar
  o tipo limpa a categoria. Clientes e fornecedores inativos só aparecem no
  formulário se já estiverem vinculados ao lançamento em edição.
- O formulário aguarda as opções antes de permitir envio. Falhas de consulta
  permitem tentar novamente sem descartar os campos já digitados.
- Valor digitado com vírgula ou ponto decimal, até duas casas e sem separador de
  milhar; respeita o limite decimal(15,2). IDs devem ser inteiros positivos seguros.
- Tipo não é editável após o cadastro. Não são enviados empresa, status, baixas,
  principal pago, saldo ou campos desconhecidos nos corpos das mutações.
- Valores pagos/recebidos e saldo não constam na resposta da listagem. Cada linha
  oferece Consultar valores e os detalhes consultam automaticamente `/saldo`.
  Não são disparadas dezenas de consultas de saldo ao abrir a lista.
- Principal baixado e saldo vêm exclusivamente do servidor; não se inferem valores
  pelo status nem se recalculam baixas, juros, descontos ou estornos no frontend.
  Cancelados não são apresentados como obrigações pendentes. Não há indicadores
  financeiros globais fabricados a partir da página atual, apenas contagem oficial.
- Edição e cancelamento dependem de ABERTO e ausência de qualquer baixa (inclusive
  estornada). Como o detalhe não informa a existência histórica de baixas, o
  frontend permite solicitar a operação em ABERTO e trata o 409 do servidor.
  A API verifica novamente as condições antes de gravar. Não há exclusão nem
  controles de baixa ou estorno nesta entrega.
- Carregamento, consulta vazia, não encontrado, validação, conflito e falha de
  comunicação têm estados distintos. Falhas não são convertidas em valores zero.
  Um saldo indisponível não impede a leitura dos demais campos do lançamento.
- Formulários preservam os campos em erros de gravação. Falhas de rede podem
  ocorrer depois de o servidor gravar: não há repetição automática; a interface
  orienta consultar a listagem antes de reenviar para evitar duplicidade.
  Botões de gravação têm bloqueio imediato contra envio simultâneo.

## Autenticação e isolamento

O transporte HTTP e `authorizedRequest` foram estendidos para POST e PUT,
reutilizando o JWT somente em memória. Nenhum token é exposto nos componentes,
persistido ou registrado. Requisições anteriores são canceladas na troca de
consulta; respostas tardias não substituem os resultados atuais. Um 401 de
consulta ou operação protegida encerra a sessão e solicita novo login. Um 401
no endpoint de login indica credenciais recusadas. Um 403 da operação mantém
a sessão e informa a falta de permissão na tela atual. Um 403 na revalidação de
`/usuarios/me` encerra a sessão e encaminha à página de acesso negado.

CONSULTA tem somente leitura; cadastro e edição também são protegidos no acesso
direto às rotas. ADMIN/FINANCEIRO veem os controles, mas o backend é quem autoriza.
Não existe seleção de empresa; `empresaId` é ignorado em URLs de consulta da tela
e nunca enviado ao backend. Respostas de outra empresa são rejeitadas, inclusive
nas mutações. Campos de validação do servidor são mapeados por uma lista conhecida;
textos arbitrários, stacks e detalhes internos não são exibidos.

## Desenvolvimento e CORS

O CORS atual do backend permite GET/POST na origem local, mas não PUT.
Para viabilizar edição sem alterar o backend, o Vite encaminha somente os caminhos
`/lancamentos` para `VITE_API_BASE_URL` (padrão `http://localhost:5216`). No modo
`dev`, o transporte dessas telas usa a URL relativa e o navegador acessa a mesma
origem. Login e dashboard continuam usando suas URLs anteriores.

Em Development, a consulta de cadastros usa o GET permitido pelo CORS local.
O proxy local é disponibilizado por `npm.cmd run dev`. Em produção/preview, configure uma origem
compatível para a API e, para PUT, um proxy reverso de mesma origem ou uma futura
configuração CORS no backend, mediante autorização própria. Esta entrega não
modifica CORS ou autorização do servidor nem testa gravações no banco original.

### Frontend Testing

Com a API Testing já validada em `http://127.0.0.1:5217`, execute na pasta
`frontend`:

```powershell
npm.cmd run dev -- --mode testing
curl.exe --noproxy "*" --fail http://localhost:5174/teste-conexao
```

Abra `http://localhost:5174`. O Vite usa essa porta com `strictPort` e encaminha
`/auth/usuarios/login`, `/usuarios/me`, `/cadastros-financeiros`, `/lancamentos` e seus subcaminhos,
`/fluxo-caixa` e seus subcaminhos, e `/teste-conexao` para a API Testing.
O navegador usa a mesma origem em todas as requisições da aplicação.

O destino Testing está definido no código. A variável `VITE_API_BASE_URL` é
opcional nesse modo, e qualquer valor diferente de `http://127.0.0.1:5217`
interrompe a inicialização. Se a API Testing estiver indisponível, o proxy
retorna erro. O modo `testing` exige o servidor dev do Vite.
O GET acima confirma o caminho até o MySQL Testing sem autenticação. Login e
gravações dependem de fixtures autorizadas e de uma etapa própria.

## Executar e testar

Na pasta `frontend`, com as dependências existentes já instaladas:

```powershell
npm.cmd run dev
npm.cmd run typecheck
npm.cmd run build
npm.cmd test -- --maxWorkers=1
```

A última execução registrada aprovou 123 testes em quatro arquivos, com
respostas de `fetch` simuladas para autenticação, Dashboard, lançamentos e
Fluxo de Caixa. Os casos de escrita simulam as respostas da API. A suíte não
depende de MySQL. Não há script de lint.

Em Testing, o ciclo real de cadastro, edição e cancelamento com auditoria foi
validado no Edge antes da introdução dos seletores. A consulta aos catálogos
foi validada por HTTP, e o envio dos IDs escolhidos foi coberto por mocks.
O resultado completo e o parcial do Fluxo de Caixa foram validados no Edge
com respostas reais da API. A simulação de falha e a retenção de resposta
para testar a troca de filtros ocorreram no navegador.

## Limitações e pendências

1. Os seletores atendem o formulário de cadastro e edição. Os filtros avançados
   da listagem continuam operando por ID e os seletores usam a busca nativa do navegador.
2. Cadastro/edição/cancelamento são autorizados pelo backend somente nos ambientes
   habilitados (Development/Testing atualmente). Não existe endpoint de capacidades
   por ambiente. Uma recusa 403 da operação é informada na tela atual e mantém
   a sessão, conforme descrito em Autenticação e isolamento.
3. Lista e saldo são leituras separadas. Operações simultâneas podem alterar o
   estado entre consultas; Atualizar lista/detalhes consulta novamente. Não há
   edição com versão/ETag porque o contrato não fornece esse controle.
4. As gravações reais feitas a partir desta interface foram validadas em
   Testing com dados fictícios. Novas validações de escrita exigem ambiente
   Testing provisionado e autorização própria.

## Histórico da validação inicial das telas operacionais

- `npm.cmd run typecheck`: aprovado, sem erros TypeScript.
- `npm.cmd run build`: aprovado (TypeScript + Vite), sem dependências adicionais.
- `npm.cmd test -- --maxWorkers=1 --reporter=dot`: 100 testes aprovados em três
  arquivos; 45 novos testes de lançamentos e 55 testes anteriores preservados.
  Nenhuma reprovação na execução final. Os novos testes cobrem ainda envio
  simultâneo, resposta tardia, conflito de edição, saldo indisponível e preservação
  do formulário ao revalidar a sessão por foco da janela.
- Proxy Vite verificado com servidor HTTP simulado local e destino confirmado
  antes de enviar qualquer requisição: GET, POST e PUT preservaram caminho,
  autorização simulada e corpo. Não houve chamada ao backend original.
- `git diff --check`: sem erros. Todas as alterações estão em `frontend/`.
- Nenhuma operação no banco original, mudança no backend ou nas credenciais,
  instalação de dependências, commit ou push foi realizada.
