# API de fechamentos

Base local: `http://localhost:5184/api/v1`. Cabeçalhos/corpos de exemplo não incluem chaves reais.

| Método e rota | Resultado |
|---|---|
| `GET /health` | `200` e `{"status":"ok"}`. Prova disponibilidade HTTP, não a do banco. |
| `GET /closures` | `200`, array com até 100 retratos recentes da coligada de demonstração. |
| `GET /closures/{id}` | `200`, resumo com matrículas e auditoria, ou `404`. |
| `POST /closures/{id}/close` | `200`, detalhe fechado; requer Bearer da coordenação e JSON `{"version":1}`. |

Campos do resumo: `id`, `organization`, `term`, `classCode`, `title`, `status` (`pending` ou `closed`), `version`, `studentCount`, `approvedCount`, `failedCount`, `importedAt` e `closedAt`. O detalhe acrescenta `rows` (`registration`, `name`, `grade`, `attendance`, `outcome`) e `audit` (`action`, `actor`, `at`). Datas são ISO 8601; frequência é percentual, não razão. A leitura das atas está em `/atas/{id}` e exige que o fechamento já exista.

Erros de negócio: `{ "code": "closure_conflict", "message": "…" }`. `401` indica chave ausente ou inválida, `403` uma função diferente, `400` corpo/versão fora do contrato, `404` ausência no escopo, `409` versão ou estado conflitante e `503` dependência indisponível. Após `409`, releia o recurso. Para `503` durante o comando, verifique o detalhe antes de decidir se é seguro repetir.
