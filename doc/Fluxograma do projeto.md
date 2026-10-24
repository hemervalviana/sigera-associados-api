# SigeraAssociados API — Fluxogramas

Situação em 24/10/2026. Os diagramas estão em Mermaid (abrem no VS Code com extensão de Mermaid, no GitHub e no Azure DevOps) e também como imagem PNG nesta pasta.

## 1. O que foi feito até agora

Verde: base do projeto. Azul: trabalho de 24/10/2026. Laranja: próximos passos.

![Etapas](fluxograma-1-etapas.png)

```mermaid
flowchart TD
    A([Início: SigeraAssociados API<br/>.NET 10 · Clean Architecture]) --> B[Solução criada<br/>Api · Application · Domain · Infrastructure<br/>UnitTests · IntegrationTests]
    B --> C[Catálogo de sistemas<br/>1 PROASP · 2 SONASP · 3 ABSP · 4 GSP<br/>cabeçalho X-Sigera-Sistema]
    C --> D[Consulta de associados<br/>listar · por código · paginada com filtros e ordenação]
    D --> E[Banco de identidade separado<br/>SigeraIdentity + login JWT 15 min<br/>confirmação de e-mail e recuperação de senha via Gmail]
    E --> F[Permissões por escopo<br/>sistema · empresa · filial · carteira do corretor<br/>tabela UsuarioAcessos + cache 5 min]
    F --> G[Levantamento de pendências<br/>doc/SigeraAssociados API — Pendências gerais.md]
    G --> H[Confirmação por link<br/>cloudflared tunnel + chave UrlPublica:BaseUrl<br/>GET /api/auth/confirmar-email]
    H --> I[Gestão de usuários<br/>listar · consultar · trocar grupo · bloquear · excluir<br/>GET /api/conta · grupos centralizados]
    I --> J[Cadastro público removido<br/>só Administrador · Administradores · Gestores criam usuários]
    J --> K[Swagger Swashbuckle 10.3.1<br/>no lugar do OpenAPI · botão Authorize]
    K --> L[Login sa trocado por usr_sisgera_api<br/>script SQL de criação do login]
    L --> M[Limpeza<br/>WeatherForecast · Class1 · backups · RegistroRequest<br/>testes renomeados · .http com chamadas reais]
    M --> N{{Build OK<br/>Swagger em https://localhost:7001/swagger}}
    N --> O[/Próximos passos/]
    O --> O1[Rodar dotnet test]
    O --> O2[Rate limiting no login]
    O --> O3[Testes dos endpoints novos]
    O --> O4[Git + README]

    classDef feito fill:#e6f4ea,stroke:#2e7d32,color:#1b1b1b
    classDef hoje fill:#e8f0fe,stroke:#1a73e8,color:#1b1b1b
    classDef prox fill:#fff4e5,stroke:#e8710a,color:#1b1b1b
    class B,C,D,E,F feito
    class G,H,I,J,K,L,M,N hoje
    class O,O1,O2,O3,O4 prox
```

## 2. Como uma requisição percorre a API

Login no banco de identidade, validação do token, regras por tipo de rota e aplicação do escopo de acesso nas 4 bases.

![Requisição](fluxograma-2-requisicao.png)

```mermaid
flowchart TD
    U([Cliente / Swagger]) --> L[POST /api/auth/login]
    L --> ID[(SigeraIdentity<br/>usr_sisgera_api)]
    ID --> T[Token JWT com grupos]
    T --> R[Requisição com<br/>Authorization: Bearer]
    R --> AU{Token válido?}
    AU -- não --> E401[401]
    AU -- sim --> RT{Tipo de rota}

    RT -- /api/conta --> CO[Dados e acessos do usuário]
    RT -- /api/administracao/* --> PG{Grupo permitido?<br/>usuários: Admin · Gestores<br/>acessos e diagnóstico: só Admin}
    PG -- não --> E403a[403]
    PG -- sim --> ADM[Criar · listar · trocar grupo<br/>bloquear · excluir usuário]
    ADM --> ID

    RT -- /api/associados · /api/diagnostico --> MW[SigeraSystemMiddleware<br/>lê X-Sigera-Sistema 1..4]
    MW --> SA{Usuário tem acesso<br/>ao sistema?}
    SA -- não --> E403b[403]
    SA -- sim --> ES[EscopoAcessoService<br/>empresa · filial · carteira]
    ES --> REP[AssociadosRepository<br/>aplica o escopo na consulta]
    REP --> DB[(PROASP_SIG · SONASP_SIG<br/>ABSP_SIG · GSP_SIG<br/>somente leitura)]
    DB --> RES[Associados filtrados<br/>e paginados]

    classDef erro fill:#fdecea,stroke:#c62828,color:#1b1b1b
    class E401,E403a,E403b erro
```

## 3. Criação de usuário e confirmação por link

Fluxo do e-mail de confirmação com o cloudflared tunnel.

![Confirmação](fluxograma-3-confirmacao.png)

```mermaid
flowchart LR
    A[Admin ou Gestor<br/>POST /api/administracao/usuarios] --> B[EmailConfirmacaoService<br/>gera token do Identity]
    B --> C{UrlPublica:BaseUrl<br/>configurada?}
    C -- sim --> D[E-mail com botão<br/>Confirmar e-mail]
    C -- não --> E[E-mail só com o token<br/>uso manual no POST]
    S[scripts/iniciar-tunel-cloudflared.ps1] -. grava URL .-> C
    D --> F[Usuário clica no link]
    F --> G[cloudflared tunnel<br/>https://xxxx.trycloudflare.com]
    G --> H[API local<br/>GET /api/auth/confirmar-email]
    H --> I{Token válido?}
    I -- sim --> J[Conta confirmada<br/>login liberado]
    I -- não --> K[Página de erro<br/>pedir novo envio]
```
