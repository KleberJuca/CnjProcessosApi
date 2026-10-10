# Uso dos dados do DataJud

Leia o [Termo de Uso vigente na documentação oficial do CNJ](https://datajud-wiki.cnj.jus.br/api-publica/termo-uso/). Este arquivo é uma orientação do projeto e não substitui o documento oficial.

A documentação do CNJ estabelece uso legal, autorizado e não comercial, atribui responsabilidade ao consumidor e informa que não há garantia de precisão ou atualização dos dados. Também prevê comunicar ao CNJ informações derivadas que sejam disponibilizadas ao público.

O código deste projeto é distribuído sob licença MIT. A licença do código não altera as condições aplicáveis à API e aos dados do CNJ.

## Comportamento da aplicação

- Coleta apenas os campos utilizados na consulta: número, classe, assuntos, órgão e nomes/datas de movimentos.
- Não extrai nomes das partes nem documentos processuais.
- Suprime resultados com nível de sigilo diferente de zero ou sem nível explicitado.
- Remove o cache de um processo quando uma nova consulta indica restrição ou ausência.
- Mascara padrões numéricos de CPF/CNPJ nas descrições, sem alegar anonimização completa.
- Limita requisições recebidas e tentativas enviadas ao DataJud por instância.
- Informa que os dados podem estar desatualizados e devem ser conferidos na origem.

O cache pode refletir a situação de acesso observada anteriormente até a próxima consulta. Defina a duração conforme o seu contexto. A conformidade da implantação depende também de configuração, retenção, logs e operação; não é garantida apenas pelo código.
