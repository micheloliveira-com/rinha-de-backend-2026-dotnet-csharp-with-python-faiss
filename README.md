# Rinha de Backend 2026 - C# AOT + FAISS (Python)

## Visão geral

Arquitetura distribuída composta por duas APIs em C# compiladas com AOT
e um serviço separado em Python utilizando FAISS para busca por
similaridade vetorial.

Objetivo: alta performance, baixa latência e isolamento do componente de
busca.

## Post oficial detalhado

https://micheloliveira.com/blog/desafio-performance-rinha-backend-2026-insights-csharp-faiss/


------------------------------------------------------------------------

## Arquitetura

``` mermaid
flowchart LR
    client[Cliente]
    lb[HAProxy]

    subgraph api_cluster["APIs C# (AOT)"]
        api1[API 1]
        api2[API 2]
    end

    faiss[(FAISS Service - Python)]

    client --> lb
    lb --> api1
    lb --> api2

    api1 <--> faiss
    api2 <--> faiss

    api1 --> lb
    api2 --> lb
    lb --> client
```

------------------------------------------------------------------------

## Componentes

### APIs (C# AOT)

-   Duas instâncias stateless
-   Compilação Ahead-of-Time (AOT)
-   Responsabilidades:
    -   Receber requisições HTTP
    -   Normalizar dados
    -   Consultar FAISS
    -   Retornar resposta final

------------------------------------------------------------------------

### Serviço de Similaridade (FAISS - Python)

-   Motor de busca vetorial com FAISS (IVF FP16)
-   Indexação de embeddings em memória
-   Serviço independente das APIs

------------------------------------------------------------------------

## Fluxo de requisição

1.  Cliente envia requisição para HAProxy
2.  HAProxy distribui para API 1 ou API 2
3.  API processa requisição
4.  API consulta serviço FAISS
5.  FAISS retorna resultados de similaridade
6.  API monta resposta e retorna ao cliente via HAProxy

------------------------------------------------------------------------

## Execução

### FAISS service

``` bash
cd src/faiss-backend
bash init.sh
bash run.sh
```

------------------------------------------------------------------------

### APIs C#

Workspace / Solution:
``` bash
src/rinha-de-backend-2026-dotnet-csharp.code-workspace
src/rinha-de-backend-2026-dotnet-csharp.sln
```

### Resources oficiais base da execução

``` bash
src/faiss-backend/references.json.gz
src/backend/Resources/mcc_risk.json
src/backend/Resources/normalization.json
```

### Resources gerados com a base oficial references.json.gz

``` bash
src/faiss-backend/train/references.faiss
src/faiss-backend/train/labels.npy
```
------------------------------------------------------------------------

### HAProxy

-   round-robin