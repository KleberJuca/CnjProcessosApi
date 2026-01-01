# CnjProcessosApi

API RESTful em .NET 8 para escritório de advocacia, integrando com a API Pública do DataJud (CNJ) para metadados de processos. Arquitetura Clean com projetos Core, Infrastructure e Web.

## Tecnologias
- ASP.NET Core 8 (Controllers, Swagger, Health Checks)
- Entity Framework Core + SQLite (cache local)
- HttpClient + Polly (retry/circuit breaker)
- JWT Bearer Authentication
- Quartz.NET (jobs em background)
- Serilog (console + arquivo)

## Endpoints principais
- POST `api/auth/login` ? Autentica e retorna JWT (exemplo padrão admin/admin).
- GET `api/processos` ? Lista paginada de processos do escritório com última movimentação.
- GET `api/processos/{numeroCNJ}` ? Detalhe do processo + última movimentação.
- POST `api/processos/sync` ? Força sincronização manual com CNJ.
- GET `health` ? Health checks.

Todos os endpoints em `api/processos` exigem JWT.

## Requisitos da API DataJud (CNJ)
- Base URL: https://api-publica.datajud.cnj.jus.br/
- Cada tribunal possui endpoint `_search` específico (ex.: `api_publica_tjsp/_search`).
- Autenticação: Header `Authorization: APIKey {SUA_CHAVE}`.
- Consulta: `POST` com corpo no formato de query Elasticsearch JSON.
- Retorno: Metadados de processos públicos (classe, assunto, órgão, movimentações).
- Limitações: Processos sigilosos retornam 403/404 ou não aparecem.

## LGPD e Sigilo
- Armazenamento somente de metadados públicos.
- Não armazenar dados sensíveis (CPF/CNPJ completos, nomes de partes vulneráveis).
- Logs apenas com metadados anonimizados.
- Sigilo: se CNJ retornar 403/404, marcar `IsSigiloso = true` e responder "Processo com acesso restrito".

## Configuração
Edite `CnjProcessosApi/appsettings.json` e `appsettings.Development.json`:
- `ConnectionStrings:DefaultConnection`: caminho do SQLite.
- `DataJud:BaseUrl`: `https://api-publica.datajud.cnj.jus.br/`
- `DataJud:ApiKey`: sua chave pública CNJ.
- `DataJud:TribunalEndpoint`: ex. `api_publica_tjsp/_search`.
- `Jwt:Issuer/Audience/Secret`: configurar segredo forte (>=32 chars).
- `Quartz:SyncIntervalCron`: cron do job (default: a cada 4h).
- `Processos:NumerosCNJ`: lista dos números CNJ do escritório.

### Obtendo a APIKey CNJ
- Cadastre-se no portal do CNJ/DataJud para obter a chave pública.
- Configure a chave no `appsettings`.

## Desenvolvimento
### Pré-requisitos
- .NET 8 SDK
- CLI do EF Core: `dotnet tool install --global dotnet-ef` (opcional)

### Restaurar e executar
- `dotnet restore`
- `dotnet run --project CnjProcessosApi`
- Swagger: `https://localhost:{porta}/swagger`

### Autenticação de teste
- POST `api/auth/login` com corpo:
```
{ "username": "admin", "password": "admin" }
```
- Use o token Bearer nos endpoints protegidos.

## Banco de dados e migrações
- Adicionar migração inicial:
```
dotnet ef migrations add InitialCreate -p CnjProcessosApi.Infrastructure -s CnjProcessosApi
```
- Atualizar DB:
```
dotnet ef database update -p CnjProcessosApi.Infrastructure -s CnjProcessosApi
```

## Job de sincronização (Quartz)
- Por padrão executa a cada 4 horas.
- Ajuste o cron em `Quartz:SyncIntervalCron`.
- Sincronização manual: `POST /api/processos/sync` (JWT obrigatório).

## Logging
- Serilog configurado para Console e arquivo em `Logs/`.
- Ajuste níveis em `Serilog.MinimumLevel` e sinks no `appsettings`.

## Exemplo de query Elasticsearch (DataJud)
- Endpoint: `POST https://api-publica.datajud.cnj.jus.br/api_publica_tjsp/_search`
- Header: `Authorization: APIKey SUA_CHAVE`
- Body:
```
{
  "query": {
    "term": {
      "numeroProcesso.keyword": "0000000-00.0000.0.00.0000"
    }
  },
  "size": 10000
}
```

## Observações finais
- Adeque o mapeamento do JSON retornado pelo DataJud no client para preencher `Classe`, `Assunto`, `OrgaoJulgador` e `Movimentacoes` conforme a documentação oficial.
- Respeite a Portaria CNJ 160/2020 e a LGPD no tratamento dos dados.
