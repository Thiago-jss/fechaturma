# Painel FechaTurma

Painel Angular de consulta de fechamentos acadêmicos, sem escrita, autenticação própria ou dados simulados em produção.

## Verificação e publicação

Com Node.js 22 (mínimo 22.12), neste diretório:

```sh
npm ci
npm test
npm run build
npm audit --omit=dev --audit-level=high
```

Publique o conteúdo de `dist/painel/browser/` em `/painel/`. O ponto de entrada é `dist/painel/browser/index.html` e os arquivos JS/CSS do mesmo diretório devem estar disponíveis sob `/painel/`. O painel solicita `GET /api/v1/closures` na mesma origem e espera um array JSON; não há CORS ou proxy configurado no frontend. Os links retornam ao hub (`/`), à importação (`/Importacao.aspx`) e à ata disponível (`/atas/{id}` somente para fechamentos `closed`).

O teste usa linhas sintéticas apenas para verificar o filtro com o executor nativo do Node; não exige servidor nem dependências extras.
