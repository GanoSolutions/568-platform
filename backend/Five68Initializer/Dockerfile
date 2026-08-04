#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS base

ARG BUILD_CONFIGURATION=Debug

WORKDIR /project/src
COPY ["backend/Five68Initializer/Five68Initializer.csproj", "backend/Five68Initializer/"]
COPY ["backend/WebAPI/WebAPI.csproj", "backend/WebAPI/"]
RUN dotnet restore "./backend/Five68Initializer/Five68Initializer.csproj"

COPY .editorconfig ./.editorconfig
COPY backend/ ./backend/
WORKDIR "/project/src/backend/Five68Initializer"
CMD ["dotnet", "run", "--project", "Five68Initializer.csproj", "-c", "Debug"]
