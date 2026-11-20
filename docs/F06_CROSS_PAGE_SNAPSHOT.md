# F06 — Consistência entre páginas e isolamento de snapshots

O F06 retém, por sessão verificada, o cabeçalho e as identidades dos índices já publicados. A próxima chamada reconfere essas identidades usando o leitor F05, antes e depois de ler a página solicitada. Também compara duas leituras completas da página nova, independentemente de mudanças no tamanho da página e da ordem de navegação.

## Garantias e critérios de rejeição

- Cabeçalho (tabela ou quantidade) diferente do primeiro cabeçalho observado.
- Ponteiro, ID, primeiro nome, sobrenome ou nome comum diferente para índice já publicado.
- ID ou ponteiro duplicado em índices distintos entre páginas.
- Falha, leitura parcial ou cancelamento: página atual não publicada, sessão F04 invalidada.
- `MaxTrackedRecords=512`: falha conservadora antes de superar o teto; nenhum descarte silencioso do histórico.
- Os objetos previamente publicados são cópias estáveis e não compartilham buffers com resultados posteriores.

## Testes simulados

Páginas estáveis, vazias e sobrepostas com tamanhos alternados; alterações de tabela, contagem, ponteiro, ID e nome entre chamadas; ID duplicado; segunda página parcial; cancelamento durante e antes da chamada; preservação do snapshot anterior após falha; recuperação em nova sessão fictícia; negativa de catálogo operacional.

## Limites

Não é snapshot atômico. ABA entre observações e mudanças após a última leitura podem não ser detectadas. A reconferência cresce em custo com o número de registros observados. Snapshots já entregues permanecem como dados históricos imutáveis, mesmo quando a sessão é invalidada posteriormente.

Nenhuma escrita, nenhuma ampliação de permissões Windows, nenhum mapa promovido. Somente `IProcessReader` injetado nos testes.

**approved=false; operational=false; somente leitura.**
