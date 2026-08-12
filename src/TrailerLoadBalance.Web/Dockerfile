FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY TrailerLoadBalance.Web/TrailerLoadBalance.Web.csproj TrailerLoadBalance.Web/
RUN dotnet restore TrailerLoadBalance.Web/TrailerLoadBalance.Web.csproj

COPY TrailerLoadBalance.Web/ TrailerLoadBalance.Web/
RUN dotnet publish TrailerLoadBalance.Web/TrailerLoadBalance.Web.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "TrailerLoadBalance.Web.dll"]
