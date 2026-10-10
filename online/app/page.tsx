"use client";
import { useRef, useState } from "react";
type Movement = { data:string; descricao:string };
type Result = {numeroCNJ:string;tribunal:string|null;classe:string|null;assunto:string;orgaoJulgador:string|null;ultimaAtualizacaoFonte:string|null;consultadoEm:string;ultimaMovimentacao:Movement|null;movimentacoes:Movement[];disclaimer:string};
const formatNumber = (n:string) => n.replace(/^(\d{7})(\d{2})(\d{4})(\d)(\d{2})(\d{4})$/,"$1-$2.$3.$4.$5.$6");
const date = (value:string|null) => value ? new Date(value).toLocaleString("pt-BR",{timeZone:"America/Sao_Paulo",dateStyle:"short",timeStyle:"short"}) : "Não informado";
export default function Home() {
  const [number,setNumber] = useState("");
  const [result,setResult] = useState<Result|null>(null);
  const [busy,setBusy] = useState(false);
  const [error,setError] = useState("");
  const [expanded,setExpanded] = useState(false);
  const keyRef = useRef<HTMLInputElement>(null);
  async function submit(event:React.FormEvent) {
    event.preventDefault();
    if (busy) return;
    setBusy(true); setError(""); setResult(null); setExpanded(false);
    const key = keyRef.current?.value.trim() || null;
    if (keyRef.current) keyRef.current.value = "";
    try {
      const response = await fetch("/api/consulta",{
        method:"POST",headers:{"Content-Type":"application/json"},cache:"no-store",
        body:JSON.stringify({numeroCNJ:number.trim(),apiKey:key})
      });
      const data = await response.json() as Result & {message?:string};
      if (!response.ok) throw new Error(data.message || "Não foi possível consultar o processo.");
      setResult(data);
    } catch (e) { setError(e instanceof Error ? e.message : "Falha de conexão. Tente novamente."); }
    finally { setBusy(false); }
  }
  return <>
    <header className="topbar"><a className="brand" href="/" aria-label="CnjProcessos início"><span className="brand-icon" aria-hidden="true">§</span>Cnj<span>Processos</span></a><nav><a href="#como-funciona">Como funciona</a><a href="https://github.com/KleberJuca/CnjProcessosApi" target="_blank" rel="noreferrer">Código aberto ↗</a></nav></header>
    <main>
      <section className="hero">
        <div className="eyebrow"><span/> INFORMAÇÃO JUDICIAL, ACESSÍVEL</div>
        <h1>Seu processo,<br/><em>mais fácil de consultar.</em></h1>
        <p className="intro">Metadados e movimentações públicas em um só lugar.<br className="desktop"/> Para cidadãos, estudantes e profissionais do Direito.</p>
        <div className="trust"><span>Sem cadastro</span><i/> <span>Gratuito</span><i/> <span>Dados do DataJud / CNJ</span></div>
      </section>
      <div className="workspace">
        <section className="query-card">
          <div className="card-heading"><span className="step">01</span><div><h2>Consultar processo</h2><p>O tribunal é identificado pelo número informado.</p></div></div>
          <form onSubmit={submit}>
            <label htmlFor="numero">Número CNJ</label>
            <input id="numero" name="numeroCNJ" value={number} onChange={e=>setNumber(e.target.value)} placeholder="0000000-00.0000.0.00.0000" maxLength={25} required autoComplete="off" aria-describedby="number-help"/>
            <p id="number-help" className="helper">20 dígitos, com ou sem pontuação.</p>
            <details className="key-options"><summary>Tenho uma chave do DataJud <span>Opcional</span></summary><div>
              <label htmlFor="api-key">Chave da API pública do DataJud</label>
              <input ref={keyRef} id="api-key" type="password" maxLength={2048} autoComplete="off" spellCheck={false} placeholder="Cole sua APIKey" aria-describedby="key-help"/>
              <p id="key-help">A chave será usada só nesta consulta e o campo será limpo ao enviar. Não salvamos a chave no banco ou no navegador. Não informe senha de tribunal, certificado digital ou credenciais do PJe/e-SAJ.</p>
              <p>Ser advogado ou informar outra chave não libera processos sigilosos. Este serviço consulta somente a API pública do DataJud.</p>
              <a href="https://datajud-wiki.cnj.jus.br/api-publica/acesso/" target="_blank" rel="noreferrer">Consultar a chave pública na Wiki do CNJ ↗</a>
            </div></details>
            <button className="primary" type="submit" disabled={busy}>{busy ? <><span className="spinner"/> Consultando o DataJud…</> : <>Consultar processo <span aria-hidden="true">→</span></>}</button>
            <p className="privacy-note">A consulta é enviada ao DataJud. O serviço não guarda seu histórico de buscas.</p>
          </form>
          {busy && <p className="status" role="status">O tribunal pode levar alguns segundos para responder.</p>}
          {error && <div className="error" role="alert"><strong>Não foi possível concluir</strong><p>{error}</p></div>}
        </section>
        <aside className="guide" id="como-funciona"><p className="small-label">ANTES DE CONSULTAR</p><h2>Informação pública.<br/>Sem complicação.</h2>
          <ol><li><span>1</span><div><strong>Informe o número</strong><p>Use a numeração única CNJ do processo que deseja consultar.</p></div></li><li><span>2</span><div><strong>Consulte a fonte</strong><p>A busca é feita na base pública do DataJud, no tribunal correspondente.</p></div></li><li><span>3</span><div><strong>Veja os movimentos</strong><p>Confira a última movimentação e os metadados disponíveis.</p></div></li></ol>
          <div className="notice"><strong>Uma consulta não substitui o tribunal.</strong><p>O DataJud pode apresentar atraso ou não incluir um processo. Confira prazos e documentos no sistema de origem.</p></div>
        </aside>
      </div>
      {result && <section className="result" aria-label="Resultado da consulta">
        <div className="result-top"><div><p className="small-label"><span className="success-dot"/> CONSULTA CONCLUÍDA {result.tribunal ? "· " + result.tribunal : ""}</p><h2>{formatNumber(result.numeroCNJ)}</h2></div><button className="secondary" onClick={()=>window.print()}>Imprimir resultado</button></div>
        <dl className="metadata"><div><dt>CLASSE PROCESSUAL</dt><dd>{result.classe || "Não informada"}</dd></div><div><dt>ÓRGÃO JULGADOR</dt><dd>{result.orgaoJulgador || "Não informado"}</dd></div><div className="full"><dt>ASSUNTO</dt><dd>{result.assunto || "Não informado"}</dd></div></dl>
        <div className="latest"><span className="small-label">ÚLTIMA MOVIMENTAÇÃO DISPONÍVEL</span><h3>{result.ultimaMovimentacao?.descricao || "Sem movimentações disponíveis"}</h3>{result.ultimaMovimentacao && <time>{date(result.ultimaMovimentacao.data)} · horário de Brasília</time>}</div>
        <div className="history-heading"><h3>Histórico de movimentações</h3><span>{result.movimentacoes.length} registros</span></div>
        <ol className="timeline">{(expanded ? result.movimentacoes : result.movimentacoes.slice(0,10)).map((m,i)=><li key={m.data+i}><time>{date(m.data)}</time><p>{m.descricao}</p></li>)}</ol>
        {result.movimentacoes.length > 10 && <button className="secondary" onClick={()=>setExpanded(!expanded)}>{expanded ? "Mostrar menos" : "Ver todas as movimentações"}</button>}
        <div className="result-foot"><p>Consultado em {date(result.consultadoEm)} · Atualização informada pela fonte: {date(result.ultimaAtualizacaoFonte)}. Horário de Brasília.</p><p>{result.disclaimer}</p></div>
      </section>}
      <section className="faq"><h2>Perguntas frequentes</h2><details><summary>Preciso ser advogado para consultar?</summary><p>Não. A consulta de metadados públicos está disponível a qualquer pessoa, sem cadastro. Advogados usam a mesma base pública neste serviço.</p></details><details><summary>Por que um processo pode não aparecer?</summary><p>Ele pode não constar da base pública, estar com dados ainda não enviados pelo tribunal ou ter restrições de acesso. A ausência de resultado não permite concluir que há sigilo.</p></details><details><summary>Como os meus dados são tratados?</summary><p>O número CNJ e a chave opcional passam pelo servidor para consultar o endereço oficial do DataJud. A aplicação não grava chaves nem resultados em banco, cookies ou armazenamento do navegador. A hospedagem pode manter registros técnicos de acesso. Consulte também os termos do CNJ.</p></details><details><summary>Posso integrar pela API?</summary><p>Envie POST para /api/consulta com JSON: numeroCNJ e, opcionalmente, apiKey. As respostas são JSON. Este serviço não oferece busca por nome, CPF, documentos ou acesso privado. O limite de saída é compartilhado; respostas 429 pedem uma nova tentativa mais tarde.</p></details></section>
    </main>
    <footer><div><a className="brand" href="/">Cnj<span>Processos</span></a><p>Projeto independente de Kleber Jucá.<br/>Uso gratuito e não comercial. Sem vínculo oficial com o CNJ.</p></div><div><a href="https://datajud-wiki.cnj.jus.br/api-publica/termo-uso/" target="_blank" rel="noreferrer">Termos do DataJud ↗</a><a href="https://github.com/KleberJuca/CnjProcessosApi" target="_blank" rel="noreferrer">Projeto no GitHub ↗</a><span>Código aberto · MIT</span></div></footer>
  </>;
}
