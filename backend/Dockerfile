# Multi-stage build for the API plus a one-shot EF Core migration bundle.
# Program.cs never migrates on startup (CLAUDE.md); docker-compose runs the `migrator` target first.

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/ src/
RUN dotnet restore src/ClaimsModule.API/ClaimsModule.API.csproj
RUN dotnet publish src/ClaimsModule.API/ClaimsModule.API.csproj -c Release -o /app/publish --no-restore
# dotnet-ef pinned to the EF Core version in Directory.Packages.props.
RUN dotnet tool install --global dotnet-ef --version 9.0.22
ENV PATH="${PATH}:/root/.dotnet/tools"
RUN dotnet ef migrations bundle \
      --project src/ClaimsModule.Persistence --startup-project src/ClaimsModule.API \
      --configuration Release --output /app/efbundle --force

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS migrator
WORKDIR /migrations
COPY --from=build /app/efbundle ./efbundle
# The connection string arrives via the environment (ConnectionStrings__ClaimsDatabase).
ENTRYPOINT ["sh", "-c", "./efbundle --connection \"$ConnectionStrings__ClaimsDatabase\""]

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "ClaimsModule.API.dll"]
