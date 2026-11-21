# F25.2 — Análise estática inicial de PE32/CLR

> Inspeção dos bytes históricos fornecidos pelo usuário, sem execução de `FMRTE.exe` nem de `fm.exe`. As características abaixo foram observadas em leitura estática; não demonstram origem oficial, legitimidade de distribuição, proveniência B03, suporte a uma versão do jogo ou mapas operacionais.

## Identidade
- ZIP: `FMRTE v1.0.0.5(1).zip`; SHA-256 `491f15b1255fe135dfd5983520cb1a090b8edbd14511205a6f3b576b06a9c9cd`.
- Membro: `FMRTE v1.0.0.5/FMRTE.exe`; 649.728 bytes; SHA-256 `e7ba25394eb1f69fc73814a4cbe198a45a5681670f7472a07d9ba9f2a0b3fb07`.

## Cabeçalhos inspecionados (OBSERVED)
| Campo | Valor |
| --- | --- |
| DOS/NT signatures | MZ / PE |
| PE offset | `0x80` |
| Machine | `0x014c` (x86) |
| Optional header | `0x10b` (PE32) |
| Sections | 3: `.text`, `.rsrc`, `.reloc` |
| CLR data directory | RVA `0x2008`, size `72` |
| CLI metadata RVA | `0x74e50`, size `158684` |
| Metadata signature | `BSJB` |
| Metadata version | `v2.0.50727` |
| Metadata streams | `#~` (51.388 B); `#Strings` (54.648 B); `#US` (46.524 B); `#GUID` (16 B); `#Blob` (6.000 B) |

A versão `v2.0.50727` é **identificador de formato/runtime declarado em metadados**, não uma prova de compilador específico ou de funcionamento em um ambiente moderno.

### Tabelas CLI observadas
| Tabela | Linhas |
| --- | ---: |
| Module | 1 |
| TypeRef | 264 |
| TypeDef | 100 |
| Field | 1554 |
| MethodDef | 1421 |
| Param | 1280 |
| MemberRef | 703 |
| CustomAttribute | 26 |
| Assembly | 1 |
| AssemblyRef | 5 |
| ManifestResource | 21 |
| NestedClass | 27 |

Estas contagens demonstram a presença de metadados estruturados. **Nenhum nome de classe, método, dependência ou capacidade funcional está comprovado nesta etapa.** O catálogo completo de tabelas suportadas consta do inspetor.

## Implementação
- `tools/f25_clr_metadata.py`: lê o membro ZIP em memória, exige SHA-256 do ZIP fixado pelo usuário, interpreta cabeçalhos PE32, diretório CLR, assinatura de metadados, limites de streams e contagens de tabelas; sem subprocessos, execução, importação de assemblies nem extração.
- `tests/FMRTE2008.MemorySimulation/test_f25_clr_metadata.py`: dez testes sintéticos (PE/CLR válido e rejeição de assinaturas erradas, RVA inválido, truncamento, tamanho de versão e sobreposição de streams).
- `.github/workflows/f25-static-clr.yml`: executa testes somente sobre bytes sintéticos no Windows/Linux. O ZIP original **não é enviado ao CI**.

## Uso local, sem execução do programa original
```powershell
python tools/f25_clr_metadata.py "C:\caminho\FMRTE v1.0.0.5(1).zip" --expected-zip-sha256 491f15b1255fe135dfd5983520cb1a090b8edbd14511205a6f3b576b06a9c9cd
python -m unittest discover -s tests/FMRTE2008.MemorySimulation -p test_f25_clr_metadata.py -v
```

## Próxima investigação (ainda não realizada)
Extrair somente nomes/identificadores do heap `#Strings`, tabelas `AssemblyRef`, `TypeDef`, `MethodDef`, recursos, com rastreabilidade por RVA/offset; diferenciar `OBSERVED`, `INFERRED`, `UNKNOWN`. Não descompilar ou executar antes de revisão específica.

## Restrições mantidas
`NoApprovedPlayerMaps`; `approved=false`; `operational=false`; `approvedPlayerMaps=0`; `B03=PENDING-DECLARED`; `A12=GATE-BLOQUEADO`; `decision=NOT_MADE`; RA-01–RA-08 não aceitos. Sem alterações no `FM2008.mapping`. F25.2 não constitui revisão humana ou autorização de mapas.
