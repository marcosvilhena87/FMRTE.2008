# Etapa C — Simulação de memória x86 (sem processo real)

## Objetivo
Verificar o caminho de leitura identificado no IL do editor FMRTE legado, **sem executar o FM2008**, e sem habilitar offsets candidatos em produção.

## Fixture deliberadamente fictícia
- Cabeçalho: `0x1000`, oito bytes little-endian.
- `[0x1000] = 0x2000`: ponteiro da tabela.
- `[0x1004] = 2`: quantidade de jogadores.
- Tabela `0x2000`: ponteiros de 32 bits `0x3000` e `0x4000`.
- Registro em `0x3000 + 256`: ID fictício `101`.
- Registro em `0x4000 + 256`: ID fictício `202`.

O deslocamento `+256` foi observado em `getPlayersIdAtIndex`; os endereços e IDs usados na fixture são **inventados exclusivamente para testes**.

## Estrutura
- `src/FMRTE2008.Application/PlayerListReader.cs`: decodifica header, tabela e ID por `IProcessReader`.
- `tests/FMRTE2008.MemorySimulation/SimulatedProcessMemory.cs`: memória esparsa, somente leitura, com regiões explícitas.
- `tests/FMRTE2008.MemorySimulation/Program.cs`: verificações de caminho normal, contagem, índice, ponteiros, overflow, cancelamento e regiões inválidas.

## Execução
```powershell
dotnet run --project tests/FMRTE2008.MemorySimulation/FMRTE2008.MemorySimulation.csproj --configuration Release
```

## Limitações
- Testa a interpretação de um caminho parcial no IL, não a compatibilidade do mapa com a build 52395.
- Não prova que endereços legados são absolutos, base-relativos ou válidos em um jogo executado.
- `MaxFixtureEntries=100000` é um limite defensivo do simulador, **não** um limite do Football Manager.
- Nenhuma alteração de memória, abertura de processo ou execução do jogo é realizada aqui.
- Não conectar este protótipo ao adaptador Windows até um catálogo de build aprovada e gate verificável.
