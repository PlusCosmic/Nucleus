FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
# Copy project files for restore (leverages Docker layer caching)
COPY ["Nucleus/Nucleus.csproj", "Nucleus/"]
COPY ["Nucleus.Data/Nucleus.Data.csproj", "Nucleus.Data/"]
RUN dotnet restore "Nucleus/Nucleus.csproj"
# Copy all source code
COPY ["Nucleus/", "Nucleus/"]
COPY ["Nucleus.Data/", "Nucleus.Data/"]
# Build
WORKDIR "/src/Nucleus"
RUN dotnet build "Nucleus.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Nucleus.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Nucleus.dll"]
