# F25 — Inventário histórico e análise estática do FMRTE v1.0.0.5

> **PLANEJAMENTO / INVESTIGAÇÃO ESTÁTICA / SOMENTE OFFLINE.** O artefato histórico é material de referência fornecido pelo usuário, não uma origem autenticada de mapas de memória. Este marco não executa FMRTE.exe nem fm.exe, não autoriza acesso operacional a processos ou saves, não modifica FM2008.mapping e não promove offsets.

## 1. Linha de base
- Repositório de implementação: `marcosvilhena87/FMRTE.2008`.
- Base: `main@ccd621fd620ffee456bfe4eccfa9fc512e7b7c54` (F24, PR #25, squash merge).
- Referência documental congelada: `marcosvilhena87/FM2008.mapping@df5be0493a1ac7be57208ff0e91c2d7193dfec73`.
- F24 permanece documental: `NoApprovedPlayerMaps`, `approved=false`, `operational=false`, `approvedPlayerMaps=0`, `B03=PENDING-DECLARED`, `A12=GATE-BLOQUEADO`, `decision=NOT_MADE`, oito riscos RA-01–RA-08 não aceitos.
- Uma revisão de código, ação GitHub ou hash não equivale a validação de proveniência histórica nem a autorização humana.

## 2. Artefato de referência recebido
O usuário forneceu o arquivo local `FMRTE v1.0.0.5(1).zip`. Análise *sem execução*, realizada em 21/11/2026:

| Item | Evidência verificada |
| --- | --- |
| ZIP SHA-256 | `491f15b1255fe135dfd5983520cb1a090b8edbd14511205a6f3b576b06a9c9cd` |
| `FMRTE.exe` | 649728 bytes; SHA-256 `e7ba25394eb1f69fc73814a4cbe198a45a5681670f7472a07d9ba9f2a0b3fb07` |
| Cabeçalho | PE válido, Machine `0x014c` (Intel 386/x86), PE32 (`0x10b`), 3 seções (`.text`, `.rsrc`, `.reloc`) |
| Diretório CLR | RVA `0x2008`, tamanho 72: indicador de cabeçalho CLI/.NET; linguagem e código-fonte ainda **não verificados** |
| `config.xml` | SHA-256 `48c4d6399a57b77ac9455b63eb7e765e69c9e70d8c5b19aeea7d19dacd664d18`; propriedades `LangFile`, `Currency`, `Debug`, `AttributesRangeMultiplier` |
| `Languages/english.xml` | 546 `DictionaryEntry`; SHA-256 `1b04d77083a38a27c092c01ec50a932c1da494d626924820e87c8e1ba349e98e` |
| `Languages/portugues.xml` | 544 `DictionaryEntry`; SHA-256 `af8b536a33d1afb19589469d159ce44b34576124df524c0b454baf10315117aa` |
| Outros recursos | `images/toolbar/`: imagens para ações de navegação, opções, fechamento, minimizar e relatórios, entre outras |
| ZIP | 27 entradas incluindo diretórios |

Os valores de hash identificam os bytes fornecidos; não estabelecem autoria, distribuição oficial, licença ou autenticidade histórica.

## 3. Resultados esperados e subfases
### F25.1 — Inventário reproduzível do pacote
Produzir inventário JSON com caminhos, tamanhos, hashes e classificação de tipo; verificar rejeição de entradas de travessia de diretório, duplicidade e arquivos inesperados. **Nunca incorporar o binário histórico ao repositório sem avaliação explícita de direitos/licença**. Manter esta documentação de identificação como referência, não prova de origem.

### F25.2 — Análise estática do PE/CLR
Examinar metadados CLI, assemblies referenciados, recursos, tipos, namespaces e métodos por ferramenta de análise estática em ambiente isolado. Distinguir resultados observados, inferências e hipóteses. Não executar, depurar, injetar ou anexar a processos.

### F25.3 — Catálogo de terminologia e interface
Catalogar chaves `DictionaryEntry` dos dois idiomas, chaves sem correspondência e conteúdo inconsistente; inventariar as imagens de toolbar e relacionar *hipóteses de funcionalidades* apenas quando corroboradas por evidências estáticas. Não copiar indiscriminadamente ícones, textos extensos nem interfaces protegidas.

### F25.4 — Matriz de reconstrução limpa
Mapear capacidades identificadas para `Domain`, `Application`, `Infrastructure.Windows` e `Desktop`; registrar evidência fonte, confiança, dependências e plano de testes determinísticos com dados fictícios. Priorizar terminologia, navegação e visualização offline. Restringir ao máximo qualquer inferência sobre estrutura de memória.

### F25.5 — Relatório de fechamento e gates
Auditar SHA-256, rastreabilidade, limites de generalização e testes negativos. Confirmar F24 intacto e `FM2008.mapping` imutável; somente então avaliar revisão de PR. Nenhum resultado F25 habilita dados reais, leitura operacional, offsets ou escrita.

## 4. Invariantes de execução e de governança
1. **Somente inspeção estática** dos bytes; nenhum `FMRTE.exe` ou `fm.exe` executado pelo CI ou pelos validadores.
2. **Nenhum acesso operacional à memória ou save**, WPF live, escrita, mapa aprovado ou alteração de `FM2008.mapping`.
3. Invariantes de F24 preservadas em quaisquer novos manifestos e testes.
4. Toda afirmação sobre funcionalidades precisa citar a evidência (string, metadado, recurso, método ou teste offline), com categoria `OBSERVED` / `INFERRED` / `UNKNOWN`.
5. Não permitir que presença de código antigo, versão exibida ou hash seja interpretada como proveniência B03 da instalação alvo.
6. Esta etapa inicia como **Draft**. Revisão humana das evidências e autorização de acesso operacional permanecem independentes de qualquer PR/CI/merge.

## 5. Critérios de aceite do planejamento
- Plano F25 registrado em branch e PR Draft derivados do commit F24 pós-merge.
- Artefato original inventariado com hashes, sem executá-lo ou versionar o binário.
- Subfases, evidências, testes negativos e restrições de segurança explicitados.
- Nenhum arquivo de `FM2008.mapping` alterado.
- Implementação das subfases pendente de nova revisão e autorização; **não efetuar merge neste marco de planejamento**.
