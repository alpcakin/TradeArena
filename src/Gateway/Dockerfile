FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/Contracts/ Contracts/
COPY src/Gateway/ Gateway/
RUN dotnet publish Gateway -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "TradeArena.Gateway.dll"]
