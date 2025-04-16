# Use the official ASP.NET Core runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
# If using HTTPS directly within the container (less common behind Ingress)
# EXPOSE 8081

# Use the SDK image to build the application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files
COPY ["src/CallingService/CallingService.csproj", "src/CallingService/"]
COPY ["src/SharedContracts/SharedContracts.csproj", "src/SharedContracts/"]
# Copy solution file if needed for restore context
COPY ["TradeyBayComms.sln", "."]

# Restore dependencies
RUN dotnet restore "./src/CallingService/CallingService.csproj"
# Or restore solution wide if preferred: RUN dotnet restore "./TradeyBayComms.sln"


# Copy the rest of the source code
COPY . .
WORKDIR "/src/src/CallingService"
RUN dotnet build "CallingService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "CallingService.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Final stage/runtime image
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "CallingService.dll"]