ARG BUILD_ENV=Production

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_ENV

WORKDIR /src

COPY WebAPI/*.csproj ./WebAPI/
RUN dotnet restore WebAPI/WebAPI.csproj

COPY . .
WORKDIR /src/WebAPI
RUN dotnet publish -c $BUILD_ENV -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
ARG BUILD_ENV
ENV DOTNET_RUNNING_ENVIRONMENT=$BUILD_ENV
ENV TZ=Europe/Rome

WORKDIR /app
COPY --from=build /app/publish .
COPY WebAPI/appsettings.json .

ENTRYPOINT ["dotnet", "WebAPI.dll"]
