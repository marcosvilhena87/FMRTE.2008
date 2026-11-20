# F21 — Plano de saneamento B03 e evidências para decisão sobre mapa mínimo (NÃO OPERACIONAL)

## Linha de base
- F20 integrado à `main` por squash no PR #21: `ee1298824d06ab7b8400ad6eb28c3e49ca1c55be`.
- Fonte de pesquisa congelada para esta etapa: `marcosvilhena87/FM2008.mapping` commit `df5be0493a1ac7be57208ff0e91c2d7193dfec73`; não alterar o repositório externo.
- Resultado de CI pós-merge deve ser anexado apenas após consulta aos quatro workflows no commit acima.
- `NoApprovedPlayerMaps`; `approved=false`, `operational=false`; A12 `GATE-BLOQUEADO`; B03 `PENDING-DECLARED`.

## Problema
A evidência de execução CI F20 comprova a execução de testes negativos A01–A12 e seus artefatos; **não comprova** executor, instante de observação e ambiente das observações históricas R01–R07. SHA/autoria de commit, texto dos logs ou data da auditoria documental não podem ser substitutos desses metadados. Os 9/9 pares distribuídos declarados para as duas partidas melhoram a cobertura amostral, mas não aprovam automaticamente os campos ou o mapa.

## Entrega F21.1 — inventário conservador por observação
Criar um **registro complementar**, separado do manifesto F20, com um item para cada R01–R07, e campos:
`observationId`, `sourceRepo`, `sourceCommit`, `sourcePath`, `evidenceScope`, `reportedResult`, `executor`, `observedAt`, `environment`, `collectionMethod`, `externalCorroboration`, `provenanceStatus`, `limitations`.
- Nenhum dos três metadados históricos ausentes pode ser preenchido automaticamente, inferido de Git ou de `GITHUB_RUN_ID`.
- Ausência é representada por `null` com `provenanceStatus=PENDING-DECLARED`. `externalCorroboration` também é `null` até existir fonte realmente independente.
- O documento aponta a localização das evidências existentes; não duplica nem altera o registro original.
- As observações podem ser segmentadas por sessão/save apenas se os dados históricos permitirem essa segmentação sem suposições.

## Entrega F21.2 — verificações offline e negativos
Implementar esquema fechado, rejeição de propriedades inesperadas, duplicidade de `observationId`, divergência de commit/fonte, estado incompatível com `null`, resultado inventado, declaração artificial de revisor ou aprovação e promoção inadvertida de qualquer mapa.
Executar negativos de mutação sobre cópias **em memória** do registro complementar. Exportar resultados estruturados próprios e rastreáveis ao commit/run, sem converter resultados de CI em observações históricas.

## Entrega F21.3 — matriz de pendências para o mapa mínimo 8.0.2v1
Catalogar lacunas por gate, com evidência documentada e próximos ensaios somente quando autorizados e apropriados:
- G1 / A01-A02 / RA-01/06: identidade da instância/PID e base do módulo; relocação real somente se ocorrer naturalmente.
- G2 / A03-A05/A10 / RA-05: confrontos de ID, nome próprio, sobrenome e nome comum **separadamente**; conferir limites de amostragem e proveniência externa; não extrapolar 9/9 para toda a tabela.
- G3 / A06-A08 / RA-02/03/04: mudanças de tabela entre saves, invalidação de cursor, falhas durante leitura, risco ABA e recuperação; distinguir o que só foi simulado do que foi observado.
- G4 / A09/A11 / RA-07/08: revisão dos limites de leitura somente, variantes exatas e proibição de 229 offsets legados não validados.
- G5 / A12: decisão humana formal **separada**, com revisor, data, escopo, matriz de riscos e versão de evidências; não produzida no F21.

## Critérios de aceitação
1. Inventário R01–R07 completo, esquema fechado, 7 IDs distintos e toda falta de metadados explicitamente pendente.
2. Proveniência sem falsificação: observações históricas nunca recebem identidade do runner ou timestamp inferido; resultados de testes e observações humanas ficam em classes de evidência distintas.
3. Negativos estruturados executáveis e resultados exportáveis; CI verde no HEAD correto, com identificação de evento/checkout/PR/run separada.
4. Documentação de cada gate pendente e referência para fonte de pesquisa, sem transformar testes ou relatos em aprovação operacional.
5. Revisão independente do inventário e da matriz; nenhuma mudança em `FM2008.mapping` ou em código de acesso real.

## Proibições permanentes
Não abrir `fm.exe` operacionalmente, não conectar fonte real à WPF, não escrever memória, não aprovar mapa ou qualquer offset, não acionar `IApprovedPlayerMapCatalog`, não modificar `FM2008.mapping` e não inferir aprovação por CI. Manter PR em Draft; não fazer merge sem revisão e instrução específicas.

## Estado
F21 iniciado **somente pelo planejamento rastreável**. A coleta retroativa de executor, data e ambiente é pendente e pode exigir evidência primária do usuário. Nenhum metadado histórico foi preenchido automaticamente.

## Revisão F21-R01/R02/R03 — correções implementadas, condicionadas à revisão CI

- **R01 — granularidade:** cada R01–R07 tem `reportedResult` individual, estritamente documental, e `sourceEvidenceStatus=user-reported-not-independently-audited`. Não afirmar que esses resultados são observações independentes; preservar `null` para os cinco campos históricos desconhecidos.
- **R02 — identidade documental:** gravar `sourceBlobSha` (Git blob SHA-1, identificador de objeto Git, NÃO SHA-256 do conteúdo) para cada fonte do commit imutável `df5be0493a1ac7be57208ff0e91c2d7193dfec73`; durante CI, consultar via GET a árvore Git pública dessa revisão, recusar árvore truncada, fonte ausente ou hash divergente. A verificação externa estabelece a identidade do documento, não a autenticidade da observação humana.
- **R03 — resultados individuais:** exportar cada mutação adversarial estruturada com `caseId`, `expectedOutcome`, `actualOutcome`, `result`, `reasonCode`, além de total agregado. Inventário agora contém 29 mutações negativas. Proibir suprimir uma mutação e manter o total.
- **Limites:** F21 não resolve B03 e não aprova leitura operacional, offsets ou edição. Não modificar o repositório de pesquisa nem dados do FM2008.
