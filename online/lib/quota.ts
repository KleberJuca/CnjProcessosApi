// D1 serializes the global gate across Worker instances. No key or case data is stored.
export async function acquireQuota(db: D1Database, now = Date.now()): Promise<boolean> {
  const row = await db.prepare(
    "INSERT INTO request_gate (id, next_at) VALUES ('datajud', ?) ON CONFLICT(id) DO UPDATE SET next_at = excluded.next_at WHERE request_gate.next_at <= ? RETURNING next_at"
  ).bind(now + 550, now).first();
  return row !== null;
}
