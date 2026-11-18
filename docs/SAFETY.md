# Segurança e integridade dos dados

## Política inicial
**Não implementar escrita no primeiro marco funcional.** Evitar qualquer alteração do processo até comprovar integridade em builds identificadas e ambientes de teste.

## Controles mínimos
- Não conectar a processo desconhecido.
- Nunca inferir offsets por aproximação quando a build não corresponde.
- Não aceitar manifesto com schema incompatível.
- Separar leitura de escrita com interfaces e permissões explícitas.
- Operações de escrita (futuras): limites, transação lógica, snapshot, confirmação do usuário, verificação de bytes, releitura e registro auditável.
- Salvar backup do jogo antes dos testes; não garantir recuperação após corrupção de memória.
- Tratamento robusto de erros, timeouts e acesso negado.
- Testar com cópias de saves e dados não sensíveis.

## Ameaças a estudar
ASLR, diferenças de patches, ponteiros inválidos, dados desalocados, desalinhamento, arquitetura 32/64 bits e condições de corrida.
