# Multi-stage Dockerfile — build, then run as a non-root user on the ASP.NET runtime.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj files first for layer caching
COPY Directory.Build.props ./
COPY src/AssignmentSystem.Shared/AssignmentSystem.Shared.csproj src/AssignmentSystem.Shared/
COPY src/AssignmentSystem.Domain/AssignmentSystem.Domain.csproj src/AssignmentSystem.Domain/
COPY src/AssignmentSystem.Application/AssignmentSystem.Application.csproj src/AssignmentSystem.Application/
COPY src/AssignmentSystem.Infrastructure/AssignmentSystem.Infrastructure.csproj src/AssignmentSystem.Infrastructure/
COPY src/AssignmentSystem.Api/AssignmentSystem.Api.csproj src/AssignmentSystem.Api/
RUN dotnet restore src/AssignmentSystem.Api/AssignmentSystem.Api.csproj

# Copy source and publish
COPY . .
RUN dotnet publish src/AssignmentSystem.Api/AssignmentSystem.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

# ── Runtime ──────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# curl is not in the base runtime image; the HEALTHCHECK below needs it.
RUN apt-get update \
  && apt-get install -y --no-install-recommends curl \
  && rm -rf /var/lib/apt/lists/*

# Non-root user for security (the user 'app' is pre-created in .NET 8+ base images)
RUN mkdir -p /data/submissions && chown -R app:app /data/submissions
VOLUME ["/data/submissions"]

COPY --from=build /app/publish .
USER app

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
  CMD curl -fsS http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "AssignmentSystem.Api.dll"]
