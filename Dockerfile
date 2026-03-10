FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Копируем только проект, затем остальные файлы
COPY ["PepperBot/PepperBot.csproj", "PepperBot/"]
RUN dotnet restore "PepperBot/PepperBot.csproj"

COPY . .
RUN dotnet publish "PepperBot/PepperBot.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV DOTNET_EnableDiagnostics=0
RUN pwd;ls -la
ENTRYPOINT ["dotnet", "/app/PepperBot.dll"]

