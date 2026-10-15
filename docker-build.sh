#!/bin/bash

# Script para build e execução do Docker Compose

set -e

echo "🚀 Monburan Docker Build Script"
echo "================================"

# Verificar se .env existe
if [ ! -f .env ]; then
    echo "⚠️  Arquivo .env não encontrado. Criando a partir de .env.example..."
    cp .env.example .env
    echo "✅ Arquivo .env criado. Por favor, edite-o com suas configurações."
fi

# Verificar se Docker está rodando
if ! docker info > /dev/null 2>&1; then
    echo "❌ Docker não está rodando. Por favor, inicie o Docker e tente novamente."
    exit 1
fi

# Menu de opções
case "$1" in
    "dev"|"development")
        echo "🔧 Iniciando ambiente de desenvolvimento..."
        docker-compose -f docker-compose.dev.yml down
        docker-compose -f docker-compose.dev.yml build
        docker-compose -f docker-compose.dev.yml up -d
        echo "✅ Ambiente de desenvolvimento iniciado!"
        echo "📱 Acesse: http://localhost:8080"
        ;;
    "prod"|"production")
        echo "🚀 Iniciando ambiente de produção..."
        docker-compose down
        docker-compose build
        docker-compose up -d
        echo "✅ Ambiente de produção iniciado!"
        echo "📱 Acesse: http://localhost:8080"
        ;;
    "stop")
        echo "🛑 Parando containers..."
        docker-compose down
        docker-compose -f docker-compose.dev.yml down
        echo "✅ Containers parados!"
        ;;
    "logs")
        echo "📋 Mostrando logs..."
        if [ "$2" == "dev" ]; then
            docker-compose -f docker-compose.dev.yml logs -f
        else
            docker-compose logs -f
        fi
        ;;
    "clean")
        echo "🧹 Limpando volumes e containers..."
        docker-compose down -v
        docker-compose -f docker-compose.dev.yml down -v
        docker system prune -f
        echo "✅ Limpeza concluída!"
        ;;
    "db")
        echo "🗄️  Apenas banco de dados..."
        docker-compose up -d sqlserver
        echo "✅ SQL Server iniciado na porta 1433"
        ;;
    *)
        echo "Uso: ./docker-build.sh [comando]"
        echo ""
        echo "Comandos disponíveis:"
        echo "  dev, development  - Inicia ambiente de desenvolvimento"
        echo "  prod, production - Inicia ambiente de produção"
        echo "  stop             - Para todos os containers"
        echo "  logs [dev]       - Mostra logs (dev opcional)"
        echo "  clean            - Remove volumes e containers"
        echo "  db               - Inicia apenas o banco de dados"
        echo ""
        echo "Exemplos:"
        echo "  ./docker-build.sh dev"
        echo "  ./docker-build.sh prod"
        echo "  ./docker-build.sh logs dev"
        echo "  ./docker-build.sh stop"
        exit 1
        ;;
esac
