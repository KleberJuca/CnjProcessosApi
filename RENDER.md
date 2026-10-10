# Publicação gratuita no Render

O arquivo `render.yaml` prepara a aplicação .NET 8 com formulário, chave opcional
do visitante e API em um serviço Docker com plano explicitamente `free`.
O Render atribui um endereço HTTPS terminado em `onrender.com` após a criação.
O nome e a disponibilidade do endereço só são confirmados na publicação.

1. Entre em uma conta gratuita em https://dashboard.render.com/.
2. Crie um Blueprint a partir deste repositório e da branch `master`.
3. Informe `DataJud__ApiKey` no painel usando a chave pública atual publicada
   em https://datajud-wiki.cnj.jus.br/api-publica/acesso/.
4. Confira que o serviço está no plano **Free**, sem banco ou disco pago.
5. Depois da publicação, verifique `/health` e faça uma consulta no formulário.

A chave fica na configuração da hospedagem e não no código. A chave opcional
do visitante continua disponível; ela não concede acesso a processos sigilosos.

## Limitações do plano gratuito

O serviço entra em repouso após 15 minutos sem tráfego e o primeiro acesso pode
demorar enquanto ele inicia. Existem cotas de utilização: consulte
https://render.com/docs/free antes de ativar. Não há garantia de disponibilidade
contínua no plano gratuito.

O SQLite em `/tmp/processos.db` serve somente como cache descartável. Ele pode
ser perdido em reinícios e novas publicações; as consultas seguintes recuperam
os dados do DataJud. Não existe disco persistente ou banco pago neste Blueprint.

Mantenha o endereço anterior até validar a nova publicação. Atualize os links
públicos apenas depois de confirmar o endereço que o Render atribuir.
