import { sqliteTable, text, integer } from "drizzle-orm/sqlite-core";
export const requestGate = sqliteTable("request_gate", {
  id: text("id").primaryKey(),
  nextAt: integer("next_at").notNull()
});
