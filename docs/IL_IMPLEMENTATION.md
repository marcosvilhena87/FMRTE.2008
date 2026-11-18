# Implementação de leitura derivada da análise estática IL

## Evidência usada
`FMRTE.GameClasses.Players.getPlayerPtrAtIndex` (FMRTE.exe v1.0.0.5) soma `_playersListPtr + 4*index` e consulta um inteiro de 32 bits. `ScanFunctions.read32BitsValue` lê quatro bytes de processo. A estrutura foi reproduzida como algoritmo *somente leitura*, sem copiar código executável do editor legado.

## Implementado
- `PointerTableReader` (Application): indexação x86, checagem de limites, aritmética com overflow verificado, leitura exata de quatro bytes, little-endian explícito e rejeição de ponteiro nulo.
- `WindowsReadOnlyProcessSession` (Infrastructure.Windows): adaptador **internal**, usa `SafeHandle`, APIs `OpenProcess`/`ReadProcessMemory`, testa retorno e tamanho lido, libera handle.
- Smoke tests sem processo real: ponteiro esperado, índice fora da tabela, overflow, leitura parcial e ponteiro nulo.

## Não implementado
- Acesso ao jogo a partir da UI, nem obtenção da sessão por chamadas públicas.
- Descoberta de processo e verificação real do SHA-256 do executável do **jogo**.
- Seleção de catálogo por build aprovada, cálculo de endereços base, varredura de memória e escrita.

## Política obrigatória de integração
Nenhum caminho da UI deve construir uma sessão sem passar por: detecção do executável do FM2008, hash SHA-256 verificado, arquitetura compatível, catálogo aprovado e autorização explícita de leitura. O hash do FMRTE.exe estudado é apenas proveniência da pesquisa.
