FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["CodeLabAPI.csproj", "./"]
RUN dotnet restore "CodeLabAPI.csproj"

COPY . .
RUN dotnet build "CodeLabAPI.csproj" -c Release -o /app/build
RUN dotnet publish "CodeLabAPI.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Install Docker CLI so the backend can manage user containers
RUN apt-get update && \
    apt-get install -y docker.io && \
    rm -rf /var/lib/apt/lists/*

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "CodeLabAPI.dll"]
