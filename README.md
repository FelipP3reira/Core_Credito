# Core de Crédito

Motor de análise e concessão de crédito: recebe uma proposta de empréstimo, decide se
aprova, sob quais condições, e guarda por que decidiu assim.

O ponto do projeto não é o CRUD. É o que um sistema de crédito precisa ter para
sobreviver a uma auditoria e a um reenvio de formulário: máquina de estados que não
aceita atalho, decisão explicável regra a regra, idempotência que aguenta duas
requisições simultâneas e CPF que não aparece em log nem em coluna de banco.

## Estado atual

A primeira fatia está pronta: a proposta nasce, entra em análise e pode ser consultada,
com idempotência, validação, limite de submissão e proteção do CPF já no lugar.

O motor de decisão, o cálculo de parcelas e a contratação ainda não existem. O que já
está escrito está testado; o que falta está listado no fim.

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
regra de negócio sem subir banco — e é por isso que 197 dos 213 testes rodam em menos de
um terço de segundo.

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
- Histórico de transições é append-only: não existe caminho de alteração nem de exclusão.

Auditoria de dependências (`dotnet list package --vulnerable --include-transitive`) sem
nenhuma ocorrência.

## O que ainda não está aqui

- Motor de regras, política versionada e laudo da decisão.
- Cálculo de parcelas em Price e SAC.
- Contratação, cronograma e liquidação.
- Autenticação e papéis. Enquanto não existirem, a renda fica fora de toda resposta.
- Atrás de proxy, o limite de submissão precisa de `ForwardedHeaders` com a lista de
  proxies confiáveis, senão o IP vira o do balanceador e o limite passa a valer para todo
  mundo junto.
- Backup do banco: ainda não documentado.

O `xunit` está na 2.9.3, marcada como legada pelo NuGet em favor da v3. É a versão que o
próprio template do .NET 10 gera, e a migração não traz ganho funcional aqui.
