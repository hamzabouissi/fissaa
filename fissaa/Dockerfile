FROM mcr.microsoft.com/dotnet/runtime:6.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["fissaa/fissaa.csproj", "fissaa/"]
RUN dotnet restore "fissaa/fissaa.csproj"
COPY . .
WORKDIR "/src/fissaa"
RUN dotnet build "fissaa.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "fissaa.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "fissaa.dll"]
