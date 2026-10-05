FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY QualityWorkflow.sln global.json ./
COPY src/QualityWorkflow.Api/QualityWorkflow.Api.csproj src/QualityWorkflow.Api/
COPY tests/QualityWorkflow.Api.Tests/QualityWorkflow.Api.Tests.csproj tests/QualityWorkflow.Api.Tests/
RUN dotnet restore QualityWorkflow.sln

COPY src/QualityWorkflow.Api/ src/QualityWorkflow.Api/
RUN dotnet publish src/QualityWorkflow.Api/QualityWorkflow.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "QualityWorkflow.Api.dll"]
