# syntax=docker/dockerfile:1
# Build from the repository root: docker build -f backend/SeenIt.Api/Dockerfile .
# Render uses linux/amd64. No build arguments or secrets are needed.
FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build
WORKDIR /src

COPY backend/SeenIt.Api/SeenIt.Api.csproj backend/SeenIt.Api/
COPY backend/SeenIt.AvatarChecks/SeenIt.AvatarChecks.csproj backend/SeenIt.AvatarChecks/
COPY backend/SeenIt.Checks/SeenIt.Checks.csproj backend/SeenIt.Checks/
RUN dotnet restore backend/SeenIt.Api/SeenIt.Api.csproj --runtime linux-x64 \
    && dotnet restore backend/SeenIt.AvatarChecks/SeenIt.AvatarChecks.csproj --runtime linux-x64 \
    && dotnet restore backend/SeenIt.Checks/SeenIt.Checks.csproj --runtime linux-x64

COPY backend/SeenIt.Api/ backend/SeenIt.Api/
COPY backend/SeenIt.AvatarChecks/ backend/SeenIt.AvatarChecks/
COPY backend/SeenIt.Checks/ backend/SeenIt.Checks/
COPY backend/docker/runtime-settings.json /container-settings.json

RUN dotnet publish backend/SeenIt.Api/SeenIt.Api.csproj \
      --configuration Release --runtime linux-x64 --self-contained false --no-restore \
      --output /out/api -p:UseAppHost=false -p:PublishTrimmed=false -p:DebugType=None -p:DebugSymbols=false \
    && cp /container-settings.json /out/api/appsettings.json \
    && dotnet publish backend/SeenIt.AvatarChecks/SeenIt.AvatarChecks.csproj \
      --configuration Release --runtime linux-x64 --self-contained false --no-restore \
      --output /out/avatar-checks -p:UseAppHost=false -p:PublishTrimmed=false \
    && dotnet publish backend/SeenIt.Checks/SeenIt.Checks.csproj \
      --configuration Release --runtime linux-x64 --self-contained false --no-restore \
      --output /out/core-checks -p:UseAppHost=false -p:PublishTrimmed=false

RUN dotnet package list --project backend/SeenIt.Api/SeenIt.Api.csproj --no-restore --vulnerable --include-transitive \
    && dotnet package list --project backend/SeenIt.AvatarChecks/SeenIt.AvatarChecks.csproj --no-restore --vulnerable --include-transitive \
    && dotnet package list --project backend/SeenIt.Checks/SeenIt.Checks.csproj --no-restore --vulnerable --include-transitive

# Ubuntu/glibc includes ICU, tzdata, CA certificates and the C/C++ runtime.
# NoDependencies Skia assets do not need fontconfig for bitmap decode/encode.
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble AS runtime-base
ENV ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_ENVIRONMENT=Production \
    PORT=10000 \
    ASPNETCORE_HTTP_PORTS=10000 \
    HOME=/home/app
WORKDIR /app
RUN mkdir -p /app/wwwroot /home/app/.aspnet/DataProtection-Keys \
    && chown -R app:app /home/app/.aspnet
USER app

# Execute the existing checks on the SAME runtime base and native library as final.
# Building final depends on this stage, so native checks cannot be silently skipped.
FROM runtime-base AS verify
COPY --from=build /out/api/ /app/
COPY --from=build /out/avatar-checks/ /checks/avatars/
COPY --from=build /out/core-checks/ /checks/core/
RUN test -f /app/libSkiaSharp.so \
    && cmp /app/libSkiaSharp.so /checks/avatars/libSkiaSharp.so \
    && cmp /app/SkiaSharp.dll /checks/avatars/SkiaSharp.dll \
    && ldd /app/libSkiaSharp.so > /tmp/skia-dependencies.txt \
    && cat /tmp/skia-dependencies.txt \
    && ! grep -q 'not found' /tmp/skia-dependencies.txt \
    && dotnet /checks/avatars/SeenIt.AvatarChecks.dll \
    && dotnet /checks/core/SeenIt.Checks.dll \
    && touch /tmp/seenit-container-checks-passed

FROM runtime-base AS final
COPY --from=build /out/api/ /app/
COPY --from=verify /tmp/seenit-container-checks-passed /app/.container-checks-passed
COPY --chmod=0444 backend/docker/certs/prod-ca-2021.crt /app/certs/prod-ca-2021.crt
RUN test -s /app/certs/prod-ca-2021.crt && test -r /app/certs/prod-ca-2021.crt
EXPOSE 10000
# No shell wrapper, EF tools, startup migration or secret; only the public CA.
ENTRYPOINT ["dotnet", "SeenIt.Api.dll"]
