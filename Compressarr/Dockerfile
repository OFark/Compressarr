#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build
WORKDIR /src
COPY *.sln .
COPY Compressarr/*.csproj ./Compressarr/
RUN dotnet restore

COPY Compressarr/. ./Compressarr/
WORKDIR "/src/Compressarr"
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:5.0
WORKDIR /app
COPY --from=build /app/publish ./
ENTRYPOINT ["dotnet", "Compressarr.dll"]