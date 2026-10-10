# CnjProcessos — versão online

Portal público de consulta ao DataJud, adaptado para hospedagem Sites (Vinext/Cloudflare Workers). O projeto original ASP.NET Core 8 é mantido em https://github.com/KleberJuca/CnjProcessosApi.

## Funcionalidade

- POST /api/consulta recebe numeroCNJ e apiKey opcional no corpo JSON.
- Valida o número CNJ e consulta exclusivamente o endereço HTTPS oficial do DataJud.
- Apenas nível de sigilo zero é aceito. Falhas externas não viram resultados fictícios.
- A chave do visitante nunca é salva nem utilizada em cache compartilhado.
- Respostas usam Cache-Control: no-store. Não há histórico persistente de consultas.
- D1 guarda somente um relógio de limitação global, sem números de processos ou chaves.
- O limite distribuído de saída permite uma consulta a cada 550 ms (menos de 120/minuto); concorrência adicional recebe 429.
- Credenciais de advogado, PJe, e-SAJ, certificados e acesso privado não são suportados.

## Configuração

DATAJUD_API_KEY deve ser definida como variável secreta na hospedagem com a chave pública vigente disponibilizada pelo CNJ. Não inclua o valor no código.

## Desenvolvimento

Instale as dependências com npm run install:ci. Gere migrações com npm run db:generate. Execute npm run dev. Para publicar use o fluxo Sites e .openai/hosting.json. As migrações D1 são aplicadas pela hospedagem.

Testes: node --experimental-strip-types --test tests/*.test.mjs

## Dados e licença

Código MIT. O uso dos dados deve respeitar o termo do DataJud: https://datajud-wiki.cnj.jus.br/api-publica/termo-uso/

A hospedagem pode manter logs técnicos de acesso. A aplicação não registra corpos das requisições nem conteúdo da resposta externa. O DataJud pode apresentar atraso; confira informações no tribunal de origem.
