# F25.3 — Catálogo estático de símbolos .NET/CLR do FMRTE v1.0.0.5

> **OBSERVED — somente inspeção de metadados, sem executar FMRTE.exe ou fm.exe.** Nenhuma classe, nome, método ou RVA listado constitui mapa aprovado do Football Manager 2008. Não se interpreta `MethodDef.RVA` como endereço de memória do processo do jogo.

## Identidade imutável da referência
- ZIP `FMRTE v1.0.0.5(1).zip` SHA-256: `491f15b1255fe135dfd5983520cb1a090b8edbd14511205a6f3b576b06a9c9cd`.
- Membro `FMRTE v1.0.0.5/FMRTE.exe` SHA-256: `e7ba25394eb1f69fc73814a4cbe198a45a5681670f7472a07d9ba9f2a0b3fb07`.
- Parser base: F25.2-R, esquema ECMA-335 e validação do tamanho de cada tabela.
- Catálogo local reproduzível: 231021 bytes (JSON UTF-8); SHA-256 `ced806e946e3d08fefd2a828ad8281769f425906393bc78dfd8c9cf70d4348d9`. O JSON completo é gerado localmente a partir do ZIP, **não é depositado como binário histórico nem dependência do CI**.

## Identificação observada
- Assembly: **FMRTE**, versão **1.0.0.5**; token `0x20000001`.
- Referências: `System.Windows.Forms`, `mscorlib`, `System.Xml`, `System`, `System.Drawing`, todas versão `2.0.0.0`.
- Símbolos: **100 TypeDef, 1421 MethodDef, 1 Assembly, 5 AssemblyRef, 21 ManifestResource**.
- O relacionamento TypeDef→MethodDef decorre da coluna `MethodList`, conforme a ordem e intervalos de linhas ECMA-335.

### Exemplos de classes (nomes observados, não confirmação funcional)
| Token | Nome |
| --- | --- |
| `0x02000002` | `FMRTE.UserControls.commonUserControl` |
| `0x02000003` | `FM_Editor.UserControls.playersPrefMoves` |
| `0x02000005` | `FM_Editor.frmOptions` |
| `0x02000008` | `FMRTE.frmMain` |
| `0x0200000d` | `FMRTE.UserControls.leagueStandings` |
| `0x02000011` | `FMRTE.GameClasses.Leagues` |
| `0x02000012` | `FMRTE.GameClasses.League` |
| `0x02000014` | `FMRTE.GameClasses.Contract` |

### Exemplos de métodos (nomes observados)
| Token | Nome | TypeDef proprietário |
| --- | --- | --- |
| `0x06000001` | `Dispose` | `0x02000002` |
| `0x06000002` | `InitializeComponent` | `0x02000002` |
| `0x06000004` | `load` | `0x02000002` |
| `0x06000005` | `save` | `0x02000002` |
| `0x06000007` | `loadLang` | `0x02000002` |

A existência da string `save` **não** demonstra escrita no jogo nem fornece autorização para implementá-la. Nomes são apenas indicações declarativas no assembly.

### Recursos manifestados (exemplos)
`FM_Editor.UserControls.relations.resources`, `FM_Editor.frmLoading.resources`, `FMRTE.frmMain.resources`, `FM_Editor.UserControls.staffAttributes.resources`, `FM_Editor.UserControls.playersPositions.resources`.

## Implementação
- `tools/f25_catalog.py` lê as tabelas `TypeDef`, `MethodDef`, `Assembly`, `AssemblyRef` e `ManifestResource`, resolve os índices de `#Strings` com UTF-8 estrito, preserva tokens CLI, os nomes originais e vínculos `MethodList`. Reaproveita `f25_clr_metadata.py` e mantém SHA-256 do ZIP obrigatório.
- `tests/FMRTE2008.MemorySimulation/test_f25_catalog.py`: 10 testes com dados **inteiramente sintéticos** (extração, índice fora do heap, string sem terminador, método órfão, heap ausente, UTF-8 inválido, contagem impossível, tabela não suportada, stream sobreposto e truncamento).
- `.github/workflows/f25-static-catalog.yml`: execução exclusiva de testes sintéticos no Windows e Linux, sem ZIP ou executável histórico.

### Reprodução local
```powershell
python tools/f25_catalog.py "C:\caminho\FMRTE v1.0.0.5(1).zip" --expected-zip-sha256 491f15b1255fe135dfd5983520cb1a090b8edbd14511205a6f3b576b06a9c9cd --output F25SymbolCatalog.json
python -m unittest discover -s tests/FMRTE2008.MemorySimulation -p test_f25_catalog.py -v
```

## Limites
O catálogo é inventário de metadados e não é descompilação ou execução. Não são validadas semanticamente todas as colunas das tabelas, as assinaturas de métodos nem corpos IL. RVA de método pertence ao formato do **executável do editor**, não ao mapeamento de memória do jogo. Recursos são nomes e metadados, não o conteúdo decodificado. O catálogo não estabelece licença ou origem oficial.

## Governança
`NoApprovedPlayerMaps`; `approved=false`; `operational=false`; `approvedPlayerMaps=0`; `B03=PENDING-DECLARED`; `A12=GATE-BLOQUEADO`; `decision=NOT_MADE`; RA-01–RA-08 não aceitos. Sem alterações em `FM2008.mapping`, sem `fm.exe`, WPF live ou memória/save real. PR #27 em Draft, sem merge.
