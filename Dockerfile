# Build aşaması
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Proje dosyalarını kopyala
COPY ["src/WhaleTracker.API/WhaleTracker.API.csproj", "WhaleTracker.API/"]
COPY ["src/WhaleTracker.Core/WhaleTracker.Core.csproj", "WhaleTracker.Core/"]
COPY ["src/WhaleTracker.Data/WhaleTracker.Data.csproj", "WhaleTracker.Data/"]
COPY ["src/WhaleTracker.Infrastructure/WhaleTracker.Infrastructure.csproj", "WhaleTracker.Infrastructure/"]

# Restore
RUN dotnet restore "WhaleTracker.API/WhaleTracker.API.csproj"

# Tüm kaynak kodları kopyala
COPY src/ .

# Build
WORKDIR "/src/WhaleTracker.API"
RUN dotnet build -c Release -o /app/build

# Publish
FROM build AS publish
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# Runtime aşaması
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .

EXPOSE 8080
ENTRYPOINT ["dotnet", "WhaleTracker.API.dll"]
