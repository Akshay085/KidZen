# --- Build Stage ---
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project file and restore dependencies
COPY ["KidZen.API.csproj", "./"]
RUN dotnet restore "KidZen.API.csproj"

# Copy source and publish
COPY . .
RUN dotnet publish "KidZen.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# --- Runtime Stage ---
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Port configuration (Works with Render, Koyeb, Railway, Docker)
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "KidZen.API.dll"]
