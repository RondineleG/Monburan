# ============================================================
# Stage 1: Build
# ============================================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copiar arquivos de projeto primeiro (cache de camadas Docker)
COPY ["src/Monburan.Web/Monburan.Web.csproj", "Monburan.Web/"]
COPY ["src/Monburan.Core/Monburan.Core.csproj", "Monburan.Core/"]
COPY ["src/Monburan.Web.Client/Monburan.Web.Client.csproj", "Monburan.Web.Client/"]

# Restaurar pacotes
RUN dotnet restore "Monburan.Web/Monburan.Web.csproj"

# Limpar cache do NuGet para evitar problemas de fallback folders
RUN dotnet nuget locals all --clear

# Copiar código fonte
COPY ["src/Monburan.Core/", "Monburan.Core/"]
COPY ["src/Monburan.Web.Client/", "Monburan.Web.Client/"]
COPY ["src/Monburan.Web/", "Monburan.Web/"]

# Publish diretamente (inclui build)
WORKDIR "/src/Monburan.Web"
RUN mkdir -p bin/Release/net10.0/pt-br /app/publish/pt-br
RUN dotnet publish "Monburan.Web.csproj" -c Release -o /app/publish \
    /p:UseAppHost=false

# ============================================================
# Stage 2: Runtime
# ============================================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Criar diretório para uploads
RUN mkdir -p /app/wwwroot/uploads

# Copiar published output do stage de build
COPY --from=build /app/publish .

# Expor portas
EXPOSE 80
EXPOSE 443

# Variáveis de ambiente padrão
ENV ASPNETCORE_URLS=http://+:80
ENV ASPNETCORE_ENVIRONMENT=Production

# Entry point
ENTRYPOINT ["dotnet", "Monburan.Web.dll"]
