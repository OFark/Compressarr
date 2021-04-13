#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/core/aspnet:3.1-buster-slim AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/core/sdk:3.1-buster AS build
WORKDIR /src
COPY ["Compressarr/Compressarr.csproj", "Compressarr/"]
RUN dotnet restore "Compressarr/Compressarr.csproj"
COPY . .
WORKDIR "/src/Compressarr"
RUN dotnet build "Compressarr.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Compressarr.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Compressarr.dll"]