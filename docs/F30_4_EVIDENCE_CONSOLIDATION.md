# F30.4 — Consolidação verificável das evidências F30.1–F30.3

## Natureza do documento e autorização

Esta consolidação é **exclusivamente offline**, com evidências de engenharia de software e perfis totalmente fictícios. É uma compilação documental **de dados já existentes**; não executa nem habilita o `fm.exe`, acesso a processos do jogo, memória, mapeamentos operacionais ou escrita em saves. Não constitui revisão ou aprovação humana de riscos, nem decisão de liberar funcionalidades.

**Âncora imutável de conteúdo:** `main@c2c379b3cbec38f393eea41ffdbd431a1e0a021c` (PR #34 mesclado). O manifesto `evidence/f30/f30_consolidated_manifest.json` fixa **nove Git blob SHA-1** da árvore revisada e identifica sete execuções pós-merge da mesma `main`. O verificador `tools/f30/validate_f30_evidence.py` reavalia os nove blobs em bytes com a codificação do Git e produz um relatório estruturado com **SHA-256 calculado sobre cada arquivo** no checkout. Um arquivo alterado sem revisão explícita falha.

**Limite de proveniência obrigatório:** ID de run/job, estado `success` e linhas históricas foram conferidos por consulta ao GitHub na elaboração, mas o CI de F30.4 **não consulta a API GitHub** para reverificar retrospectivamente esses runs ou autenticar os bytes dos logs. O manifesto é um registro histórico identificado, **não** uma prova criptográfica do histórico de CI, da aprovação de um revisor, nem uma reprodução dos logs completos.

## Quadro consolidado (execuções da main após F30.3-R)

| Marco | PR mesclado | Run GitHub Actions | Job Windows | Evidência do log de 21/11/2026 | Limite |
| --- | ---: | ---: | ---: | --- | --- |
| F30.1-R | #32 | [44926965311](https://github.com/marcosvilhena87/FMRTE.2008/actions/runs/44926965311) | 136915058846 | **120 ciclos, 5.532 asserções, três sequências e 12 falhas persistentes** | Eventos sintéticos WPF em STA |
| F30.2-R | #33 | [44926965354](https://github.com/marcosvilhena87/FMRTE.2008/actions/runs/44926965354) | 136915058961 | **48 STAs isolados + 48 janelas em Dispatcher compartilhado, 986 asserções, zero referências retidas** | Coletabilidade gerenciada; não mede todos recursos nativos |
| F30.3-R | #34 | [44926965330](https://github.com/marcosvilhena87/FMRTE.2008/actions/runs/44926965330) | 136915058988 | **96 janelas exibidas, 1.307 asserções, 0 referências retidas, teste negativo de +24 handles** | Handles nativos do processo de teste e objetos USER/GDI; não abrange GPU/COM |

A execução da `main` no commit `c2c379b3` registrou sete workflows `completed/success`: F29 offline robustness (inclui F30.1-R), F30.2, F30.3, compilação do diagnóstico somente leitura, testes do leitor x86, compilação Windows e simulação de memória x86. Cada run e seu nome são fixados no manifesto para evitar mistura de SHAs, jobs e execuções.

## Integridade, relatórios e testes negativos

O workflow `F30.4 offline consolidated evidence` roda em Ubuntu com Python da biblioteca padrão, **sem download de ferramentas, rede ou leitura de processos** na etapa de validação. O checkout do repositório é efetuado pela GitHub Action padrão.

O verificador confere esquema JSON fechado (campos extras são recusados), igualdade exata com a âncora da `main`, nomes e blobs dos nove arquivos, associação entre F30.1–F30.3 e PRs, commits, runs e jobs, e estado de todos os bloqueios humanos e operacionais. Emite `f30-verification.json` determinístico: hashes SHA-1 Git, hashes SHA-256, tamanhos e resultado `PASS`, com `offline_historical_log_authentication=false`. Dois relatórios calculados independentemente no mesmo checkout precisam ser idênticos byte a byte. O workflow publica o relatório por `actions/upload-artifact`.

A bateria negativa executa mutações independentes sobre cópias da estrutura (e sobre uma cópia temporária dos arquivos para alteração de bytes): manipulação do HEAD histórico, IDs CI, contagens ou log, caminhos relativos, hashes, esquema, estados `approved`/`operational`, B03/A12, riscos RA-01–RA-08, identidade/data/autoridade de revisor e decisão humana. **Qualquer mutação proibida precisa ser rejeitada**. Sucesso dos testes negativos significa apenas robustez da política declarada, não verificação completa de todo o sistema.

## Critérios de aceitação técnica

1. Todos os arquivos da evidência estão presentes e as assinaturas Git blob correspondem à `main` âncora; o relatório SHA-256 é gerado de modo determinístico.
2. Os três marcos e os sete runs históricos estão registrados, sem sugerir que uma conferência offline autentique os logs originais.
3. O workflow F30.4 e os workflows F29/F30.1, F30.2 e F30.3 continuam aprovados no novo HEAD, sem regredir comportamento da aplicação.
4. A suíte negativa passa e demonstra falha em mutações adversariais, inclusive falsa aprovação humana, operação online e alteração de arquivos.
5. `FM2008.mapping` permanece inalterado e **não** há implementação nova de acesso a memória, offset, processo real, save ou escrita.
6. O PR fica **Draft** até nova revisão técnica independente e reconfirmação do HEAD/checks. Nenhum merge antes da decisão explícita do usuário.

## Bloqueios inalterados (não negociáveis)

- `NoApprovedPlayerMaps`; `approved=false`; `operational=false`; `approvedPlayerMaps=0`.
- `B03=PENDING-DECLARED`; `A12=GATE-BLOQUEADO`; `decision=NOT_MADE`.
- `RA-01` a `RA-08`: todos **sem aceite**.
- `human_reviewer=null`, `human_review_timestamp=null`, `human_authority=null`, `human_approval=null`. O autor do PR, o CI e comentários de engenharia **não** substituem esses campos.

### Limitações não resolvidas por consolidação

A documentação não transforma os ensaios em execução real do FM2008, e não comprova ausência universal de vazamentos de COM/GPU, comportamento de mouse/teclado físico ou validação de mapas de jogadores. Runs do GitHub e hashes de arquivos do repositório não estabelecem por si só aprovação humana ou autorização operacional. A exigência de revisão humana permanece explicitamente pendente.
