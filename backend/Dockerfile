# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Skopiuj wszystko i opublikuj host API (przywraca też zależne projekty modułów).
COPY . .
RUN dotnet publish src/ZipZap.Api/ZipZap.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

# ---------- runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
# Bind to the host-provided $PORT (Render sets it, e.g. 10000); default 8080 (Fly/local).
ENTRYPOINT ["sh", "-c", "ASPNETCORE_URLS=http://0.0.0.0:${PORT:-8080} dotnet ZipZap.Api.dll"]
