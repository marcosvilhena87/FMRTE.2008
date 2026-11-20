# F04 — Falha fechada após leituras incompletas e interrupção

## Alcance
Este marco reforça a `VerifiedPlayerReadSession` com fontes injetadas em testes offline. O catálogo de produção segue `NoApprovedPlayerMaps`; não é habilitada nenhuma leitura operacional nova, nem escrita.

## Política de segurança
- Uma leitura parcial de cabeçalho, ponteiro ou ID gera `InvalidDataException`.
- Ponteiros nulos, regiões não mapeadas e estouros x86 são rejeitados, jamais corrigidos ou reaproveitados por suposição.
- Exceções de leitura (inclusive processo interrompido e cancelamento após o início) invalidam a sessão lógica inteira e liberam o handle injetado. Uma nova leitura exige uma **nova** abertura verificada, sem reutilizar cursor ou handle.
- Um acesso concorrente à mesma sessão é rejeitado, sem misturar leituras de página e identidade.
- A leitura de página não publica prefixos de uma página que falhou.
- Erros de validação de argumentos não promovem mapas nem abrem memória.

## Testes
A suíte `ReadFailureTests` injeta bytes insuficientes no header, na entrada da tabela e no ID; ponteiro zero, inválido ou com overflow; perda simulada de processo; cancelamento durante a leitura e falha ao obter uma página. Em cada falha a sessão é rejeitada e permanece indisponível; a recuperação acontece apenas criando uma nova sessão fictícia. Também confirma que `NoApprovedPlayerMaps` não chama o opener.

## Limitações
O teste não abre `fm.exe` e não substitui uma auditoria de concorrência entre `Dispose` e leituras em execução. A falha fechada protege a sessão lógica, mas não garante snapshot atômico, nem resolve ABA. Nada aqui permite promover offsets de pesquisa.

**Estado operacional:** `approved=false`, `operational=false`, nenhuma operação de escrita.
