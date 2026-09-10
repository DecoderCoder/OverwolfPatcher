# Do patch em disco ao IL em memória: investigando o OverwolfPatcher

Peguei um repositório antigo de OverwolfPatcher para entender por que ele tinha parado de funcionar nas versões atuais do Overwolf. O programa abria assemblies .NET, modificava métodos com Mono.Cecil e gravava as DLLs de volta. Minha investigação começou dentro dessa arquitetura: descobrir o que havia mudado e o que precisava ser corrigido.

Um dos experimentos acabou reduzindo o problema a uma operação quase vazia. Ler o Core e gravá-lo novamente, sem alterar deliberadamente seus métodos, já era suficiente para perder a assinatura exigida pelo launcher. Eu podia produzir IL que funcionava nos testes e, ainda assim, entregar um arquivo que o aplicativo recusava antes de iniciar.

Até chegar a esse teste, precisei separar coisas que, para mim, ainda estavam misturadas: o arquivo no disco, o código dentro dele, as verificações de carregamento e aquilo que o processador efetivamente executa. Eu não comecei sabendo como CLR, IL, JIT ou CLR profiling funcionavam. Fui precisando entender cada um conforme as tentativas deixavam de caber na minha explicação anterior.

O resultado foi uma implementação que mantém a DLL assinada intacta e entrega novos corpos de método ao CLR, em memória, antes da compilação JIT. O caminho até ela passou por uma reescrita menor, um teste de carregamento alternativo e uma sequência de redirecionamentos de métodos que falhou de maneiras bem diferentes.

## O que eu tinha herdado

O Git ajuda a evitar a impressão de que o projeto sempre foi um profiler. Estes são alguns marcos reais do histórico, selecionados com seus títulos originais:

```text
2eee788 2022-03-29 1.33 ( Patched Premium checking in all apps )
4fa7ffb 2024-09-12 total rewrite+upgrade to .net8 + rebrand
96f7e14 2025-03-18 Merge pull request #13 from Bluscream/patch-4
29fedac 2026-09-08 Remove Mono.Cecil 0.11.4 package and add compatibility probe tests
c49e2d7 2026-09-08 docs: document profiler workflow and organize launcher
```

Antes da modernização, o ponto de entrada registrava patches para Core, BL, CommonUtils, Subscriptions e Extensions. Fechava os processos do Overwolf, localizava a instalação pelo registro do Windows e percorria os patches. O código de Core procurava tipos e métodos por nome, apagava instruções de alguns deles e emitia substituições.

Em [ClientCore.cs](../../OverwolfPatcher/Patches/ClientCore.cs), a alteração terminava com estas linhas:

```csharp
fullPath.Backup(true);
overwolfCore.Write(fullPath.FullName);
Console.WriteLine(Utils.Pad("Patched successfully"));
```

Esse sucesso significava que o arquivo havia sido escrito. Ainda faltava saber se o Overwolf conseguiria carregá-lo.

Uma assembly é uma unidade de código .NET distribuída, neste caso, como DLL. Ela contém instruções intermediárias, chamadas IL ou CIL, junto com metadados que descrevem classes, métodos e tipos. Mono.Cecil permite ler e editar essa estrutura sem ter o código-fonte original. O patcher podia encontrar um método pelo nome e construir outro corpo para ele.

Para visualizar o nível em que isso acontece, um método ilustrativo que retorna `7` pode ter este corpo em IL:

```il
ldc.i4.7
ret
```

A primeira instrução coloca o inteiro `7` na pilha de avaliação. A segunda devolve esse valor ao chamador. Essa pilha é o lugar onde as instruções de IL deixam os operandos para as próximas instruções consumirem. Mais adiante aparecem chamadas, criação de objetos e desvios, mas a ideia de emitir uma sequência de operações continua a mesma.

O patch antigo fazia isso em escala maior: construía objetos de plano, preenchia propriedades e devolvia uma coleção. Havia detalhes concretos para revisar. Por exemplo, ele emitia `Ldc_R4`, um valor de ponto flutuante de 32 bits, antes de chamar `set_Price`; a API inspecionada na instalação atual recebia `System.Double`, de 64 bits. Corrigir a forma do método era uma preocupação legítima. Só que a investigação encontraria uma falha anterior à execução desse método.

O [commit 29fedac](https://github.com/koobzaar/OverwolfPatcher/commit/29fedac1e16cbb3b95190b33f8713978a4e754ce) merece cuidado ao ser lido: apesar do título, ele inclui o profiler nativo, o harness, os testes e os relatórios, tudo junto. A remoção se refere ao pacote 0.11.4 versionado; o código de experimentos offline ainda usa Cecil. As tentativas intermediárias não ganharam um commit cada. Para reconstruí-las, usei o [registro de tentativas](../attempt-log.md) e os relatórios. O commit seguinte organizou o launcher e moveu o fluxo antigo para `LegacyWorkflow.cs`.

## Diminuir o patch parecia um teste razoável

A instalação investigada era Overwolf `0.309.0.14`, com Outplayed `175.3.12981`. O aplicativo gerenciado usava .NET Framework 4.8. O projeto que eu havia herdado tinha passado por uma migração para .NET 8; o trabalho atual voltou a mirar `net48`. A versão usada para compilar o patcher e o runtime hospedado pelo aplicativo eram informações diferentes que precisavam ficar explícitas.

Em vez de continuar com a sequência ampla do legado, a investigação isolou dois métodos de consulta no Core: `GetExtensionSubscriptions` e `GetExtensionSubscriptionsIds`. Um devolvia planos detalhados; o outro, identificadores inteiros. O experimento atendia apenas ao identificador da extensão selecionada e preservava o corpo original para as demais. A checagem de login permanecia nos métodos que envolviam essas consultas.

Essa escolha veio da inspeção do consumidor. O Outplayed ainda tinha um provedor legado que consultava `getDetailedActivePlans()` e reconhecia o plano `61`. Também tinha um provedor Tebex, com consulta autenticada a um servidor. O agregador verificava login e combinava os resultados dos provedores com uma operação equivalente a OR. A presença do provedor novo, portanto, não bastava para concluir que o caminho legado havia deixado de participar das decisões locais.

Nesse momento, a hipótese era limitada: se o Core modificado carregasse, um resultado legado poderia alterar algumas decisões locais. A inspeção não demonstrava que a conta receberia uma assinatura no servidor, nem que cada recurso funcionaria.

Os testes offline deram resultados encorajadores. Uma assembly sintética, com tipos parecidos com os esperados, executava o IL gerado. Essa era a fixture: código controlado pelo teste, no qual eu podia conferir o resultado de cada alteração. Os testes verificavam o retorno detalhado, os IDs e o `double` de `Price`, além de manter o caminho de outra extensão. Também havia controles para recusar formatos de método inesperados, como uma mudança de tipo no retorno ou nos parâmetros, e impedir que uma restauração sobrescrevesse uma DLL alterada posteriormente por uma atualização.

No aplicativo real, porém, o teste restrito ao Core terminou aqui:

```text
Refusing to start - deployed assembly failed verification:
C:\Program Files (x86)\Overwolf\0.309.0.14\OverWolf.Client.Core.dll
```

O erro, preservado no [relatório de investigação](../compatibility-investigation-report.md), acontecia antes da inicialização do Outplayed. Login e consultas de assinatura nem tinham oportunidade de decidir alguma coisa. A validade do IL nos testes não respondia à pergunta que agora importava: por que o launcher rejeitava aquele arquivo?

## O experimento em que não mudar os métodos já quebrava tudo

Havia duas verificações de assinatura que eu precisava distinguir. Uma tentativa ampla anterior tinha produzido um dump envolvendo `CommonUtils`, com falha de strong name e código `0x8013141A`. Strong name faz parte da identidade de uma assembly .NET. O Core inspecionado não tinha chave pública de strong name, mas tinha uma assinatura Authenticode válida, com certificado da Overwolf. A ausência da primeira não dispensava a segunda.

A assinatura Authenticode permite verificar a integridade do conteúdo assinado e quem o assinou. Uma regravação posterior pode remover essa assinatura ou tornar sua verificação inválida. Continuar com os mesmos métodos não equivale a continuar com o mesmo arquivo assinado.

A inspeção do launcher mostrou onde isso era exigido. `Program.Main` registrava um observador de carregamento de assemblies e chamava `VerifyDeployedAssembliesOrFail()` antes de `Run()`. Havia verificações também durante a resolução das DLLs da versão ativa e após determinados carregamentos. Esses caminhos convergiam para `VerifyDeployedAssembly`, que usava `FileSignedByOverwolf` e a verificação de assinatura embutida via WinTrust.

Isso explicava a rejeição, mas ainda cabia um controle melhor: o patch tinha estragado alguma coisa além da assinatura? O experimento seguinte leu uma cópia limpa com Mono.Cecil e a escreveu sem mudanças deliberadas nos métodos. Depois, um processo separado invocou o helper limpo `WinTrust.VerifyEmbeddedSignature` nas cópias preservadas.

A [bateria de compatibilidade](../compatibility-test-battery.md) registra os resultados:

| Arquivo examinado | `accepted` | `broken` | Comparação dos métodos |
| --- | --- | --- | --- |
| Core limpo | `True` | `False` | Referência |
| Core com patch restrito | `False` | `False` | Dois corpos diferentes |
| Core apenas lido e regravado | `False` | `False` | Zero diferenças |

O comparador percorreu 16.992 métodos, incluindo tipos aninhados. Conferiu IL, propriedades dos métodos, variáveis locais e limites de regiões de exceção. Não comparou tudo: ficaram de fora recursos, outras partes dos metadados, layout do arquivo e assinaturas, entre outros detalhes. Zero diferenças nesse teste não é uma prova de equivalência completa da assembly. É um controle suficientemente específico para mostrar que a rejeição também acontecia sem diferenças nos métodos examinados.

O campo `broken=False` foi outra armadilha de interpretação. Nesse helper, ele só sinalizava uma categoria específica de erro nativo, a de digest inválido. Uma cópia sem assinatura podia ter `broken=False` e continuar rejeitada. Quem respondia se a verificação havia sido aceita era `accepted`.

O próprio instrumento de comparação precisou de correções. Uma primeira regravação sem o resolvedor de dependências adequado produziu um arquivo de zero bytes; suas diferenças foram descartadas. Depois, o comparador descobriu que `MethodDefinition.FullName` não era uma chave única para todos os overloads e passou a considerar a ordem de declaração. Os números acima são da execução corrigida.

O teste executava um predicado necessário do launcher, sem executar sua política inteira. Como esse predicado já rejeitava os candidatos, repetir a substituição ao vivo não traria uma resposta nova. O comando `apply` passou a recusar a versão conhecida como incompatível.

Ainda experimentei um caminho alternativo de carregamento. Numa cópia descartável da instalação, coloquei o candidato em `Locales` e removi o Core da posição habitual, para testar a ordem de procura já presente na configuração. O processo terminou com código `-2146232797`, sem uma sessão utilizável ou log novo do aplicativo. Esse resultado não identifica sozinho a causa exata da saída. Ele registra que o caminho alternativo testado não funcionou.

Eu também não consegui datar quando a verificação atual entrou no Overwolf. O histórico de 2022 demonstra a existência do patcher, mas não substitui um launcher antigo, conhecido como funcional, para comparação. O que ficou estabelecido foi a incompatibilidade da regravação com a instalação examinada em setembro de 2026.

## Mudar o destino de uma chamada parecia resolver a parte do disco

Com a regravação inviável, comecei a testar mudanças em memória. Para acompanhar o que falhou nessa etapa, precisei entender o intervalo entre a DLL e uma chamada de método.

O CLR, Common Language Runtime, é o runtime que carrega e executa o código gerenciado. No caminho com JIT, ele transforma IL em instruções nativas quando precisa compilar um método. JIT significa *just in time*. O corpo em IL e o código nativo resultante são representações diferentes do método. O .NET Framework também pode carregar imagens nativas pré-compiladas, o que será relevante para o profiler. A Microsoft descreve essas etapas no [processo de execução gerenciada](https://learn.microsoft.com/en-us/dotnet/standard/managed-execution-process).

Uma tentativa usou um `AppDomainManager` personalizado para entrar cedo, antes de `Main`, e redirecionar os dois métodos para implementações geradas em memória. Um AppDomain é um ambiente de execução de código gerenciado dentro do processo; seu gerenciador oferecia, naquele experimento, um ponto de inicialização antecipado.

O primeiro teste sintético parecia parcialmente funcionar: o método detalhado era redirecionado, mas o de IDs ainda retornava o resultado original. O método pequeno tinha sido incorporado ao chamador pelo JIT, numa otimização chamada *inlining*. Se o compilador copia as operações do método para dentro de quem o chama, aquela execução deixa de passar pela entrada que eu estava redirecionando.

Marcar os métodos da fixture com `NoInlining` permitiu testar o redirecionamento sem essa interferência. Isso resolveu uma condição do teste; não demonstrava que todos os chamadores reais estariam sob controle.

Em seguida apareceu um problema no caminho que eu queria preservar. Para outras extensões, a substituição deveria chamar a implementação original. Guardei um delegate criado a partir do `MethodInfo` antes da troca. Um delegate é uma referência chamável a um método; eu estava tratando essa referência como se ela congelasse a implementação daquele momento.

O teste mostrou outra coisa: o delegate acompanhava a entrada redirecionada. O fallback, esse caminho de retorno ao comportamento original, voltava à substituição, que chamava o fallback de novo. O processo terminou com `StackOverflowException`.

O fluxo abaixo é uma representação simplificada dessa recursão:

```text
substituição
  -> extensão diferente: chamar original salvo
     -> delegate segue a entrada redirecionada
        -> substituição
           -> extensão diferente: chamar original salvo
              -> ...
```

Tentar guardar o ponteiro anterior à troca abriu outra sequência de falhas. `Marshal.GetDelegateForFunctionPointer` recusou o delegate genérico `Func<,>`. Gerar uma classe de delegate não genérica com a assinatura exata passou desse obstáculo, mas a API então rejeitou o retorno de array gerenciado com `MarshalDirectiveException`.

O endereço de uma função não descreve, sozinho, como chamá-la. Chamador e chamado precisam concordar sobre passagem de argumentos, retorno e tratamento de referências gerenciadas. As tentativas estavam esbarrando na fronteira entre chamadas gerenciadas e a infraestrutura de marshaling para código nativo.

Ainda houve uma tentativa de emitir `calli`, a instrução de chamada indireta, usando o ponteiro salvo. A primeira versão nem compilou por usar o enum de convenção de chamada errado para a API escolhida. Após a correção para `ThisCall`, a preparação do método gerado terminou em `InvalidProgramException`. Compilar o gerador não garantia que o CLR aceitaria o programa que ele gerava.

O [registro das tentativas 4 a 9](../attempt-log.md) termina nessa abordagem. Hoje, [PremiumRuntime.cs](../../OverwolfPatcher.Runtime/PremiumRuntime.cs) conserva apenas a estrutura necessária para compatibilidade: o inicializador não instala redirecionamentos, e `Install` lança `NotSupportedException`. O arquivo preserva os nomes que configurações antigas podiam referenciar, mas recusa o mecanismo abandonado.

## Havia um ponto anterior à compilação

O redirecionamento me obrigava a lidar com código preparado, entradas de método e chamadores que podiam já ter incorporado a implementação antiga. A API de profiling do CLR oferecia outro ponto de intervenção: receber o aviso de que um método ia ser compilado e fornecer seu IL naquele momento.

A API de profiling permite que uma ferramenta acompanhe eventos internos do runtime. O CLR chama funções dessa ferramenta, os callbacks, quando os eventos acontecem. Entre eles está [`JITCompilationStarted`](https://learn.microsoft.com/en-us/dotnet/framework/unmanaged-api/profiling/icorprofilercallback-jitcompilationstarted-method), que informa o início da compilação. A operação [`SetILFunctionBody`](https://learn.microsoft.com/en-us/dotnet/framework/unmanaged-api/profiling/icorprofilerinfo-setilfunctionbody-method) permite fornecer outro corpo para um método ainda não compilado pelo JIT. O CLR atualiza suas estruturas para usar esse corpo. Eu podia trabalhar sobre o IL que entraria no compilador, antes de existir código nativo daquele método para redirecionar.

O profiler implementado no projeto é uma DLL nativa x64 em C++. O launcher prepara o processo de inicialização para que o CLR a carregue. A instrumentação se restringe ao `Overwolf.exe` autorizado e limpa as variáveis de profiling no filho, para não propagá-las indiscriminadamente aos próximos processos.

A separação que faltava no começo pode ser desenhada assim:

```text
Disco                              Processo instrumentado

Core original e assinado ---------> carregamento do módulo
        |                                    |
        v                                    v
verificação do arquivo             início da compilação do método
continua examinando                          |
os bytes originais                           v
                                   profiler fornece outro corpo IL
                                             |
                                             v
                                   JIT produz código nativo
                                             |
                                             v
                                   chamada usa o comportamento novo
```

O verificador continua encontrando o arquivo original. A intervenção ocorre nas estruturas usadas pelo runtime para compilar aqueles métodos. Essa separação explica por que a assinatura do arquivo pode continuar válida durante a experiência. Ela depende do caminho de verificação que foi inspecionado; não estabelece compatibilidade com qualquer versão futura ou qualquer outra checagem de integridade.

Antes de entregar IL, o adaptador confere a identidade do Core e os metadados esperados. Usa SHA-256 do arquivo, arquitetura e MVID, um identificador do módulo. Também confere os métodos associados aos tokens conhecidos. Um token é uma referência numérica numa tabela de metadados daquela assembly, e não um endereço universal de uma função. Outra compilação pode atribuir outro significado ao mesmo número.

Na implementação, o primeiro callback de um alvo dispara a preparação dos dois corpos. O trecho abaixo, extraído de `InstallBody` com as verificações de erro omitidas, mostra a transferência para o CLR:

```cpp
info->GetILFunctionBodyAllocator(module, &allocator);
BYTE *destination = reinterpret_cast<BYTE *>(
    allocator->Alloc(static_cast<ULONG>(body.size())));
CopyMemory(destination, body.data(), body.size());
hr = info->SetILFunctionBody(module, method, destination);
allocator->Release();
```

A memória vem do alocador fornecido pela API. O corpo contém cabeçalho e instruções no formato que o runtime espera. Aqui o projeto deixou de escrever uma nova DLL e passou a entregar ao CLR o material que ele vai compilar.

As experiências com inlining também aparecem no desenho atual. O profiler desativa inlining e o carregamento de imagens NGEN no processo instrumentado. NGEN é o mecanismo de imagens nativas pré-compiladas do .NET Framework; usar essas imagens permitiria executar código já compilado sem passar pelo caminho de JIT pretendido. As duas opções têm alcance no processo inteiro, embora a troca de IL seja restrita aos dois métodos. Seus efeitos estão definidos nas [flags de profiling do CLR](https://learn.microsoft.com/en-us/dotnet/framework/unmanaged-api/profiling/cor-prf-monitor-enumeration).

## Preservar o original sem voltar a chamar uma entrada alterada

O fallback atual evita a dependência que tinha causado recursão: o corpo original é copiado para o final do novo corpo. Um prefixo compara o identificador da extensão e decide entre devolver a fixture ou seguir para as instruções antigas.

Este IL é um esquema explicativo, com a construção do retorno abreviada:

```il
ldarg.0
call get_UID
ldstr "identificador da extensão alvo"
call string::op_Equality
brfalse ORIGINAL

// Construir e devolver o resultado local do experimento.
ret

ORIGINAL:
// Instruções originais, copiadas para cá.
```

`ldarg.0` coloca a instância na pilha; o getter obtém seu identificador. A comparação produz um booleano. Quando ele é falso, `brfalse` salta para o corpo original. Esse caminho segue dentro do próprio método, sem procurar uma implementação antiga através de um delegate.

Copiar instruções, porém, ainda exige cuidar do restante do corpo. Um método pode descrever variáveis locais e regiões de `try`, `catch`, `finally` ou filtros de exceção. Esses registros dizem ao CLR quais intervalos de instruções estão protegidos e onde tratar uma exceção.

Se acrescento um prefixo de tamanho `P`, o início do código antigo se desloca em `P` bytes. Desvios relativos inteiramente internos ao código antigo mantêm suas distâncias: origem e destino andam juntos. Já os offsets das regiões de exceção, medidos desde o início do código, precisam ser ajustados.

O construtor de corpos em [Profiler.cpp](../../native/Profiler.cpp) faz esse ajuste. Um detalhe especialmente fácil de perder está no campo que pode representar uma classe de exceção ou a posição de um filtro:

```cpp
const ULONG classOrFilter = (clause.flags & 0x1u) != 0
    ? clause.classOrFilter + static_cast<ULONG>(originalEntry)
    : clause.classOrFilter;
```

Quando o campo contém o offset de um filtro, ele se desloca. Quando contém um token de tipo para um `catch`, somar bytes corromperia a referência. O formato do campo depende da flag da cláusula.

O código também conserva a assinatura das variáveis locais e a opção de inicializá-las, recalcula o tamanho do corpo e acomoda a profundidade necessária da pilha de avaliação. Essas informações fazem parte do programa que o JIT recebe, mesmo que uma descompilação em C# as esconda.

Há um limite nessa preservação: o prefixo introduz uma chamada antecipada ao getter de UID, fora do corpo antigo. Preservar os bytes originais não prova que essa chamada extra seja indistinguível para toda implementação possível de extensão. O relatório já registrava essa ressalva; os testes sintéticos não a eliminam.

## Chegar ao método real ainda exigiu outros controles

Carregar o profiler, trocar o corpo e observar o aplicativo eram verificações separadas. O launcher atual permite executar uma baseline, a referência sem instrumentação, carregar só a infraestrutura em `bootstrap`, observar callbacks em `observe` e acrescentar as flags em `flags`. O modo `neutral` entrega os corpos originais pela mesma API de substituição. Depois vem o modo que modifica o retorno local.

O teste neutro me permitia verificar a mecânica da troca sem mudar a decisão da aplicação. Se ela falhasse nesse estágio, ainda haveria motivos para investigar inicialização, contexto de execução ou instalação do corpo antes de discutir os dados devolvidos.

O contexto de execução interferiu. Na conta restrita de automação, os testes encontraram `Failed to initialize CEF runtime`, associado ao acesso aos dados de CEF e Crashpad, componentes de navegador embutido e registro de falhas. O aplicativo falhava antes de chegar aos métodos de assinatura. O teste precisou da conta interativa que possuía o perfil do Overwolf para avançar até aquele ponto.

Havia ainda a distinção entre testar uma fixture e observar o Core real. O [harness x64](../../tests/ProfilerHarness/Program.cs), o programa que conduz esse teste, instancia uma classe sintética cujo retorno original usa o plano `7`. Ele chama os dois métodos e exige os resultados substituídos, incluindo estado e expiração do plano detalhado. Isso exercita o carregamento do profiler e a execução do IL em um ambiente controlado. A fixture não reproduz todos os métodos, chamadores e estados do aplicativo.

Os logs locais preservados de 8 de setembro registram depois a passagem pelo Core real. No modo neutro, a substituição ocorreu às `21:08:22` UTC. No modo de retorno modificado, este é o trecho literal relevante, sem o restante do log:

```text
2026-09-08T21:09:08.275Z JIT target started function=0x7FFEC3916450 token=0x60030B7
2026-09-08T21:09:08.284Z premium SetILFunctionBody detailed=ok ids=ok rollback=ok
2026-09-08T21:09:08.285Z Core JIT finished function=0x7FFEC3916450 token=0x60030B7 status=0x0
```

O token corresponde ao método detalhado na versão revisada. O log mostra que a API aceitou os dois corpos e que o JIT concluiu a compilação daquele método com sucesso. `rollback=ok` é o campo de estado da rotina; nessa execução bem-sucedida, não significa que houve uma restauração. Também não aparece aí uma execução independente do método de IDs. A aceitação de seu corpo pela API é uma evidência mais estreita.

O [README atual](../../README.md) registra o resultado observado na interface: o botão "Go Premium" desapareceu, os anúncios não estavam visíveis na sessão testada e recursos locais premium aparentavam estar disponíveis. A tela de plano continuava mostrando `Outplayed Core - Free`.

Essa combinação fazia sentido diante dos caminhos separados do aplicativo. O booleano de assinatura aceitava o resultado legado, enquanto a seleção agregada do plano exibido excluía planos legados ativos da Overwolf. O nome mostrado na conta e o estado consumido por algumas decisões locais não eram a mesma variável.

Ainda assim, ausência de anúncio isoladamente seria uma evidência fraca: a inspeção havia encontrado outras condições que também os ocultavam. E mudar um layout uma vez não demonstrava sua persistência após reiniciar. A validação registrada não estabeleceu essa persistência, nem benefícios dependentes do servidor, como retenção de uploads ou acesso remoto. Os relatórios anteriores também contêm propostas de testes que não chegaram a ser executados naquela etapa; tratei essas propostas como perguntas em aberto.

O que resolveu a incompatibilidade investigada foi encontrar um momento em que eu podia fornecer código ao runtime sem regravar o arquivo que o launcher verificava. O Core que antes era rejeitado permaneceu intacto no disco; dentro do processo instrumentado, o CLR aceitou outro corpo e o compilou. Depois de tantas tentativas de conservar uma chamada para a implementação antiga, o caminho que funcionou foi manter suas instruções no próprio corpo novo e deixar o JIT fazer a compilação.
