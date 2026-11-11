import { chromium } from "playwright";
import { writeFile } from "node:fs/promises";
import { resolve } from "node:path";

const MAX_JOBS = numberArg("--max-jobs", 250);
const OUTPUT = valueArg("--output", "collected-jobs.json");
const SOURCE_FILTER = valueArg("--source", "");
const WAIT_MS = 1200;

const regionCities = {
  "Ponta Grossa": ["Ponta Grossa", "Carambeí", "Castro", "Palmeira", "Porto Amazonas", "Ipiranga"],
  Curitiba: ["Curitiba", "Almirante Tamandaré", "Araucária", "Balsa Nova", "Campina Grande do Sul", "Campo Largo", "Campo Magro", "Colombo", "Contenda", "Fazenda Rio Grande", "Mandirituba", "Pinhais", "Piraquara", "Quatro Barras", "Quitandinha", "São José dos Pinhais", "Tijucas do Sul"],
  Joinville: ["Joinville", "Araquari", "Balneário Barra do Sul", "Garuva", "Guaramirim", "Jaraguá do Sul", "São Francisco do Sul", "Schroeder"],
  Itajaí: ["Itajaí", "Navegantes", "Balneário Camboriú", "Camboriú", "Penha", "Balneário Piçarras", "Ilhota", "Itapema", "Porto Belo", "Brusque", "Gaspar", "Tijucas", "Luiz Alves"],
};

const sources = [
  {
    id: "itajai-balcao",
    name: "Balcão de Empregos de Itajaí",
    region: "Itajaí",
    kind: "itajai",
    url: "https://empregos.itajai.sc.gov.br/buscar-vagas/controller/externo",
    hosts: ["empregos.itajai.sc.gov.br"],
  },
  {
    id: "itajai-online",
    name: "Itajaí Online",
    region: "Itajaí",
    kind: "cards",
    url: "https://itajaionline.com.br/vagas",
    hosts: ["itajaionline.com.br"],
  },
  {
    id: "joinville-emprega-mais",
    name: "Joinville Emprega Mais",
    region: "Joinville",
    kind: "joinville",
    url: "https://joinvilleempregamais.empregaja.org/oportunidades",
    hosts: ["joinvilleempregamais.empregaja.org"],
  },
  {
    id: "emprega-curitiba",
    name: "Emprega Curitiba",
    region: "Curitiba",
    kind: "curitiba",
    url: "https://emprega.curitiba.pr.gov.br/search",
    hosts: ["emprega.curitiba.pr.gov.br", "assets.selecty.app"],
  },
  {
    id: "pg-empregos",
    name: "PG Empregos",
    region: "Ponta Grossa",
    kind: "cards",
    url: "https://www.pgempregos.com.br/",
    hosts: ["www.pgempregos.com.br", "pgempregos.com.br"],
  },
];

const chosenSources = SOURCE_FILTER
  ? sources.filter((source) => source.id === SOURCE_FILTER || source.region.toLowerCase() === SOURCE_FILTER.toLowerCase())
  : sources;
if (chosenSources.length === 0) {
  throw new Error(`Fonte ou região desconhecida: ${SOURCE_FILTER}`);
}
const regionCount = new Set(chosenSources.map((source) => source.region)).size;
const maxPerRegion = Math.floor(MAX_JOBS / regionCount);
const countsByRegion = new Map();
const sourceCountsByRegion = new Map();
for (const source of chosenSources) {
  sourceCountsByRegion.set(source.region, (sourceCountsByRegion.get(source.region) ?? 0) + 1);
}

const browser = await chromium.launch({ headless: true });
const collected = [];
const failures = [];

try {
  for (const source of chosenSources) {
    try {
      console.log(`Consultando ${source.name} (${source.url})`);
      const remaining = maxPerRegion - (countsByRegion.get(source.region) ?? 0);
      if (remaining <= 0) continue;
      const sourceLimit = Math.ceil(maxPerRegion / sourceCountsByRegion.get(source.region));
      const jobs = await collectSource(browser, source, Math.min(remaining, sourceLimit));
      for (const job of jobs) {
        if (!isInScope(job.city, source.region)) continue;
        collected.push(job);
        countsByRegion.set(source.region, (countsByRegion.get(source.region) ?? 0) + 1);
        if (collected.length >= MAX_JOBS || countsByRegion.get(source.region) >= maxPerRegion) break;
      }
      console.log(`  ${jobs.length} itens lidos; ${collected.length} dentro do limite geral.`);
    } catch (error) {
      failures.push({ source: source.name, message: String(error?.message ?? error) });
      console.error(`  Falha em ${source.name}: ${String(error?.message ?? error)}`);
    }
    if (collected.length >= MAX_JOBS) break;
  }
} finally {
  await browser.close();
}

const unique = deduplicate(collected);
const batch = {
  version: 1,
  generatedAt: new Date().toISOString(),
  scope: Object.keys(regionCities),
  jobs: unique,
  failures,
};
await writeFile(resolve(OUTPUT), `${JSON.stringify(batch, null, 2)}\n`, "utf8");
console.log(`Gravadas ${unique.length} vagas em ${resolve(OUTPUT)}.`);
if (failures.length > 0) process.exitCode = 2;

async function collectSource(browser, source, limit) {
  const context = await browser.newContext({
    userAgent: "MonburanPublicJobCollector/1.0 (+https://monburan.com.br)",
    locale: "pt-BR",
  });
  const page = await context.newPage();
  page.setDefaultNavigationTimeout(30_000);
  const results = [];

  try {
    await page.goto(source.url, { waitUntil: "domcontentloaded" });
    await page.waitForTimeout(900);

    if (source.kind === "itajai") {
      let pageNumber = 1;
      const visitedPages = new Set();
      while (!visitedPages.has(pageNumber) && results.length < limit) {
        visitedPages.add(pageNumber);
        const cards = await page.locator(".card_balcao_empregos").evaluateAll((nodes) => nodes.map((card) => {
          const text = card.innerText;
          const href = card.querySelector("a.span_saiba_mais")?.href;
          const title = card.querySelector("h2, h3, h4, h5")?.innerText?.trim()
            ?? text.split("\n").map((line) => line.trim()).find((line) => line.length > 4 && !/contratação rápida/i.test(line));
          const description = card.querySelector(".descricao_vaga")?.innerText?.trim() ?? "";
          const city = text.match(/\b(Itaja[ií]|Navegantes|Balne[aá]rio Cambori[uú]|Cambori[uú]|Penha|Itapema|Brusque|Gaspar)\b/i)?.[0] ?? "Itajaí";
          return { title, description, city, href };
        }));
        for (const card of cards) {
          if (card.title && card.href) results.push(makeJob(source, card));
          if (results.length >= limit) break;
        }
        if (results.length >= limit) break;
        const nextHref = await page.locator("nav[aria-label='Page navigation example'] a.page-link")
          .filter({ hasText: "Próximo" }).getAttribute("href");
        if (!nextHref) break;
        const nextPage = Number(new URL(nextHref).searchParams.get("pagina"));
        if (!Number.isFinite(nextPage) || visitedPages.has(nextPage)) break;
        pageNumber = nextPage;
        await page.goto(nextHref, { waitUntil: "domcontentloaded" });
        await page.waitForTimeout(WAIT_MS);
      }
      return results;
    }

    if (source.kind === "joinville") {
      for (let pageNumber = 0; pageNumber < Math.ceil(limit / 20); pageNumber++) {
        const cards = await page.locator("#lista-oportunidades a.card-oportunidade").evaluateAll((links) => links.map((link) => {
          const paragraphs = [...link.querySelectorAll("p")].map((paragraph) => paragraph.innerText.trim()).filter(Boolean);
          const title = paragraphs[0];
          const company = paragraphs[1];
          const city = paragraphs.find((paragraph) => /\/(?:SC|PR)\b/i.test(paragraph))?.split("/")[0]?.trim() ?? "Joinville";
          const description = link.innerText.split("\n").map((line) => line.trim()).filter(Boolean).join(" · ");
          return { title, companyName: company, city, description, href: link.href };
        }));
        for (const card of cards) {
          if (card.title && card.href) results.push(makeJob(source, card));
          if (results.length >= limit) break;
        }
        const next = page.locator("button.next-page");
        if (results.length >= limit || await next.count() === 0 || await next.isDisabled().catch(() => false)) break;
        await next.click();
        await page.waitForTimeout(WAIT_MS);
      }
      return results;
    }

    if (source.kind === "curitiba") {
      const seenCards = new Set();
      let unchangedScrolls = 0;
      while (results.length < limit && unchangedScrolls < 3) {
        const cardIds = await page.locator("[id^='vacancy-card-occupation']").evaluateAll((nodes) => nodes.map((node) => node.id));
        const before = seenCards.size;
        for (const id of cardIds) {
          if (seenCards.has(id) || results.length >= limit) continue;
          seenCards.add(id);
          const cardTitle = page.locator(`[id="${id}"]`);
          const suffix = id.replace("vacancy-card-occupation", "");
          const title = await cardTitle.getAttribute("title") ?? await cardTitle.innerText();
          const companyName = await page.locator(`[id="vacancy-card-company${suffix}"]`).innerText().catch(() => "");
          const card = cardTitle.locator("xpath=ancestor::div[contains(concat(' ', normalize-space(@class), ' '), ' vacancy-card ')]");
          const cardText = await card.innerText().catch(() => "");
          const city = cardText.match(/([^\n]+?\s-\s(?:PR|SC))/i)?.[1]?.trim() ?? "Curitiba";
          await card.click();
          await page.getByRole("button", { name: "Candidatar-se" }).waitFor({ timeout: 10_000 });
          const detailText = await page.locator("body").innerText();
          const code = detailText.match(/C[oó]d\.\s*da\s*Vaga:\s*(\d+)/i)?.[1];
          if (code) {
            const stateCode = /\bSC\b/i.test(city) ? "sc" : "pr";
            const citySlug = slugify(city).replace(/-(?:pr|sc)$/, "");
            const jobPath = `/vaga/${code}/vaga-para-${slugify(title)}-em-${citySlug}-${stateCode}`;
            const description = extractDescription(detailText) || cardText;
            results.push(makeJob(source, { title, companyName, city, description, href: new URL(jobPath, source.url).href }));
          }
          await page.waitForTimeout(700);
        }
        if (results.length >= limit) break;
        unchangedScrolls = seenCards.size === before ? unchangedScrolls + 1 : 0;
        await page.evaluate(() => window.scrollTo(0, document.body.scrollHeight));
        await page.waitForTimeout(WAIT_MS);
      }
      return results;
    }

    if (source.kind === "cards") {
      // Portais distintos usam componentes diferentes. Só são aceitos links claramente localizados em cartões
      // de vagas ou em rotas de vaga; menus, notícias e links de terceiros ficam de fora.
      const links = await page.locator("a[href]").evaluateAll((anchors) => anchors.map((anchor) => {
        const href = anchor.href;
        const text = anchor.innerText?.trim() ?? "";
        const card = anchor.closest("article, li, [class*=card], [class*=vaga], [class*=job], [class*=oportunidade]") ?? anchor;
        const cardText = card.innerText?.trim() ?? text;
        const title = card.querySelector("h1, h2, h3, h4, h5, [class*=title], [class*=titulo]")?.innerText?.trim() ?? text;
        const company = card.querySelector("[class*=company], [class*=empresa]")?.innerText?.trim() ?? "";
        const location = card.querySelector("[class*=location], [class*=local], [class*=cidade]")?.innerText?.trim() ?? "";
        return { href, title, companyName: company, city: location, description: cardText, cardText };
      }));

      const seen = new Set();
      for (const link of links) {
        let target;
        try { target = new URL(link.href); } catch { continue; }
        if (!source.hosts.includes(target.hostname)) continue;
        if (canonicalUrl(target.href) === canonicalUrl(source.url)) continue;
        if (!isLikelyJobLink(target.pathname, link.title, link.cardText)) continue;
        if (seen.has(target.href)) continue;
        seen.add(target.href);
        const city = findCity(`${link.city}\n${link.cardText}`, source.region) ?? source.region;
        if (!isInScope(city, source.region)) continue;
        results.push(makeJob(source, { ...link, city, href: target.href }));
        if (results.length >= limit) break;
      }
      return results;
    }

    return results;
  } finally {
    await context.close();
  }
}

function makeJob(source, input) {
  const target = new URL(input.href, source.url);
  if (!source.hosts.includes(target.hostname) || target.protocol !== "https:") {
    throw new Error(`Link de vaga fora dos domínios autorizados: ${target.href}`);
  }
  const city = findCity(input.city ?? "", source.region) ?? source.region;
  return {
    title: clean(input.title).slice(0, 200),
    companyName: clean(input.companyName).slice(0, 200) || null,
    city,
    state: ["Ponta Grossa", "Curitiba"].includes(source.region) ? "PR" : "SC",
    description: clean(input.description).slice(0, 600) || null,
    sourceName: source.name,
    sourceUrl: target.href,
    capturedAt: new Date().toISOString(),
    region: source.region,
  };
}

function isLikelyJobLink(path, title, text) {
  if (!title || title.length < 4 || title.length > 200) return false;
  if (/noticia|blog|contato|login|cadastro|sobre|termo|privacidade|curriculo/i.test(path)) return false;
  return /\/(?:vaga|vagas|oportunidade|oportunidades|job|jobs)(?:\/|$)/i.test(path);
}

function extractDescription(text) {
  const marker = /Responsabilidades e atribui[cç][oõ]es/i;
  const start = text.search(marker);
  if (start >= 0) return clean(text.slice(start + "Responsabilidades e atribuições".length)).slice(0, 600);
  const details = text.search(/Dados da vaga/i);
  return details >= 0 ? clean(text.slice(details + "Dados da vaga".length)).slice(0, 600) : "";
}

function slugify(value) {
  return normalize(value).replace(/[^a-z0-9]+/g, "-").replace(/^-|-$/g, "");
}

function findCity(text, region) {
  const normalized = normalize(text);
  return [...regionCities[region]]
    .sort((left, right) => normalize(right).length - normalize(left).length)
    .find((city) => normalized.includes(normalize(city))) ?? null;
}

function isInScope(city, region) {
  return regionCities[region].some((candidate) => normalize(candidate) === normalize(city));
}

function deduplicate(jobs) {
  const seen = new Set();
  return jobs.filter((job) => {
    const key = canonicalUrl(job.sourceUrl);
    if (seen.has(key)) return false;
    seen.add(key);
    return true;
  });
}

function canonicalUrl(value) {
  const url = new URL(value);
  url.hash = "";
  for (const key of [...url.searchParams.keys()]) {
    if (/^(utm_|fbclid|gclid)/i.test(key)) url.searchParams.delete(key);
  }
  return url.toString().replace(/\/$/, "");
}

function clean(value) {
  return String(value ?? "").replace(/\s+/g, " ").trim();
}

function normalize(value) {
  return clean(value).toLocaleLowerCase("pt-BR").normalize("NFD").replace(/[\u0300-\u036f]/g, "");
}

function valueArg(name, fallback) {
  const index = process.argv.indexOf(name);
  return index >= 0 ? process.argv[index + 1] ?? fallback : fallback;
}

function numberArg(name, fallback) {
  const value = Number(valueArg(name, String(fallback)));
  return Number.isFinite(value) && value > 0 ? value : fallback;
}
