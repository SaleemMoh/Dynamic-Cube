# Builds one Dynamic Cube .NET process image.
#   docker build -f deploy/docker/dotnet.Dockerfile \
#     --build-arg PROJECT=src/Hosts/Internal.Api/DynamicCube.Internal.Api.csproj \
#     --build-arg ASSEMBLY=DynamicCube.Internal.Api.dll -t dynamic-cube/internal-api .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/ src/
ARG PROJECT
RUN dotnet restore "$PROJECT"
RUN dotnet publish "$PROJECT" -c Release -o /app --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
ARG ASSEMBLY
WORKDIR /app
COPY --from=build /app ./
RUN printf '#!/bin/sh\nexec dotnet /app/%s "$@"\n' "$ASSEMBLY" > /entrypoint.sh && chmod 0755 /entrypoint.sh
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    FileStorage__RootPath=/data/files
# Non-root user provided by the .NET base images.
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["/entrypoint.sh"]
