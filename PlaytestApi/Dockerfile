FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/Playtest.Api/Playtest.Api.csproj src/Playtest.Api/
RUN dotnet restore src/Playtest.Api/Playtest.Api.csproj
COPY src/Playtest.Api/ src/Playtest.Api/
RUN dotnet publish src/Playtest.Api/Playtest.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Playtest.Api.dll"]
