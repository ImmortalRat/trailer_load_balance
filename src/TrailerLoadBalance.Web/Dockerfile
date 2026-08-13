FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# A single `dotnet publish` (no separate `dotnet restore` step) is used deliberately: restoring
# without an explicit `-c Release` and then publishing `-c Release --no-restore` in a later layer
# is a known trap - since the two commands target different configurations, the Release-specific
# static web asset generation (which is what produces wwwroot/_framework/blazor.web.js, the file
# that boots the entire Blazor Server client runtime) can be silently skipped even though both
# commands report success. Losing a bit of Docker layer-caching on restore is a fine trade for a
# build that doesn't intermittently ship without a working UI.
COPY TrailerLoadBalance.Web/ TrailerLoadBalance.Web/
RUN dotnet publish TrailerLoadBalance.Web/TrailerLoadBalance.Web.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "TrailerLoadBalance.Web.dll"]
