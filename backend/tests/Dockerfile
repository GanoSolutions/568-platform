FROM mcr.microsoft.com/dotnet/sdk:10.0

ARG TEST_PROJECT=Five68.UnitTests
ENV TEST_PROJECT=${TEST_PROJECT}

WORKDIR /project/src

COPY ["backend/WebAPI/WebAPI.csproj", "backend/WebAPI/"]
COPY ["backend/tests/Five68.UnitTests/Five68.UnitTests.csproj", "backend/tests/Five68.UnitTests/"]
COPY ["backend/tests/Five68.IntegrationTests/Five68.IntegrationTests.csproj", "backend/tests/Five68.IntegrationTests/"]
RUN dotnet restore "backend/tests/Five68.UnitTests/Five68.UnitTests.csproj" && \
    dotnet restore "backend/tests/Five68.IntegrationTests/Five68.IntegrationTests.csproj"

COPY .editorconfig ./.editorconfig
COPY backend/ ./backend/

CMD ["sh", "-c", "dotnet test backend/tests/${TEST_PROJECT}/${TEST_PROJECT}.csproj -c Debug"]
