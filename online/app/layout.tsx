import type { Metadata } from "next";
import "./globals.css";
export const metadata: Metadata = {
  title:"CnjProcessos | Consulta pública de processos",
  description:"Consulte metadados e movimentações de processos públicos no DataJud. Gratuito, não comercial e aberto a cidadãos e profissionais do Direito.",
  metadataBase:new URL("https://cnjprocessos-kleberjuca.aglowpixie3.chatgpt.site"),
  icons:{icon:"/favicon.svg"}
};
export default function Layout({children}:{children:React.ReactNode}) {
  return <html lang="pt-BR"><body>{children}</body></html>;
}
