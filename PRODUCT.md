# Produto — Monburan

## Register
product

A Monburan é um portal de vagas em pt-BR que conecta candidatos e empresas anunciantes,
com aplicação web e app móvel compartilhando a mesma identidade.

## Público e jornadas

- **Candidato**: busca e explora vagas (públicas), salva vagas, cria alertas, se
  candidata com ou sem currículo anexado e acompanha cada etapa em "Minhas vagas".
  Mantém currículos (criação, importação de PDF, edição, CV para impressão) e pode
  usar sugestões de melhoria com IA (plano Premium), revisando e aplicando só o que escolher.
- **Empresa (anunciante)**: publica e gerencia vagas, recebe candidaturas, vê perfil e
  o currículo anexado a cada candidatura e move a situação do processo.
- **Administrador**: modera vagas, contas e provedores de IA (fora do escopo de UX pública).

## Identidade e princípios de UX

- Tom confiante, claro e discreto: hierarquia forte, ações explícitas, nada de
  decoração gratuita nem nomes técnicos expostos (provedor/modelo de IA ficam fora da interface).
- Divulgação progressiva: conteúdo denso (currículo na gaveta, seções longas) abre por demanda.
- Preservação de dados onde vale: desfazer restaura o formulário sem perder o que não é
  editável na tela (ex.: endereço do perfil); o rascunho de sugestões de IA fica criptografado
  no navegador; falhas de consulta nunca se disfarçam de "não existe" (candidatura duplicada,
  currículo sem permissão).
- Ações diretas: candidato já candidatado vê "Acompanhar candidatura"; empresa edita e vê
  candidaturas sem abrir menu; destrutivo é sempre secundário.
- Responsivo de verdade: CTA fixo no celular, tabelas viram cartões, campos com
  tipos/autocomplete adequados ao teclado móvel.
