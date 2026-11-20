# F09 — Identidade de processo e falhas de abertura

## Escopo
F09 reforça exclusivamente a abertura **somente leitura**. O catálogo de produção continua `NoApprovedPlayerMaps`, sem mapas aprovados, sem acesso operacional liberado e sem escrita.

## Invariantes de abertura
- PID positivo e token ativo antes de qualquer inspeção.
- Na primeira inspeção: PID solicitado, nome `fm.exe`, horário de criação da instância, SHA-256, arquitetura e versão reconhecida devem coincidir com a identidade esperada.
- O handle de leitura só é adquirido após a primeira inspeção.
- A segunda inspeção, já com handle adquirido, exige a mesma identidade completa de instância e executável; divergência rejeita a sessão e descarta o handle.
- Cancelamento após aquisição, falha ou exceção na segunda inspeção descartam o handle antes de expor resultado.
- Um cancelamento anterior à abertura não chama a aquisição. Recuperação requer **nova** abertura, sem reutilizar a instância anterior.

## Testes determinísticos sem processo real
`ProcessOpeningGateTests` usa delegates e handles fictícios, sem relógio, sistema operacional, PID real ou acesso a `fm.exe`:
- identidade válida;
- PID divergente, nome incorreto, fingerprint divergente e identidade sem horário de criação;
- reutilização do mesmo PID com nova data de criação;
- mudança de PID ou versão após aquisição;
- cancelamento após aquisição e antes da publicação;
- falha da segunda inspeção com descarte único;
- token previamente cancelado bloqueando aquisição;
- recuperação por nova sessão independente;
- cancelamento enquanto a segunda inspeção está suspensa, com liberação única do recurso;
- duas aberturas concorrentes do mesmo PID com handles distintos: falha de uma não descarta o handle da outra.

As corridas são sincronizadas por `TaskCompletionSource` (sem `Sleep`).

## Limitações documentadas
- A verificação antes/depois **não é atômica** com a abertura Win32: entre o último check e uma leitura pode ocorrer encerramento ou mudança do processo, que deve falhar fechada pelas regras de sessão.
- O hash obtido do executável em disco não comprova a integridade de bytes em memória nem elimina janelas ABA entre inspeções. A instância é identificada por PID e horário de criação observados, não por vínculo criptográfico com o handle.
- A camada de plataforma real não foi executada por estes testes offline; falhas internas de aquisição devem limpar seus próprios recursos antes de lançar exceção.
- Não há aprovação de mapeamentos nem promoção de offsets legados.

**Política:** `NoApprovedPlayerMaps` ativo; `approved=false`; `operational=false`; nenhuma escrita, nenhuma permissão Win32 adicional.
