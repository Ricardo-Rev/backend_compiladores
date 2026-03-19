# Etapa 1: build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

COPY . .

WORKDIR /app/src/umg_basic_rover_api
RUN dotnet restore
RUN dotnet publish -c Release -o /out

# Etapa 2: runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /out .

ENV ASPNETCORE_URLS=http://+:${PORT}

EXPOSE 8080

ENTRYPOINT ["dotnet", "umg_basic_rover_api.dll"]