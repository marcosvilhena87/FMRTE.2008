# F30.3 — Recursos nativos em janelas WPF exibidas (offline)

## Escopo e limites de autorização

Base `main@923fb99cea1b37526a78124dd8fb54f237160346` após a validação pós-merge do F30.2-R. O ensaio é restrito a janelas WPF que exibem **apenas** perfis `Baseline` e `CollisionStress` inteiramente fictícios. Nunca abre `fm.exe`, acessa saves ou outro processo, usa offsets, memória externa, chamadas Win32 de acesso remoto ou escrita.

## Protocolo

- Antes da medição, registra os contadores brutos e abre/fecha janelas fictícias de aquecimento em pares, até dois grupos consecutivos estáveis (máximo de oito pares); falha se não estabilizar. Depois executa **96 ciclos sequenciais medidos** no mesmo STA/Dispatcher, sem desligá-lo entre janelas.
- Chama `Show()` e exige `IsVisible`, `IsLoaded` e um `HwndSource` com `Handle != IntPtr.Zero` para comprovar a criação de janela nativa no contexto de CI Windows; `Close()` deve disparar exatamente uma vez `Closing` e `Closed`.
- Três sequências rotativas: busca sem resultado e reset; navegação/recuperação após erro; fechamento durante falha persistente de reset. Alterna perfis básicos e adversariais (3/6).
- Verifica um incremento exato do handler `Reset_Click` por clique, identidade fictícia e nenhuma reativação após callback enfileirado depois do fechamento.
- A cada oito janelas, drena a fila WPF em `ApplicationIdle`, força GC, verifica referências fracas e monitora `Process.GetCurrentProcess().HandleCount` **apenas do próprio executável de testes**, `GetGuiResources` USER/GDI e `GC.GetTotalMemory`.
- Limites conservadores de regressão: até quatro janelas retidas por `WeakReference` após cada coleta; até +128 handles de pico e +64 handles ao término; heap gerenciado até +64 MiB. **São limites experimentais**, não referência universal de vazamentos.
- Verifica que o Dispatcher permanece ativo durante todo o ensaio.

## Limites das conclusões

`Process.HandleCount` é uma contagem agregada de handles do próprio processo; as medições adicionais USER/GDI são separadas, mas não fazem inventário de todos os objetos de janela, recursos COM, HwndSource ou GPU. `HwndSource` confirma criação de HWND real durante `Show()`, sem reproduzir interação de usuário. `WeakReference`/GC não prova ausência universal de vazamentos. Este teste não realiza automação física de mouse/teclado, capturas de tela, uso real de `Application.Run`, exposição pública ou interação com o jogo. O resultado depende das características do runner Windows. Se houver falha no critério de encerramento da fonte HWND, a revisão deve investigar o comportamento exato antes de ajustar qualquer limiar.

## Segurança e governança

Permanece `NoApprovedPlayerMaps`, `approved=false`, `operational=false`, `approvedPlayerMaps=0`, `B03=PENDING-DECLARED`, `A12=GATE-BLOQUEADO`, `decision=NOT_MADE`, RA-01–RA-08 sem aceite. `FM2008.mapping` não pode ser alterado. CI e revisão de software não são aprovação humana de evidências históricas. PR **Draft** até todos os checks e revisão técnica independente; não fazer merge nesta fase.

## Achado inicial e correção de protocolo

A primeira execução Windows sem aquecimento encontrou +178 handles após oito janelas em relação ao início do processo, excedendo +128. Como a primeira exibição do WPF ativa recursos de inicialização, adicionou-se aquecimento determinístico por pares de janelas até estabilidade observável, com coleta/Dispatcher antes da linha de base. **O limite de +128 pico / +64 final foi preservado**, não relaxado. A validade da medição depende dos checks posteriores.

## F30.3-R — Correções das observações R01–R04

- **R01 — Sensibilidade a vazamento progressivo:** teste negativo injeta 48 `EventWaitHandle` reais **no próprio processo de testes**, mantendo os primeiros 24 e os 24 seguintes abertos. A mesma regra de tendência aplicada às janelas reais (`<=8` handles novos entre os últimos 24 ciclos do ensaio) deve **rejeitar** a evolução de um handle por ciclo. Os 48 handles de controle são fechados em `finally`. Durante o ensaio real de 96 janelas, verifica-se a tendência dos últimos 24 ciclos, com até +8 handles totais e até +4 objetos GDI e +4 USER adicionais; os antigos limites globais (+128/+64 handles; +64 MiB heap) permanecem.
- **R02 — Aquecimento observável:** registra contagens **brutas anteriores ao aquecimento** de handles, GDI e USER. Em seguida, cria janelas fictícias de aquecimento em pares, coleta e drena o Dispatcher, registra cada grupo e exige **dois pares consecutivos estáveis** (variação absoluta de até 8 handles, 4 GDI e 4 USER). No máximo oito pares: sem estabilidade o teste falha. O relatório conserva tanto a linha de base bruta quanto a linha de base pós-aquecimento e os deltas relativos a ambas; não suprime os custos da inicialização.
- **R03 — Diagnóstico específico:** duas importações restritas de `user32.dll` (`GetGuiResources` e `IsWindow`) medem somente **o próprio processo** e confirmam que o HWND exibido existe de verdade e que o HWND original deixa de existir após `Close()`. Há orçamento explícito para recursos USER/GDI nos checkpoints (+16) e evolução nos últimos 24 ciclos (+4).
- **R04 — Limite de proteção textual:** a guarda de CI continua proibindo assinaturas explícitas de acesso a processo alheio/memória/saves e agora permite **exatamente duas funções user32 declaradas**; uma terceira importação nativa é rejeitada. A mutação negativa é executada em texto em memória, não escrita no repositório. Essa verificação **não é análise semântica total**, não demonstra segurança de dependências/reflexão e requer revisão humana do diff.

Os valores de tolerância são parâmetros de regressão específicos do runner Windows, não prova absoluta de ausência de vazamentos nativos. `GetGuiResources` fornece contagem de objetos USER/GDI do próprio processo, não mede memória da GPU, COM ou arquivos externos. O controle de vazamento utiliza exclusivamente handles sintéticos criados pelo teste e fechados em `finally`. O PR continua em **Draft**, sem aprovação operacional.
