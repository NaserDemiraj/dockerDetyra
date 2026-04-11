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

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "CodeLabAPI.dll"]
