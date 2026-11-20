# F14 — Históricos concorrentes, pontos de linearização e testes adversariais

## Contexto e escopo
O F14 exercita o guardião de leitura `NativeReadOnlyGuard` somente com callbacks e handles fictícios. Nenhum código operacional é alterado. Base: merge do F13 `4044c68930f9da23f4d0f782c24ad179f94163f4`.

## Contrato público e modelo independente
`LinearizabilityScheduleTests` registra invocação e resposta das chamadas de leitura, inspeção e descarte. Um verificador sequencial independente enumera possíveis ordens de linearização e só aceita uma ordem se:
- respeitar precedência temporal: resposta da operação A anterior à invocação B implica A antes de B;
- uma sessão inicial utilizável só permite leitura/inspeção bem-sucedidas antes de Dispose/falha;
- falha de leitura nativa e falha de identidade invalidam a sessão;
- chamadas após invalidação são rejeitadas;
- Dispose é terminal e idempotente.

A existência de ao menos uma ordem sequencial compatível é verificada para históricos de métodos públicos com resultados cobertos pelo modelo. Controles positivos e negativos confirmam que o oráculo **rejeita** históricos inválidos (por exemplo, leitura bem-sucedida começando depois de Dispose concluído).

## Históricos adversariais determinísticos
64 identidades independentes × 6 modos = **384 cenários**: Dispose durante leitura, inspeção saudável concorrente, falha de inspeção concorrente, cancelamento, segunda leitura rejeitada por ocupação, falha nativa em voo. `TaskCompletionSource` controla entrada e liberação da leitura. Para os quatro modos compatíveis com o modelo sequencial público, são verificados 256 históricos. Em todos os modos verificam-se lease, ausência de publicação após erro, bloqueio após invalidação e fechamento único.

**Limite importante:** Cancelamento por token e rejeição imediata da segunda leitura enquanto outra está em voo são propriedades de protocolo/admissão, não cobertas pelo modelo sequencial simplificado das operações públicas. São verificadas diretamente sem alegar prova completa de linearizabilidade. A inspeção real entre duas leituras nativas tampouco prova snapshot atômico.

## Pontos de decisão relevantes
- `ReadAsync`: admissão da leitura e aquisição do lease no lock; publicação apenas após leitura completa, validação de identidade e último lock.
- `Dispose`: invalida a sessão no lock e adia a liberação até o lease terminar.
- `GetLiveHandleIdentity`: consulta protegida por lock; exceções invalidam a sessão.
- `Close`: marca fechamento único sob lock, executa callback fora do lock; a tentativa única não comprova que a liberação física ocorreu em callback excepcional.

## Revisão final — abrangência do oráculo e combinações de falhas
A auditoria acrescentou controles negativos com leitura bem-sucedida após Dispose, após falha de inspeção, consulta saudável após falha nativa, rejeição espúria enquanto a sessão está ativa e consulta saudável depois de Dispose. Também incluiu um histórico válido de operações sobrepostas. Esses controles avaliam se o verificador realmente distingue histórias admissíveis e inadmissíveis, mas não o transformam numa prova total de linearizabilidade.

A suíte `CombinedLinearizationFaultTests` cobre **384 cenários adicionais** (64 identidades × 6 modos): erro nativo + Dispose; falha de identidade + Dispose; cancelamento + Dispose; erro nativo + exceção no fechamento; Dispose durante leitura + exceção no fechamento; leitura saudável seguida de Dispose excepcional. Cada cenário controla o momento de conclusão da leitura com `TaskCompletionSource`, exige que o handle permaneça aberto enquanto a leitura está pendente, confirma uma única tentativa de fechamento e verifica que recuperação emprega um recurso novo e independente.

**Risco residual deliberadamente explicitado:** se o fechamento lançar exceção, a exceção primária da leitura pode ser substituída pela exceção de limpeza. A tentativa de fechamento único não garante que a operação de liberação física teve êxito. O verificador não modela completamente operações canceladas, leituras rejeitadas por ocupação ou eventual consistência temporal da memória nativa.

## CI e segurança
A validação pós-merge do F13 confirmou o commit da `main` e presença dos testes, mas a ferramenta disponível não lista workflows `push`; os quatro workflows pós-merge não foram confirmados.

Os testes do F14 são exclusivamente offline. Não habilitam operação no `fm.exe`, nem escrita, nem permissões novas. Preservados `NoApprovedPlayerMaps`, `approved=false`, `operational=false`, sem promoção de mapas experimentais.

O PR permanece em Draft até revisão de invariantes, confirmação do HEAD e 4/4 workflows de PR.
