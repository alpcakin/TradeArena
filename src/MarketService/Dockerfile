FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/Contracts/ Contracts/
COPY src/MarketService/ MarketService/
RUN dotnet publish MarketService -c Release -o /app

FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "TradeArena.MarketService.dll"]
