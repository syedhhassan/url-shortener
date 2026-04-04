FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

COPY solidshortener.sln .
COPY src/SolidShortener.Domain/SolidShortener.Domain.csproj src/SolidShortener.Domain/
COPY src/SolidShortener.Application/SolidShortener.Application.csproj src/SolidShortener.Application/
COPY src/SolidShortener.Infrastructure/SolidShortener.Infrastructure.csproj src/SolidShortener.Infrastructure/
COPY src/SolidShortener.Api/SolidShortener.Api.csproj src/SolidShortener.Api/

RUN dotnet restore

COPY src/ src/

RUN dotnet publish src/SolidShortener.Api/SolidShortener.Api.csproj -c Release -o out

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=build /app/out .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "SolidShortener.Api.dll"]