# F25.1 — Inventário estático do pacote histórico

## Resultado da inspeção (2026-11-21)
Artefato fornecido pelo usuário: `FMRTE v1.0.0.5(1).zip`. Inspecionado **sem extração de arquivos para disco e sem executar FMRTE.exe ou fm.exe**.

- ZIP: 374.984 bytes, SHA-256 `491f15b1255fe135dfd5983520cb1a090b8edbd14511205a6f3b576b06a9c9cd`.
- 27 entradas, incluindo 4 diretórios; 835.178 bytes de conteúdo total descompactado, lidos apenas em fluxo.
- `FMRTE v1.0.0.5/FMRTE.exe`: 649.728 bytes, SHA-256 `e7ba25394eb1f69fc73814a4cbe198a45a5681670f7472a07d9ba9f2a0b3fb07`.
- Cada entrada possui caminho, categoria, tamanho, tamanho compactado, CRC32 e SHA-256 registrados em `tests/FMRTE2008.MemorySimulation/F25HistoricalInventory.json`.
- O ZIP original e o binário histórico **não** foram adicionados ao Git.

## Implementação
- `tools/f25_inventory.py`: verificador fail-closed para ZIP local, sem subprocessos, extração ou execução. Inspeciona SHA-256 do arquivo completo e de cada entrada, caminhos, duplicidades, CRC validado pela API ZIP, indicadores de criptografia, links simbólicos e limites de tamanho.
- `tests/FMRTE2008.MemorySimulation/test_f25_inventory.py`: 24 testes positivos/negativos **em pacotes sintéticos**, inclusive adulteração de hash/tamanho/CRC/contagem, omissão/duplicação, aprovação falsa, decisão humana fictícia, travessia, caminho absoluto, case-collision e symlink. Execução local: **24/24 passaram**.
- `.github/workflows/f25-static-inventory.yml`: matriz Windows e Linux para executar os testes em ambiente isolado e conferir a consistência documental do manifesto; sem distribuir o artefato original.

## Reprodução com o pacote fornecido
```powershell
python tools/f25_inventory.py "C:\caminho\FMRTE v1.0.0.5(1).zip" --manifest tests/FMRTE2008.MemorySimulation/F25HistoricalInventory.json
python -m unittest discover -s tests/FMRTE2008.MemorySimulation -p test_f25_inventory.py -v
```
O comando de validação somente lê bytes: não executa o `.exe`. O relatório de CI, sem o ZIP histórico, **não pode comprovar que os bytes do arquivo local correspondem ao manifesto**; essa verificação precisa ocorrer onde o pacote está presente.

## Limitações
- Os hashes demonstram identidade dos bytes examinados, **não** autenticidade, origem, licença nem aderência ao build do jogo.
- Não há engenharia reversa de métodos, offsets, execução dinâmica ou validação de mapas nesta fase.
- Um ZIP substituído juntamente com um novo manifesto pode ser autoconsistente: a proteção de identidade depende da comparação com o hash previamente fixado, registrado acima.
- Revisão de código/CI não é revisão humana das evidências históricas; nenhuma aprovação operacional decorre deste marco.

## Invariantes
`NoApprovedPlayerMaps`, `approved=false`, `operational=false`, `approvedPlayerMaps=0`, `B03=PENDING-DECLARED`, `A12=GATE-BLOQUEADO`, `decision=NOT_MADE` e oito riscos não aceitos. Repositório `FM2008.mapping` intocado.

**Status:** F25.1 implementado em PR Draft; aguarda verificação de CI e revisão técnica antes de qualquer merge.
