# Identificação da build 52395 sem habilitar acesso à memória

## Componentes
- `Infrastructure.Windows/ExecutableFingerprint.cs`: lê um arquivo local PE32 x86, calcula SHA-256 e consulta `FileVersionInfo`.
- `Application/SupportedBuildCatalog.cs`: compara o hash exato, versão de arquivo, FilePrivatePart e arquitetura.
- `MemorySimulation/BuildRecognitionTests.cs`: testes sem binários reais.

## Build reconhecida
- `fm.exe` SHA-256 `157f484be2ead4ab27006d1002c2c666a7228197b88b14c8d026e76f31b1d62d`
- `FileVersion=8.0.2` / `FilePrivatePart=52395` / `x86`
- Variante do editor legado: `8.0.2v1`.

**Identificação NÃO significa aprovação dos offsets.** `MemoryMapApproved` continua `false`. Nenhuma UI conecta-se ao processo; nenhum endereço candidato é utilizado pelo adaptador nativo de leitura.

## Critérios para liberar integração com memória real
1. Selecionar mapa aprovado do repositório `FM2008.mapping`, com hash exato e contrato versionado.
2. Demonstrar repetibilidade dos dados com saves de teste e verificação independente de campos.
3. Implementar fábrica de sessão que exija identidade e aprovação, e testes de negativas.
4. Manter modo leitura inicialmente; não oferecer escrita.
