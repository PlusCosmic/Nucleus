FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
# Copy only the project file first to leverage Docker layer caching
COPY ["Nucleus.csproj", "./"]
RUN dotnet restore "./Nucleus.csproj"
# Copy the rest of the source
COPY . .
# Build from the project root (the build context is the Nucleus project directory)
WORKDIR "/src"
RUN dotnet build "./Nucleus.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Nucleus.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM build AS efbundle
RUN dotnet tool install --global dotnet-ef
ENV PATH="$PATH:/root/.dotnet/tools"
RUN dotnet ef migrations bundle --self-contained -r linux-x64 -o /app/efbundle

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
COPY --from=efbundle /app/efbundle .
ENTRYPOINT ["dotnet", "Nucleus.dll"]
