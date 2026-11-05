# Estágio de Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["GoJob/GoJob.csproj", "GoJob/"]
RUN dotnet restore "GoJob/GoJob.csproj"

COPY . .
WORKDIR "/src/GoJob"
RUN dotnet publish "GoJob.csproj" -c Release -o /app/publish

# Estágio de Execução
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Copia explicitamente a pasta wwwroot para a raiz da aplicação
COPY GoJob/wwwroot ./wwwroot

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "GoJob.dll"]