# CnjProcessosApi

API pública e gratuita em **.NET 8** para consultar metadados de processos públicos pelo número CNJ na API Pública do DataJud. Inclui formulário responsivo com resultados na página, Swagger, SQLite, Quartz, Polly e Serilog.

O uso dos dados deve observar o [Termo de Uso do CNJ](https://datajud-wiki.cnj.jus.br/api-publica/termo-uso/), incluindo a finalidade não comercial. O DataJud pode apresentar atraso ou ausência de informações; confirme os dados no tribunal de origem. Este projeto é independente do CNJ.

## Versão online

Portal: https://cnjprocessos-kleberjuca.onrender.com

O portal principal executa a aplicação ASP.NET Core 8 no plano gratuito do Render. Após 15 minutos sem visitas, o serviço pode entrar em repouso; o próximo acesso pode levar cerca de um minuto ou mais. O SQLite é um cache descartável, recriado após reinícios. Veja [a configuração da hospedagem](RENDER.md).

A pasta [online](online/README.md) preserva a adaptação alternativa Vinext/Cloudflare Workers para Sites; ela não é usada na publicação .NET do Render.

O portal permite consultas públicas sem cadastro e oferece um campo opcional para a chave da API pública do DataJud. O serviço permanece sujeito à disponibilidade e aos limites da hospedagem e do CNJ.

## Chave opcional do visitante

O formulário aceita uma APIKey do DataJud exclusivamente para a consulta atual. A chave é enviada no corpo de POST e repassada somente ao endereço oficial HTTPS do DataJud, no cabeçalho Authorization. O campo é limpo ao enviar. Ela não é salva em banco, armazenamento do navegador ou logs da aplicação.

No backend .NET, consultas com chave própria ignoram o cache compartilhado e não persistem resultados nele. Use `POST /api/processos/consulta` com `{"numeroCNJ":"0000832-35.2018.4.01.3202","apiKey":"CHAVE_OPCIONAL"}`. Sem apiKey, vale a chave configurada no serviço e o cache público habitual.

Não informe senha de tribunal, certificado digital ou credenciais do PJe/e-SAJ. Ser advogado ou informar outra chave não concede acesso a processos sigilosos; essa integração consulta somente metadados públicos.

## Executar localmente

Requisito: SDK .NET 8 ou superior com suporte ao destino .NET 8.

1. Clone o repositório e entre na pasta.
2. Copie a **chave pública vigente** da [página de acesso do DataJud](https://datajud-wiki.cnj.jus.br/api-publica/acesso/). A documentação do CNJ publica essa chave; não é necessário solicitar uma chave individual. Ela pode mudar.
3. Configure a variável e execute:

PowerShell:

```powershell
$env:DataJud__ApiKey = "COLE_A_CHAVE_PUBLICA_AQUI"
dotnet restore
dotnet run --project CnjProcessosApi --launch-profile http
```

Linux/macOS:

```bash
export DataJud__ApiKey="COLE_A_CHAVE_PUBLICA_AQUI"
dotnet restore
dotnet run --project CnjProcessosApi --launch-profile http
```

- Formulário: http://localhost:5038/
- Swagger: http://localhost:5038/swagger
- Saúde da aplicação: http://localhost:5038/health

O banco é criado por migrações na primeira execução. Sem chave, a aplicação inicia, mas consultas externas retornam 503 com instruções de configuração.

Exemplo de número utilizado na documentação oficial: `0000832-35.2018.4.01.3202`. Sua disponibilidade depende do DataJud.

## Como funciona

1. Valida formato e dígito verificador do CNJ e remove a pontuação.
2. Identifica o tribunal pelo próprio número: justiça estadual, federal, trabalhista, eleitoral, tribunais superiores mapeados e justiça militar estadual.
3. Consulta o DataJud quando não há cache ou quando ele expirou (60 minutos por padrão).
4. Lê classe, assuntos, órgão julgador e movimentos, ordenando a última movimentação pela data.
5. Armazena os metadados no SQLite. Uma nova consulta que encontre sigilo ou ausência remove os detalhes antigos.

Há limite de 100 registros de origem por consulta. Os movimentos retornados são combinados sem duplicatas; a classe e o órgão vêm do registro mais recentemente atualizado. A aplicação não fornece documentos, nomes das partes, CPF ou CNPJ, nem garante cobertura de todos os processos.

`UltimaAtualizacao` indica quando a aplicação consultou o DataJud, e não quando o tribunal atualizou o processo. As datas das movimentações são as informadas pela fonte. O cache não confirma, em tempo real, a situação de acesso na origem.

## Endpoints

| Método | Caminho | Comportamento |
| --- | --- | --- |
| GET | `/api/processos/{numeroCNJ}` | Consulta por número com ou sem pontuação; utiliza cache recente |
| GET | `/api/processos?page=1&pageSize=20&filtro=0000832` | Lista somente o cache local, com filtro parcial |
| POST | `/api/processos/sync` | Atualiza a lista configurada; desabilitado por padrão |
| GET | `/health` | Verifica se a aplicação responde; não consulta o CNJ |
| GET | `/swagger` | Documentação interativa |

Consultas são públicas, sem JWT. `pageSize` aceita de 1 a 100 e `page`, de 1 a 1.000.000.

- **400**: número, tribunal ou paginação inválidos.
- **404**: processo ausente ou sem metadados públicos disponíveis; não afirma que o processo é sigiloso.
- **429**: limite local por IP excedido.
- **502**: falha HTTP ou resposta inválida do DataJud.
- **503**: chave ausente/recusada ou limite externo atingido.
- **504**: tempo de resposta externo excedido.

Erros não viram processos vazios nem respostas de sincronização bem-sucedida.

## Configurações

Use variáveis de ambiente com dois sublinhados para separar seções:

| Variável | Padrão / finalidade |
| --- | --- |
| `DataJud__ApiKey` | Chave pública vigente, sem o prefixo APIKey |
| `DataJud__CacheMinutes` | 60; 0 força consulta à origem |
| `ConnectionStrings__DefaultConnection` | SQLite; desenvolvimento usa processos.dev.db |
| `RateLimiting__PermitLimitPerMinute` | 120, limitado ao intervalo 1–120 |
| `Swagger__Enabled` | true; defina false para ocultar fora de Development |
| `HttpsRedirection__Enabled` | false; ative somente com HTTPS configurado |
| `Quartz__SyncIntervalCron` | Diário; perfil Development usa a cada 4 horas |
| `Processos__NumerosCNJ__0` | Primeiro processo para sincronização programada |
| `Processos__EnableManualSync` | false; habilite apenas se desejar expor a atualização manual |

O tribunal é inferido pelo número, inclusive em lotes com tribunais diferentes. Não é necessário configurar `DataJud:TribunalEndpoint`.

O limitador de entrada aceita até 120 requisições/minuto por IP e retorna 429. As tentativas de saída, inclusive retries e jobs, compartilham um limitador de aproximadamente 109 requisições/minuto por instância. Se houver várias instâncias ou outros consumidores no mesmo IP, será necessário coordenar o limite externamente.

Atrás de proxy, o endereço observado pode ser o do proxy; configure encaminhamento confiável antes de usar limites individuais nesse cenário. A aplicação não confia automaticamente em cabeçalhos enviados pelo cliente.

## Docker

```powershell
Copy-Item .env.example .env
# Edite .env e preencha DATAJUD_API_KEY.
docker compose up --build -d
```

Em Linux/macOS, use `cp .env.example .env`.

Acesse http://localhost:8080/ ou http://localhost:8080/swagger. O volume `processos` conserva o SQLite. O servidor respeita `ASPNETCORE_URLS` e não exige certificado HTTPS para iniciar em contêiner.

O Dockerfile permanece baseado em .NET 8. Configure TLS no ambiente de hospedagem quando publicar na internet.

## Testes

```text
dotnet test CnjProcessosApi.sln
dotnet publish CnjProcessosApi/CnjProcessosApi.csproj -c Release
```

Os testes usam respostas simuladas no formato do DataJud e bancos SQLite temporários, sem depender do CNJ. Cobrem validação de número, extração dos metadados, erros externos, sigilo, cancelamento, persistência, página, Swagger e rate limiting.

## Estrutura

- **Core**: modelos, validação do CNJ, interfaces e serviço de consulta.
- **Infrastructure**: cliente DataJud, limitação de saída, persistência e agendamento.
- **CnjProcessosApi**: endpoints, formulário e configuração da aplicação.
- **Tests**: testes de unidade e integração.

Arquivos de dados, logs e `.env` não devem ser versionados.

## Dados e licença

O código possui [licença MIT](LICENSE). Essa licença não substitui os termos aplicáveis ao DataJud e seus dados. Consulte [TERMOs_DE_USO.md](TERMOs_DE_USO.md).

O projeto limita os campos coletados e mascara padrões de CPF/CNPJ nas descrições. Isso não representa anonimização completa nem certificação de conformidade com a LGPD. A implantação deve considerar retenção, logs e controles adequados ao uso pretendido.

Referências oficiais:
- [Acesso e chave pública](https://datajud-wiki.cnj.jus.br/api-publica/acesso/)
- [Consulta por número](https://datajud-wiki.cnj.jus.br/api-publica/exemplos/exemplo1/)
- [Glossário](https://datajud-wiki.cnj.jus.br/api-publica/glossario/)
- [Endpoints dos tribunais](https://datajud-wiki.cnj.jus.br/api-publica/endpoints/)
