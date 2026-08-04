ARG BUILD_ENV=Production

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_ENV

WORKDIR /src

COPY backend/WebAPI/*.csproj ./backend/WebAPI/
RUN dotnet restore backend/WebAPI/WebAPI.csproj

COPY .editorconfig ./.editorconfig
COPY backend/ ./backend/
WORKDIR /src/backend/WebAPI
RUN dotnet publish -c $BUILD_ENV -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
ARG BUILD_ENV
ENV DOTNET_RUNNING_ENVIRONMENT=$BUILD_ENV

WORKDIR /app
COPY --from=build /app/publish .
COPY backend/WebAPI/appsettings.json .

ENTRYPOINT ["dotnet", "WebAPI.dll"]
