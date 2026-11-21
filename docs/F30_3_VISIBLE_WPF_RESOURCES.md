# F30.3 — Recursos nativos em janelas WPF exibidas (offline)

## Escopo e limites de autorização

Base `main@923fb99cea1b37526a78124dd8fb54f237160346` após a validação pós-merge do F30.2-R. O ensaio é restrito a janelas WPF que exibem **apenas** perfis `Baseline` e `CollisionStress` inteiramente fictícios. Nunca abre `fm.exe`, acessa saves ou outro processo, usa offsets, memória externa, chamadas Win32 de acesso remoto ou escrita.

## Protocolo

- Executa **48 ciclos sequenciais** no mesmo STA/Dispatcher, sem desligá-lo entre janelas.
- Chama `Show()` e exige `IsVisible`, `IsLoaded` e um `HwndSource` com `Handle != IntPtr.Zero` para comprovar a criação de janela nativa no contexto de CI Windows; `Close()` deve disparar exatamente uma vez `Closing` e `Closed`.
- Três sequências rotativas: busca sem resultado e reset; navegação/recuperação após erro; fechamento durante falha persistente de reset. Alterna perfis básicos e adversariais (3/6).
- Verifica um incremento exato do handler `Reset_Click` por clique, identidade fictícia e nenhuma reativação após callback enfileirado depois do fechamento.
- A cada oito janelas, drena a fila WPF em `ApplicationIdle`, força GC, verifica referências fracas e monitora `Process.GetCurrentProcess().HandleCount` **apenas do próprio executável de testes** e `GC.GetTotalMemory`.
- Limites conservadores de regressão: até quatro janelas retidas por `WeakReference` após cada coleta; até +128 handles de pico e +64 handles ao término; heap gerenciado até +64 MiB. **São limites experimentais**, não referência universal de vazamentos.
- Verifica que o Dispatcher permanece ativo durante todo o ensaio.

## Limites das conclusões

`Process.HandleCount` é uma contagem global de handles do próprio processo, não inventário discriminado de USER/GDI, nem medição específica da integridade dos objetos de janela, recursos COM, HwndSource ou GPU. `HwndSource` confirma criação de HWND real durante `Show()`, sem reproduzir interação de usuário. `WeakReference`/GC não prova ausência universal de vazamentos. Este teste não realiza automação física de mouse/teclado, capturas de tela, uso real de `Application.Run`, exposição pública ou interação com o jogo. O resultado depende das características do runner Windows. Se houver falha no critério de encerramento da fonte HWND, a revisão deve investigar o comportamento exato antes de ajustar qualquer limiar.

## Segurança e governança

Permanece `NoApprovedPlayerMaps`, `approved=false`, `operational=false`, `approvedPlayerMaps=0`, `B03=PENDING-DECLARED`, `A12=GATE-BLOQUEADO`, `decision=NOT_MADE`, RA-01–RA-08 sem aceite. `FM2008.mapping` não pode ser alterado. CI e revisão de software não são aprovação humana de evidências históricas. PR **Draft** até todos os checks e revisão técnica independente; não fazer merge nesta fase.
