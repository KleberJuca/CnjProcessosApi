import test from "node:test";
import assert from "node:assert/strict";
import { DatabaseSync } from "node:sqlite";
import { normalize, tribunal, keyValue, parseResult, consult } from "../lib/datajud.ts";
import { acquireQuota } from "../lib/quota.ts";
const number = "00008323520184013202";
const payload = () => ({hits:{total:{value:1},hits:[{_source:{numeroProcesso:number,nivelSigilo:0,classe:{nome:"Procedimento"},tribunal:"TRF1",orgaoJulgador:{nome:"JEF"},assuntos:[[{nome:"Concessão"}]],dataHoraUltimaAtualizacao:"2025-01-01T00:00:00Z",movimentos:[{nome:"Distribuição",dataHora:"2020-01-01T00:00:00Z"},{nome:"Sentença",dataHora:"2025-01-01T00:00:00Z"}]}}]}});
test("CNJ validado, normalizado e tribunal identificado",()=>{
  assert.equal(normalize("0000832-35.2018.4.01.3202"),number);
  assert.equal(tribunal(number),"api_publica_trf1/_search");
  assert.throws(()=>normalize("0000832-34.2018.4.01.3202"));
  assert.throws(()=>normalize("abc"+number));
});
test("chave aceita prefixo e rejeita cabeçalho injetado",()=>{
  assert.equal(keyValue("APIKey visitor-key"),"visitor-key");
  assert.equal(keyValue(null),null);
  assert.throws(()=>keyValue("secret\r\nAuthorization: another"));
});
test("metadados e movimentos ordenados",()=>{
  const p=parseResult(payload(),number);
  assert.equal(p.assunto,"Concessão");
  assert.equal(p.ultimaMovimentacao.descricao,"Sentença");
  assert.equal(p.movimentacoes.length,2);
});
for(const level of [1,undefined,"0"]) test("sigilo ou nível inválido: "+level,()=>{
  const p=payload();p.hits.hits[0]._source.nivelSigilo=level;
  assert.throws(()=>parseResult(p,number), e=>e.status===404);
});
test("resultado vazio e resposta parcial não viram processos",()=>{
  assert.throws(()=>parseResult({hits:{hits:[]}},number),e=>e.status===404);
  assert.throws(()=>parseResult({...payload(),timed_out:true},number),e=>e.status===502);
  assert.throws(()=>parseResult({},number),e=>e.status===502);
});
for(const status of [401,403,429,500]) test("erro externo "+status+" preservado",async()=>{
  await assert.rejects(()=>consult(number,"visitor-key",async()=>new Response("{}",{status})),e=>e.status>=500);
});
test("a chave só vai ao endpoint oficial em cabeçalho",async()=>{
  let received;
  await consult(number,"visitor-key",async(url,options)=>{received={url,options};return Response.json(payload());});
  assert.equal(received.url,"https://api-publica.datajud.cnj.jus.br/api_publica_trf1/_search");
  assert.equal(received.options.headers.Authorization,"APIKey visitor-key");
  assert.equal(received.options.redirect,"manual");
  assert.equal(JSON.parse(received.options.body).query.match.numeroProcesso,number);
});
test("quota global é atômica no SQLite e expira em 550ms",async()=>{
  const sqlite=new DatabaseSync(":memory:");
  sqlite.exec("CREATE TABLE request_gate(id text PRIMARY KEY,next_at integer NOT NULL)");
  const db={prepare(sql){return{bind(...args){return{async first(){return sqlite.prepare(sql).get(...args)||null;}}}}}};
  assert.equal(await acquireQuota(db,1000),true);
  assert.equal(await acquireQuota(db,1001),false);
  assert.equal(await acquireQuota(db,1549),false);
  assert.equal(await acquireQuota(db,1550),true);
  assert.equal(sqlite.prepare("SELECT count(*) AS n FROM request_gate").get().n,1);
  sqlite.close();
});
