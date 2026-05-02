# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["ResumeAnalyzer.csproj", "./"]
RUN dotnet restore "ResumeAnalyzer.csproj"

COPY . .
RUN dotnet publish ResumeAnalyzer.csproj -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:${PORT:-5000}
ENV DOTNET_EnableDiagnostics=0

ENTRYPOINT ["dotnet", "ResumeAnalyzer.dll"]
