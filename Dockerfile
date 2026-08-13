FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Museum-System.sln ./
COPY src/MuseumSystem.Domain/MuseumSystem.Domain.csproj src/MuseumSystem.Domain/
COPY src/MuseumSystem.Application/MuseumSystem.Application.csproj src/MuseumSystem.Application/
COPY src/MuseumSystem.Infrastructure/MuseumSystem.Infrastructure.csproj src/MuseumSystem.Infrastructure/
COPY src/MuseumSystem.Web/MuseumSystem.Web.csproj src/MuseumSystem.Web/
COPY tests/MuseumSystem.Domain.Tests/MuseumSystem.Domain.Tests.csproj tests/MuseumSystem.Domain.Tests/
COPY tests/MuseumSystem.Application.Tests/MuseumSystem.Application.Tests.csproj tests/MuseumSystem.Application.Tests/
COPY tests/MuseumSystem.Integration.Tests/MuseumSystem.Integration.Tests.csproj tests/MuseumSystem.Integration.Tests/
COPY tests/MuseumSystem.Web.AcceptanceTests/MuseumSystem.Web.AcceptanceTests.csproj tests/MuseumSystem.Web.AcceptanceTests/

RUN dotnet restore Museum-System.sln

COPY . .
RUN dotnet publish src/MuseumSystem.Web/MuseumSystem.Web.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "MuseumSystem.Web.dll"]
