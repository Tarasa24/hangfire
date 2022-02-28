FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["hangfire.csproj", "."]
RUN dotnet restore "./hangfire.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "hangfire.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "hangfire.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
COPY Assets/ /app/Assets/

ENV MYSQL_CS=
ENV STRAPI_TOKEN=
ENV GITHUB_TOKEN=
ENV ASPNETCORE_ENVIRONMENT="Production"
ENV ASPNETCORE_URLS="http://*:5000"
EXPOSE 5000

ENTRYPOINT ["dotnet", "hangfire.dll"]