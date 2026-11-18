# Roadmap conjunto

| Marco | Objetivo | Entregável | Aceitação |
| --- | --- | --- | --- |
| M0 | Arquitetura e contratos | Repositórios documentados, schema e interfaces | CI do mapping válido |
| M1 | Ambiente | Solução .NET, build e teste unitário | Compilação Windows reproduzível |
| M2 | Detecção | Processos, hash e arquitetura | Build desconhecida bloqueada |
| M3 | Mapeamentos | Importação e validação segura | Rejeição de schema incompatível |
| M4 | Consulta | Primeira entidade, somente leitura | Valores repetíveis e verificados |
| M5 | Interface | Busca, filtros e detalhes | UI sem dependência de acesso nativo |
| M6 | Escrita experimental | Edição controlada em testes | Snapshot, limites, confirmação e releitura |
| M7 | Estabilização | Testes de regressão, pacote e release | Matriz de compatibilidade publicada |

## Prioridades
P0: integridade e identificação de build.
P1: mapeamento real de uma versão.
P2: experiência de navegação.
P3: edição reversível quando possível.

## Riscos e mitigação
- Build diferente: bloquear; manter matriz de hashes.
- Corrupção: leitura primeiro; backups e testes.
- Offsets instáveis: evidência por execução e por save.
- Plataforma: CI em Windows e testes de x86.
- Acoplamento: contratos versionados e portas no domínio.
