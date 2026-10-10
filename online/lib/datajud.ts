export class ConsultaError extends Error {
  status: number;
  constructor(message: string, status = 400) { super(message); this.status = status; }
}
const states = ["","ac","al","ap","am","ba","ce","dft","es","go","ma","mt","ms","mg","pa","pb","pr","pe","pi","rj","rn","rs","ro","rr","sc","se","sp","to"];
export function normalize(value: unknown): string {
  if (typeof value !== "string" || !/^(?:[0-9]{20}|[0-9]{7}-[0-9]{2}\.[0-9]{4}\.[0-9]\.[0-9]{2}\.[0-9]{4})$/.test(value.trim()))
    throw new ConsultaError("Informe os 20 dígitos do número CNJ, com ou sem pontuação.");
  const n = value.trim().replace(/[.-]/g, "");
  let rest = 0;
  for (const d of n.slice(0,7) + n.slice(9) + "00") rest = (rest * 10 + Number(d)) % 97;
  if (98 - rest !== Number(n.slice(7,9))) throw new ConsultaError("O dígito verificador do número CNJ é inválido. Confira o número.");
  return n;
}
export function tribunal(n: string): string {
  const branch = n[13], code = Number(n.slice(14,16));
  let alias = "";
  if (branch === "8" && code >= 1 && code <= 27) alias = "tj" + states[code];
  if (branch === "4" && code >= 1 && code <= 6) alias = "trf" + code;
  if (branch === "5" && code >= 1 && code <= 24) alias = "trt" + code;
  if (branch === "6" && code >= 1 && code <= 27) alias = "tre-" + states[code];
  if (code === 0) alias = ({ "3":"stj", "5":"tst", "6":"tse", "7":"stm" } as Record<string,string>)[branch] || alias;
  if (branch === "9") alias = ({13:"tjmmg",21:"tjmrs",26:"tjmsp"} as Record<number,string>)[code] || "";
  if (!alias) throw new ConsultaError("Este tribunal não é atendido pelo mapeamento da API Pública do DataJud.");
  return "api_publica_" + alias + "/_search";
}
export function keyValue(input: unknown): string | null {
  if (input === undefined || input === null || input === "") return null;
  if (typeof input !== "string") throw new ConsultaError("A chave deve ser um texto.");
  const key = input.trim().replace(/^APIKey\s+/i, "");
  if (!/^[A-Za-z0-9+/=_-]{8,2048}$/.test(key)) throw new ConsultaError("Chave DataJud inválida. Use somente uma chave da API pública, sem senhas de tribunais.");
  return key;
}
type Obj = Record<string, any>;
const text = (v: unknown): string | null => typeof v === "string" ? v : null;
function names(v: unknown): string[] {
  if (Array.isArray(v)) return v.flatMap(names);
  if (v && typeof v === "object" && "nome" in v && typeof v.nome === "string") return [v.nome];
  return [];
}
function date(v: unknown): string | null {
  if (typeof v !== "string") return null;
  const millis = Date.parse(v);
  return Number.isFinite(millis) ? new Date(millis).toISOString() : null;
}
function mask(s: string): string {
  return s.replace(/\b\d{2}\.?\d{3}\.?\d{3}\/?\d{4}-?\d{2}\b/g,"[CNPJ]")
    .replace(/\b\d{3}\.?\d{3}\.?\d{3}-?\d{2}\b/g,"[CPF]");
}
export function parseResult(json: Obj, numero: string) {
  if (json?.timed_out || Number(json?._shards?.failed) > 0)
    throw new ConsultaError("O DataJud retornou uma busca incompleta. Tente novamente.",502);
  if (!Array.isArray(json?.hits?.hits)) throw new ConsultaError("Resposta inesperada do DataJud.",502);
  if (Number(json.hits.total?.value ?? json.hits.total ?? 0) > 100)
    throw new ConsultaError("A consulta tem mais registros do que este serviço consegue reunir. Consulte o tribunal de origem.",502);
  const sources: Obj[] = json.hits.hits.map((hit: Obj) => hit?._source).filter((s: Obj) => s?.numeroProcesso === numero);
  if (!sources.length || sources.some(s => s.nivelSigilo !== 0))
    throw new ConsultaError("Processo não encontrado ou sem metadados públicos disponíveis no DataJud.",404);
  sources.sort((a,b) => (date(b.dataHoraUltimaAtualizacao) || "").localeCompare(date(a.dataHoraUltimaAtualizacao) || ""));
  const source = sources[0];
  const movements = new Map<string,{data:string;descricao:string}>();
  for (const s of sources) for (const m of Array.isArray(s.movimentos) ? s.movimentos : []) {
    const data = date(m.dataHora), description = text(m.nome);
    if (data && description) movements.set(data + "\n" + description, {data,descricao:mask(description)});
  }
  const movimentacoes = [...movements.values()].sort((a,b) => b.data.localeCompare(a.data));
  return {
    numeroCNJ:numero, tribunal:text(source.tribunal),
    classe:text(source.classe?.nome), assunto:[...new Set(names(source.assuntos))].join("; "),
    orgaoJulgador:text(source.orgaoJulgador?.nome),
    ultimaAtualizacaoFonte:date(source.dataHoraUltimaAtualizacao),
    consultadoEm:new Date().toISOString(), ultimaMovimentacao:movimentacoes[0] || null, movimentacoes,
    disclaimer:"Metadados públicos do DataJud. Uso não comercial. Confirme informações e prazos no tribunal de origem."
  };
}
export async function consult(numero: string, key: string, fetcher: typeof fetch = (input, init) => fetch(input, init)) {
  const endpoint = tribunal(numero);
  let response: Response;
  try {
    response = await fetcher("https://api-publica.datajud.cnj.jus.br/" + endpoint, {
      method:"POST", redirect:"manual",
      headers:{"Authorization":"APIKey " + key,"Content-Type":"application/json","Accept":"application/json"},
      body:JSON.stringify({query:{match:{numeroProcesso:numero}},size:100,track_total_hits:true,sort:[{dataHoraUltimaAtualizacao:{order:"desc"}}]}),
      signal:AbortSignal.timeout(35000)
    });
  } catch {
    throw new ConsultaError("O DataJud não respondeu a tempo ou está indisponível. Tente novamente em alguns instantes.",504);
  }
  if (response.status === 401 || response.status === 403) throw new ConsultaError("O DataJud recusou a chave. Confira a chave pública vigente na Wiki do CNJ.",503);
  if (response.status === 429) throw new ConsultaError("O DataJud atingiu o limite de consultas. Aguarde um minuto.",503);
  if (!response.ok) throw new ConsultaError("O tribunal está indisponível no DataJud neste momento.",502);
  let json: Obj;
  try { json = await response.json(); } catch { throw new ConsultaError("Resposta inesperada do DataJud.",502); }
  return parseResult(json,numero);
}
