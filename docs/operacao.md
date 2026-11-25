# Operação do laboratório

## Validação

Use o SDK definido em `global.json`; os executáveis de desktop e web continuam sendo `net48`.

```sh
dotnet test tests/FechaTurma.Core.Tests -c Release -p:RestoreLockedMode=true
python3 scripts/bootstrap-db.py
docker compose up -d --wait
python3 scripts/bootstrap-db.py --init-sql
python3 scripts/bootstrap-db.py --exec dotnet test tests/FechaTurma.Data.Tests -c Release -p:RestoreLockedMode=true
docker compose down
```

O Compose exige Linux x86_64 com Docker e licenças de desenvolvimento/avaliação dos bancos. O Oracle XE foi escolhido pela disponibilidade do laboratório; não é uma homologação de ERP ou prova de execução no Oracle 19c. `docker compose down` preserva as bases. `docker compose down --volumes` apaga os dois volumes do laboratório; só use em um ambiente descartável.

## Diagnóstico

| Sintoma | O que verificar |
|---|---|
| 401 ao fechar | Chave da coordenação válida, enviada em `Authorization: Bearer ...`. |
| 403 | Chave conhecida, mas de outra função. A secretaria não fecha e a coordenação não importa. |
| 409 | Atualize o detalhe. Se a origem mudou, gere uma nova prévia. Não force uma segunda gravação. |
| 422 na prévia | Corrija as pendências de nota, frequência ou cadastro na origem. Resposta não representa um retrato salvo. |
| 503 | Verifique o modo da origem, a conectividade e as permissões. A aplicação não substitui um Oracle indisponível por fixtures. |

No Windows, `GET http://localhost:5184/api/v1/health` confirma que o host responde. `GET /api/v1/closures` exercita o banco de destino. Os erros públicos não incluem connection strings ou stack traces; logs de inicialização da CI são separados dos dados de negócio. O limite de 100 itens do painel é da janela recente, não uma paginação nem um total histórico.

## Fila de melhorias

O escopo fecha um processo inteiro antes de abrir outros. Foram priorizados o fluxo verificável e a preservação dos dados acima de cadastro genérico, múltiplas etapas e reabertura. O fluxo de trabalho usa branch curta, PR revisável e CI; próximos itens candidatos a Kanban: identidade corporativa, paginação e ambiente de homologação com o contrato real da origem.
