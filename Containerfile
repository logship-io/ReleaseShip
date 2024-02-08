FROM mcr.microsoft.com/dotnet/sdk:8.0-jammy AS build
WORKDIR src
COPY . .
RUN dotnet restore ./src/ConsoleHost/ReleaseShip.ConsoleHost.csproj
RUN dotnet publish ./src/ConsoleHost/ReleaseShip.ConsoleHost.csproj --no-restore -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0-jammy-chiseled-composite
EXPOSE 8080
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["./ReleaseShip.ConsoleHost"]