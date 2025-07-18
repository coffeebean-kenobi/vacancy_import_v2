FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copy csproj and restore dependencies
COPY src/VacancyImport/*.csproj ./src/VacancyImport/
RUN dotnet restore ./src/VacancyImport/VacancyImport.csproj

# Copy all files and build app
COPY . ./
RUN dotnet publish src/VacancyImport/VacancyImport.csproj -c Release -o out

# Build runtime image
FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /app/out .

# Create test data directory
RUN mkdir -p test_data/excel

# Set environment variables
ENV ASPNETCORE_ENVIRONMENT=Development

# Run the application
ENTRYPOINT ["dotnet", "VacancyImport.dll"] 