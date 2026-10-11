const byId = id => document.getElementById(id);
const put = (id, text) => { byId(id).textContent = text || 'Não informado'; };
const date = value => value ? new Date(value).toLocaleString('pt-BR', { timeZone: 'America/Sao_Paulo', dateStyle: 'short', timeStyle: 'short' }) : 'Não informado';
let movements = [], expanded = false;
function renderMovements() {
    byId('movimentos').replaceChildren();
    for (const movement of expanded ? movements : movements.slice(0, 10)) {
        const item = document.createElement('li');
        const time = document.createElement('time');
        const text = document.createElement('p');
        time.textContent = date(movement.data);
        text.textContent = movement.descricao;
        item.append(time, text);
        byId('movimentos').append(item);
    }
    byId('expand').hidden = movements.length <= 10;
    put('expand', expanded ? 'Mostrar menos' : 'Ver todas as movimentações');
}
byId('expand').addEventListener('click', () => { expanded = !expanded; renderMovements(); });
byId('print').addEventListener('click', () => window.print());
byId('consulta').addEventListener('submit', async event => {
    event.preventDefault();
    if (byId('buscar').disabled) return;
    byId('buscar').disabled = true;
    byId('resultado').hidden = true;
    byId('error').hidden = true;
    put('button-label', 'Consultando o DataJud…');
    put('status', 'O tribunal pode levar alguns segundos para responder.');
    const apiKey = byId('api-key').value.trim() || null;
    byId('api-key').value = '';
    try {
        const response = await fetch('/api/processos/consulta', {
            method: 'POST', headers: { 'Content-Type': 'application/json' }, cache: 'no-store',
            body: JSON.stringify({ numeroCNJ: byId('numero').value.trim(), apiKey })
        });
        const data = await response.json();
        if (!response.ok) throw new Error(data.detail || data.message || 'Não foi possível consultar o processo.');
        put('titulo', data.numeroCNJ.replace(/^(\d{7})(\d{2})(\d{4})(\d)(\d{2})(\d{4})$/, '$1-$2.$3.$4.$5.$6'));
        put('classe', data.classe); put('orgao', data.orgaoJulgador); put('assunto', data.assunto);
        put('ultima', data.ultimaMovimentacao?.descricao || 'Sem movimentações disponíveis');
        byId('ultima-data').textContent = data.ultimaMovimentacao ? date(data.ultimaMovimentacao.data) + ' · horário de Brasília' : '';
        movements = data.movimentacoes || []; expanded = false;
        put('count', movements.length + ' registros'); renderMovements();
        put('atualizacao', 'Consultado no DataJud em ' + date(data.ultimaAtualizacao) + ' · horário de Brasília. Dados públicos podem ser reutilizados do cache temporário.');
        put('disclaimer', data.disclaimer);
        byId('resultado').hidden = false;
        put('status', 'Consulta concluída.');
    } catch (error) {
        byId('status').textContent = '';
        put('error-text', error.message || 'Falha de conexão. Tente novamente.');
        byId('error').hidden = false;
    } finally {
        byId('buscar').disabled = false;
        put('button-label', 'Consultar processo');
    }
});
