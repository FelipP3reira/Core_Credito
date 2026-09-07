# Core de Crédito

Motor de análise e concessão de crédito: recebe uma proposta de empréstimo, decide se
aprova, sob quais condições, e guarda por que decidiu assim.

O ponto do projeto não é o CRUD. É o que um sistema de crédito precisa ter para
sobreviver a uma auditoria e a um reenvio de formulário: máquina de estados que não
aceita atalho, decisão explicável regra a regra, idempotência que aguenta duas
requisições simultâneas e CPF que não aparece em log nem em coluna de banco.

## Estado atual

O ciclo fecha: a proposta nasce, é analisada e recebe um laudo dizendo regra a regra por
que foi aprovada ou negada, é contratada com o cronograma congelado, e liquida sozinha
quando a última parcela cai.

```
POST /propostas                        cadastra em rascunho (exige Idempotency-Key)
POST /propostas/{id}/analise           decide e grava o laudo
POST /propostas/{id}/simulacao         mostra o cronograma que essa proposta teria
POST /propostas/{id}/cancelamento      desiste, enquanto não houver decisão
GET  /propostas/{id}                   detalhe, trilha de estados e laudo
POST /propostas/{id}/contrato          assina e congela o cronograma
GET  /propostas/{id}/contrato          cronograma com vencimentos e pagamentos
POST /propostas/{id}/contrato/parcelas/{n}/pagamento   quita uma parcela
```

O que já está escrito está testado; o que falta está listado no fim.

## Como rodar

Precisa de .NET 10 e Docker.

```bash
cp .env.example .env
```

Preencha os três segredos do `.env`:

```bash
openssl rand -base64 48   # ProtecaoDeCpf__Pepper
openssl rand -base64 32   # ProtecaoDeCpf__ChaveDeCifra - precisa ter exatamente 32 bytes
```

A senha do SA aparece duas vezes no arquivo: em `SENHA_SA`, que o compose usa ao criar o
container, e dentro de `ConnectionStrings__Banco`. Têm que ser a mesma.

```bash
docker compose up -d
dotnet tool restore
dotnet ef database update --project src/Credito.Infraestrutura --startup-project src/Credito.Api
dotnet run --project src/Credito.Api
```

### Testes

```bash
dotnet test
```

Os testes de unidade não precisam de nada. Os de integração sobem um SQL Server próprio
via Testcontainers, então precisam do Docker no ar — e não encostam no banco de
desenvolvimento.

## Camadas

```
Credito.Dominio          zero dependências. Entidades, regras, máquina de estados.
Credito.Aplicacao        casos de uso e portas (interfaces de repositório e de cifra).
Credito.Infraestrutura   EF Core, migrações, proteção de CPF, máscara de log.
Credito.Api              rotas, validação de borda, limite de submissão, tradução de erro.
```

A dependência anda em um sentido só: `Api → Aplicacao → Dominio`, e a `Infraestrutura`
entra pela `Aplicacao`. A `Api` só encosta na `Infraestrutura` no `Program.cs`, para
amarrar a injeção de dependência.

O domínio não referencia EF, ASP.NET nem nada da Microsoft. É isso que permite testar
regra de negócio sem subir banco — e é por isso que 2.920 dos 2.974 testes rodam em pouco
mais de um segundo. Só os 54 de integração precisam de Docker.

## Máquina de estados

```
Rascunho ──▶ EmAnalise ──▶ Aprovada ──▶ Contratada ──▶ Liquidada
    │            │             │
    │            ├──▶ Negada   └──▶ Expirada
    └────────────┴──▶ Cancelada
```

A tabela de transições é explícita e a ausência de uma aresta é proibição, não omissão.
`Negada` é terminal de propósito: reanalisar exige proposta nova, senão o laudo da decisão
anterior seria sobrescrito e a trilha de auditoria perderia o sentido. `Aprovada` expira
porque taxa aprovada tem validade.

O estado tem `private set` e só muda pelos métodos de intenção do próprio agregado, que
passam todos pelo mesmo portão antes de gravar a transição no histórico. O portão valida
antes de mexer em qualquer campo — agregado que lança no meio da mudança fica inválido na
memória de quem capturou o erro.

O teste percorre os 64 pares do produto cartesiano contra uma cópia da tabela escrita à
parte, a partir do desenho do fluxo. Duplicar o dado é o objetivo: divergência entre as
duas versões acusa erro de digitação na tabela real.

## O motor de decisão

Cinco regras, cada uma em sua classe: restrição cadastral, score mínimo, valor dentro do
produto, prazo dentro do produto e comprometimento de renda. Um exemplo de laudo, como sai
da API:

```
[ok ] RESTRICAO_CADASTRAL       Sem restricao cadastral.
[ok ] SCORE_MINIMO              Score 843 atende o minimo de 500.
[ok ] VALOR_DENTRO_DO_PRODUTO   Valor de R$ 20.000,00 dentro da faixa do produto (R$ 1.000,00 a R$ 100.000,00).
[ok ] PRAZO_DENTRO_DO_PRODUTO   Prazo de 24 meses dentro da faixa do produto (6 meses a 96 meses).
[ok ] COMPROMETIMENTO_DE_RENDA  Parcela de R$ 1.045,48 compromete 12,3% da renda, dentro do limite de 30%.
```

### O motor não tem atalho, e isso é o ponto

Todas as regras rodam, sempre, mesmo depois da primeira reprovação. Parar antes seria mais
rápido e gravaria uma causa quando existiam três — e quem corrigisse só aquela voltaria a
ser negado sem entender por quê. Auditoria completa é o motivo de o sistema existir;
economizar avaliação de regra em memória não paga esse preço.

Uma Specification devolvendo `bool` não serviria: o requisito é guardar *por que*, e um
booleano perde isso no caminho. Cada regra devolve motivo, valor observado e limite
exigido. Os números ficam nulos nas regras que não têm número, como restrição cadastral —
preencher com zero só atrapalharia quem consultasse o laudo depois.

### A análise acontece em duas fases

As regras teriam dependência entre si se cada uma fosse buscar o que precisa: o
comprometimento precisa da parcela, que precisa da taxa, que sai da faixa de score. Então
a primeira fase monta tudo — consulta ao birô, taxa da faixa, simulação do cronograma — e
congela num registro imutável. A segunda avalia as regras contra ele, em qualquer ordem.

O ganho é concreto: cada regra vira função pura de um registro. O teste unitário é montar
o contexto na mão e chamar `Avaliar`, sem simulação de dependência, sem banco, sem ordem.

### A política é versionada, e a decisão guarda qual versão aplicou

Não é refinamento. Mudar o score mínimo no mês que vem tornaria toda negativa passada
incompreensível se a decisão não registrasse sob qual versão foi tomada — e explicar
decisão antiga é exatamente o que a auditoria vem cobrar.

As faixas de taxa precisam cobrir a escala inteira de score, de 0 a 1000, e a política
recusa buraco e sobreposição na construção. Cobrir até o fundo tem um motivo específico:
score abaixo do mínimo ainda recebe taxa, a da pior faixa, e com ela dá para montar o
cronograma e avaliar **todas** as regras. A proposta é negada pela regra de score, e não
por faltar taxa para calcular.

### Não existe decidir sem registrar por quê

`Aprovar` e `Negar` exigem o laudo na assinatura. Não é documentação — é o compilador
impedindo que exista caminho de código capaz de mudar o estado sem gravar o parecer. O
agregado ainda recusa laudo de outra proposta e laudo cuja conclusão discorda da transição
pedida: aprovar carregando um parecer que reprovou seria o jeito mais silencioso de fraudar
a auditoria.

Análise e laudo entram numa transação só. Proposta aprovada sem laudo gravado seria o pior
resultado possível deste sistema.

### Cronograma impossível é tratado diferente em cada caminho

Na análise vira reprovação explicada no laudo, porque proposta impossível merece parecer,
não erro de servidor. Na simulação vira 400, porque quem simula quer o cronograma e não há
laudo para explicar a ausência dele.

### O birô é um substituto, e é determinístico de propósito

Não há integração real. O substituto deriva score e restrição de um resumo do próprio CPF,
então o mesmo solicitante recebe sempre a mesma resposta. Sem isso, reanalisar a mesma
proposta daria resultado diferente a cada vez e nenhuma demonstração seria reproduzível.

A porta recebe o CPF em texto claro de propósito: birô de verdade precisa do número, e
deixar isso na assinatura obriga quem chama a passar por `Revelar` — que é justamente o
ponto que uma revisão de segurança quer encontrar de primeira. A integração real entra
trocando a classe no registro de dependências, sem tocar em nada do domínio.

## A contratação

Até a assinatura, o cronograma é projeção: recalculado a cada consulta com a taxa da
política vigente naquele instante. Na contratação ele vira dívida, e por isso é gravado
parcela a parcela. Política que mude depois não altera contrato já assinado.

### A taxa contratada é a da decisão, não a de hoje

A proposta foi aprovada sob uma condição e é essa que se contrata. Política que mudou entre
a análise e a assinatura vale para a próxima análise, não para uma aprovação já dada. Há
teste que muda o score do birô entre os dois passos e exige que a taxa do contrato não se
mexa.

### Vencimento soma meses, não trinta dias

Parcela vence no mesmo dia do mês. Somar trinta dias corridos faria a data escorregar
progressivamente — três parcelas depois já não é mais o mesmo dia. `AddMonths` ainda encurta
o dia quando o mês seguinte não o tem, então um vencimento em 31 de janeiro cai em 28 de
fevereiro e volta a 31 em março, em vez de virar 3 de março para sempre.

### A liquidação não é um botão

Acontece quando a última parcela cai. A conferência do estado deixa o reenvio do mesmo
pagamento inofensivo: a segunda vez encontra o contrato já quitado e a proposta já
liquidada, e não faz nada.

`Liquidar` exige o contrato quitado na própria assinatura. Sem essa conferência, bastaria
chamar o método para dar uma dívida aberta como paga.

### Pagamento carrega chave, e a chave fica guardada

Reenvio com a mesma chave é repetição e não muda nada; chave diferente sobre parcela já paga
é recusado. Sem guardar a chave só daria para responder "já está paga" — e o cliente que
perdeu a resposta por timeout não saberia se foi ele mesmo quem pagou.

Pagar fora de ordem é aceito: quem antecipa a última parcela não deveria ser barrado.

### Cancelar só vale antes de haver decisão

Depois de aprovada ou negada existe laudo, e cancelar apagaria o motivo de uma decisão já
tomada. Para aprovação que o solicitante não quer mais, o caminho é deixar expirar.

### A aprovação expira quando alguém tenta usá-la

Taxa aprovada tem prazo — contratar hoje uma aprovação de seis meses atrás seria conceder
crédito com condição que ninguém mais ofereceria. A validade é campo da política.

A expiração acontece no momento da tentativa de contratação, e não por varredura periódica.
É o único momento em que a diferença importa, e evita um processo de fundo cuja única função
seria mudar um estado que ninguém estava olhando.

### O contrato não é navegação da proposta

A chave estrangeira existe, mas sem navegação do lado da proposta. Carregar uma proposta
para analisá-la não pode arrastar noventa e seis parcelas junto.

Como a contratação mexe em dois repositórios, o `Salvar` saiu de dentro deles e virou uma
unidade de trabalho explícita. Com um `Salvar` em cada repositório ficaria ambíguo quem de
fato grava, e a resposta certa — os dois compartilham o mesmo contexto, um `Salvar` basta —
só era descobrível lendo a infraestrutura.

## Decisões e trade-offs

### O agregado não guarda o CPF em texto claro

A proposta guarda um par de strings opacas: um hash HMAC-SHA256 para busca e o número
cifrado com AES-GCM. O CPF em claro não existe nem em memória depois do cadastro, o que
zera o vazamento por despejo de memória, serialização acidental e log de objeto inteiro.

Custa uma coisa: a validade do número deixa de ser reconferível dentro do agregado. Ela
passa a ser garantida pelo único caminho que produz a forma protegida, que exige um CPF já
validado na assinatura.

São dois campos porque cada um resolve o que o outro não resolve. A cifra usa nonce novo a
cada chamada, então o mesmo CPF nunca gera o mesmo texto — bom para privacidade, inútil
para procurar no banco. O hash é determinístico e vira índice, mas não volta.

O pepper é o que segura o hash de pé: são cerca de um bilhão de CPFs válidos, então um
dump do banco sem o pepper não permite enumerar, e um dump com ele permite. Por isso ele
mora fora do banco e fora do repositório. Um KDF lento no lugar do HMAC daria folga mesmo
com o pepper vazado, ao custo de tornar lenta toda busca por CPF — troca que não compensa
no volume deste projeto.

O caminho de leitura decifra e mascara em seguida. Parece rodeio, mas evita uma terceira
coluna com a máscara e duas versões do mesmo dado no banco.

*Always Encrypted do SQL Server seria a resposta de banco de verdade. Ficou de fora porque
o custo é gerenciamento de chave e configuração de driver, não código.*

### A renda não é cifrada

De propósito, e essa é a parte que costuma ser feita errada por reflexo. A renda entra no
cálculo de comprometimento e em agregação; cifrada, todo relatório vira varredura em
memória. Ela fica em coluna clara, fora de log, fora de listagem e fora da resposta da API.
Cifrar aqui seria teatro de segurança com custo real de consulta.

### Idempotência mora no índice único

O cadastro exige o cabeçalho `Idempotency-Key`. A garantia não é uma consulta antes do
insert — entre a consulta e o insert cabem duas requisições com a mesma chave, e as duas
passariam. É o índice único no banco.

O SQL Server não tem o equivalente do `ON CONFLICT DO NOTHING` do Postgres, então o caminho
é inserir, capturar o erro 2601 ou 2627 e reler a linha existente. Se a releitura não acha
ninguém com aquela chave, a colisão foi em outro índice e o erro sobe — engolir ali
esconderia defeito de verdade.

A proposta guarda também uma impressão do conteúdo enviado. Mesma chave com dados
diferentes é erro do cliente e recebe 409: devolver a proposta já gravada com 200 seria
pior que falhar, porque o cliente acharia que os dados novos entraram.

Primeiro envio responde 201, reenvio idêntico responde 200. O cliente distingue sem
precisar comparar o corpo.

### Chave gerada no cliente, e o que isso quase custou

O `Id` é um GUID versão 7, ordenado no tempo. O SQL Server ordena as linhas em disco pelo
índice agrupado da chave primária, então GUID aleatório espalharia inserção por todas as
páginas e fragmentaria o índice.

Só que gerar a chave no construtor exige dizer isso ao EF com `ValueGeneratedNever`. Sem
essa declaração o EF trata chave `Guid` como gerada na inserção e cai na heurística "chave
preenchida significa registro existente": uma transição recém-criada, anexada a uma
proposta já rastreada, virava `UPDATE` em vez de `INSERT`. O `UPDATE` não achava linha,
atingia zero linhas e subia como conflito de concorrência — um erro que não tem nenhuma
relação com a causa.

### A sobra do arredondamento vai toda para a última parcela

Somar parcelas arredondadas a centavo nunca fecha exatamente com o valor financiado. Em
vez de afrouxar o teste para uma margem, a última parcela recebe o que sobrou — que é o
que banco faz. Assim o teste assere igualdade: a soma das amortizações é o financiado, sem
tolerância. Em dez mil a 1% ao mês em doze meses isso aparece como onze parcelas de
R$ 888,49 e uma última de R$ 888,47.

Tudo em `decimal`, nunca `double`. A potência da fórmula da Price é feita por multiplicação
repetida porque `Math.Pow` só existe em `double`, e levar dinheiro para ponto flutuante
binário e trazer de volta é a origem clássica do centavo que falta no fim do contrato.

O arredondamento é comercial, não bancário: o padrão do .NET leva a metade exata para o par
mais próximo, o que desvia do que o cliente confere no boleto.

### Nem toda combinação de valor, taxa e prazo existe em centavos

Duas entradas quebravam o cronograma em silêncio, e as duas foram achadas por uma varredura
sobre valor, taxa e prazo — não por caso escolhido a dedo:

- **R$ 7,00 em 360 meses.** 7/360 dá 0,0194, que arredonda para 0,02, e 359 parcelas de dois
  centavos passam dos sete reais. O saldo devedor virava negativo.
- **R$ 1.292,40 a 1,89% ao mês em 360 meses.** A parcela de R$ 24,46 mal cobre os R$ 24,43
  de juros do primeiro mês. O meio centavo em que a própria parcela foi arredondada se
  acumula e a dívida zera na parcela 352, deixando oito parcelas fantasma.

Tentei barrar isso por fórmula na entrada e errei duas vezes seguidas — o limite depende de
valor, taxa e prazo ao mesmo tempo, e cada tentativa cobria só um dos casos. A conferência
passou a acontecer parcela a parcela, onde o problema de fato aparece: é recusada tanto a
parcela que passa do saldo quanto a que não amortiza nem um centavo. Recusar é melhor que
devolver cronograma curto ou com parcela zerada no fim.

### A máscara de log filtra por nome, não por tipo

O vazamento típico não é alguém logando um objeto de domínio inteiro. É um
`LogInformation("renda {Renda}", ...)` escrito para diagnosticar um problema e nunca
removido. Esse caso só é alcançável pelo nome da propriedade, e a máscara percorre
estrutura, lista e dicionário até o fim.

Há teste conferindo os dois lados: que o dado sensível não sai, e que campo inofensivo
continua saindo. Log que esconde tudo não serve para diagnóstico.

### O registro de requisição fica por fora do tratador de erros

Por dentro, uma proposta inexistente aparecia no log como 500 com pilha inteira em vez do
404 que o cliente de fato recebeu — alarme falso garantido. Em compensação o tratador
passou a registrar a recusa, senão o erro de domínio não deixaria rastro nenhum.

### Configuração lida no provedor, não capturada no registro

Configuração lida na hora de montar a injeção de dependência congela o valor que existia
antes das fontes adicionadas depois. Isso não é teoria: os testes de integração subiam o
container, rodavam a migração no banco de desenvolvimento e gravavam tudo lá — passando,
sem provar isolamento nenhum.

### A validação de borda repete o agregado de propósito

Não é duplicação por descuido. Na borda o objetivo é dizer ao cliente qual campo está
errado e por quê; no agregado é impedir que proposta inválida exista, venha de onde vier.
Idade mínima é a exceção que mora só no domínio: capacidade civil é lei, não política de
crédito ajustável.

## Segurança

- CPF cifrado com AES-GCM em repouso, buscado por HMAC com pepper, sempre mascarado na
  resposta. `ToString()` do tipo devolve a versão mascarada, então interpolar o objeto num
  log não vaza nada.
- Adulteração do texto cifrado é detectada pela tag do GCM. Sem ela, mexer no campo
  devolveria lixo em silêncio em vez de estourar.
- Chave de cifra fora do tamanho certo derruba a subida do processo, não o primeiro
  cadastro.
- Toda entrada validada no servidor com FluentValidation; CPF é um tipo que não pode ser
  construído inválido, com dígito verificador e descarte de sequências repetidas.
- Limite de submissão por IP, com `Retry-After` na recusa e fila zero.
- Zero SQL cru: tudo passa pelo EF Core, parametrizado.
- Segredos em `.env`, fora do git, com `.env.example` versionado.
- HSTS e redirecionamento de HTTPS fora de desenvolvimento; `nosniff`, `no-referrer` e
  `DENY` de enquadramento em toda resposta.
- Histórico de transições e laudo de decisão são append-only: não existe caminho de
  alteração nem de exclusão em nenhum dos dois.
- A trilha de transições é numerada, não ordenada por data. A análise faz duas transições
  na mesma requisição, com o mesmo instante gravado nas duas, e ordenar por data deixaria a
  trilha sair em ordem indefinida justamente na hora em que ela precisa ser lida como
  sequência.

Auditoria de dependências (`dotnet list package --vulnerable --include-transitive`) sem
nenhuma ocorrência.

## O que ainda não está aqui

- Integração com birô de crédito de verdade. Hoje há um substituto determinístico, e a
  troca é só no registro de dependências.
- Cadastro de nova versão de política pela API. Hoje a versão 1 vem semeada na migração e
  uma versão nova exigiria migração ou inserção manual.
- Cobrança de fato: boleto, Pix, conciliação. O sistema registra que a parcela foi paga,
  mas não é quem recebe.
- Juros e multa por atraso. A parcela vencida e não paga é consultável pelo índice de
  vencimento, mas nada cobra encargo sobre ela.
- Autenticação e papéis. Enquanto não existirem, a renda fica fora de toda resposta.
- Atrás de proxy, o limite de submissão precisa de `ForwardedHeaders` com a lista de
  proxies confiáveis, senão o IP vira o do balanceador e o limite passa a valer para todo
  mundo junto.
- Backup do banco: ainda não documentado.

O `xunit` está na 2.9.3, marcada como legada pelo NuGet em favor da v3. É a versão que o
próprio template do .NET 10 gera, e a migração não traz ganho funcional aqui.
