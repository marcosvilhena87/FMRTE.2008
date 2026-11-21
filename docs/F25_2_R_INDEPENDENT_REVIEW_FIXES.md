# F25.2-R — Correções da revisão técnica independente

## Escopo
Resolução dos achados F25.2-R01–R04 da revisão técnica #6005816259 do PR #27, mantendo a análise **somente estática** do `FMRTE.exe` recebido. Nenhum executável histórico é incorporado ao repositório, executado ou submetido ao CI.

## Correções e critérios
| ID | Achado | Correção |
| --- | --- | --- |
| R01 | Contagens impossíveis de linhas CLR eram aceitas | Cálculo dos tamanhos de registros de todas as tabelas ECMA-335 II.22 (0–44), larguras de heap, índices simples e índices codificados; cada bloco deve caber no stream de tabelas |
| R02 | Stream podia invadir diretório de metadados | Ranges de streams devem iniciar após o término do diretório, com checagem de sobreposição entre streams e limites do metadata root |
| R03 | Campos estruturais não verificados | Rejeição de CLI major versão zero, reservado não nulo do metadata root, reservado da tabela e bits inválidos de heap; preserva compatibilidade com o binário analisado |
| R04 | Cobertura adversarial insuficiente | Adicionados 17 testes sintéticos direcionados, além dos 10 testes originais (total: 27); verificação de SHA-256 exigida também dentro do helper de leitura do ZIP; leitura limitada por tamanho |

## Evidência local
Execução sobre fixtures sintéticas: **27/27 testes aprovados**. A inspeção estática do ZIP histórico fornecido confirmou os mesmos SHA-256, 28 tabelas presentes e contagens documentadas, incluindo 100 TypeDef, 1.421 MethodDef, 5 AssemblyRef e 21 ManifestResource. O novo campo `tableLayout` contém, para cada tabela presente, linhas, bytes por linha e deslocamento relativo ao stream `#~`.

SHA-256 ZIP: `491f15b1255fe135dfd5983520cb1a090b8edbd14511205a6f3b576b06a9c9cd`.
SHA-256 membro `FMRTE.exe`: `e7ba25394eb1f69fc73814a4cbe198a45a5681670f7472a07d9ba9f2a0b3fb07`.

## Limites e segurança
O parser valida **consistência espacial dos registros**, não a semântica de cada linha, os tokens internos, a licença, a origem histórica, nem a capacidade operacional. Tabelas desconhecidas/sem layout suportado são rejeitadas. Não se converte resultado de CI em autenticação das observações B03 ou aprovação humana.

O workflow `f25-static-clr.yml` executa os testes sintéticos no Windows e no Linux; os workflows antigos devem permanecer aprovados no novo HEAD. Não há merge nesta correção.

Continuam: `NoApprovedPlayerMaps`, `approved=false`, `operational=false`, `approvedPlayerMaps=0`, `B03=PENDING-DECLARED`, `A12=GATE-BLOQUEADO`, `decision=NOT_MADE`, RA-01–RA-08 sem aceite e `FM2008.mapping` inalterado.
