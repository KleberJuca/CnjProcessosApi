# CnjProcessosApi

API RESTful pública e gratuita em .NET 8 para consulta de metadados de processos judiciais públicos via API Pública do DataJud (CNJ). Arquitetura Clean com projetos Core, Infrastructure e Web.

> Aviso Legal Importante
>
> - O consumo da API Pública do DataJud implica aceitação tácita e integral do Termo de Uso do CNJ (cláusula 1.2).
> - Uso estritamente legal, não comercial e autorizado. Uso comercial é proibido (cláusulas 3.3 e 3.8).
> - O CNJ não garante precisão, integridade ou atualidade dos dados (cláusula 3.6). Dados fornecidos "as is".
> - O usuário é responsável por qualquer informação derivada (cláusula 3.2) e deve dar ciência ao CNJ sobre publicações ao público (cláusula 3.9).
> - Limite de 120 requisições/minuto por IP (cláusula 3.13). Esta API aplica rate limiting.
> - Sigilo: processos restritos retornam erro sem vazamento (cláusula 3.19).
> - Termo de Uso: https://datajud-wiki.cnj.jus.br/api-publica/termo-de-uso

## Tecnologias
- ASP.NET Core 8 (Controllers, Razor Pages, Swagger, Health Checks)
- Entity Framework Core + SQLite (cache local somente de metadados públicos)
- HttpClient + Polly (retry leve)
- Quartz.NET (job diário)
- Serilog (console + arquivo)
- Rate Limiting (AspNetCore.RateLimiting)

## Endpoints principais (públicos)
- GET `api/processos` – Lista paginada de processos com última movimentação e `Disclaimer`.
- GET `api/processos/{numeroCNJ}` – Detalhe do processo + última movimentação com `Disclaimer`.
- POST `api/processos/sync` – Solicita sincronização manual com CNJ (pode ser usado com critério; sujeito a rate limit).
- GET `health` – Health checks.
- Frontend: `Pages/Index.cshtml` – Consulta simples para cidadãos.

Todos os endpoints são públicos e sujeitos a rate limiting (120 req/min/IP).

## Requisitos da API DataJud (CNJ)
- Base URL: https://api-publica.datajud.cnj.jus.br/
- Endpoint por tribunal: `_search` (ex.: `api_publica_tjsp/_search`).
- Autenticação: Header `Authorization: APIKey {SUA_CHAVE}` (cada usuário deve usar sua própria chave).
- Consulta: `POST` com corpo de query Elasticsearch.
- Retorno: Metadados públicos (classe, assunto, órgão, movimentações).
- Sigilo: 403/404 sem vazamento.

## LGPD e Sigilo
- Armazenamento apenas de metadados públicos.
- Não armazenar dados sensíveis (CPF/CNPJ completos, nomes de partes vulneráveis). Descrições são anonimizadas (máscara de CPFs/CNPJs).
- Logs anonimizados.
- Sigilo: se CNJ retornar 403/404, não persistir detalhes; apenas informar acesso restrito.

## Configuração
Edite `CnjProcessosApi/appsettings.json` e `appsettings.Development.json`:
- `ConnectionStrings:DefaultConnection`: caminho do SQLite.
- `DataJud:BaseUrl`: `https://api-publica.datajud.cnj.jus.br/`
- `DataJud:ApiKey`: sua chave pública CNJ.
- `DataJud:TribunalEndpoint`: ex. `api_publica_tjsp/_search`.
- `RateLimiting:PermitLimitPerMinute`: default 120.
- `Quartz:SyncIntervalCron`: cron diário (default `0 0 0 * * ?`).
- `Legal:NonCommercialOnly` e `Legal:TermsOfUseUrl`.
- `Legal:Disclaimer`: mensagem padrão nas respostas.

### Obtendo a APIKey CNJ
- Cadastre-se no portal do CNJ/DataJud e obtenha sua chave pública.
- Configure a chave em `appsettings` ou via variável de ambiente.

## Desenvolvimento
### Pré-requisitos
- .NET 8 SDK
- CLI do EF Core: `dotnet tool install --global dotnet-ef` (opcional)

### Restaurar e executar
- `dotnet restore`
- `dotnet run --project CnjProcessosApi`
- Swagger: `https://localhost:{porta}/swagger`
- Frontend: `https://localhost:{porta}/`

## Exemplos para testar

### Configurar a APIKey e executar
- Windows PowerShell:
```
$env:DataJud__ApiKey="SUA_CHAVE_AQUI"
dotnet run --project CnjProcessosApi
```
- Linux/macOS:
```
DataJud__ApiKey="SUA_CHAVE_AQUI" dotnet run --project CnjProcessosApi
```

### Listar processos com filtro
```
curl "http://localhost:5000/api/processos?filtro=0000000-00.0000.0.00.0000&page=1&pageSize=10"
```

### Detalhe de um processo
```
curl "http://localhost:5000/api/processos/0000000-00.0000.0.00.0000"
```

### Sincronização manual (respeita rate limiting)
```
curl -X POST "http://localhost:5000/api/processos/sync"
```

### Health check
```
curl "http://localhost:5000/health"
```

### Testar rate limiting (exemplo simplificado)
Dispare mais de 120 requisições em 1 minuto do mesmo IP e observe resposta 429.
```
for i in {1..130}; do curl -s -o /dev/null -w "%{http_code}\n" "http://localhost:5000/health"; done
```

### Usar via navegador (Razor Pages e Swagger)
- Acesse `http://localhost:5000/` e use o formulário da página para consultar.
- Acesse `http://localhost:5000/swagger` e teste os endpoints pelo Swagger UI.

## Banco de dados e migrações
- Migração inicial:
```
dotnet ef migrations add InitialCreate -p CnjProcessosApi.Infrastructure -s CnjProcessosApi
```
- Atualizar DB:
```
dotnet ef database update -p CnjProcessosApi.Infrastructure -s CnjProcessosApi
```

## Job de sincronização (Quartz)
- Execução diária (cron default `0 0 0 * * ?`).
- Ajuste o cron em `Quartz:SyncIntervalCron`.
- Sincronização manual: `POST /api/processos/sync` (rate limited).

## Logging
- Serilog para Console e arquivo em `Logs/`.
- Ajuste níveis em `Serilog.MinimumLevel`.

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

## Conformidade e Termos
- Leia `TERMOs_DE_USO.md` para resumo das cláusulas chave (1.2, 3.2, 3.3, 3.6, 3.8, 3.9, 3.13, 3.19).
- Uso gratuito e não comercial. Informações "as is".
- Informe o CNJ ao publicar estudos ou relatórios: ver canais oficiais no portal DataJud (email ou formulário disponibilizado pelo CNJ).

## Licença
- MIT License. Este projeto é open-source e gratuito, sem exploração comercial dos dados do DataJud.

## Deploy com Docker (gratuito)

### Dockerfile (Linux)
Crie um arquivo `Dockerfile` na raiz:
```
# build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore
RUN dotnet publish CnjProcessosApi/CnjProcessosApi.csproj -c Release -o /app

# runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "CnjProcessosApi.dll"]
```

### Executar localmente
```
docker build -t cnjprocessosapi:latest .
docker run -p 8080:8080 -e DataJud__ApiKey=SUA_CHAVE cnjprocessosapi:latest
```
Acesse: `http://localhost:8080/` e `http://localhost:8080/swagger`

### Deploy gratuito (Azure App Service Free)
- Crie um Azure App Service (Free) com Container Linux.
- Envie a imagem para um registry (ex.: Docker Hub) ou use build do Azure.
- Configure no App Service:
  - `WEBSITES_PORT=8080`
  - `ASPNETCORE_URLS=http://+:8080`
  - `DataJud__ApiKey=SUA_CHAVE`
- Ative HTTP/2 e monitore logs.

Observações de conformidade: mantenha o rate limiting ativo e evite uso comercial; cada usuário deve usar sua própria APIKey do CNJ.
