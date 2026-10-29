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

# Copiar published output do stage de build
COPY --from=build /app/publish .

# Pastas graváveis (volumes em produção): uploads, chaves do DataProtection, evidências das sugestões e logs. Pertencem ao
# usuário sem privilégios da imagem (app, UID 1654); chaves e evidências só o dono lê.
RUN mkdir -p /app/wwwroot/uploads /app/keys /app/App_Data/evidencias /app/logs \
    && chown -R "$APP_UID":"$APP_UID" /app/wwwroot/uploads /app/keys /app/App_Data /app/logs \
    && chmod 700 /app/keys /app/App_Data/evidencias

# A aplicação roda sem root. Usuário sem privilégio não abre porta abaixo de 1024, então o Kestrel escuta na 8080.
USER $APP_UID

EXPOSE 8080

# Variáveis de ambiente padrão
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Entry point
ENTRYPOINT ["dotnet", "Monburan.Web.dll"]
