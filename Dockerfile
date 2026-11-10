FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["global.json", "./"]
COPY ["Contracts/", "Contracts/"]
COPY ["01- API/FCG.Users.Api/FCG.Users.Api.csproj", "01- API/FCG.Users.Api/"]
COPY ["02- Core/FCG.Users.Application/FCG.Users.Application.csproj", "02- Core/FCG.Users.Application/"]
COPY ["02- Core/FCG.Users.Domain/FCG.Users.Domain.csproj", "02- Core/FCG.Users.Domain/"]
COPY ["03- Infrastructure/FCG.Users.Infrastructure/FCG.Users.Infrastructure.csproj", "03- Infrastructure/FCG.Users.Infrastructure/"]
RUN dotnet restore "01- API/FCG.Users.Api/FCG.Users.Api.csproj"

COPY . .
RUN dotnet publish "01- API/FCG.Users.Api/FCG.Users.Api.csproj" -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "FCG.Users.Api.dll"]
