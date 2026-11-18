# Evidências estáticas — FMRTE v1.0.0.5

Fonte: ZIP fornecido pelo proprietário do projeto; inspeção offline de PE/CLI, metadados #Strings, configuração XML e recursos. O EXE **não foi executado**.

## Binário analisado
- `FMRTE.exe`, 649728 bytes, SHA-256: `e7ba25394eb1f69fc73814a4cbe198a45a5681670f7472a07d9ba9f2a0b3fb07`.
- PE32, Intel x86 (machine 0x14c), GUI Windows, CLR com metadados `v2.0.50727`, flags ILONLY e 32BITREQUIRED.
- Metadados referenciam `System.Windows.Forms`; arquitetura original parece WinForms, não WPF.
- `config.xml`: LangFile=english.xml; Currency=Pound; Debug=False; AttributesRangeMultiplier=5.
- Idiomas: 546 chaves em english.xml e 544 em portugues.xml; português parcialmente traduzido.

## Indicadores internos relevantes
- Memória: `ProcessMemoryReader`, `ReadProcessMemory`, `WriteProcessMemory`, `VirtualQueryEx`, `MemoryFreeze`, `RegularMemoryScan`, `IrregularMemoryScan`.
- Versões: `v8_0_0`, `v8_0_1`, `v8_0_2v1`, `v8_0_2v2` e métodos `loadOffsets_8_0_*`.
- Domínio: `Player`, `Person`, `Club`, `Nation`, `League`, `Contract`, `LoanContract`, `Stadium`, `Relations`.
- Interface: `cloneCenter`, `clubsAffiliations`, `leagueStandings`, `playersPositions`, `playersPrefMoves`.
- Busca e edição: `searchPlayersByID`, `searchPlayersByName`, `savePlayer`, `saveContract`, `saveFinances`, `saveStadium`.

**Cuidado:** símbolos e rótulos indicam arquitetura e intenções; não comprovam funcionamento, suporte efetivo de versões ou valores de offsets. Não foram identificados endereços reais. O SHA-256 citado é do *editor*, não do executável *Football Manager 2008*.

## Mudanças propostas para o editor

1. Implementar descoberta e fingerprint de processo do jogo; bloqueio para builds desconhecidas.
2. Criar `IMemoryRegionInspector`, `IProcessReader`, `IMappingCatalog`, e somente futuramente `IProcessWriter`, separados.
3. Introduzir serviços de busca por ID/nome, cancelamento e progresso — preservando a separação entre domain/application/infrastructure/desktop.
4. Projetar modelos de Person, Player, Staff, Club, Nation, League e relações com contratos/estádios.
5. Interface WPF/MVVM moderna; não reproduzir acoplamento de forms antigos.
6. Criar infraestrutura de internacionalização PT/EN e validação de tradução.
7. Manter `MemoryFreeze`, clonagem e alteração de memória fora do MVP de leitura.

## Critério de aceite do próximo marco
Processo detectado, hash de executável do **jogo** apresentado, versão desconhecida bloqueada, sem leitura de memória em build não validada. Automação de build Windows verde antes de integração real.
