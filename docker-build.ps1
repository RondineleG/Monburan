# Script para build e execução do Docker Compose (Windows)

$ErrorActionPreference = "Stop"

Write-Host "🚀 Monburan Docker Build Script" -ForegroundColor Green
Write-Host "================================" -ForegroundColor Green

# Verificar se .env existe
if (-not (Test-Path .env)) {
    Write-Host "⚠️  Arquivo .env não encontrado. Criando a partir de .env.example..." -ForegroundColor Yellow
    Copy-Item .env.example .env
    Write-Host "✅ Arquivo .env criado. Por favor, edite-o com suas configurações." -ForegroundColor Green
}

# Verificar se Docker está rodando
try {
    docker info > $null 2>&1
} catch {
    Write-Host "❌ Docker não está rodando. Por favor, inicie o Docker e tente novamente." -ForegroundColor Red
    exit 1
}

# Menu de opções
switch ($args[0]) {
    "dev" {
        Write-Host "🔧 Iniciando ambiente de desenvolvimento..." -ForegroundColor Yellow
        docker-compose -f docker-compose.dev.yml down
        docker-compose -f docker-compose.dev.yml build
        docker-compose -f docker-compose.dev.yml up -d
        Write-Host "✅ Ambiente de desenvolvimento iniciado!" -ForegroundColor Green
        Write-Host "📱 Acesse: http://localhost:8080" -ForegroundColor Cyan
    }
    "development" {
        Write-Host "🔧 Iniciando ambiente de desenvolvimento..." -ForegroundColor Yellow
        docker-compose -f docker-compose.dev.yml down
        docker-compose -f docker-compose.dev.yml build
        docker-compose -f docker-compose.dev.yml up -d
        Write-Host "✅ Ambiente de desenvolvimento iniciado!" -ForegroundColor Green
        Write-Host "📱 Acesse: http://localhost:8080" -ForegroundColor Cyan
    }
    "prod" {
        Write-Host "🚀 Iniciando ambiente de produção..." -ForegroundColor Yellow
        docker-compose down
        docker-compose build
        docker-compose up -d
        Write-Host "✅ Ambiente de produção iniciado!" -ForegroundColor Green
        Write-Host "📱 Acesse: http://localhost:8080" -ForegroundColor Cyan
    }
    "production" {
        Write-Host "🚀 Iniciando ambiente de produção..." -ForegroundColor Yellow
        docker-compose down
        docker-compose build
        docker-compose up -d
        Write-Host "✅ Ambiente de produção iniciado!" -ForegroundColor Green
        Write-Host "📱 Acesse: http://localhost:8080" -ForegroundColor Cyan
    }
    "stop" {
        Write-Host "🛑 Parando containers..." -ForegroundColor Yellow
        docker-compose down
        docker-compose -f docker-compose.dev.yml down
        Write-Host "✅ Containers parados!" -ForegroundColor Green
    }
    "logs" {
        Write-Host "📋 Mostrando logs..." -ForegroundColor Yellow
        if ($args[1] -eq "dev") {
            docker-compose -f docker-compose.dev.yml logs -f
        } else {
            docker-compose logs -f
        }
    }
    "clean" {
        Write-Host "🧹 Limpando volumes e containers..." -ForegroundColor Yellow
        docker-compose down -v
        docker-compose -f docker-compose.dev.yml down -v
        docker system prune -f
        Write-Host "✅ Limpeza concluída!" -ForegroundColor Green
    }
    "db" {
        Write-Host "🗄️  Apenas banco de dados..." -ForegroundColor Yellow
        docker-compose up -d sqlserver
        Write-Host "✅ SQL Server iniciado na porta 1433" -ForegroundColor Green
    }
    default {
        Write-Host "Uso: .\docker-build.ps1 [comando]" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "Comandos disponíveis:" -ForegroundColor White
        Write-Host "  dev, development  - Inicia ambiente de desenvolvimento" -ForegroundColor White
        Write-Host "  prod, production - Inicia ambiente de produção" -ForegroundColor White
        Write-Host "  stop             - Para todos os containers" -ForegroundColor White
        Write-Host "  logs [dev]       - Mostra logs (dev opcional)" -ForegroundColor White
        Write-Host "  clean            - Remove volumes e containers" -ForegroundColor White
        Write-Host "  db               - Inicia apenas o banco de dados" -ForegroundColor White
        Write-Host ""
        Write-Host "Exemplos:" -ForegroundColor Cyan
        Write-Host "  .\docker-build.ps1 dev" -ForegroundColor White
        Write-Host "  .\docker-build.ps1 prod" -ForegroundColor White
        Write-Host "  .\docker-build.ps1 logs dev" -ForegroundColor White
        Write-Host "  .\docker-build.ps1 stop" -ForegroundColor White
        exit 1
    }
}
