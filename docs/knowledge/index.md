# Knowledge Base Index

Current-state documentation of this project, written for Claude (and humans). **Read this before scanning code.** It describes what exists *now*; the journal (`docs/journal.md`) records history and AI decisions for the assessment panel. They are different documents.

## Files
| File | What it holds | Read when |
|---|---|---|
| `status.md` | What's done, in progress, next, known gaps | Start of every task |
| `domain.md` | Entities, state machine, invariants, glossary | Touching domain rules |
| `api.md` | Every endpoint: role, request, response, errors | Touching API or frontend contract |
| `events.md` | Event catalog: trigger, key, payload, consumers | Touching Kafka/outbox |
| `acceptance-criteria.md` | Testable criteria per feature | Starting or finishing a slice |
| `decisions.md` | One-line summary of each ADR | Questioning an architectural choice |

## Project map
<!-- Fill in as projects are scaffolded -->
- `src/Claims.Domain`: ...
- `src/Claims.Application`: ...
- `src/Claims.Infrastructure`: ...
- `src/Claims.Api`: ...
- `web/`: ...

## How to run
<!-- Fill in: docker compose up -d, connection string location, migrations, run API, run web -->

## Rules for keeping this accurate
1. Update the affected files in the same commit as the code change.
2. If this KB and the code disagree, the code wins: fix the KB and tell the developer.
3. Keep entries short and factual. Link to code paths instead of copying code.
