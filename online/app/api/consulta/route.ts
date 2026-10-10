import { env } from "cloudflare:workers";
import { consult, ConsultaError, keyValue, normalize, tribunal } from "@/lib/datajud";
import { acquireQuota } from "@/lib/quota";
export const dynamic = "force-dynamic";
function reply(body: unknown, status = 200) {
  return Response.json(body, { status, headers:{
    "Cache-Control":"no-store, private","X-Content-Type-Options":"nosniff",
    ...(status === 429 ? {"Retry-After":"1"} : {})
  }});
}
export async function POST(request: Request) {
  try {
    if (!(request.headers.get("content-type") || "").startsWith("application/json"))
      return reply({message:"Envie uma consulta JSON."},415);
    if (Number(request.headers.get("content-length")) > 4096) return reply({message:"Consulta muito grande."},413);
    const reader = request.body?.getReader();
    if (!reader) return reply({message:"Informe o número CNJ."},400);
    const chunks: Uint8Array[] = []; let size = 0;
    try {
      while (true) {
        const {done,value} = await reader.read();
        if (done) break;
        size += value.byteLength;
        if (size > 4096) { await reader.cancel(); return reply({message:"Consulta muito grande."},413); }
        chunks.push(value);
      }
    } finally { reader.releaseLock(); }
    const bytes = new Uint8Array(size); let offset = 0;
    for (const chunk of chunks) { bytes.set(chunk,offset); offset += chunk.length; }
    let body;
    try { body = JSON.parse(new TextDecoder().decode(bytes)); } catch { return reply({message:"Consulta JSON inválida."},400); }
    const numero = normalize(body?.numeroCNJ);
    tribunal(numero);
    const key = keyValue(body?.apiKey) ?? keyValue(env.DATAJUD_API_KEY);
    if (!key) return reply({message:"A chave pública do serviço precisa ser atualizada. Você pode informar a chave vigente do DataJud no campo opcional."},503);
    if (!env.DB) return reply({message:"Serviço temporariamente indisponível."},503);
    if (!await acquireQuota(env.DB)) return reply({message:"Há outra consulta em andamento. Aguarde um segundo e tente novamente."},429);
    return reply(await consult(numero,key));
  } catch (error) {
    if (error instanceof ConsultaError) return reply({message:error.message},error.status);
    // Never log request bodies, credentials, or upstream content.
    return reply({message:"Não foi possível concluir a consulta. Tente novamente."},503);
  }
}
