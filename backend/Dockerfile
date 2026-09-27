# Build context is the `backend/` directory (see infra/render.yaml's
# rootDir). Multi-stage: the SDK image restores/builds/publishes, the
# much smaller ASP.NET runtime image is what actually ships and runs.

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files first so `dotnet restore` is cached across builds
# that only change source, not dependencies. Restoring the Api project
# directly (not the .sln) also means the test projects don't need to be
# copied into the image at all.
COPY src/BloodDonationNetwork.Api/BloodDonationNetwork.Api.csproj src/BloodDonationNetwork.Api/
COPY src/BloodDonationNetwork.Application/BloodDonationNetwork.Application.csproj src/BloodDonationNetwork.Application/
COPY src/BloodDonationNetwork.Domain/BloodDonationNetwork.Domain.csproj src/BloodDonationNetwork.Domain/
COPY src/BloodDonationNetwork.Infrastructure/BloodDonationNetwork.Infrastructure.csproj src/BloodDonationNetwork.Infrastructure/
RUN dotnet restore src/BloodDonationNetwork.Api/BloodDonationNetwork.Api.csproj

COPY src/ src/
RUN dotnet publish src/BloodDonationNetwork.Api/BloodDonationNetwork.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080

# Render (and most PaaS hosts) assign the actual listening port via the
# $PORT env var at runtime, which can vary — bind to whatever it gives
# us, falling back to 8080 for a plain local `docker run`.
ENTRYPOINT ["/bin/sh", "-c", "ASPNETCORE_URLS=http://+:${PORT:-8080} dotnet BloodDonationNetwork.Api.dll"]
