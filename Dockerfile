# 1. Base Runtime Stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
RUN apt-get update && apt-get install -y libgssapi-krb5-2 && rm -rf /var/lib/apt/lists/*
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

# 2. SDK Build Stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files for efficient layer caching
COPY ["src/Firefly.Api/Firefly.Api.csproj", "src/Firefly.Api/"]
COPY ["src/Firefly.Application/Firefly.Application.csproj", "src/Firefly.Application/"]
COPY ["src/Firefly.Domain/Firefly.Domain.csproj", "src/Firefly.Domain/"]
COPY ["src/Firefly.Infrastructure/Firefly.Infrastructure.csproj", "src/Firefly.Infrastructure/"]

# Restore dependencies
RUN dotnet restore "src/Firefly.Api/Firefly.Api.csproj"

# Copy remaining source code (including wwwroot inside Firefly.Api) and build
COPY . .
WORKDIR "/src/src/Firefly.Api"
RUN dotnet build "Firefly.Api.csproj" -c Release -o /app/build

# 3. Publish Stage
FROM build AS publish
RUN dotnet publish "Firefly.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# 4. Final Image Stage
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Explicitly copy wwwroot from publish output or source to ensure static assets are present
COPY --from=build /src/src/Firefly.Api/wwwroot ./wwwroot

ENTRYPOINT ["dotnet", "Firefly.Api.dll"]