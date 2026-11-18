# Estratégia de testes

## Testes unitários
- `BuildGate`: rejeitar ID vazio, arquitetura diferente, hash inválido e catálogo não aprovado.
- `Domain`: validar entidades sem dependências de Windows.
- `Application`: testar casos de uso contra adaptadores falsos.

## Testes de integração (futuros)
- Hash real do executável em ambiente controlado.
- Catálogo e manifesto de mapping publicados e versionados.
- Adaptador Windows: leitura de processo de teste, nunca escrita no jogo inicialmente.
- Regressão por build/patch e múltiplos saves.

## Critério de segurança
Nenhum teste unitário deve depender de instalação do Football Manager 2008 ou escrever memória do processo.
