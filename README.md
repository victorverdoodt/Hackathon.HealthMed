# Hackathon HealthMed

Hackathon HealthMed é um MVP para agendamento de consultas médicas, desenvolvido com base em Domain-Driven Design (DDD) e projetado para alta escalabilidade e desempenho.  
Este repositório contém a implementação da API, a configuração para testes (usando Testcontainers para PostgreSQL, Redis, NBomber para testes de carga, etc.) e uma sugestão de arquitetura para a implantação na nuvem.

> **Importante:**  
> - A API está configurada para rodar na porta **8080**.  
> - O frontend (Next.js/React) **ainda não foi desenvolvido**; a seção de Frontend apresenta apenas uma sugestão para futuras implementações.  
> - A arquitetura de hospedagem apresentada é uma sugestão para o MVP, utilizando serviços AWS (Elastic Beanstalk, RDS, ElastiCache, S3, CloudFront) dentro de uma VPC.

---

## Índice

- [Tecnologias Utilizadas](#tecnologias-utilizadas)
- [Arquitetura da Aplicação](#arquitetura-da-aplicação)
- [Arquitetura de Hospedagem em Nuvem (Sugestão)](#arquitetura-de-hospedagem-em-nuvem-sugestão)
- [Como Rodar o Projeto](#como-rodar-o-projeto)
  - [Executando com Docker Compose](#executando-com-docker-compose)
  - [Executando a API Diretamente](#executando-a-api-diretamente)
- [Testes](#testes)
  - [Testes Unitários e de Integração](#testes-unitários-e-de-integração)
  - [Testes de Carga](#testes-de-carga)
- [Relatório de Teste de Carga](#relatório-de-teste-de-carga)
- [Documentação Técnica Adicional](#documentação-técnica-adicional)
- [Implantação](#implantação)
- [Contribuição](#contribuição)
- [Licença](#licença)

---

## Tecnologias Utilizadas

- **ASP.NET Core** – API desenvolvida em ASP.NET Core.
- **Entity Framework Core** – Acesso a dados com PostgreSQL.
- **PostgreSQL (RDS ou Testcontainers)** – Banco de dados relacional.
- **Redis (ElastiCache)** – Cache para melhorar o desempenho.
- **Hangfire** – Processamento de tarefas em background (ex.: consolidação de estatísticas).
- **NBomber** – Ferramenta para testes de carga (simulando até 20.000 usuários concorrentes).
- **Testcontainers.Postgres** – Execução de testes unitários e de integração contra um PostgreSQL real em container.
- **xUnit** – Framework para testes.
- **(Sugestão futura) Next.js/React** – Frontend moderno e responsivo.
- **AWS Elastic Beanstalk** – Hospedagem da API com auto scaling.
- **AWS S3 & CloudFront** – Hospedagem e distribuição do site estático.
- **AWS VPC** – Rede isolada para segurança e alta disponibilidade.

---

## Arquitetura da Aplicação

A aplicação é dividida em camadas, cada uma com responsabilidades bem definidas:

### Domain Layer
- **Entidades:**  
  - Doctor, Patient, Appointment, DoctorScheduleRule, DoctorReview, DoctorStatistics.
- **Enums:**  
  - ScheduleType, FrequencyType, AppointmentStatus, Specialty.

### Infrastructure Layer
- **SchedulingContext:**  
  - Contexto do Entity Framework Core para acesso ao banco de dados.
- **Repositories:**  
  - Exemplo: AppointmentRepository, que abstrai o acesso aos dados.

### Application Layer
- **Serviços:**  
  - SchedulingService, NotificationContextService, CacheService.
- **Helpers e DTOs:**  
  - PasswordHasher, DTOs para comunicação.

### API Layer
- **Controllers:**  
  - AuthController, AppointmentsController, DoctorController, DoctorsController, ReviewsController, ConsultationController.
- **Injeção de Dependências:**  
  - Configurada via ASP.NET Core DI.

![Application Architecture](./images/application_architecture.png)  
*Placeholder: Diagrama da Arquitetura da Aplicação*

Essa divisão permite separar responsabilidades, facilitar testes e garantir que cada camada possa evoluir de forma independente.

---

## Arquitetura de Hospedagem em Nuvem (Sugestão)

A arquitetura de hospedagem sugerida para o MVP utiliza serviços gerenciados da AWS:

- **API:**  
  Hospedada no **AWS Elastic Beanstalk** com auto scaling, garantindo alta disponibilidade.
  
- **Banco de Dados:**  
  O **PostgreSQL** é hospedado no **AWS RDS**, garantindo alta confiabilidade e gerenciamento simplificado.
  
- **Cache:**  
  O **AWS ElastiCache (Redis)** é utilizado para armazenamento de dados frequentemente acessados.
  
- **Frontend:**  
  Desenvolvido com **Next.js/React** (sugestão futura) e hospedado como site estático no **AWS S3**, distribuído via **AWS CloudFront**.
  
- **Rede:**  
  Todos os componentes críticos (API, RDS, ElastiCache) operam dentro de uma **AWS VPC** para segurança e isolamento.

![Cloud Hosting Architecture](./images/cloud_architecture.png)  
*Placeholder: Diagrama da Arquitetura de Hospedagem em Nuvem*

Essa solução foi escolhida para oferecer alta escalabilidade, segurança e desempenho, aproveitando os serviços gerenciados da AWS para reduzir a complexidade operacional.
