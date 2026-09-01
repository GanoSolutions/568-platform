FROM mcr.microsoft.com/dotnet/sdk:10.0

ARG TEST_PROJECT=Five68.UnitTests
ENV TEST_PROJECT=${TEST_PROJECT}

WORKDIR /project/src

COPY ["WebAPI/WebAPI.csproj", "WebAPI/"]
COPY ["tests/Five68.UnitTests/Five68.UnitTests.csproj", "tests/Five68.UnitTests/"]
COPY ["tests/Five68.IntegrationTests/Five68.IntegrationTests.csproj", "tests/Five68.IntegrationTests/"]
RUN dotnet restore "tests/Five68.UnitTests/Five68.UnitTests.csproj" && \
	dotnet restore "tests/Five68.IntegrationTests/Five68.IntegrationTests.csproj"

COPY . .

# CMD ["sh", "-c", "dotnet test tests/${TEST_PROJECT}/${TEST_PROJECT}.csproj -c Debug"]
CMD ["sh", "-c", "dotnet test tests/${TEST_PROJECT}/${TEST_PROJECT}.csproj -c Debug --logger \"console;verbosity=detailed\""]
