using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.ValueObjects;
using Monburan.Web.Models;

namespace Monburan.Web.Data;

/// <summary>
/// Massa de dados para o ambiente público de homologação (dev): empresas, pessoas, vagas, currículos e candidaturas com
/// nomes e textos realistas, para testar o site como se fosse produção. Liga com <c>Seed:DemoData=true</c> e nunca roda
/// em Production (Program.cs). As contas usam o domínio <see cref="Domain"/>, que não recebe e-mail, e a senha vem de
/// <c>Seed:DemoPassword</c> (secret do ambiente; sem ela o seed não roda).
/// Os dados são gerados com semente fixa: rodar de novo num banco vazio gera a mesma massa, e num banco já semeado não faz nada.
/// </summary>
public static class DemoSeedData
{
    public const string Domain = "demo.monburan.com.br";
    private const string TenantId = "default";
    private const string AdminRole = "Admin";

    private static readonly (string City, string State)[] Cities =
    [
        ("São Paulo", "SP"), ("Campinas", "SP"), ("Ribeirão Preto", "SP"), ("Rio de Janeiro", "RJ"), ("Niterói", "RJ"),
        ("Belo Horizonte", "MG"), ("Uberlândia", "MG"), ("Curitiba", "PR"), ("Londrina", "PR"), ("Porto Alegre", "RS"),
        ("Florianópolis", "SC"), ("Joinville", "SC"), ("Salvador", "BA"), ("Recife", "PE"), ("Fortaleza", "CE"),
        ("Goiânia", "GO"), ("Brasília", "DF"), ("Manaus", "AM"), ("Belém", "PA"), ("Vitória", "ES")
    ];

    private static readonly string[] FirstNames =
    [
        "Ana", "Bruno", "Carla", "Diego", "Eduarda", "Felipe", "Gabriela", "Henrique", "Isabela", "João", "Larissa",
        "Lucas", "Mariana", "Mateus", "Natália", "Otávio", "Patrícia", "Rafael", "Renata", "Rodrigo", "Sabrina", "Thiago",
        "Vanessa", "Vinícius", "Beatriz", "Caio", "Débora", "Fernanda", "Gustavo", "Juliana", "Leonardo", "Letícia",
        "Marcelo", "Priscila", "Ricardo", "Tatiane", "Wesley", "Yasmin", "Alexandre", "Camila"
    ];

    private static readonly string[] LastNames =
    [
        "Almeida", "Barbosa", "Cardoso", "Costa", "Dias", "Ferreira", "Gomes", "Lima", "Martins", "Melo", "Moreira",
        "Nascimento", "Oliveira", "Pereira", "Ribeiro", "Rocha", "Santos", "Silva", "Souza", "Teixeira", "Vieira", "Araújo",
        "Carvalho", "Correia", "Freitas", "Mendes", "Monteiro", "Nunes", "Pinto", "Ramos"
    ];

    private sealed record Company(string Name, string Slug, string Description, string Category, int CityIndex);

    private static readonly Company[] Companies =
    [
        new("Construtora Horizonte Azul", "horizonteazul", "Construtora de médio porte com obras residenciais e comerciais no Sudeste.", "Engenharia e manufatura", 5),
        new("Supermercados Bom Preço", "bompreco", "Rede regional de supermercados com 18 lojas e centro de distribuição próprio.", "Varejo e lojas", 0),
        new("Clínica Vida Plena", "vidaplena", "Clínica multiprofissional com atendimento em fisioterapia, nutrição e psicologia.", "Saúde e bem-estar", 7),
        new("Transportes Rota Sul", "rotasul", "Transportadora com frota própria atendendo o Sul e o Sudeste.", "Logística e transporte", 9),
        new("Nuvem Clara Tecnologia", "nuvemclara", "Software house que desenvolve sistemas de gestão para pequenas empresas.", "Tecnologia da informação", 10),
        new("Escola Caminho do Saber", "caminhodosaber", "Escola particular de educação infantil e ensino fundamental.", "Educação", 12),
        new("Contabilidade Precisa", "contprecisa", "Escritório de contabilidade com 25 anos de atuação em empresas do Simples Nacional.", "Contabilidade e finanças", 15),
        new("Padaria Pão Dourado", "paodourado", "Padaria e confeitaria artesanal com três unidades.", "Serviços gerais", 1),
        new("Metalúrgica Forte Aço", "forteaco", "Indústria metalúrgica de peças sob encomenda para o setor automotivo.", "Indústria e produção", 11),
        new("Agência Faísca Comunicação", "faisca", "Agência de comunicação e marketing digital para marcas regionais.", "Marketing e comunicação", 13),
        new("Hotel Mar Sereno", "marsereno", "Hotel de 120 quartos com centro de eventos à beira-mar.", "Atendimento ao cliente", 14),
        new("Pereira & Moura Advogados", "pereiramoura", "Escritório de advocacia trabalhista e empresarial.", "Jurídico", 16)
    ];

    private sealed record JobTemplate(
        string Category, string Title, string Description, string Requirements, string Responsibilities, string Benefits,
        decimal Min, decimal Max, EContractType Contract, string Mode, string Tags);

    private static readonly JobTemplate[] Templates =
    [
        new("Engenharia e manufatura", "Engenheiro(a) Civil de Obras", "Acompanhar a execução de obras residenciais, garantindo prazo, custo e qualidade.", "Graduação em Engenharia Civil, CREA ativo, experiência com gestão de obras.", "Planejar etapas, acompanhar equipes e fornecedores, controlar medições.", "Vale-refeição, plano de saúde, PLR.", 7000, 10000, EContractType.Clt, "OnSite", "Obras, AutoCAD, Gestão"),
        new("Engenharia e manufatura", "Mestre de Obras", "Coordenar as equipes de campo em obras de até 10 pavimentos.", "Experiência comprovada como mestre de obras, leitura de projetos.", "Distribuir tarefas, conferir materiais, garantir o uso de EPI.", "Vale-transporte, cesta básica, plano de saúde.", 4500, 6000, EContractType.Clt, "OnSite", "Construção civil, Liderança"),
        new("Varejo e lojas", "Operador(a) de Caixa", "Atendimento no caixa com foco em agilidade e cordialidade.", "Ensino médio completo, disponibilidade para escala 6x1.", "Registrar compras, receber pagamentos, fechar o caixa.", "Vale-alimentação, desconto em compras, plano odontológico.", 1600, 1900, EContractType.Clt, "OnSite", "Atendimento, Caixa"),
        new("Varejo e lojas", "Repositor(a) de Mercadorias", "Manter as gôndolas abastecidas e organizadas.", "Ensino fundamental completo.", "Repor produtos, conferir validade, organizar estoque.", "Vale-transporte, vale-alimentação.", 1500, 1750, EContractType.Clt, "OnSite", "Estoque, Organização"),
        new("Varejo e lojas", "Gerente de Loja", "Responsável pelos resultados e pela equipe de uma loja da rede.", "Experiência em gestão de varejo, superior completo ou cursando.", "Gerir equipe, metas, estoque e indicadores da loja.", "Bônus por resultado, plano de saúde, carro da empresa.", 6000, 8500, EContractType.Clt, "OnSite", "Gestão, Varejo, Metas"),
        new("Saúde e bem-estar", "Fisioterapeuta", "Atendimento em fisioterapia ortopédica e esportiva.", "Graduação em Fisioterapia e CREFITO ativo.", "Avaliar pacientes, montar planos de tratamento, registrar evolução.", "Comissão por atendimento, horário flexível.", 3500, 5500, EContractType.Pj, "OnSite", "Fisioterapia, Ortopedia"),
        new("Saúde e bem-estar", "Recepcionista de Clínica", "Recepção de pacientes e organização da agenda.", "Ensino médio completo, boa comunicação.", "Agendar consultas, confirmar horários, atender telefone e WhatsApp.", "Vale-transporte, vale-refeição.", 1700, 2100, EContractType.Clt, "OnSite", "Recepção, Agenda"),
        new("Saúde e bem-estar", "Nutricionista", "Atendimento nutricional individual e em grupos.", "Graduação em Nutrição e CRN ativo.", "Consultas, planos alimentares e palestras.", "Horário flexível, participação em congressos.", 3800, 5000, EContractType.Clt, "Hybrid", "Nutrição, Atendimento"),
        new("Logística e transporte", "Motorista Carreteiro", "Transporte de cargas entre os centros de distribuição do Sul e do Sudeste.", "CNH categoria E, curso MOPP, experiência em viagens longas.", "Conduzir o veículo, conferir carga e documentação, seguir rotas.", "Diárias, seguro de vida, plano de saúde.", 4200, 5800, EContractType.Clt, "OnSite", "CNH E, MOPP"),
        new("Logística e transporte", "Auxiliar de Logística", "Apoio às operações de carga, descarga e conferência.", "Ensino médio completo.", "Separar pedidos, conferir notas, operar leitor de código de barras.", "Vale-transporte, vale-refeição, PLR.", 1800, 2200, EContractType.Clt, "OnSite", "Armazém, Conferência"),
        new("Tecnologia da informação", "Desenvolvedor(a) .NET Pleno", "Desenvolvimento de APIs e sistemas web em C# para clientes de gestão.", "C#, ASP.NET Core, Entity Framework, SQL; Git.", "Implementar funcionalidades, revisar código, apoiar a sustentação.", "Trabalho remoto, auxílio home office, plano de saúde.", 8000, 11000, EContractType.Clt, "Remote", "C#, .NET, SQL"),
        new("Tecnologia da informação", "Analista de Suporte Técnico", "Suporte aos clientes do sistema de gestão por chat e telefone.", "Conhecimento em Windows, redes básicas e boa escrita.", "Atender chamados, registrar soluções, treinar clientes.", "Vale-refeição, plano de saúde, cursos.", 2500, 3300, EContractType.Clt, "Hybrid", "Suporte, Atendimento"),
        new("Tecnologia da informação", "Desenvolvedor(a) Front-end Júnior", "Construção de telas web responsivas para os produtos da empresa.", "HTML, CSS, JavaScript; noções de React ou Blazor.", "Implementar telas, corrigir bugs, escrever testes.", "Trabalho remoto, mentoria, auxílio educação.", 3500, 4800, EContractType.Clt, "Remote", "Front-end, JavaScript"),
        new("Educação", "Professor(a) de Matemática", "Aulas para o ensino fundamental II.", "Licenciatura em Matemática.", "Planejar e ministrar aulas, avaliar alunos, participar de reuniões pedagógicas.", "Bolsa de estudos para filhos, plano de saúde.", 3200, 4200, EContractType.Clt, "OnSite", "Docência, Matemática"),
        new("Educação", "Auxiliar de Sala", "Apoio à professora na educação infantil.", "Cursando Pedagogia.", "Acompanhar as crianças, preparar materiais, apoiar atividades.", "Vale-transporte, refeição no local.", 1500, 1800, EContractType.Internship, "OnSite", "Educação infantil"),
        new("Contabilidade e finanças", "Analista Contábil", "Rotinas contábeis e fiscais de empresas do Simples Nacional.", "Graduação em Ciências Contábeis, experiência com conciliações.", "Conciliar contas, apurar impostos, fechar balancetes.", "Plano de saúde, vale-refeição, day off no aniversário.", 3500, 4800, EContractType.Clt, "Hybrid", "Contabilidade, Fiscal"),
        new("Contabilidade e finanças", "Assistente Financeiro", "Contas a pagar e a receber e conciliação bancária.", "Ensino técnico ou superior em andamento, Excel intermediário.", "Lançar pagamentos, emitir boletos, conciliar extratos.", "Vale-refeição, vale-transporte.", 2200, 2800, EContractType.Clt, "OnSite", "Financeiro, Excel"),
        new("Serviços gerais", "Confeiteiro(a)", "Produção de bolos, tortas e doces finos.", "Experiência em confeitaria.", "Produzir receitas, decorar, controlar estoque de insumos.", "Refeição no local, vale-transporte.", 2200, 2800, EContractType.Clt, "OnSite", "Confeitaria"),
        new("Serviços gerais", "Atendente de Balcão", "Atendimento no balcão da padaria.", "Ensino fundamental completo, simpatia.", "Atender clientes, embalar produtos, organizar vitrine.", "Refeição no local, vale-transporte.", 1550, 1750, EContractType.Clt, "OnSite", "Atendimento"),
        new("Indústria e produção", "Soldador(a) MIG", "Soldagem de peças metálicas na linha de produção.", "Curso de soldagem e experiência com MIG.", "Soldar peças conforme desenho, inspecionar qualidade.", "Adicional de insalubridade, PLR, plano de saúde.", 2800, 3600, EContractType.Clt, "OnSite", "Solda, MIG"),
        new("Indústria e produção", "Operador(a) de Máquinas CNC", "Operação de tornos e centros de usinagem CNC.", "Curso técnico em mecânica, leitura de desenho técnico.", "Preparar máquina, operar, medir peças.", "PLR, plano de saúde, refeitório.", 3000, 4000, EContractType.Clt, "OnSite", "CNC, Usinagem"),
        new("Marketing e comunicação", "Social Media", "Planejamento e produção de conteúdo para redes sociais de clientes.", "Experiência com Instagram, TikTok e ferramentas de agendamento.", "Criar calendário, produzir textos, analisar resultados.", "Horário flexível, trabalho híbrido.", 2800, 3800, EContractType.Clt, "Hybrid", "Redes sociais, Conteúdo"),
        new("Marketing e comunicação", "Designer Gráfico", "Criação de peças para campanhas digitais e impressas.", "Domínio de Figma, Photoshop e Illustrator; portfólio.", "Criar peças, adaptar formatos, apoiar o atendimento.", "Trabalho híbrido, plano de saúde.", 3000, 4200, EContractType.Pj, "Hybrid", "Design, Figma"),
        new("Atendimento ao cliente", "Recepcionista de Hotel", "Atendimento a hóspedes na recepção 24 horas.", "Ensino médio completo, inglês intermediário.", "Check-in e check-out, reservas, atendimento ao hóspede.", "Alimentação no local, vale-transporte.", 1900, 2400, EContractType.Clt, "OnSite", "Hotelaria, Inglês"),
        new("Atendimento ao cliente", "Camareira", "Arrumação e limpeza dos apartamentos.", "Experiência em hotelaria é um diferencial.", "Arrumar quartos, repor enxoval e amenidades.", "Alimentação no local, vale-transporte.", 1550, 1750, EContractType.Clt, "OnSite", "Hotelaria, Limpeza"),
        new("Jurídico", "Advogado(a) Trabalhista", "Atuação contenciosa e consultiva na área trabalhista.", "OAB ativa e experiência em audiências.", "Elaborar peças, participar de audiências, atender clientes.", "Participação nos honorários, plano de saúde.", 6000, 9000, EContractType.Pj, "Hybrid", "Trabalhista, OAB"),
        new("Recursos humanos", "Analista de Departamento Pessoal", "Rotinas de folha de pagamento, admissões e eSocial.", "Experiência com folha e eSocial.", "Processar folha, admissões, férias e rescisões.", "Plano de saúde, vale-refeição.", 3200, 4200, EContractType.Clt, "Hybrid", "DP, eSocial, Folha"),
        new("Administrativo", "Assistente Administrativo", "Apoio às rotinas administrativas do escritório.", "Ensino médio completo, pacote Office.", "Organizar documentos, atender fornecedores, controlar agenda.", "Vale-transporte, vale-refeição.", 1900, 2400, EContractType.Clt, "OnSite", "Administrativo, Office"),
        new("Comercial e vendas", "Vendedor(a) Externo", "Prospecção e atendimento de clientes empresariais na região.", "CNH B e experiência em vendas consultivas.", "Visitar clientes, negociar, acompanhar pedidos.", "Comissão, ajuda de custo com combustível.", 2500, 6000, EContractType.Clt, "OnSite", "Vendas, Negociação")
    ];

    private static readonly (string Name, string Course)[] Schools =
    [
        ("Universidade Federal de Minas Gerais", "Engenharia Civil"), ("Universidade de São Paulo", "Administração"),
        ("Universidade Federal do Paraná", "Ciências Contábeis"), ("SENAI", "Técnico em Mecânica"),
        ("SENAC", "Técnico em Administração"), ("Universidade Federal da Bahia", "Nutrição"),
        ("PUC-Rio", "Comunicação Social"), ("Universidade Federal de Pernambuco", "Ciência da Computação"),
        ("Instituto Federal de Santa Catarina", "Técnico em Logística"), ("Universidade Estadual de Londrina", "Pedagogia")
    ];

    private static readonly string[] ApplicationNotes =
    [
        "Tenho experiência na função e disponibilidade imediata.",
        "Moro perto da empresa e tenho interesse em crescer na área.",
        "Busco uma oportunidade para aplicar o que aprendi no curso.",
        "Já atuei em empresa do mesmo segmento por três anos.",
        "Tenho flexibilidade de horário, inclusive aos fins de semana."
    ];

    public static async Task InitializeAsync(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        IConfiguration configuration,
        ILogger logger)
    {
        var password = configuration["Seed:DemoPassword"];
        if (string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("Seed de demonstração ligado sem Seed:DemoPassword: nada foi criado");
            return;
        }

        if (await context.Advertisers.AnyAsync(a => a.Email.EndsWith("@" + Domain)))
            return; // já semeado

        var random = new Random(2026);
        var now = DateTime.UtcNow;

        // Endereços: um por cidade
        var addresses = Cities.Select(c => new Address
        {
            Id = Id.Create(Guid.NewGuid()),
            TenantId = TenantId,
            City = c.City,
            State = c.State,
            Country = "BR",
            District = "Centro"
        }).ToList();
        context.Addresses.AddRange(addresses);

        // Empresas
        var advertisers = Companies.Select((c, i) => new Advertiser
        {
            Id = Id.Create(Guid.NewGuid()),
            TenantId = TenantId,
            Name = c.Name,
            Email = $"rh.{c.Slug}@{Domain}",
            Phone = $"({11 + i * 3 % 80}) 3{random.Next(100, 999)}-{random.Next(1000, 9999)}",
            PersonType = EPersonType.Company,
            AddressId = addresses[c.CityIndex].Id,
            Status = EAdvertiserStatus.Active,
            IsEmailVerified = true,
            CompanyDescription = c.Description,
            ActivePlan = i % 4 == 0 ? "Premium" : "Free"
        }).ToList();
        context.Advertisers.AddRange(advertisers);

        // Pessoas candidatas: combinações únicas de nome e sobrenome
        var candidates = new List<Candidate>();
        var usedEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (candidates.Count < 40)
        {
            var first = FirstNames[random.Next(FirstNames.Length)];
            var last = LastNames[random.Next(LastNames.Length)];
            var email = $"{Slug(first)}.{Slug(last)}@{Domain}";
            if (!usedEmails.Add(email))
                continue;

            var city = random.Next(addresses.Count);
            candidates.Add(new Candidate
            {
                Id = Id.Create(Guid.NewGuid()),
                TenantId = TenantId,
                Name = $"{first} {last}",
                Email = email,
                Phone = $"(11) 9{random.Next(1000, 9999)}-{random.Next(1000, 9999)}",
                DateOfBirth = new DateTime(random.Next(1975, 2005), random.Next(1, 13), random.Next(1, 28), 0, 0, 0, DateTimeKind.Utc),
                AddressId = addresses[city].Id,
                Status = EAccountStatus.Active,
                IsEmailVerified = true,
                ActivePlan = candidates.Count % 5 == 0 ? "Premium" : "Free"
            });
        }
        context.Candidates.AddRange(candidates);
        await context.SaveChangesAsync();

        // Vagas: duas por modelo, em empresas da mesma área quando houver; algumas pausadas, expiradas ou em moderação
        var jobs = new List<Job>();
        for (var i = 0; i < Templates.Length * 2; i++)
        {
            var template = Templates[i % Templates.Length];
            var companyIndex = Array.FindIndex(Companies, c => c.Category == template.Category);
            if (companyIndex < 0 || i >= Templates.Length)
                companyIndex = random.Next(Companies.Length);
            var company = Companies[companyIndex];
            var (city, state) = i < Templates.Length ? Cities[company.CityIndex] : Cities[random.Next(Cities.Length)];

            var (status, moderation, expires) = (i % 13) switch
            {
                3 => (EJobStatus.Paused, EModerationStatus.Approved, now.AddDays(20)),
                7 => (EJobStatus.Expired, EModerationStatus.Approved, now.AddDays(-5)),
                11 => (EJobStatus.Active, EModerationStatus.Pending, now.AddDays(30)),
                _ => (EJobStatus.Active, EModerationStatus.Approved, now.AddDays(random.Next(10, 75)))
            };

            jobs.Add(new Job
            {
                Id = Id.Create(Guid.NewGuid()),
                TenantId = TenantId,
                AdvertiserId = advertisers[companyIndex].Id,
                Title = template.Title,
                Description = template.Description,
                Requirements = template.Requirements,
                Responsibilities = template.Responsibilities,
                Benefits = template.Benefits,
                Category = template.Category,
                Tags = template.Tags,
                ContractType = template.Contract,
                NumberOfOpenings = random.Next(1, 4),
                Status = status,
                ExpirationDate = expires,
                Location = new JobLocation { City = city, State = state, Type = template.Mode, IsRemote = template.Mode != "OnSite" },
                Salary = new SalaryRange { Min = template.Min, Max = template.Max, Display = random.Next(5) > 0, Period = ESalaryPeriod.Month },
                Moderation = new ModerationInfo { Status = moderation, ModerationDate = moderation == EModerationStatus.Approved ? now.AddDays(-random.Next(1, 20)) : null },
                Boost = new JobBoost { IsActive = false },
                Highlight = new JobHighlight { IsActive = i % 9 == 0 },
                Counters = new JobCounters { Views = random.Next(20, 400) }
            });
        }

        // Vagas externas (sem anunciante): candidatura por WhatsApp, e-mail ou site, como as que vêm de sugestões
        jobs.Add(ExternalJob("Auxiliar de Cozinha", "Prefeitura — Banco de Empregos", "Serviços gerais", "Recife", "PE", EApplicationChannel.WhatsApp, "(81) 99876-5432", now));
        jobs.Add(ExternalJob("Agente de Limpeza", "Cooperativa Mãos Unidas", "Serviços gerais", "Fortaleza", "CE", EApplicationChannel.Email, $"selecao.maosunidas@{Domain}", now));
        jobs.Add(ExternalJob("Estoquista", "Atacado Central", "Logística e transporte", "Goiânia", "GO", EApplicationChannel.ExternalUrl, "https://monburan.com.br/vagas", now));
        context.Jobs.AddRange(jobs);
        await context.SaveChangesAsync();

        // Currículos: um por pessoa, com experiência, formação, habilidades e idiomas
        var resumes = new List<Resume>();
        foreach (var candidate in candidates)
        {
            var template = Templates[random.Next(Templates.Length)];
            var school = Schools[random.Next(Schools.Length)];
            var start = now.AddYears(-random.Next(2, 9));
            var resume = new Resume
            {
                Id = Id.Create(Guid.NewGuid()),
                TenantId = TenantId,
                CandidateId = candidate.Id,
                Title = template.Title,
                Summary = $"Profissional com experiência em {template.Category.ToLowerInvariant()}, comprometido(a) com qualidade e bom atendimento.",
                IsActive = true
            };
            resume.ExperienceHistory.Add(new Experience
            {
                Company = Companies[random.Next(Companies.Length)].Name,
                Position = template.Title,
                StartDate = start,
                IsCurrent = random.Next(3) == 0,
                EndDate = null,
                Description = template.Responsibilities
            });
            if (!resume.ExperienceHistory.First().IsCurrent)
                resume.ExperienceHistory.First().EndDate = start.AddYears(random.Next(1, 3));
            resume.EducationHistory.Add(new Education
            {
                Institution = school.Name,
                Course = school.Course,
                Level = school.Course.StartsWith("Técnico") ? EEducationLevel.Technical : EEducationLevel.Bachelor,
                Status = random.Next(4) == 0 ? EEducationStatus.Ongoing : EEducationStatus.Completed,
                StartDate = start.AddYears(-4),
                EndDate = start.AddYears(-1)
            });
            foreach (var tag in template.Tags.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                resume.Skills.Add(new ResumeSkill
                {
                    Id = Id.Create(Guid.NewGuid()),
                    TenantId = TenantId,
                    ResumeId = resume.Id,
                    Skill = tag,
                    Level = random.Next(3) switch { 0 => "Básico", 1 => "Intermediário", _ => "Avançado" },
                    YearsOfExperience = random.Next(1, 8)
                });
            }
            resume.Languages.Add(new Language { Name = "Português", Level = ELanguageLevel.Native });
            if (random.Next(3) == 0)
                resume.Languages.Add(new Language { Name = "Inglês", Level = (ELanguageLevel)random.Next(0, 3) });
            resumes.Add(resume);
        }
        context.Resumes.AddRange(resumes);
        await context.SaveChangesAsync();

        // Candidaturas nas vagas internas ativas, com histórico e notificação de mudança de status
        var openJobs = jobs.Where(j => j.AdvertiserId is not null && j.Status == EJobStatus.Active && j.Moderation!.Status == EModerationStatus.Approved).ToList();
        var applications = new List<Application>();
        var pairs = new HashSet<(Guid, Guid)>();
        var notifications = new List<Notification>();
        while (applications.Count < 120)
        {
            var candidateIndex = random.Next(candidates.Count);
            var job = openJobs[random.Next(openJobs.Count)];
            if (!pairs.Add((candidates[candidateIndex].Id.Value, job.Id.Value)))
                continue;

            var applied = now.AddDays(-random.Next(1, 30)).AddHours(-random.Next(0, 23));
            var status = random.Next(10) switch
            {
                < 4 => EApplicationStatus.Sent,
                < 7 => EApplicationStatus.Viewed,
                < 8 => EApplicationStatus.Selected,
                _ => EApplicationStatus.Rejected
            };
            var application = new Application
            {
                Id = Id.Create(Guid.NewGuid()),
                TenantId = TenantId,
                CandidateId = candidates[candidateIndex].Id,
                JobId = job.Id,
                ResumeId = resumes[candidateIndex].Id.Value,
                Status = status,
                AppliedAt = applied,
                Notes = ApplicationNotes[random.Next(ApplicationNotes.Length)]
            };
            application.StatusHistory.Add(History(application, EApplicationStatus.Sent, applied, "Candidatura enviada"));
            if (status != EApplicationStatus.Sent)
            {
                application.ViewedAt = applied.AddDays(1);
                application.StatusHistory.Add(History(application, EApplicationStatus.Viewed, application.ViewedAt.Value, "Currículo visualizado pela empresa"));
            }
            if (status == EApplicationStatus.Selected)
            {
                application.SelectedAt = applied.AddDays(3);
                application.StatusHistory.Add(History(application, status, application.SelectedAt.Value, "Selecionado(a) para entrevista"));
            }
            if (status == EApplicationStatus.Rejected)
            {
                application.RejectedAt = applied.AddDays(3);
                application.StatusHistory.Add(History(application, status, application.RejectedAt.Value, "Perfil não seguiu no processo"));
            }
            if (status is EApplicationStatus.Selected or EApplicationStatus.Rejected)
            {
                notifications.Add(new Notification
                {
                    Id = Id.Create(Guid.NewGuid()),
                    TenantId = TenantId,
                    RecipientId = candidates[candidateIndex].Id.Value.ToString(),
                    Title = status == EApplicationStatus.Selected ? "Você foi selecionado(a)!" : "Atualização da sua candidatura",
                    Message = status == EApplicationStatus.Selected
                        ? $"A empresa selecionou você na vaga \"{job.Title}\". Fique de olho no seu e-mail e telefone."
                        : $"Sua candidatura para \"{job.Title}\" não seguiu no processo. Continue se candidatando!",
                    Type = ENotificationType.ApplicationStatus,
                    IsRead = random.Next(2) == 0
                });
            }
            job.Counters.Applications++;
            applications.Add(application);
        }
        context.Applications.AddRange(applications);
        context.Notifications.AddRange(notifications);

        // Vagas salvas e perfil profissional para parte das pessoas
        foreach (var candidate in candidates.Where((_, i) => i % 3 == 0))
        {
            var job = openJobs[random.Next(openJobs.Count)];
            context.SavedJobs.Add(new SavedJob { Id = Id.Create(Guid.NewGuid()), TenantId = TenantId, CandidateId = candidate.Id, JobId = job.Id });
            context.CareerProfiles.Add(new CareerProfile
            {
                Id = Id.Create(Guid.NewGuid()),
                TenantId = TenantId,
                CandidateId = candidate.Id,
                DesiredRole = job.Title,
                Category = job.Category,
                City = job.Location?.City,
                State = job.Location?.State,
                Mode = job.Location?.Type,
                ContractType = job.ContractType,
                MinimumSalary = job.Salary?.Min
            });
        }
        await context.SaveChangesAsync();

        // Contas de acesso (o vínculo com o perfil é o e-mail) e o administrador da homologação
        foreach (var email in advertisers.Select(a => a.Email).Concat(candidates.Select(c => c.Email)))
            await EnsureUserAsync(userManager, email, password);

        if (!await roleManager.RoleExistsAsync(AdminRole))
            await roleManager.CreateAsync(new IdentityRole<Guid>(AdminRole));
        var admin = await EnsureUserAsync(userManager, $"admin@{Domain}", password);
        if (!await userManager.IsInRoleAsync(admin, AdminRole))
            await userManager.AddToRoleAsync(admin, AdminRole);

        logger.LogInformation(
            "Seed de demonstração: {Empresas} empresas, {Pessoas} candidatos, {Vagas} vagas e {Candidaturas} candidaturas",
            advertisers.Count, candidates.Count, jobs.Count, applications.Count);
    }

    private static Job ExternalJob(string title, string company, string category, string city, string state,
        EApplicationChannel channel, string contact, DateTime now) => new()
    {
        Id = Id.Create(Guid.NewGuid()),
        TenantId = TenantId,
        AdvertiserId = null,
        PublicationType = EPublicationType.External,
        CompanyName = company,
        SourceName = "Sugestão de vaga",
        Title = title,
        Description = $"Vaga divulgada por {company}. Confira os requisitos e candidate-se pelo canal informado.",
        Category = category,
        ApplicationChannel = channel,
        ApplicationContact = channel == EApplicationChannel.ExternalUrl ? null : contact,
        ApplicationUrl = channel == EApplicationChannel.ExternalUrl ? contact : null,
        NumberOfOpenings = 2,
        Status = EJobStatus.Active,
        ExpirationDate = now.AddDays(25),
        LastVerifiedAt = now.AddDays(-2),
        Location = new JobLocation { City = city, State = state, Type = "OnSite" },
        Salary = new SalaryRange { Display = false },
        Moderation = new ModerationInfo { Status = EModerationStatus.Approved, ModerationDate = now.AddDays(-2) },
        Boost = new JobBoost { IsActive = false },
        Highlight = new JobHighlight { IsActive = false },
        Counters = new JobCounters()
    };

    private static ApplicationStatusHistory History(Application application, EApplicationStatus status, DateTime date, string note) => new()
    {
        Id = Id.Create(Guid.NewGuid()),
        TenantId = TenantId,
        ApplicationId = application.Id,
        Status = status,
        Date = date,
        Note = note,
        Responsible = status == EApplicationStatus.Sent ? "Candidato" : "Empresa"
    };

    private static async Task<ApplicationUser> EnsureUserAsync(UserManager<ApplicationUser> userManager, string email, string password)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is not null)
            return user;

        user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };
        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
            throw new InvalidOperationException($"Seed de demonstração: não foi possível criar {email}: " +
                string.Join(", ", result.Errors.Select(e => e.Description)));
        return user;
    }

    private static string Slug(string value) =>
        new string(value.Normalize(System.Text.NormalizationForm.FormD)
            .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
            .ToArray()).ToLowerInvariant();
}
