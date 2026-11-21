# F31 — Lacunas do mapa mínimo 8.0.2v1 e preparação WPF somente leitura

**Escopo:** planejamento e validação **exclusivamente offline**, baseado em `FMRTE.2008@47d9d6f4` e `FM2008.mapping@df5be049`. Nenhum código de processo, offsets, leitura real, escrita, saves, catálogo aprovado ou `FM2008.mapping` é alterado.

## Evidências conhecidas versus o que falta
A pesquisa de 18–19/11/2026 documentou amostras distribuídas de nove jogadores em cada um de dois saves (contagens 6.697/6.698 e 5.154), mais repetição após reinício e conferência manual **declarada** dos pares de ID/nome. Isso não demonstra integridade de todas as entradas, atributos além da identidade, ausência de ABA, autenticidade independente dos logs ou aprovação humana. A base real observada foi 0x00400000; realocação do processo não foi observada. Não forçar realocação ou falhas no jogo.

O contrato fechado e legível por máquina está em `evidence/f31/minimal_map_readiness.json`, com A01–A12, RA-01–RA-08 e o estado de cada lacuna. A enumeração indica evidência **parcial, amostral, declarada ou simulada**; não representa liberação. O relatório é validado por `tools/f31/validate_readiness.py` e seus 30 testes negativos, que rejeitam aprovação fictícia, expansão dos campos, adaptação WPF real e risco aceito. A verificação é local/offline; não autentica retrospectivamente dados históricos do mapping.

## Trabalho priorizado para o próximo gate (não realizado pelo F31)
1. **A01/A02/A11, RA-01/06:** revisão independente da identidade do PID e da imagem, variantes negativas, base por instância; registrar ausência de realocação natural.
2. **A03/A05/A06/A07/A08, RA-02/03/04:** contratos de invalidação, amostragem best-effort, falhas durante leitura e limpeza de recursos; **não** alegar atomicidade após testes sintéticos.
3. **A04/A10, RA-05:** revisar proveniência dos confrontos 9/9 e verificar separadamente nomes comuns, diferentes da exibição de nome completo.
4. **A09, RA-07/08:** auditar exclusividade de leitura e garantir nenhum acesso a 229 offsets legados, nenhum caminho de escrita.
5. **A12, todos os RA:** fornecer evidências independentes, documentar tratamento/aceite de risco, identidade, autoridade, data e escopo, com **decisão humana separada**. Até lá: NÃO APROVADO.

## Contrato de futura integração WPF (somente projeto)
```text
WPF offline -> IOfflineIdentityQuery -> Synthetic fixture (ATUAL)
Futura WPF -> IReadOnlyIdentityQuery (PROPOSTO; NÃO IMPLEMENTADO)
             -> Verified session -> Exact build + reviewed approved manifest
             -> Immutable identity DTO { playerId, firstName, lastName, commonName? }
```
Só considerar adaptador real após aprovação independente e humana, sob permissão somente leitura, com cancelamento, revalidação por sessão, descarte de páginas e cursores inconsistentes, sem cache de ponteiros entre saves. **Nenhuma porta real foi conectada neste PR.**

## Governança inalterada
`NoApprovedPlayerMaps`, `approved=false`, `operational=false`, `approvedPlayerMaps=0`, `B03=PENDING-DECLARED`, `A12=GATE-BLOQUEADO`, `decision=NOT_MADE`. Oito riscos RA-01–RA-08 sem aceite. Identidade, data, autoridade e aprovação humana nulas. Sucesso do CI, revisão do PR e autoria de commits **não** constituem revisão/decisão humana sobre riscos ou mapeamento.

**Aceitação do F31:** manifesto com cobertura exata e validação negativa no CI; documentação do próximo gate; WPF existente inalterado; PR em Draft, sem merge.
