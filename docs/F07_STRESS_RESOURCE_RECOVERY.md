# F07 — Estresse determinístico, limites de recursos e recuperação

## Objetivo
Limitar o custo de leitura por sessão verificada sem habilitar escrita, sem abrir processos reais nos testes e sem alterar a política de mapas não aprovados. Este marco é adicional aos controles F01–F06.

## Orçamento de sessão
`BudgetedReadOnlySession` implementa somente `IProcessReadSession` e encaminha `ReadAsync` ao leitor injetado. Por padrão, limita a **8.192 solicitações** e **2 MiB de bytes solicitados** por sessão. Ambos os contadores são protegidos por um lock; a verificação é feita **antes** de chamar o leitor. Chamadas mal sucedidas consomem orçamento, sem repetição automática. Quando o orçamento se esgota, é lançada `InvalidDataException`, que invalida a `VerifiedPlayerReadSession` pela política F04.

Esses limites são defensivos e podem impedir a conclusão de uma paginação grande devido à reconferência repetida do F06. O teto histórico de 512 registros no F06 continua existindo, mas **não é promessa de que todos os 512 possam ser lidos sob o orçamento menor**. Não há política de aumentar orçamento automaticamente.

## Testes simulados
- 32 sessões independentes repetindo várias páginas, páginas sobrepostas, paginação vazia e descarte uma única vez.
- Estouro de chamadas de leitura; nenhuma chamada extra ao leitor fictício.
- Estouro do limite de bytes; rejeição antes de acessar a fonte.
- Orçamento esgotado durante página; invalidar sessão, não reutilizar handle e recuperar somente com uma sessão verificada nova.
- 16 cancelamentos determinísticos antes da primeira leitura; nenhum acesso à fonte e descarte exato.
- Mapa não aprovado: o opener não é chamado.

A suíte F04 existente mantém testes de interleavings de `Dispose()` e leituras assíncronas. O F07 não adiciona temporizadores, aleatoriedade nem acesso a `fm.exe`.

## Limitações e segurança
Esses testes não constituem benchmark de latência, garantia de ausência de deadlocks ou prova de snapshot atômico. ABA, mudanças após a última observação e degradação de performance de ambientes reais permanecem limitações.

`NoApprovedPlayerMaps` segue bloqueando o uso operacional. Sem `WriteProcessMemory`, permissões adicionais ou alteração dos mapeamentos. **approved=false; operational=false; somente leitura.**
