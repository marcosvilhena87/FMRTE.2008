# Diagnóstico somente leitura — cabeçalho de jogadores 8.0.2v1

## Resultado da inspeção estática
Analisado `fm.exe` original enviado pelo usuário:
- SHA-256 `157f484be2ead4ab27006d1002c2c666a7228197b88b14c8d026e76f31b1d62d`;
- FileVersion `8.0.2` / FilePrivatePart `52395`;
- ImageBase preferencial `0x00400000`;
- SizeOfImage `0x01804000`;
- `.data`: RVA `0x0147D000`, VirtualSize `0x00362238`, RawSize `0x00094000`;
- cabeçalho legado: VA candidato `0x01B1ED4C`, RVA `0x0171ED4C`;
- RVA dentro de `.data` e **além do tamanho de dados presentes no arquivo**. Não se pode obter bytes do cabeçalho diretamente do `fm.exe`.

### Hipótese de endereçamento
O FMRTE antigo passa o inteiro retornado por `getPlayersListsOffset` à rotina de memória. Na build com base preferencial, o valor pode representar **VA absoluta**. Para tolerar realocação da imagem, o diagnóstico usa `Process.MainModule.BaseAddress + 0x0171ED4C`. Esta hipótese ainda exige teste de execução. Nunca confundir RVA com deslocamento em disco.

## Uso manual — jogo local, partida de teste e backup de save
Não executar o FMRTE legado nem escrever em memória. No Windows com .NET 8 e a build exata identificada:
1. Abrir FM2008 (8.0.2f117931), carregar uma partida de teste, anotar jogadores conhecidos e seus IDs se acessíveis.
2. Localizar o PID do processo `fm.exe` via Gerenciador de Tarefas.
3. No checkout deste repositório:
```powershell
dotnet run --project tools/FMRTE2008.ReadOnlyProbe/FMRTE2008.ReadOnlyProbe.csproj -c Release -- --pid 1234 --confirm-read-only
```
Substituir `1234` pelo PID real. O programa solicita somente permissões de leitura e consulta do processo, não grava memória nem altera arquivos de save. Ele calcula a identidade do executável vinculado ao processo; caso não corresponda exatamente ao hash conhecido, aborta.

## Critérios para considerar resultados reproduzíveis
- Invariantes: ponteiro da tabela não nulo, contagem plausível, região legível; primeiro ponteiro legível; ID positivo.
- Verificar coerência do ID com um jogador conhecido por método independente.
- Repetir com **pelo menos duas partidas de teste independentes**, de preferência após reiniciar o jogo.
- Coletar resultado da quantidade e do ID, nunca dumps de memória nem caminhos pessoais completos.
- Se houver divergência, manter o manifesto em `research/` e registrar a falha. **ID positivo sozinho não aprova o mapa.**

## Limites
- O probe é um utilitário de diagnóstico *experimental separado* do FMRTE2008 de produção. Seu uso exige ação explícita `--confirm-read-only`.
- `NoApprovedPlayerMaps` continua como único catálogo de produção e sempre bloqueia sessão real.
- A identidade do arquivo em disco e seu hash não comprovam ausência de patching de memória em tempo de execução.
- O teste não substitui inspeção de múltiplos elementos, cruzamento com IDs e revisão independente.


## Leitura múltipla (.NET 10)

Todos os projetos e workflows foram migrados para `net10.0` / `net10.0-windows`. No Windows com SDK .NET 10, atualize o checkout do repositório:

```powershell
git pull --ff-only
dotnet --version
dotnet build .\tools\FMRTE2008.ReadOnlyProbe\FMRTE2008.ReadOnlyProbe.csproj -c Release
$fm = Get-Process fm -ErrorAction Stop
dotnet run --no-build --project .\tools\FMRTE2008.ReadOnlyProbe\FMRTE2008.ReadOnlyProbe.csproj -c Release -- --pid $fm.Id --confirm-read-only --first 5 --repeat 2
```

- `--first`: 1 a 20; padrão 1
- `--repeat`: 1 a 10; padrão 1
- Máximo 200 registros consultados por diagnóstico.
- Por amostra, relê o cabeçalho, os N ponteiros e os respectivos IDs. Comparação envolve tabela, quantidade, ponteiros e IDs, sem disparar qualquer escrita.
- Quando existe só uma amostra, a estabilidade **não** foi avaliada.
- Duas amostras idênticas na mesma partida **não** equivalem a validar o mapa em saves independentes.
- Mudanças de memória entre leituras podem ser normais; se houver divergência, investigar sem promover mapa.
- Endereços concretos são candidatos extraídos do IL legado e utilizados **exclusivamente pelo probe experimental**, não pela aplicação de produção.
