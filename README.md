# Sistema de Gestão Financeira Empresarial

API REST desenvolvida em C# com ASP.NET Core (.NET 10) e MySQL, voltada ao gerenciamento financeiro empresarial.

O projeto tem como objetivo construir uma solução de gestão financeira com suporte a múltiplas empresas, organização de informações financeiras e controle de acesso por usuários.

## Tecnologias utilizadas

- C#
- .NET 10
- ASP.NET Core Web API
- MySQL
- Entity Framework Core
- Git e GitHub
- Visual Studio Code

## Objetivo do projeto

Desenvolver um sistema de gestão financeira empresarial que permita:

- Gerenciar empresas.
- Cadastrar clientes e fornecedores.
- Controlar contas bancárias.
- Registrar receitas e despesas.
- Acompanhar lançamentos financeiros.
- Consultar informações de fluxo de caixa.
- Administrar usuários e permissões.
- Trabalhar com múltiplas empresas.

As funcionalidades serão implementadas progressivamente.

## Funcionalidades implementadas

### Módulo Empresas

Primeira etapa de desenvolvimento da API REST.

Operações desenvolvidas:

- GET: consulta de empresas.
- POST: cadastro de empresas.
- PUT: atualização de empresas.

Integração com banco de dados MySQL utilizando Entity Framework Core.

## Arquitetura atual

O projeto utiliza uma estrutura inicial organizada em:

- Program.cs: configuração e endpoints da API.
- Models/: entidades e modelos de requisição.
- Models/Data/: contexto de acesso ao banco de dados.
- GestaoFinanceira.csproj: configuração do projeto .NET.
- GestaoFinanceira.http: arquivo para requisições HTTP.

A arquitetura será aprimorada conforme a evolução do desenvolvimento.

## Banco de dados

O projeto utiliza MySQL como banco de dados relacional.

A estrutura existente contempla entidades relacionadas a:

- Empresas
- Usuários
- Clientes
- Fornecedores
- Categorias
- Centros de custo
- Contas bancárias
- Formas de pagamento
- Lançamentos financeiros
- Baixas
- Transferências
- Auditoria

Também estão previstas consultas relacionadas ao fluxo de caixa e aos saldos financeiros.

## Como executar o projeto

### Pré-requisitos

- .NET SDK 10
- MySQL
- Banco de dados configurado
- Visual Studio Code ou IDE compatível

### 1. Clonar o repositório

```bash
git clone https://github.com/carlosbuenodev-coder/gestaofinanceiraempresarial.git
```

### 2. Acessar a pasta

```bash
cd gestaofinanceiraempresarial
```

### 3. Configurar a conexão com o banco

Configure uma connection string chamada `GestaoFinanceira` utilizando os mecanismos de configuração do ASP.NET Core.

Por segurança, credenciais e configurações locais não são publicadas no repositório.

É necessário possuir uma instância MySQL com a estrutura de banco compatível com o projeto.

### 4. Restaurar dependências

```bash
dotnet restore
```

### 5. Executar a aplicação

```bash
dotnet run
```

A API estará disponível no endereço indicado pelo terminal.

## Próximas etapas

- Evoluir o módulo Empresas.
- Implementar autenticação e autorização.
- Criar controle de acesso por empresa.
- Implementar isolamento de dados entre empresas.
- Desenvolver módulos de clientes e fornecedores.
- Implementar lançamentos financeiros.
- Desenvolver consultas de fluxo de caixa.
- Criar testes automatizados.
- Aprimorar a arquitetura da aplicação.
- Preparar a aplicação para futura implantação.

## Estrutura multiempresa planejada

O sistema terá suporte inicial a cinco empresas, com usuários e permissões individualizados.

Também está previsto um administrador geral com acesso à administração das empresas.

O isolamento de dados e as regras de autorização serão implementados em etapas futuras.

## Status do projeto

**Em desenvolvimento.**

Etapa atual: desenvolvimento da API REST, integração com MySQL e implementação inicial do módulo Empresas.

O projeto faz parte de um plano de evolução profissional em desenvolvimento back-end, com foco em C#, .NET, APIs REST e bancos de dados relacionais.

## Autor

Carlos Bueno

GitHub: [carlosbuenodev-coder](https://github.com/carlosbuenodev-coder)