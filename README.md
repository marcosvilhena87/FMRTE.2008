# FMRTE.2008

Editor Windows em desenvolvimento para leitura/edição assistida de dados do Football Manager 2008.

> Estado: **esqueleto de arquitetura**. Não há leitura ou escrita real de memória implementada.

## Dependência
Consome contratos versionados do repositório [FM2008.mapping](https://github.com/marcosvilhena87/FM2008.mapping). Hash e versão do executável devem coincidir exatamente com um manifesto validado.

## Estrutura
- `src/FMRTE2008.Domain`: entidades, regras e portas independentes de plataforma.
- `src/FMRTE2008.Application`: casos de uso e orquestração.
- `src/FMRTE2008.Infrastructure.Windows`: adaptação de plataforma (futura leitura de processos).
- `src/FMRTE2008.Desktop`: aplicação WPF (futura UI).
- `tests/`: testes de domínio, contrato e integração.
- `docs/`: arquitetura, segurança e roadmap.

## Princípios
- Separação estrita entre **visualização**, **casos de uso**, **mapeamento** e **acesso ao processo**.
- **Somente leitura por padrão** e builds desconhecidas bloqueadas.
- Sem endereços de memória embutidos no código.
- Dados ilustrativos jamais usados contra processo real.
- Escrita futura exige confirmação, snapshot, validação e leitura posterior.

Veja [arquitetura](docs/ARCHITECTURE.md), [segurança](docs/SAFETY.md) e [roteiro](docs/ROADMAP.md).

## Ambiente alvo
Windows moderno e SDK .NET 8 (proposta inicial); execução do FM2008 em sistemas legados deve ser pesquisada separadamente. Não há compatibilidade funcional comprovada.
