# F32 — Preparação da revisão humana do mapa mínimo FM2008 8.0.2v1

## Natureza e limites
**Documentação e validação offline, sem decisão humana.** Este marco prepara a análise, mas não abre `fm.exe`, não lê memória de processos, não altera saves, não cria mapas aprovados e não integra dados reais ao WPF. Nenhuma etapa converte CI ou PR em autoridade humana.

**Base imutável:** `FMRTE.2008@486fabe2aff423fb5544110ec992ba6b1707b317` (F31-R integrado) e `FM2008.mapping@df5be0493a1ac7be57208ff0e91c2d7193dfec73`. `evidence/f32/human_review_dossier.json` replica os 12 critérios e os oito riscos do manifesto F31 referenciado pelo Git blob `adb0dc1e83d09337afc04e425a9ca73d52c8d8dc`; o verificador calcula o blob local novamente e compara campos por ID. As cinco fontes documentais externas e seus hashes continuam sujeitos ao verificador F31.

## Agenda proposta para uma revisão humana futura (NÃO EXECUTADA)
1. **Identidade e autoridade:** designar revisor independente e autoridade aprovadora, identificar função, escopo, data e regras de conflito de interesses. Os campos continuam nulos.
2. **Proveniência e suficiência:** examinar documentação versionada e evidências primárias; distinguir observação fornecida pelo usuário de reprodução independente, incluindo a conferência 9/9 dos dois saves.
3. **G1 — identidade, G2 — integridade amostral, G3 — consistência, G4 — escopo somente leitura:** registrar resultados por critério, exceções, riscos e evidência de suporte, sem extrapolar aos registros não inspecionados.
4. **G5 — decisão formal:** avaliar RA-01–RA-08 explicitamente; qualquer aceite ou aprovação exige registro humano independente, identificável e separado do processo de CI, com versão exata do mapa e escopo restrito.
5. **Após revisão:** publicar decisão em um novo registro versionado para análise por humanos, sem alterar automaticamente os flags operacionais. Mesmo eventual aprovação de leitura não aprova escrita nem offsets adicionais.

## Situação declarada
- **A01–A12:** todos `PENDING_INDEPENDENT_REVIEW`; preservados os estados históricos PARCIAL/SAMPLED/USER_DECLARED/SIMULATED em separado.
- **RA-01–RA-08:** todos `NOT_REVIEWED`, `accepted=false`, aprovadores e datas nulos.
- **G1–G5:** todos `PENDING`.
- **Revisão humana:** identidade/autoridade/data/assinatura/decisão permanecem nulas, `decision=NOT_MADE`.
- **WPF:** fonte sintética; adaptador real e escrita desabilitados.
- **Governança:** `NoApprovedPlayerMaps`, `approved=false`, `operational=false`, `approvedPlayerMaps=0`, `B03=PENDING-DECLARED`, `A12=GATE-BLOQUEADO`, `decision=NOT_MADE`. RA-01–RA-08 sem aceite.

## Evidências e limitações
A integridade de documentos e de SHA não autentica logs históricos nem resultados amostrais. Os testes sobre memória simulada não demonstram ausência de leituras rasgadas, ABA, erros de runtime ou realocação efetiva de `fm.exe`. A revisão de código do F31 não foi uma deliberação humana sobre o mapa. O R04 (integração WPF somente planejada) permanece vigente.

## Aceitação técnica F32
O workflow `F32 human review preparation` verifica o dossiê, referência ao manifesto F31 e **45 negativos dirigidos por motivo**, executa duas vezes e compara relatórios byte a byte. Somente `PASS` documental é permitido. O PR deve ficar em Draft até revisão independente, sem merge automático nem aprovação do mapa.
