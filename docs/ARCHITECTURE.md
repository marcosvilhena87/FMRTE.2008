# Arquitetura técnica

## Componentes
```text
WPF Desktop -> Application -> Domain
                   |             ^
                   v             |
             Interfaces/ports ---+
                   ^
                   |
         Infrastructure.Windows
                   |
     Processo FM2008 (somente leitura inicialmente)

FM2008.mapping -> carregador/validador de contrato -> Application
```

## Projetos propostos
- `Domain`: `GameBuild`, `MappingManifest`, `FieldDefinition`, políticas de acesso e interfaces `IProcessReader`/`IMappingCatalog`.
- `Application`: detecção do processo, detecção da build, carregamento de mapas, consultas, preview de alteração e auditoria local.
- `Infrastructure.Windows`: descoberta de processos Windows, hash de executável, leitura de memória com verificação de fronteiras e abstrações testáveis.
- `Desktop`: views de conexão, busca, detalhes, edição e histórico; MVVM, sem acesso direto a APIs nativas.

## Fluxo de leitura
1. Descobrir processo e obter caminho do executável.
2. Calcular SHA-256 e arquitetura.
3. Selecionar manifesto **real e aprovado** por match exato.
4. Validar schema, limites e permissões.
5. Resolver entidade e endereço por mecanismo ainda a pesquisar.
6. Ler dados, decodificar e exibir; se falhar, bloquear operação.

## Fluxo de escrita (futuro, não implementado)
Somente após prova de conceito de leitura estável: verificar build, fazer snapshot, validar limites, conferir bytes esperados, pedir confirmação e reler resultado; abortar em qualquer divergência.

## Dependências e limites
A arquitetura é proposta; offsets, esquema de ponteiros, mecanismos para encontrar entidades e sistemas suportados ainda exigem investigação. Não confundir dados de save com memória de processo.
