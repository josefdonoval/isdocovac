# syntax=docker/dockerfile:1.7

# Build stage — restore + publish a release-mode app.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj first so layer caching can reuse package restore when only sources change.
COPY Isdocovac.csproj ./
RUN dotnet restore Isdocovac.csproj

COPY . .
RUN dotnet publish Isdocovac.csproj -c Release -o /app /p:UseAppHost=false

# Runtime stage — minimal ASP.NET image.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_PRINT_TELEMETRY_MESSAGE=false

RUN apt-get update \
 && apt-get install -y --no-install-recommends curl \
 && rm -rf /var/lib/apt/lists/*

COPY --from=build /app ./
EXPOSE 8080

ENTRYPOINT ["dotnet", "Isdocovac.dll"]
